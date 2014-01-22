using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsItemOperationsCommand : AsCommand
    {
        public AsItemOperationsCommand (IAsDataSource dataSource) : base (Xml.ItemOperations.Ns, Xml.ItemOperations.Ns, dataSource)
        {
            Update = NextToDnld ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var attachment = Attachment ();
            var itemOp = new XElement (m_ns + Xml.ItemOperations.Ns,
                             new XElement (m_ns + Xml.ItemOperations.Fetch,
                                 new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                                 new XElement (m_baseNs + Xml.AirSyncBase.FileReference, attachment.FileReference)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (itemOp);
            Update.IsDispatched = true;
            DataSource.Owner.Db.Update (Update);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
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
                File.WriteAllBytes (Path.Combine (DataSource.Owner.AttachmentsDir, attachment.LocalFileName),
                    Convert.FromBase64String (xmlData.Value));
                attachment.PercentDownloaded = 100;
                attachment.IsDownloaded = true;
                DataSource.Owner.Db.Update (attachment);
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate), new [] { Update.Token });
                break;
            default:
                // FIXME - handle other status values less bluntly.
                DataSource.Control.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed), new [] { Update.Token });
                break;
            }
            DataSource.Owner.Db.Delete (Update);
            return Event.Create ((uint)SmEvt.E.Success, "IOSUCCESS");
        }

        private McPendingUpdate NextToDnld ()
        {
            return NextPendingUpdate (McPendingUpdate.DataTypes.Attachment, McPendingUpdate.Operations.Download);
        }

        private McAttachment Attachment ()
        {
            return DataSource.Owner.Db.Table<McAttachment> ().Single (rec => rec.AccountId == DataSource.Account.Id &&
            rec.Id == Update.AttachmentId);
        }
    }
}

