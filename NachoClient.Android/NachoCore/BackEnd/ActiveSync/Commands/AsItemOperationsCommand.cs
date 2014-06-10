using System;
using System.Collections.Generic;
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
        public AsItemOperationsCommand (IBEContext dataSource) : base (Xml.ItemOperations.Ns, Xml.ItemOperations.Ns, dataSource)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.AttachmentDownload);
            PendingSingle.MarkDispached ();
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
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var attachment = Attachment ();
            switch ((Xml.ItemOperations.StatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.ItemOperations.Status).Value)) {
            case Xml.ItemOperations.StatusCode.Success_1:
                var xmlFetch = doc.Root.Element (m_ns + Xml.ItemOperations.Response).Element (m_ns + Xml.ItemOperations.Fetch);
                var xmlFileReference = xmlFetch.Element (m_ns + Xml.AirSyncBase.FileReference);
                if (null != xmlFileReference && xmlFileReference.Value != attachment.FileReference) {
                    Log.Error (Log.LOG_AS, "as:itemoperations: FileReference mismatch.");
                    throw new Exception ();
                }
                // TODO: move the file-manip stuff to McAttachment.
                var xmlProperties = xmlFetch.Element (m_ns + Xml.ItemOperations.Properties);
                attachment.ContentType = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.ContentType).Value;
                var xmlData = xmlProperties.Element (m_ns + Xml.ItemOperations.Data);
                var saveAttr = xmlData.Attributes ().Where (x => x.Name == "nacho-attachment-file").SingleOrDefault ();
                if (null != saveAttr) {
                    attachment.SaveFromTemp (saveAttr.Value);
                    attachment.PercentDownloaded = 100;
                    attachment.IsDownloaded = true;
                    attachment.Update ();
                    PendingApply ((pending) => {
                        pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate));
                    });
                    return Event.Create ((uint)SmEvt.E.Success, "IOSUCCESS");
                } else {
                    PendingApply ((pending) => {
                        pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed));
                    });
                    return Event.Create ((uint)SmEvt.E.HardFail, "IOHARDU");
                }

            case Xml.ItemOperations.StatusCode.ProtocolError_2:
            case Xml.ItemOperations.StatusCode.ByteRangeInvalidOrTooLarge_8:
            case Xml.ItemOperations.StatusCode.StoreUnknownOrNotSupported_9:
            case Xml.ItemOperations.StatusCode.AttachmentOrIdInvalid_15:
            case Xml.ItemOperations.StatusCode.ProtocolErrorMissing_155:
                PendingApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.ProtocolError));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD0");

            case Xml.ItemOperations.StatusCode.ServerError_3:
            case Xml.ItemOperations.StatusCode.IoFailure_12:
            case Xml.ItemOperations.StatusCode.ConversionFailure_14:
                PendingApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.ServerError));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD1");

            case Xml.ItemOperations.StatusCode.DocLibBadUri_4:
            case Xml.ItemOperations.StatusCode.DocLibAccessDenied_5:
            case Xml.ItemOperations.StatusCode.DocLibAccessDeniedOrMissing_6:
            case Xml.ItemOperations.StatusCode.DocLibFailedServerConn_7:
            case Xml.ItemOperations.StatusCode.PartialFailure_17:
            case Xml.ItemOperations.StatusCode.ActionNotSupported_156:
                PendingApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD2");

            

            case Xml.ItemOperations.StatusCode.FileEmpty_10:
                PendingApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.MissingOnServer));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.RequestTooLarge_11:
                PendingApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.TooBig));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.ResourceAccessDenied_16:
                PendingApply ((pending) => {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.AccessDeniedOrBlocked));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD4");

            /* FIXME. Need to be able to trigger cred-req from here.
             * case Xml.ItemOperations.StatusCode.CredRequired_18:
             * PendingSingle.ResoveAsDeferredForce ();
             */
            default:
                PendingApply ((pending) => {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IOFAIL");
            }
        }

        private McAttachment Attachment ()
        {
            var attachment = McObject.QueryById<McAttachment> (PendingSingle.AttachmentId);
            NcAssert.NotNull (attachment);
            return attachment;
        }
    }
}

