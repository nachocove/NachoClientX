//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public partial class AsProtoControl : ProtoControl, IBEContext
    {
        public override void UnblockPendingCmd (int pendingId)
        {
            var pending = McObject.QueryById<McPending> (pendingId);
            if (null != pending) {
                NcAssert.True (Account.Id == pending.AccountId);
                NcAssert.True (McPending.StateEnum.UserBlocked == pending.State);
                pending.BlockReason = McPending.BlockReasonEnum.NotBlocked;
                pending.State = McPending.StateEnum.Eligible;
                Task.Run (delegate {
                    Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCUNBLK");
                });
            }
        }

        public override void DeletePendingCmd (int pendingId)
        {
            var pending = McObject.QueryById<McPending> (pendingId);
            if (null != pending) {
                NcAssert.True (Account.Id == pending.AccountId);
                pending.ResolveAsCancelled ();
            }
        }

        public override string StartSearchContactsReq (string prefix, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchContactsReq (prefix, maxResults, token);
            return token;
        }

        public override void SearchContactsReq (string prefix, uint? maxResults, string token)
        {
            McPending.ResolvePendingSearchReqs (Account.Id, token, true);
            var newSearch = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactSearch,
                Search_Prefix = prefix,
                Search_MaxResults = (null == maxResults) ? 50 : (uint)maxResults,
                Token = token
            };
            newSearch.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCSRCH");
            });
        }

        public override string SendEmailCmd (int emailMessageId)
        {
            var sendUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSend,
                ItemId = emailMessageId
            };
            sendUpdate.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCSEND");
            });
            return sendUpdate.Token;
        }

        public override string SendEmailCmd (int emailMessageId, int calId)
        {
            var cal = McObject.QueryById<McCalendar> (calId);
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == cal || null == emailMessage) {
                return null;
            }

            var pendingCalCre = NcModel.Instance.Db.Table<McPending> ().LastOrDefault (x => calId == x.ItemId);
            var pendingCalCreId = (null == pendingCalCre) ? 0 : pendingCalCre.Id;

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSend,
                ItemId = emailMessageId,
            };

            // 0 means pending has already been completed & deleted.
            if (0 != pendingCalCreId) {
                // FIXME - race condition WRT state of pred pending obj - it could change between switch and case.
                switch (pendingCalCre.State) {
                case McPending.StateEnum.Deferred:
                case McPending.StateEnum.Dispatched:
                case McPending.StateEnum.Eligible:
                case McPending.StateEnum.PredBlocked:
                case McPending.StateEnum.UserBlocked:
                    pending.MarkPredBlocked (pendingCalCreId);
                    break;

                case McPending.StateEnum.Failed:
                    return null;

                case McPending.StateEnum.Deleted:
                    // On server already.
                    break;

                default:
                    NcAssert.True (false);
                    break;
                }
            }

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCSENDCAL");
            });
            return pending.Token;
        }

        private string SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
                                      int folderId, bool originalEmailIsEmbedded)
        {
            if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
                return SendEmailCmd (newEmailMessageId);
            }

            McEmailMessage refdEmailMessage;
            McFolder folder;

            refdEmailMessage = McObject.QueryById<McEmailMessage> (refdEmailMessageId);
            folder = McObject.QueryById<McFolder> (folderId);
            if (null == refdEmailMessage || null == folder) {
                return null;
            }

            var smartUpdate = new McPending (Account.Id) {
                Operation = Op,
                ItemId = newEmailMessageId,
                ServerId = refdEmailMessage.ServerId,
                ParentId = folder.ServerId,
                Smart_OriginalEmailIsEmbedded = originalEmailIsEmbedded,
            };
            smartUpdate.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCSMF");
            });
            return smartUpdate.Token;
        }

        public override string ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded)
        {
            return SmartEmailCmd (McPending.Operations.EmailReply,
                newEmailMessageId, repliedToEmailMessageId, folderId, originalEmailIsEmbedded);
        }

        public override string ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded)
        {
            return SmartEmailCmd (McPending.Operations.EmailForward,
                newEmailMessageId, forwardedEmailMessageId, folderId, originalEmailIsEmbedded);
        }

        public override string DeleteEmailCmd (int emailMessageId)
        {
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailDelete,
                ParentId = primeFolder.ServerId,
                ServerId = emailMessage.ServerId
            };   
            pending.Insert ();

            // SCORING - Email read? If not, it's going to cost you
            if (!emailMessage.IsRead) {
                McContact sender = emailMessage.GetFromContact ();
                if (null != sender) {
                    sender.UpdateScore ("delete unread", -1);
                }
            }

            // Delete the actual item.
            foreach (var folder in folders) {
                folder.Unlink (emailMessage);
            }
            emailMessage.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCDELMSG");
            });
            return pending.Token;
        }

        private string MoveItemCmd (McPending.Operations op, NcResult.SubKindEnum subKind,
            McItem item, McFolder srcFolder, int destFolderId)
        {
            if (null == srcFolder) {
                return null;
            }
            var destFolder = McObject.QueryById<McFolder> (destFolderId);
            if (null == destFolder) {
                return null;
            }

            var move = new McPending (Account.Id) {
                Operation = op,
                ServerId = item.ServerId,
                ParentId = srcFolder.ServerId,
                DestParentId = destFolder.ServerId,
            };

            move.Insert ();
            // Move the actual item.
            destFolder.Link (item);
            srcFolder.Unlink (item);

            StatusInd (NcResult.Info (subKind));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCMOVMSG");
            });
            return move.Token;
        }

        public override string MoveEmailCmd (int emailMessageId, int destFolderId)
        {
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.EmailMove, NcResult.SubKindEnum.Info_EmailMessageSetChanged,
                emailMessage, srcFolder, destFolderId);
        }

        private bool GetItemAndFolder<T> (int itemId, 
                                          out T item,
                                          int folderId,
                                          out McFolder folder) where T : McItem, new()
        {
            folder = null;
            item = McObject.QueryById<T> (itemId);
            if (null == item) {
                return false;
            }

            var folders = McFolder.QueryByFolderEntryId<T> (Account.Id, itemId);
            foreach (var maybe in folders) {
                if (maybe.IsClientOwned) {
                    continue;
                }
                if (-1 == folderId || maybe.Id == folderId) {
                    folder = maybe;
                    return true;
                }
            }
            return false;
        }

        public override string MarkEmailReadCmd (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                return null;
            }

            var markUpdate = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailMarkRead,
                ServerId = emailMessage.ServerId,
                ParentId = folder.ServerId,
            };   
            markUpdate.Insert ();

            // SCORING - Email read. Has it been an hour?
            Log.Info (Log.LOG_BRAIN, "EMAIL DATE:", emailMessage.DateReceived);
            if (emailMessage.DateReceived.AddHours (1.0) > DateTime.Now) {
                McContact sender = emailMessage.GetFromContact ();
                if (null != sender) {
                    sender.UpdateScore ("timely read", +1);
                }
            }

            // Mark the actual item.
            emailMessage.IsRead = true;
            emailMessage.Update ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCMRMSG");
            });
            return markUpdate.Token;
        }

        public override string SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                return null;
            }

            var setFlag = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSetFlag,
                ServerId = emailMessage.ServerId,
                ParentId = folder.ServerId,
                EmailSetFlag_FlagType = flagType,
                EmailSetFlag_Start = start,
                EmailSetFlag_UtcStart = utcStart,
                EmailSetFlag_Due = due,
                EmailSetFlag_UtcDue = utcDue,
            };
            setFlag.Insert ();

            // Set the Flag info in the DB item.
            emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
            emailMessage.FlagType = flagType;
            emailMessage.FlagDeferUntil = start;
            emailMessage.FlagUtcDeferUntil = utcStart;
            emailMessage.FlagDue = due;
            emailMessage.FlagUtcDue = utcDue;
            emailMessage.Update ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCSF");
            });
            return setFlag.Token;
        }

        public override string ClearEmailFlagCmd (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                return null;
            }

            var clearFlag = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailClearFlag,
                ServerId = emailMessage.ServerId,
                ParentId = folder.ServerId,
            };
            clearFlag.Insert ();

            emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
            emailMessage.Update ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            });
            return clearFlag.Token;
        }

        public override string MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                return null;
            }

            var markFlagDone = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailMarkFlagDone,
                ServerId = emailMessage.ServerId,
                ParentId = folder.ServerId,
                EmailMarkFlagDone_CompleteTime = completeTime,
                EmailMarkFlagDone_DateCompleted = dateCompleted,
            };
            markFlagDone.Insert ();

            emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
            emailMessage.FlagCompleteTime = completeTime;
            emailMessage.FlagDateCompleted = dateCompleted;
            emailMessage.Update ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            });
            return markFlagDone.Token;
        }

        public override string DnldAttCmd (int attId)
        {
            var att = McObject.QueryById<McAttachment> (attId);
            if (null == att) {
                return null;
            }
            if (att.IsDownloaded) {
                return null;
            }
            var update = new McPending (Account.Id) {
                Operation = McPending.Operations.AttachmentDownload,
                AttachmentId = attId,
            };
            update.Insert ();
            att.PercentDownloaded = 1;
            att.Update ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDATT");
            });
            return update.Token;
        }

        public override string CreateCalCmd (int calId, int folderId)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalCreate,
                ItemId = calId,
                ParentId = folder.ServerId,
                ClientId = cal.ClientId,
            };

            pending.Insert ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECAL");
            });
            return pending.Token;
        }

        public override string UpdateCalCmd (int calId)
        {
            var cal = McObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                ItemId = calId,
                Operation = McPending.Operations.CalUpdate,
                ParentId = primeFolder.ServerId,
                ServerId = cal.ServerId,
            };   
            pending.Insert ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCAL");
            });
            return pending.Token;
        }

        public override string DeleteCalCmd (int calId)
        {
            var cal = McObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalDelete,
                ParentId = primeFolder.ServerId,
                ServerId = cal.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            foreach (var folder in folders) {
                folder.Unlink (cal);
            }
            cal.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCAL");
            });
            return pending.Token;
        }

        public override string MoveCalCmd (int calId, int destFolderId)
        {
            var cal = McObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.CalMove, NcResult.SubKindEnum.Info_CalendarSetChanged,
                cal, srcFolder, destFolderId);
        }

        public override string CreateContactCmd (int contactId, int folderId)
        {
            McContact contact;
            McFolder folder;
            if (!GetItemAndFolder<McContact> (contactId, out contact, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactCreate,
                ItemId = contactId,
                ParentId = folder.ServerId,
                ClientId = contact.ClientId,
            };

            pending.Insert ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECNT");
            });
            return pending.Token;
        }

        public override string UpdateContactCmd (int contactId)
        {
            var contact = McObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactUpdate,
                ItemId = contactId,
                ParentId = primeFolder.ServerId,
                ServerId = contact.ServerId,
            };   
            pending.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCTC");
            });
            return pending.Token;
        }

        public override string DeleteContactCmd (int contactId)
        {
            var contact = McObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactDelete,
                ParentId = primeFolder.ServerId,
                ServerId = contact.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            foreach (var folder in folders) {
                folder.Unlink (contact);
            }
            contact.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCTC");
            });
            return pending.Token;
        }

        public override string MoveContactCmd (int contactId, int destFolderId)
        {
            var contact = McObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.ContactMove, NcResult.SubKindEnum.Info_ContactSetChanged,
                contact, srcFolder, destFolderId);
        }

        public override string CreateTaskCmd (int taskId, int folderId)
        {
            McTask task;
            McFolder folder;
            if (!GetItemAndFolder<McTask> (taskId, out task, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.TaskCreate,
                ItemId = taskId,
                ParentId = folder.ServerId,
                ClientId = task.ClientId,
            };

            pending.Insert ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCRETSK");
            });
            return pending.Token;
        }

        public override string UpdateTaskCmd (int taskId)
        {
            var task = McObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.TaskUpdate,
                ItemId = taskId,
                ParentId = primeFolder.ServerId,
                ServerId = task.ServerId,
            };   
            pending.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGTSK");
            });
            return pending.Token;
        }

        public override string DeleteTaskCmd (int taskId)
        {
            var task = McObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            // FIXME - ensure not client-owned folder.
            var primeFolder = folders.First ();

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactDelete,
                ParentId = primeFolder.ServerId,
                ServerId = task.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            foreach (var folder in folders) {
                folder.Unlink (task);
            }
            task.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCDELTSK");
            });
            return pending.Token;
        }

        public override string MoveTaskCmd (int taskId, int destFolderId)
        {
            var task = McObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.TaskMove, NcResult.SubKindEnum.Info_TaskSetChanged,
                task, srcFolder, destFolderId);
        }

        public override string RespondCalCmd (int calId, NcResponseType response)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, -1, out folder)) {
                return null;
            }

            Xml.MeetingResp.UserResponseCode apiResponse;
            switch (response) {
            case NcResponseType.Accepted:
                apiResponse = Xml.MeetingResp.UserResponseCode.Accepted_1;
                break;

            case NcResponseType.Tentative:
                apiResponse = Xml.MeetingResp.UserResponseCode.Tentatively_2;
                break;

            case NcResponseType.Declined:
                apiResponse = Xml.MeetingResp.UserResponseCode.Declined_3;
                break;

            default:
                return null;
            }

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalRespond,
                ServerId = cal.ServerId,
                ParentId = folder.ServerId,
                CalResponse = apiResponse,
            };

            pending.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCRESPCAL");
            });

            return pending.Token;
        }

        public override string CreateFolderCmd (int destFolderId, string displayName, 
                                                Xml.FolderHierarchy.TypeCode folderType,
                                                bool isClientOwned, bool isHidden)
        {
            var serverId = DateTime.UtcNow.Ticks.ToString ();
            string destFldServerId;

            if (0 > destFolderId) {
                // Root case.
                destFldServerId = "0";
            } else {
                // Sub-folder case.
                var destFld = McObject.QueryById<McFolder> (destFolderId);
                if (null == destFld) {
                    return null;
                }
                if (isClientOwned ^ destFld.IsClientOwned) {
                    // Keep client/server-owned domains separate for now.
                    return null;
                }
                destFldServerId = destFld.ServerId;
            }

            if (isHidden && !isClientOwned) {
                return null;
            }

            var folder = McFolder.Create (Account.Id,
                             isClientOwned,
                             isHidden,
                             destFldServerId,
                             serverId,
                             displayName,
                             folderType);
            folder.IsAwaitingCreate = !isClientOwned;
            folder.Insert ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

            if (isClientOwned) {
                return McPending.KSynchronouslyCompleted;
            }

            var createFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderCreate,
                ServerId = serverId,
                ParentId = destFldServerId,
                DisplayName = displayName,
                FolderCreate_Type = folderType,
                // Epoch intentionally not set.
            };

            createFolder.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCFCRE");
            });

            return createFolder.Token;
        }

        public override string CreateFolderCmd (string displayName, Xml.FolderHierarchy.TypeCode folderType,
                                                bool isClientOwned, bool isHidden)
        {
            return CreateFolderCmd (-1, displayName, folderType, isClientOwned, isHidden);
        }

        public override string DeleteFolderCmd (int folderId)
        {
            var folder = McObject.QueryById<McFolder> (folderId);
            if (folder.IsClientOwned) {
                folder.Delete ();
                return McPending.KSynchronouslyCompleted;
            }

            var delFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderDelete,
                ServerId = folder.ServerId,
                ParentId = folder.ParentId,
            };

            folder.Delete ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

            delFolder.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCFDEL");
            });

            return delFolder.Token;
        }

        public override string MoveFolderCmd (int folderId, int destFolderId)
        {
            var folder = McObject.QueryById<McFolder> (folderId);
            var destFolder = McObject.QueryById<McFolder> (destFolderId);
            if (folder.IsClientOwned ^ destFolder.IsClientOwned) {
                return null;
            }

            folder.ParentId = destFolder.ServerId;
            folder.Update ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

            if (folder.IsClientOwned) {
                return McPending.KSynchronouslyCompleted;
            }

            var upFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderUpdate,
                ServerId = folder.ServerId,
                ParentId = folder.ParentId,
                DestParentId = destFolder.ServerId,
                DisplayName = folder.DisplayName,
            };

            upFolder.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP1");
            });

            return upFolder.Token;
        }

        public override string RenameFolderCmd (int folderId, string displayName)
        {
            var folder = McObject.QueryById<McFolder> (folderId);

            folder.DisplayName = displayName;
            folder.Update ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

            if (folder.IsClientOwned) {
                return McPending.KSynchronouslyCompleted;
            }

            var upFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderUpdate,
                ServerId = folder.ServerId,
                ParentId = folder.ParentId,
                DestParentId = folder.ParentId, // Set only because Move & Rename map to the same EAS command.
                DisplayName = displayName,
            };

            upFolder.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP2");
            });
            return upFolder.Token;
        }
    }
}