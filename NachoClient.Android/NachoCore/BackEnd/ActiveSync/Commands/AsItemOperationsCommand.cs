using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsItemOperationsCommand : AsCommand
    {
        private FetchKit FetchKit;
        private List<McAttachment> Attachments;
        private static XNamespace AirSyncNs = Xml.AirSync.Ns;

        public AsItemOperationsCommand (IBEContext dataSource, FetchKit fetchKit) :
            base (Xml.ItemOperations.Ns, Xml.ItemOperations.Ns, dataSource)
        {
            Attachments = new List<McAttachment> ();
            FetchKit = fetchKit;
            NcModel.Instance.RunInTransaction (() => {
                foreach (var pending in fetchKit.FetchBodies) {
                    pending.Pending.MarkDispatched ();
                    PendingList.Add (pending.Pending);
                }
            });
        }

        private XElement ToEmailFetch (string parentId, string serverId, Xml.AirSync.TypeCode bodyPref)
        {
            if (0 == bodyPref) {
                bodyPref = Xml.AirSync.TypeCode.Mime_4;
            }
            return new XElement (m_ns + Xml.ItemOperations.Fetch,
                new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                new XElement (AirSyncNs + Xml.AirSync.CollectionId, parentId),
                new XElement (AirSyncNs + Xml.AirSync.ServerId, serverId),
                new XElement (m_ns + Xml.ItemOperations.Options,
                    new XElement (AirSyncNs + Xml.AirSync.MimeSupport,
                        Xml.AirSync.TypeCode.Mime_4 == bodyPref ?
                        (uint)Xml.AirSync.MimeSupportCode.AllMime_2 : (uint)Xml.AirSync.MimeSupportCode.NoMime_0),
                    new XElement (m_baseNs + Xml.AirSync.BodyPreference,
                        new XElement (m_baseNs + Xml.AirSyncBase.Type, (uint)bodyPref),
                        new XElement (m_baseNs + Xml.AirSyncBase.TruncationSize, "100000000"),
                        new XElement (m_baseNs + Xml.AirSyncBase.AllOrNone, "1"))));
        }

        private XElement ToAttaFetch (string fileRef)
        {
            return new XElement (m_ns + Xml.ItemOperations.Fetch,
                new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                new XElement (m_baseNs + Xml.AirSyncBase.FileReference, fileRef));
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var itemOp = new XElement (m_ns + Xml.ItemOperations.Ns);
            XElement fetch = null;
            // Add in the pendings, if any.
            foreach (var pendingInfo in FetchKit.FetchBodies) {
                var pending = pendingInfo.Pending;
                fetch = null;
                switch (pending.Operation) {
                case McPending.Operations.AttachmentDownload:
                    var attachment = McAbstrObject.QueryById<McAttachment> (pending.AttachmentId);
                    if (null != attachment) {
                        Attachments.Add (attachment);
                        fetch = ToAttaFetch (attachment.FileReference);
                    }
                    break;

                case McPending.Operations.EmailBodyDownload:
                    fetch = ToEmailFetch (pending.ParentId, pending.ServerId, pendingInfo.BodyPref);
                    break;

                case McPending.Operations.CalBodyDownload:
                    fetch = new XElement (m_ns + Xml.ItemOperations.Fetch,
                        new XElement (m_ns + Xml.ItemOperations.Store, Xml.ItemOperations.StoreCode.Mailbox),
                        new XElement (AirSyncNs + Xml.AirSync.ServerId, pending.ServerId),
                        new XElement (AirSyncNs + Xml.AirSync.CollectionId, pending.ParentId),
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
                // The to-be-fetched attachment can be deleted before we get here.
                if (null != fetch) {
                    itemOp.Add (fetch);
                }
            }
            foreach (var pfAtta in FetchKit.FetchAttachments) {
                Attachments.Add (pfAtta.Attachment);
                itemOp.Add (ToAttaFetch (pfAtta.Attachment.FileReference));
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
                    return PendingList.Where (x => x.AttachmentId == attachment.Id).FirstOrDefault ();
                }
            }
            if (null != xmlServerId) {
                return PendingList.Where (x => x.ServerId == xmlServerId.Value).FirstOrDefault ();
            }
            return null;
        }

        private void MaybeErrorFileDesc (XElement xmlFileReference, XElement xmlServerId)
        {
            if (null != FindPending (xmlFileReference, xmlServerId)) {
                // McPending-based requests will deal with this in the McPending logic.
                // TODO: unify result logic for API-based and speculative fetches.
                return;
            }
            if (null != xmlFileReference) {
                // This means we are processing an AttachmentDownload prefetch response.
                NcModel.Instance.RunInTransaction (() => {
                    var attachment = Attachments.Where (x => x.FileReference == xmlFileReference.Value).FirstOrDefault ();
                    if (null == attachment) {
                        Log.Error (Log.LOG_AS, "MaybeErrorFileDesc: could not find FileReference {0}", xmlFileReference.Value);
                    } else {
                        attachment.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Error);
                        attachment.Update ();
                    }
                });
            } else if (null != xmlServerId) {
                // This means we are processing a body download prefetch response.
                NcModel.Instance.RunInTransaction (() => {
                    var item = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, xmlServerId.Value);
                    if (null == item) {
                        Log.Error (Log.LOG_AS, "MaybeErrorFileDesc: could not find McEmailMessage with ServerId {0}", xmlServerId.Value);
                    } else {
                        if (0 == item.BodyId) {
                            var body = McBody.InsertError (AccountId);
                            item = item.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.BodyId = body.Id;
                                return true;
                            });
                        } else {
                            var body = McBody.QueryById<McBody> (item.BodyId);
                            if (null == body) {
                                Log.Error (Log.LOG_AS, "MaybeErrorFileDesc: could not find McBody with Id {0}", item.BodyId);
                            } else {
                                body.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Error);
                                body.Update ();
                            }
                        }
                    }
                });
            } else {
                Log.Error (Log.LOG_AS, "MaybeErrorFileDesc: null xmlFileReference and xmlServerId");
            }
        }

        private void MaybeResolveAsHardFail (McPending pending, NcResult.WhyEnum why)
        {
            if (null != pending) {
                pending.ResolveAsHardFail (BEContext.ProtoControl, why);
            }
            PendingList.Remove (pending);
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "IOPCANCEL");
            }
            var xmlStatus = doc.Root.Element (m_ns + Xml.ItemOperations.Status);
            var outerStatus = (Xml.ItemOperations.StatusCode)uint.Parse (xmlStatus.Value);
            if (Xml.ItemOperations.StatusCode.Success_1 != outerStatus) {
                Log.Warn (Log.LOG_AS, "ItemOperations: Status {0}", outerStatus);
            }
            switch (outerStatus) {
            case Xml.ItemOperations.StatusCode.Success_1:
                var xmlResponse = doc.Root.Element (m_ns + Xml.ItemOperations.Response);
                if (null == xmlResponse) {
                    PendingResolveApply ((pending) => {
                        pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.Unknown);
                    });
                    return Event.Create ((uint)SmEvt.E.HardFail, "IONORESP");
                }
                var xmlFetches = xmlResponse.Elements (m_ns + Xml.ItemOperations.Fetch);
                foreach (var xmlFetch in xmlFetches) {
                    var xmlFileReference = xmlFetch.Element (m_baseNs + Xml.AirSyncBase.FileReference);
                    var xmlServerId = xmlFetch.Element (AirSyncNs + Xml.AirSync.ServerId);
                    var xmlProperties = xmlFetch.Element (m_ns + Xml.ItemOperations.Properties);
                    xmlStatus = xmlFetch.ElementAnyNs (Xml.ItemOperations.Status);
                    var innerStatus = (Xml.ItemOperations.StatusCode)uint.Parse (xmlStatus.Value);
                    if (Xml.ItemOperations.StatusCode.Success_1 != innerStatus) {
                        Log.Warn (Log.LOG_AS, "ItemOperations: Status {0}", innerStatus);
                    }
                    switch (innerStatus) {
                    case Xml.ItemOperations.StatusCode.Success_1:
                        if (null != xmlFileReference) {
                            // This means we are processing an AttachmentDownload response.
                            var attachment = Attachments.Where (x => x.FileReference == xmlFileReference.Value).First ();
                            attachment.ContentType = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.ContentType).Value;
                            var xmlData = xmlProperties.Element (m_ns + Xml.ItemOperations.Data);
                            var saveAttr = xmlData.Attributes ().Where (x => x.Name == "nacho-attachment-file").SingleOrDefault ();
                            var pending = FindPending (xmlFileReference, null);
                            if (null != saveAttr) {
                                attachment.UpdateFileCopy (saveAttr.Value);
                                File.Delete (saveAttr.Value);
                                if (null != pending) {
                                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate));
                                }
                            } else {
                                if (null != pending) {
                                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed));
                                }
                            }
                            // TODO - remove by the Id to avoid ambiguity if we use the result of ResolveAs...
                            PendingList.Remove (pending);
                        } else if (null != xmlServerId) {
                            // This means we are processing a body download response.
                            var xmlBody = xmlProperties.Element (m_baseNs + Xml.AirSyncBase.Body);
                            var serverId = xmlServerId.Value;
                            var pending = FindPending (null, xmlServerId);
                            McAbstrItem item = null;
                            NcResult.SubKindEnum successInd = NcResult.SubKindEnum.Error_UnknownCommandFailure;
                            McPending.Operations op;
                            if (null == pending) {
                                // If there is no pending, then we are doing an email prefetch.
                                op = McPending.Operations.EmailBodyDownload;
                            } else {
                                op = pending.Operation;
                            }
                            switch (op) {
                            case McPending.Operations.EmailBodyDownload:
                                item = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, serverId);
                                if (item == null) {
                                    successInd = NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed;
                                } else {
                                    successInd = NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded;
                                }
                                if (null != pending) {
                                    Log.Info (Log.LOG_AS, "Processing DnldEmailBodyCmd({0}) {1} for email {2}", item.AccountId, pending, item.Id);
                                } else {
                                    Log.Info (Log.LOG_AS, "Processing DnldEmailBodyCmd({0}) for email {1}", item.AccountId, item.Id);
                                }
                                BackEnd.Instance.BodyFetchHints.RemoveHint (AccountId, item.Id);
                                break;
                            case McPending.Operations.CalBodyDownload:
                                item = McCalendar.QueryByServerId<McCalendar> (AccountId, serverId);
                                successInd = NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded;
                                break;
                            case McPending.Operations.ContactBodyDownload:
                                item = McContact.QueryByServerId<McContact> (AccountId, serverId);
                                successInd = NcResult.SubKindEnum.Info_ContactBodyDownloadSucceeded;
                                break;
                            case McPending.Operations.TaskBodyDownload:
                                item = McTask.QueryByServerId<McTask> (AccountId, serverId);
                                successInd = NcResult.SubKindEnum.Info_TaskBodyDownloadSucceeded;
                                break;
                            default:
                                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                                break;
                            }
                            if (null != item) {
                                // We are ignoring all the other crap that can come down (for now). We just want the Body.
                                // The item can be already deleted while we are waiting for this response.
                                // TODO - make sure we're not leaking the body if it is already deleted.
                                if (item is McEmailMessage) {
                                    item = item.UpdateWithOCApply<McEmailMessage> ((record) => {
                                        // In theory, ApplyAsXmlBody() can create an orphaned McBody if (1) UpdateWithOCApply repeats
                                        // the mutator and (2) the XML has the text of the body in the <Data> element.  But #2 doesn't
                                        // happen, since the WBXML code saves the <Data> to a McBody in advance.  So this is a theoretical
                                        // problem, not a practical problem.
                                        item.ApplyAsXmlBody (xmlBody);
                                        return true;
                                    });
                                    if (item.BodyId == 0) {
                                        Log.Error (Log.LOG_AS, "ItemOperations: BodyId == 0 after message body download");
                                        successInd = NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed;
                                    }
                                } else {
                                    item.ApplyAsXmlBody (xmlBody);
                                    item.Update ();
                                }
                            }
                            Log.Info (Log.LOG_AS, "ItemOperations item {0} {1}fetched.", serverId, 
                                (pending.DelayNotAllowed) ? "" : "pre");
                            if (null != pending) {
                                var result = NcResult.Info (successInd);
                                result.Value = item;
                                pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                                PendingList.Remove (pending);
                            }
                        } else {
                            // Can't figure out WTF here.
                            Log.Error (Log.LOG_AS, "ItemOperations: no ServerId and no FileReference.");
                        }
                        break;

                    case Xml.ItemOperations.StatusCode.ProtocolError_2:
                    case Xml.ItemOperations.StatusCode.ByteRangeInvalidOrTooLarge_8:
                    case Xml.ItemOperations.StatusCode.StoreUnknownOrNotSupported_9:
                    case Xml.ItemOperations.StatusCode.AttachmentOrIdInvalid_15:
                    case Xml.ItemOperations.StatusCode.ProtocolErrorMissing_155:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.ProtocolError);
                        break;

                    case Xml.ItemOperations.StatusCode.ServerError_3:
                    case Xml.ItemOperations.StatusCode.IoFailure_12:
                    case Xml.ItemOperations.StatusCode.ConversionFailure_14:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.ServerError);
                        break;

                    case Xml.ItemOperations.StatusCode.DocLibBadUri_4:
                    case Xml.ItemOperations.StatusCode.DocLibAccessDenied_5:
                    case Xml.ItemOperations.StatusCode.DocLibFailedServerConn_7:
                    case Xml.ItemOperations.StatusCode.PartialFailure_17:
                    case Xml.ItemOperations.StatusCode.ActionNotSupported_156:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.Unknown);
                        break;

                    case Xml.ItemOperations.StatusCode.DocLibAccessDeniedOrMissing_6:
                    case Xml.ItemOperations.StatusCode.FileEmpty_10:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.MissingOnServer);
                        break;

                    case Xml.ItemOperations.StatusCode.RequestTooLarge_11:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.TooBig);
                        break;

                    case Xml.ItemOperations.StatusCode.ResourceAccessDenied_16:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        MaybeResolveAsHardFail (FindPending (xmlFileReference, xmlServerId), NcResult.WhyEnum.AccessDeniedOrBlocked);
                        break;

                    default:
                        MaybeErrorFileDesc (xmlFileReference, xmlServerId);
                        Log.Error (Log.LOG_AS, "ItemOperations: unknown Status {0}", innerStatus);
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
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.ProtocolError);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD0");

            case Xml.ItemOperations.StatusCode.ServerError_3:
            case Xml.ItemOperations.StatusCode.IoFailure_12:
            case Xml.ItemOperations.StatusCode.ConversionFailure_14:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.ServerError);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD1");

            case Xml.ItemOperations.StatusCode.DocLibBadUri_4:
            case Xml.ItemOperations.StatusCode.DocLibAccessDenied_5:
            case Xml.ItemOperations.StatusCode.DocLibFailedServerConn_7:
            case Xml.ItemOperations.StatusCode.PartialFailure_17:
            case Xml.ItemOperations.StatusCode.ActionNotSupported_156:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.Unknown);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD2");

            case Xml.ItemOperations.StatusCode.DocLibAccessDeniedOrMissing_6:
            case Xml.ItemOperations.StatusCode.FileEmpty_10:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.MissingOnServer);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.RequestTooLarge_11:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.TooBig);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD3");

            case Xml.ItemOperations.StatusCode.ResourceAccessDenied_16:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.AccessDeniedOrBlocked);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IOHARD4");

            case Xml.ItemOperations.StatusCode.CredRequired_18:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.AuthFail, "IOAUTH");
            default:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.Unknown);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IOFAIL");
            }
        }
    }
}
