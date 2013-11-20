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
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;
using NachoPlatform;

// NOTE: The class that interfaces with HttpClient (or other low-level network API) needs 
// to manage retries & network conditions. If the operation fails "enough", then the
// state machine gets the failure event. There are three classes of failure:
// #1 - unable to perform because of present conditions.
// #2 - unable to perform because of some protocol issue, expected to persist.
namespace NachoCore.ActiveSync {
    public abstract class AsCommand : IAsCommand, IAsHttpOperationOwner {
        // Constants.
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";

        private static XmlSchemaSet commonXmlSchemas;
        private static Dictionary<string,XmlSchemaSet> requestXmlSchemas;
        private static Dictionary<string,XmlSchemaSet> responseXmlSchemas;

        // Properties & IVars.
        protected string CommandName;
        protected XNamespace m_ns;
        protected XNamespace m_baseNs = Xml.AirSyncBase.Ns;
        protected StateMachine OwnerSm;
        protected IAsDataSource DataSource;
        protected AsHttpOperation Op;
        protected uint RetriesLeft;

        public uint RetriesMax { set; get; }
        public TimeSpan Timeout { set; get; }

        // Initializers.
        public AsCommand (string commandName, string nsName, IAsDataSource dataSource) : this (commandName, dataSource)
        {
            m_ns = nsName;
        }

        public AsCommand (string commandName, IAsDataSource dataSource)
        {
            Timeout = TimeSpan.Zero;
            RetriesMax = 3;
            RefreshRetries ();
            CommandName = commandName;
            DataSource = dataSource;
            var assetMgr = new NachoPlatform.Assets ();
            if (null == commonXmlSchemas) {
                commonXmlSchemas = new XmlSchemaSet ();
                foreach (var xsdFile in assetMgr.List (Path.Combine(KXsd, KCommon))) {
                    commonXmlSchemas.Add (null, new XmlTextReader (assetMgr.Open (xsdFile)));
                }
            }
            if (null == requestXmlSchemas) {
                requestXmlSchemas = new Dictionary<string, XmlSchemaSet> ();
                foreach (var xsdRequest in assetMgr.List (Path.Combine(KXsd, KRequest))) {
                    var requestSchema = new XmlSchemaSet ();
                    requestSchema.Add (null, new XmlTextReader (assetMgr.Open (xsdRequest)));
                    requestXmlSchemas [Path.GetFileNameWithoutExtension (xsdRequest)] = requestSchema;
                }
            }
            if (null == responseXmlSchemas) {
                responseXmlSchemas = new Dictionary<string, XmlSchemaSet> ();
                foreach (var xsdResponse in assetMgr.List (Path.Combine(KXsd, KResponse))) {
                    var requestSchema = new XmlSchemaSet ();
                    requestSchema.Add (null, new XmlTextReader (assetMgr.Open (xsdResponse)));
                    responseXmlSchemas [Path.GetFileNameWithoutExtension (xsdResponse)] = requestSchema;
                }
            }
        }       

        // Virtual Methods.
        protected virtual void Execute (StateMachine sm, ref AsHttpOperation opRef)
        {
            Op = new AsHttpOperation (CommandName, this, DataSource);
            opRef = Op;
            Op.Execute (sm);
        }

        public virtual void Execute(StateMachine sm)
        {
            // Op is a "dummy" here for DRY purposes.
            Execute (sm, ref Op);
        }

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
            return string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
                                  CommandName, 
                                  DataSource.Cred.Username,
                                  Device.Instance.Identity (),
                                  Device.Instance.Type ());
        }

        public virtual Uri ServerUriCandidate (AsHttpOperation Sender)
        {
            var requestLine = QueryString (Sender);
            var rlParams = ExtraQueryStringParams (Sender);
            if (null != rlParams) {
                var pairs = new List<string>();
                foreach (KeyValuePair<string,string> pair in rlParams) {
                    pairs.Add (string.Format ("{0}={1}", pair.Key, pair.Value));
                    requestLine = requestLine + '&' + string.Join ("&", pair);
                }
            }
            return new Uri (AsCommand.BaseUri (DataSource.Server), requestLine);
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

        // Subclass can cleanup in the case where a ProcessResponse will never be called.
        public virtual void CancelCleanup (AsHttpOperation Sender)
        {
        }

        // Subclass can override and add specialized support for top-level status codes as needed.
        // Subclass must call base if it does not handle the status code itself.
        public virtual Event TopLevelStatusToEvent (AsHttpOperation Sender, uint status)
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
                return Event.Create ((uint)SmEvt.E.HardFail, null, string.Format ("Xml.StatusCode {0}", status));

            case Xml.StatusCode.InvalidDateTime: // Maybe the next time generated may parse okay.
                return Event.Create ((uint)SmEvt.E.TempFail, null, "Xml.StatusCode.InvalidDateTime");

            case Xml.StatusCode.InvalidCombinationOfIDs: // NOTE(A).
            case Xml.StatusCode.InvalidMIME: // NOTE(B).
            case Xml.StatusCode.DeviceIdMissingOrInvalid:
            case Xml.StatusCode.DeviceTypeMissingOrInvalid:
            case Xml.StatusCode.ServerError:
                return Event.Create ((uint)SmEvt.E.HardFail, null, string.Format ("Xml.StatusCode {0}", status));

            case Xml.StatusCode.ServerErrorRetryLater:
                return Event.Create ((uint)SmEvt.E.TempFail, null, "Xml.StatusCode.ServerErrorRetryLater");

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
                return Event.Create ((uint)SmEvt.E.HardFail, null, string.Format ("Xml.StatusCode {0}", status));
                // Meh. do some cases end-to-end, with user messaging (before all this typing).
            }
            return null;
        }

        protected void RefreshRetries ()
        {
            RetriesLeft = RetriesMax;
        }

        protected void DoSucceed ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.Success);
        }

        protected void DoHardFail ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.HardFail);
        }

        // Static internal helper methods.
        static internal XDocument ToEmptyXDocument ()
        {
            return new XDocument (new XDeclaration ("1.0", "utf8", null));
        }

        static internal Uri BaseUri(NcServer server)
        {
            var retval = string.Format ("{0}://{1}:{2}{3}",
                                        server.Scheme, server.Fqdn, server.Port, server.Path);
            return new Uri(retval);
        }
    }
}

