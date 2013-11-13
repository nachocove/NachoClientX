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
		private NcPendingUpdate m_update;

		public AsSendMailCommand (IAsDataSource dataSource) : base(Xml.ComposeMail.SendMail, Xml.ComposeMail.Ns, dataSource) {
			m_update = NextToSend ();
		}

        public override XDocument ToXDocument (AsHttpOperation Sender) {
			if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
				return null;
			}
			var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
			                             // FIXME - ClientId.
			                             new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
			                             new XElement (m_ns + Xml.ComposeMail.Mime, ToMime (Sender)));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sendMail);
			m_update.IsDispatched = true;
			DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, m_update);
			return doc;
		}

        public override string ToMime (AsHttpOperation Sender) {
			var emailMessage = DataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == m_update.EmailMessageId);
			return emailMessage.ToMime ();
		}

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response) {
			var emailMessage = DataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == m_update.EmailMessageId);
			DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, emailMessage);
			DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, m_update);
            return Event.Create ((uint)Ev.Success);
		}

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc) {
			// Only needed for the case where there is a failure.
            return Event.Create ((uint)Ev.Success);
		}

		private NcPendingUpdate NextToSend () {
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

