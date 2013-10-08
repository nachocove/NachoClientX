using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

/* FIXME
 * put metadata int the pending update pool.
 * folders needing sync.
 * email messages waiting on attachment download(s).
 */
namespace NachoCore.ActiveSync
{
	// NOTE: right now we just download attachments. ItemOperations is overloaded in the protocol, and we may want to subclass.
	public class AsItemOperationsCommand : AsCommand
	{
		private NcPendingUpdate m_update;

		public AsItemOperationsCommand (IAsDataSource dataSource) : base(Xml.ItemOperations.Ns, Xml.ItemOperations.Ns, dataSource) {
			m_update = NextToDnld ();
		}

		protected override XDocument ToXDocument () {
			var attachment = Attachment ();
			var itemOp = new XElement (m_ns + Xml.ItemOperations.Ns,
			                          new XElement (m_ns + Xml.ItemOperations.Fetch,
			                                       new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
			                                       new XElement (m_baseNs + Xml.AirSyncBase.FileReference, attachment.FileReference)));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (itemOp);
			m_update.IsDispatched = true;
			m_dataSource.Owner.Db.Update (BackEnd.DbActors.Proto, m_update);
			return doc;
		}

		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			var attachment = Attachment ();
			switch ((Xml.ItemOperations.StatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.ItemOperations.Status).Value)) {
			case Xml.ItemOperations.StatusCode.Success:
				var xmlFetch = doc.Root.Element (m_ns + Xml.ItemOperations.Response).Element (m_ns + Xml.ItemOperations.Fetch);
				var xmlFileReference = xmlFetch.Element (m_ns + Xml.AirSyncBase.FileReference);
				if (null != xmlFileReference && xmlFileReference.Value != attachment.FileReference) {
					Console.WriteLine ("as:itemoperations: FileReference mismatch.");
					throw new Exception ();
				}
				var xmlProperties = xmlFetch.Element (m_ns + Xml.ItemOperations.Properties);
				attachment.ContentType = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.ContentType).Value;
				attachment.LocalFileName = attachment.Id.ToString ();
				var xmlData = xmlProperties.Element (m_ns + Xml.ItemOperations.Data);
				File.WriteAllBytes (Path.Combine (m_dataSource.Owner.AttachmentsDir, attachment.LocalFileName),
				                    Convert.FromBase64String (xmlData.Value));
				attachment.IsDownloaded = true;
				break;
				// FIXME - handle other status values.
			}
			m_dataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, m_update);
			return (uint)Ev.Success;
		}

		private NcPendingUpdate NextToDnld () {
			var query = m_dataSource.Owner.Db.Table<NcPendingUpdate> ()
				.Where (rec => rec.AccountId == m_dataSource.Account.Id &&
				        NcPendingUpdate.DataTypes.Attachment == rec.DataType &&
				        NcPendingUpdate.Operations.Download == rec.Operation);
			if (0 == query.Count ()) {
				return null;
			}
			return query.First ();
		}

		private NcAttachment Attachment () {
			return m_dataSource.Owner.Db.Table<NcAttachment> ().Single (rec => rec.AccountId == m_dataSource.Account.Id &&
			                                                            rec.Id == m_update.AttachmentId);
		}
	}
}

