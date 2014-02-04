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
        public AsSendMailCommand (IAsDataSource dataSource) : base (Xml.ComposeMail.SendMail, Xml.ComposeMail.Ns, dataSource)
        {
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            Update = NextToSend ();
            var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().Single (rec => rec.Id == Update.EmailMessageId);

            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
                               new XElement (m_ns + Xml.ComposeMail.ClientId, emailMessage.ClientId),
                               new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                               new XElement (m_ns + Xml.ComposeMail.Mime, GenerateMime ()));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sendMail);
            Update.IsDispatched = true;
            BackEnd.Instance.Db.Update (Update);
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
            var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().Single (rec => rec.Id == Update.EmailMessageId);
            DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded), new [] { Update.Token });
            emailMessage.DeleteBody (BackEnd.Instance.Db);
            BackEnd.Instance.Db.Delete (emailMessage);
            BackEnd.Instance.Db.Delete (Update);
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
            var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().Single (rec => rec.Id == Update.EmailMessageId);
            return emailMessage.ToMime (BackEnd.Instance.Db);
        }

        private McPending NextToSend ()
        {
            var query = BackEnd.Instance.Db.Table<McPending> ()
                .Where (rec => rec.AccountId == DataSource.Account.Id &&
                    McPending.Operations.EmailSend == rec.Operation);
            if (0 == query.Count ()) {
                return null;
            }
            return query.First ();
        }
    }
}

