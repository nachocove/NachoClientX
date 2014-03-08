using System;
using System.Collections.Generic;
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

        public AsSendMailCommand (IAsDataSource dataSource) : base (Xml.ComposeMail.SendMail, Xml.ComposeMail.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.EmailSend);
            EmailMessage = McObject.QueryById<McEmailMessage> (Update.EmailMessageId);
        }

        public override Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            if (14.0 <= Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            return new Dictionary<string, string> () {
                { "SaveInSent", "T" },
            };
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
                               new XElement (m_ns + Xml.ComposeMail.ClientId, EmailMessage.ClientId),
                               new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                               new XElement (m_ns + Xml.ComposeMail.Mime, GenerateMime ()));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sendMail);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override string ToMime (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return GenerateMime ();
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded), new [] { Update.Token });
            EmailMessage.Delete ();
            Update.Delete ();
            return Event.Create ((uint)SmEvt.E.Success, "SMSUCCESS");
        }
        // FIXME - need an OnFail callback for negative indication delivery.
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            return Event.Create ((uint)SmEvt.E.HardFail, "SMHARD0", null, 
                string.Format ("Server sent non-empty response to SendMail: {0}", doc.ToString ()));
        }

        public override void Cancel ()
        {
            base.Cancel ();
            // FIXME - revert IsDispatched.
        }

        private string GenerateMime ()
        {
            return EmailMessage.ToMime ();
        }
    }
}

