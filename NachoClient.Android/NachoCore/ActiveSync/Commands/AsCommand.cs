using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;
using NachoPlatform;

// NOTE: The class that interfaces with HttpClient (or other low-level network API) needs
// to manage network conditions. There are three classes of failure:
// #1 - unable to perform because of present conditions.
// #2 - unable to perform because of some protocol issue, expected to persist.
namespace NachoCore.ActiveSync
{
    public abstract class AsCommand : IAsCommand, IAsHttpOperationOwner
    {
        // Constants.
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";
        // Properties & IVars.
        protected string CommandName;
        public XNamespace m_ns;
        protected XNamespace m_baseNs = Xml.AirSyncBase.Ns;
        protected NcStateMachine OwnerSm;
        protected IAsDataSource DataSource;
        protected AsHttpOperation Op;
        protected McPendingUpdate Update;
        protected NcResult SuccessInd;
        protected NcResult FailureInd;

        public TimeSpan Timeout { set; get; }
        // Initializers.
        public AsCommand (string commandName, string nsName, IAsDataSource dataSource) : this (commandName, dataSource)
        {
            m_ns = nsName;
        }

        public AsCommand (string commandName, IAsDataSource dataSource)
        {
            Timeout = TimeSpan.Zero;
            CommandName = commandName;
            DataSource = dataSource;
        }
        // Virtual Methods.
        protected virtual void Execute (NcStateMachine sm, ref AsHttpOperation opRef)
        {
            Op = new AsHttpOperation (CommandName, this, DataSource);
            if (TimeSpan.Zero != Timeout) {
                Op.Timeout = Timeout;
            }
            opRef = Op;
            Op.Execute (sm);
        }

        public virtual void Execute (NcStateMachine sm)
        {
            // Op is a "dummy" here for DRY purposes.
            Execute (sm, ref Op);
        }

        // Cancel() must be safe to call even when the command has already completed.
        public virtual void Cancel ()
        {
            if (null != Op) {
                Op.Cancel ();
                Op = null;
            }
        }

        public virtual bool UseWbxml (AsHttpOperation Sender)
        {
            return true;
        }
        // Override if the subclass wants to add more parameters to the query string.
        public virtual HttpMethod Method (AsHttpOperation Sender)
        {
            return HttpMethod.Post;
        }

        public virtual Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            return null;
        }
        // Override if the subclass wants total control over the query string.
        public virtual string QueryString (AsHttpOperation Sender)
        {
            var ident = (Device.Instance.IsSimulator ()) ? DataSource.ProtocolState.KludgeSimulatorIdentity : Device.Instance.Identity ();
            return string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
                CommandName, 
                DataSource.Cred.Username,
                ident,
                Device.Instance.Type ());
        }

        public virtual Uri ServerUri (AsHttpOperation Sender)
        {
            var requestLine = QueryString (Sender);
            var rlParams = ExtraQueryStringParams (Sender);
            if (null != rlParams) {
                var pairs = new List<string> ();
                foreach (KeyValuePair<string,string> pair in rlParams) {
                    pairs.Add (string.Format ("{0}={1}", pair.Key, pair.Value));
                    requestLine = requestLine + '&' + string.Join ("&", pair);
                }
            }
            return new Uri (AsCommand.BaseUri (DataSource.Server), requestLine);
        }

        public virtual void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender)
        {
            var server = DataSource.Server;
            server.Scheme = ServerUri.Scheme;
            server.Fqdn = ServerUri.Host;
            server.Port = ServerUri.Port;
            server.Path = ServerUri.AbsolutePath;
            // Updates the value in the DB.
            DataSource.Server = server;
        }

        // The subclass should for any given instatiation only return non-null from ToXDocument XOR ToMime.
        public virtual XDocument ToXDocument (AsHttpOperation Sender)
        {
            return null;
        }

        public virtual string ToMime (AsHttpOperation Sender)
        {
            return null;
        }

        public virtual void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                if (null != SuccessInd) {
                    DataSource.Owner.StatusInd (DataSource.Control, SuccessInd);
                }
            } else {
                if (null != FailureInd) {
                    DataSource.Owner.StatusInd (DataSource.Control, FailureInd);
                }
            }
        }

        public virtual Event PreProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            return null;
        }
        // Called for non-WBXML HTTP 200 responses.
        public virtual Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            return new Event () { EventCode = (uint)SmEvt.E.Success };
        }

        public virtual Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            return new Event () { EventCode = (uint)SmEvt.E.Success };
        }
        // Subclass can override and add specialized support for top-level status codes as needed.
        // Subclass must call base if it does not handle the status code itself.
        public virtual Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status)
        {
            // returning -1 means that this function did not know how to convert the status value.
            // NOTE(A): Subclass can possibly make this a TempFail or Success if the id issue is just a sync issue.
            // NOTE(B): Subclass can retry with a formatting simplification.
            // NOTE(C): Subclass MUST catch & handle this code.
            // FIXME - package enough telemetry information so that we can fix our bugs.
            // FIXME - catch TempFail loops and convert to HardFail.
            // FIXME(A): MUST provide user with information about how to possibly rectify.
            switch ((Xml.StatusCode)status) {
            case Xml.StatusCode.InvalidContent:
            case Xml.StatusCode.InvalidWBXML:
            case Xml.StatusCode.InvalidXML:
                return Event.Create ((uint)SmEvt.E.HardFail, "TLSHARD0", null, string.Format ("Xml.StatusCode {0}", status));

            case Xml.StatusCode.InvalidDateTime: // Maybe the next time generated may parse okay.
                return Event.Create ((uint)SmEvt.E.TempFail, "TLSTEMP0", null, "Xml.StatusCode.InvalidDateTime");

            case Xml.StatusCode.InvalidCombinationOfIDs: // NOTE(A).
            case Xml.StatusCode.InvalidMIME: // NOTE(B).
            case Xml.StatusCode.DeviceIdMissingOrInvalid:
            case Xml.StatusCode.DeviceTypeMissingOrInvalid:
            case Xml.StatusCode.ServerError:
                return Event.Create ((uint)SmEvt.E.HardFail, "TLSHARD1", null, string.Format ("Xml.StatusCode {0}", status));

            case Xml.StatusCode.ServerErrorRetryLater:
                return Event.Create ((uint)SmEvt.E.TempFail, "TLSTEMP1", null, "Xml.StatusCode.ServerErrorRetryLater");

            case Xml.StatusCode.ActiveDirectoryAccessDenied: // FIXME(A).
            case Xml.StatusCode.MailboxQuotaExceeded: // FIXME(A).
            case Xml.StatusCode.MailboxServerOffline: // FIXME(A).
            case Xml.StatusCode.SendQuotaExceeded: // NOTE(C).
            case Xml.StatusCode.MessageRecipientUnresolved: // NOTE(C).
            case Xml.StatusCode.MessageReplyNotAllowed: // NOTE(C).
            case Xml.StatusCode.MessagePreviouslySent:
            case Xml.StatusCode.MessageHasNoRecipient: // NOTE(C).
            case Xml.StatusCode.MailSubmissionFailed:
            case Xml.StatusCode.MessageReplyFailed:
            case Xml.StatusCode.UserHasNoMailbox: // FIXME(A).
            case Xml.StatusCode.UserCannotBeAnonymous: // FIXME(A).
            case Xml.StatusCode.UserPrincipalCouldNotBeFound: // FIXME(A).
                return Event.Create ((uint)SmEvt.E.HardFail, "TLSHARD2", null, string.Format ("Xml.StatusCode {0}", status));
            // Meh. do some cases end-to-end, with user messaging (before all this typing).

            case Xml.StatusCode.DeviceNotProvisioned:
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLSREPROV0", null, "Global DeviceNotProvisioned");
            }
            return null;
        }

        protected virtual void DoSucceed ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.Success, "ASCDSUCCESS");
        }

        protected virtual void DoHardFail ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.HardFail, "ASCDHARD0");
        }

        protected virtual void DoReSync ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReSync, "ASCDRESYNC");
        }

        protected virtual void DoReDisc ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReDisc, "ASCDRESYNC");
        }

        protected virtual void DoUiGetCred ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.AuthFail, "ASCDAUTH");
        }

        protected void DoNop ()
        {
        }

        protected virtual McPendingUpdate NextPendingUpdate (McPendingUpdate.DataTypes dataType, 
            McPendingUpdate.Operations operation)
        {
            return BackEnd.Instance.Db.Table<McPendingUpdate> ()
                .FirstOrDefault (rec => rec.AccountId == DataSource.Account.Id &&
                    dataType == rec.DataType && operation == rec.Operation);
        }

        // Static internal helper methods.
        static internal XDocument ToEmptyXDocument ()
        {
            var doc = new XDocument ();
            doc.Declaration = new XDeclaration ("1.0", "utf-8", "no");
            return doc;
        }

        static internal Uri BaseUri (McServer server)
        {
            var retval = string.Format ("{0}://{1}:{2}{3}",
                             server.Scheme, server.Fqdn, server.Port, server.Path);
            return new Uri (retval);
        }

    }
}

