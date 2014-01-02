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

            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
                               new XElement (m_ns + Xml.ComposeMail.ClientId, Guid.NewGuid ()),
                               new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                               new XElement (m_ns + Xml.ComposeMail.Mime, GenerateMime ()));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (sendMail);
            Update.IsDispatched = true;
            DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, Update);
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
            var emailMessage = DataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == Update.EmailMessageId);
            DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, emailMessage);
            DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, Update);
            return Event.Create ((uint)SmEvt.E.Success);
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            return Event.Create ((uint)SmEvt.E.HardFail, null, 
                string.Format("Server sent non-empty response to SendMail: {0}", doc.ToString()));
        }

        public override void Cancel ()
        {
            base.Cancel ();
            // FIXME - revert IsDispatched.
        }

        private string GenerateMime ()
        {
            var emailMessage = DataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == Update.EmailMessageId);
            return emailMessage.ToMime ();
        }

        private NcPendingUpdate NextToSend ()
        {
            var query = DataSource.Owner.Db.Table<NcPendingUpdate> ()
                .Where (rec => rec.AccountId == DataSource.Account.Id &&
                        NcPendingUpdate.DataTypes.EmailMessage == rec.DataType &&
                        NcPendingUpdate.Operations.Send == rec.Operation);
            if (0 == query.Count ()) {
                return null;
            }
            return query.First ();
        }
    }
}

