using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // TODO: it looks like one might be able to consolidate this with AsSmartCommand.
    public class AsSendMailCommand : AsCommand
    {
        private McEmailMessage EmailMessage;

        public AsSendMailCommand (IBEContext dataSource, McPending pending) : 
            base (Xml.ComposeMail.SendMail, Xml.ComposeMail.Ns, dataSource)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }

        public override Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            if (14.0 <= Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                return null;
            }
            return new Dictionary<string, string> () {
                { "SaveInSent", "T" },
            };
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        public override double TimeoutInSeconds {
            get {
                return ((AsProtoControl)BEContext.ProtoControl).Strategy.UploadTimeoutSecs (new FileInfo (EmailMessage.MimePath ()).Length);
            }
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                return null;
            }
            var mimePath = EmailMessage.MimePath ();
            var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
                               new XElement (m_ns + Xml.ComposeMail.ClientId, EmailMessage.ClientId),
                               new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                               new XElement (m_ns + Xml.ComposeMail.Mime, 
                                   new XAttribute ("nacho-body-path", mimePath)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sendMail);
            return doc;
        }

        protected override FileStream ToMime (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                long length;
                var stream = EmailMessage.ToMime (out length);
                return stream;
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SMCANCEL0");
            }
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });

            var sentFolder = McFolder.GetDefaultSentFolder (AccountId);
            if (null != sentFolder) {
                sentFolder.UpdateSet_AsSyncMetaToClientExpected (true);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SMSUCCESS");
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SMCANCEL1");
            }
            // The only applicable TL Status codes are the general ones.
            PendingResolveApply ((pending) => {
                pending.ResolveAsHardFail (BEContext.ProtoControl, 
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed,
                        NcResult.WhyEnum.Unknown));
            });
            return Event.Create ((uint)SmEvt.E.HardFail, "SMHARD0", null, 
                string.Format ("Server sent non-empty response to SendMail w/unrecognized TL Status: {0}", doc.ToString ()));
        }
    }
}

