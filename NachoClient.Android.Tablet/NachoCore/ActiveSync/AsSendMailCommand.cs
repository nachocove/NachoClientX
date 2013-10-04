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

		protected override XDocument ToXDocument () {
			if (14.0 > Convert.ToDouble (m_dataSource.ProtocolState.AsProtocolVersion)) {
				return null;
			}
			var sendMail = new XElement (m_ns + Xml.ComposeMail.SendMail, 
			                             // FIXME - ClientId.
			                             new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
			                             new XElement (m_ns + Xml.ComposeMail.Mime, ToMime ()));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sendMail);
			return doc;
		}

		protected override string ToMime () {
			var emailMessage = m_dataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == m_update.EmailMessageId);
			return emailMessage.ToMime ();
		}

		protected override uint ProcessResponse (HttpResponseMessage response) {
			var emailMessage = m_dataSource.Owner.Db.Table<NcEmailMessage> ().Single (rec => rec.Id == m_update.EmailMessageId);
			m_dataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, emailMessage);
			m_dataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, m_update);
			var possibleNext = NextToSend ();
			if (null != possibleNext) {
				return (uint)AsProtoControl.Lev.SendMail;
			}
			return (uint)Ev.Success;
		}

		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			// Only needed for the case where there is a failure.
			return (uint)Ev.Success;
		}

		private NcPendingUpdate NextToSend () {
			var query = m_dataSource.Owner.Db.Table<NcPendingUpdate> ()
				.Where (rec => rec.AccountId == m_dataSource.Account.Id &&
				NcPendingUpdate.DataTypes.EmailMessage == rec.DataType &&
				NcPendingUpdate.Operations.Send == rec.Operation);
			if (0 == query.Count ()) {
				return null;
			}
			return query.First ();
		}
	}
}

