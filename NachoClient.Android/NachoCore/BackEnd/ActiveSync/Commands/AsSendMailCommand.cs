using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsSendMailCommand : AsCommand
    {
        private McEmailMessage EmailMessage;

        public AsSendMailCommand (IBEContext dataSource, McPending pending) : 
            base (Xml.ComposeMail.SendMail, Xml.ComposeMail.Ns, dataSource)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }

        public override Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            if (14.0 <= Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
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

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
                               new XElement (m_ns + Xml.ComposeMail.ClientId, EmailMessage.ClientId),
                               new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                               new XElement (m_ns + Xml.ComposeMail.Mime, 
                                   new XAttribute ("nacho-body-path", EmailMessage.MimePath ())));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sendMail);
            return doc;
        }

        protected override Stream ToMime (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
                return EmailMessage.ToMime ();
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });

            EmailMessage.Delete ();

            var sentFolder = McFolder.GetDefaultSentFolder (BEContext.Account.Id);
            if (null != sentFolder) {
                sentFolder.UpdateSet_AsSyncMetaToClientExpected (true);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SMSUCCESS");
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            // The only applicable TL Status codes are the general ones.
            PendingResolveApply ((pending) => {
                pending.ResolveAsHardFail (BEContext.ProtoControl, 
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed,
                        NcResult.WhyEnum.Unknown));
            });
            return Event.Create ((uint)SmEvt.E.HardFail, "SMHARD0", null, 
                string.Format ("Server sent non-empty response to SendMail w/unrecognized TL Status: {0}", doc.ToString ()));
        }

        public override bool IsContentLarge (AsHttpOperation Sender)
        {
            return true;
        }
    }
}

