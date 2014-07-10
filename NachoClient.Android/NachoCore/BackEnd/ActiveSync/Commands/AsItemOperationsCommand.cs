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
        private List<McAttachment> Attachments;
        private List<McItem> Prefetches;
        private static XNamespace AirSyncNs = Xml.AirSync.Ns;

        private void ApplyStrategy ()
        {
            var fetchKit = BEContext.ProtoControl.SyncStrategy.FetchKit ();
            PendingList.AddRange (fetchKit.Item1);
            Prefetches = fetchKit.Item2.ToList ();
        }

        public AsItemOperationsCommand (IBEContext dataSource) : base (Xml.ItemOperations.Ns, Xml.ItemOperations.Ns, dataSource)
        {
            Attachments = new List<McAttachment> ();
            ApplyStrategy ();
            foreach (var pending in PendingList) {
                pending.MarkDispached ();
            }
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var itemOp = new XElement (m_ns + Xml.ItemOperations.Ns);
            XElement fetch = null;
            foreach (var pending in PendingList) {
                switch (pending.Operation) {
                case McPending.Operations.AttachmentDownload:
                    var attachment = McObject.QueryById<McAttachment> (pending.AttachmentId);
                    Attachments.Add (attachment);
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (m_baseNs + Xml.AirSyncBase.FileReference, attachment.FileReference));
                    break;

                case McPending.Operations.EmailBodyDownload:
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (AirSyncNs + Xml.AirSync.CollectionId, pending.ParentId),
                        new XElement (AirSyncNs + Xml.AirSync.ServerId, pending.ServerId),
                        new XElement (m_ns + Xml.ItemOperations.Options,
                            new XElement (AirSyncNs + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2),
                            new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"),
                                new XElement (m_baseNs + Xml.AirSyncBase.AllOrNone, "1"))));
                    break;

                case McPending.Operations.CalBodyDownload:
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (AirSyncNs + Xml.AirSync.ServerId, pending.ServerId),
                        new XElement (AirSyncNs + Xml.AirSync.Options,
                            new XElement (m_ns + Xml.AirSync.MimeSupport, (uint)Xml.AirSync.MimeSupportCode.AllMime_2),
                            new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.Mime_4),
                                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"),
                                new XElement (m_baseNs + Xml.AirSyncBase.AllOrNone, "1"))));
                    break;

                case McPending.Operations.ContactBodyDownload:
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (AirSyncNs + Xml.AirSync.ServerId, pending.ServerId),
                        new XElement (AirSyncNs + Xml.AirSync.Options,
                            new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"))));
                    break;

                case McPending.Operations.TaskBodyDownload:
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (AirSyncNs + Xml.AirSync.ServerId, pending.ServerId),
                        new XElement (AirSyncNs + Xml.AirSync.Options,
                            new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                                new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                                new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"))));
                    break;
                default:
                    NcAssert.True (false);
                    break;
                }
                itemOp.Add (fetch);
            }
            foreach (var prefetch in Prefetches) {
            }
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (itemOp);
            return doc;
        }

        private McPending FindPending (XElement xmlFileReference, XElement xmlServerId)
        {
            if (null != xmlFileReference) {
                var attachment = Attachments.Where (x => xmlFileReference.Value == x.FileReference).FirstOrDefault ();
                if (null != attachment) {
                    return PendingList.Where (x => x.AttachmentId == attachment.Id).First ();
                }
            }
            if (null != xmlServerId) {
                return PendingList.Where (x => x.ServerId == xmlServerId.Value).FirstOrDefault ();
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlStatus = doc.Root.Element (m_ns + Xml.ItemOperations.Status);
            switch ((Xml.ItemOperations.StatusCode)Convert.ToUInt32 (xmlStatus.Value)) {
            case Xml.ItemOperations.StatusCode.Success_1:
                var xmlResponse = doc.Root.Element (m_ns + Xml.ItemOperations.Response);
                var xmlFetches = xmlResponse.Elements (m_ns + Xml.ItemOperations.Fetch);
                foreach (var xmlFetch in xmlFetches) {
                    var xmlFileReference = xmlFetch.Element (m_baseNs + Xml.AirSyncBase.FileReference);
                    var xmlServerId = xmlFetch.Element (AirSyncNs + Xml.AirSync.ServerId);
                    var xmlProperties = xmlFetch.Element (m_ns + Xml.ItemOperations.Properties);
                    xmlStatus = xmlFetch.ElementAnyNs (Xml.ItemOperations.Status);
                    switch ((Xml.ItemOperations.StatusCode)Convert.ToUInt32 (xmlStatus.Value)) {
                    case Xml.ItemOperations.StatusCode.Success_1:
                        if (null != xmlFileReference) {
                            // This means we are processing an AttachmentDownload response.
                            var attachment = Attachments.Where (x => x.FileReference == xmlFileReference.Value).First ();
                            var pending = FindPending (xmlFileReference, null);
                            attachment.ContentType = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.ContentType).Value;
                            var xmlData = xmlProperties.Element (m_ns + Xml.ItemOperations.Data);
                            // TODO: move the file-manip stuff to McAttachment.
                            var saveAttr = xmlData.Attributes ().Where (x => x.Name == "nacho-attachment-file").SingleOrDefault ();
                            if (null != saveAttr) {
                                attachment.SaveFromTemp (saveAttr.Value);
                                attachment.PercentDownloaded = 100;
                                attachment.IsDownloaded = true;
                                attachment.Update ();
                                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate));
                            } else {
                                pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed));
                            }
                            PendingList.Remove (pending);
                        } else if (null != xmlServerId) {
                            // This means we are processing a body download response.
                            var xmlBody = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.Body);
                            var serverId = xmlServerId.Value;
                            var pending = FindPending (null, xmlServerId);
                            McItem item = null;
                            NcResult.SubKindEnum successInd = NcResult.SubKindEnum.Error_UnknownCommandFailure;
                            switch (pending.Operation) {
                            case McPending.Operations.EmailBodyDownload:
                                item = McItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, serverId);
                                successInd = NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded;
                                break;
                            case McPending.Operations.CalBodyDownload:
                                item = McItem.QueryByServerId<McCalendar> (BEContext.Account.Id, serverId);
                                successInd = NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded;
                                break;
                            case McPending.Operations.ContactBodyDownload:
                                item = McItem.QueryByServerId<McContact> (BEContext.Account.Id, serverId);
                                successInd = NcResult.SubKindEnum.Info_ContactBodyDownloadSucceeded;
                                break;
                            case McPending.Operations.TaskBodyDownload:
                                item = McItem.QueryByServerId<McTask> (BEContext.Account.Id, serverId);
                                successInd = NcResult.SubKindEnum.Info_TaskBodyDownloadSucceeded;
                                break;
                            default:
                                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                                break;
                            }
                            // We are ignoring all the other crap that can come down (for now). We just want the Body.
                            item.ApplyAsXmlBody (xmlBody);
                            item.Update ();
                            var result = NcResult.Info (successInd);
                            result.Value = item;
                            pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                            PendingList.Remove (pending);
                        } else {
                            // Can't figure out which pending to resolve here.
                            Log.Error (Log.LOG_AS, "ItemOperations: no ServerId and no FileReference.");
                        }
                        break;
                    default:
                        // TODO: we let the general fail case clean-up non-Success codes right now.
                        // We should attempt to find the associated pending (if we can) and give a more
                        // specific failure.
                        break;
                    }
                }
                return Event.Create ((uint)SmEvt.E.Success, "IOSUCCESS");

            case Xml.ItemOperations.StatusCode.ProtocolError_2:
            case Xml.ItemOperations.StatusCode.ByteRangeInvalidOrTooLarge_8:
            case Xml.ItemOperations.StatusCode.StoreUnknownOrNotSupported_9:
            case Xml.ItemOperations.StatusCode.AttachmentOrIdInvalid_15:
            case Xml.ItemOperations.StatusCode.ProtocolErrorMissing_155:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.ProtocolError));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD0");

            case Xml.ItemOperations.StatusCode.ServerError_3:
            case Xml.ItemOperations.StatusCode.IoFailure_12:
            case Xml.ItemOperations.StatusCode.ConversionFailure_14:
                PendingResolveApply ((pending) => {
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
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD2");

            case Xml.ItemOperations.StatusCode.FileEmpty_10:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.MissingOnServer));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.RequestTooLarge_11:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.TooBig));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.ResourceAccessDenied_16:
                PendingResolveApply ((pending) => {
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
                PendingResolveApply ((pending) => {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed,
                            NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IOFAIL");
            }
        }
    }
}
