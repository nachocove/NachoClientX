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
            var pending = McAbstrObject.QueryById<McPending> (pendingId);
            if (null != pending) {
                NcAssert.True (Account.Id == pending.AccountId);
                NcAssert.True (McPending.StateEnum.UserBlocked == pending.State);
                pending.BlockReason = McPending.BlockReasonEnum.NotBlocked;
                pending.State = McPending.StateEnum.Eligible;
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCUNBLK");
                }, "UnblockPendingCmd");
            }
        }

        public override void DeletePendingCmd (int pendingId)
        {
            var pending = McAbstrObject.QueryById<McPending> (pendingId);
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSRCH");
            }, "SearchContactsReq");
        }

        public override string SendEmailCmd (int emailMessageId)
        {
            Log.Info (Log.LOG_AS, "SendEmailCmd({0})", emailMessageId);
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return null;
            }

            var pending = new McPending (Account.Id, emailMessage) {
                Operation = McPending.Operations.EmailSend,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSEND");
            }, "SendEmailCmd");
            Log.Info (Log.LOG_AS, "SendEmailCmd({0}) returning", emailMessageId);
            return pending.Token;
        }

        public override string SendEmailCmd (int emailMessageId, int calId)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == cal || null == emailMessage) {
                return null;
            }

            var pendingCalCre = NcModel.Instance.Db.Table<McPending> ().LastOrDefault (x => calId == x.ItemId);
            var pendingCalCreId = (null == pendingCalCre) ? 0 : pendingCalCre.Id;

            var pending = new McPending (Account.Id, emailMessage) {
                Operation = McPending.Operations.EmailSend,
            };
            pending.Insert ();

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
                    // FIXME needs to be a transaction.
                    return null;

                case McPending.StateEnum.Deleted:
                    // On server already.
                    break;

                default:
                    NcAssert.True (false);
                    break;
                }
            }

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSENDCAL");
            }, "SendEmailCmd(cal)");
            return pending.Token;
        }

        private string SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
                                      int folderId, bool originalEmailIsEmbedded)
        {
            if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
                return SendEmailCmd (newEmailMessageId);
            }
            McFolder folder;

            var refdEmailMessage = McAbstrObject.QueryById<McEmailMessage> (refdEmailMessageId);
            var newEmailMessage = McAbstrObject.QueryById<McEmailMessage> (newEmailMessageId);
            folder = McAbstrObject.QueryById<McFolder> (folderId);
            if (null == refdEmailMessage || null == newEmailMessage || null == folder) {
                return null;
            }

            var pending = new McPending (Account.Id, newEmailMessage) {
                Operation = Op,
                ServerId = refdEmailMessage.ServerId,
                ParentId = folder.ServerId,
                Smart_OriginalEmailIsEmbedded = originalEmailIsEmbedded,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSMF");
            }, "SmartEmailCmd");
            return pending.Token;
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
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailDelete,
                ParentId = primeFolder.ServerId,
                ServerId = emailMessage.ServerId
            };   
            pending.Insert ();

            // Delete the actual item.
            emailMessage.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            Log.Debug (Log.LOG_AS, "DeleteEmailCmd:Info_EmailMessageSetChanged sent.");
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELMSG");
            }, "DeleteEmailCmd");
            return pending.Token;
        }

        private string MoveItemCmd (McPending.Operations op, NcResult.SubKindEnum subKind,
                                    McAbstrItem item, McFolder srcFolder, int destFolderId)
        {
            if (null == srcFolder) {
                return null;
            }

            NcAssert.True (srcFolder.IsClientOwned != true, "Back end should not modify client-owned folders");

            var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
            if (null == destFolder) {
                return null;
            }

            NcAssert.True (destFolder.IsClientOwned != true, "Back end should not modify client-owned folders");
                
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMOVMSG");
            }, "MoveItemCmd");
            return move.Token;
        }

        public override string MoveEmailCmd (int emailMessageId, int destFolderId)
        {
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
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
                                          out McFolder folder) where T : McAbstrItem, new()
        {
            folder = null;
            item = McAbstrObject.QueryById<T> (itemId);
            if (null == item) {
                return false;
            }

            var folders = McFolder.QueryByFolderEntryId<T> (Account.Id, itemId);
            foreach (var maybe in folders) {
                NcAssert.True (maybe.IsClientOwned == false, "BackEnd should not operate on client-owned folders");
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

            // Mark the actual item.
            emailMessage.IsRead = true;
            emailMessage.Update ();
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMRMSG");
            }, "MarkEmailReadCmd");
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSF");
            }, "SetEmailFlagCmd");
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "ClearEmailFlagCmd");
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "MarkEmailFlagDone");
            return markFlagDone.Token;
        }

        public override string DnldEmailBodyCmd (int emailMessageId)
        {
            McEmailMessage emailMessage;
            McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                return null;
            }
            if (McAbstrItem.BodyStateEnum.Whole_0 == emailMessage.BodyState) {
                return null;
            }
            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailBodyDownload,
                ServerId = emailMessage.ServerId,
                ParentId = folder.ServerId,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDEBOD");
            }, "DnldEmailBodyCmd");
            return pending.Token;
        }

        public override string DnldAttCmd (int attId)
        {
            var att = McAbstrObject.QueryById<McAttachment> (attId);
            if (null == att) {
                return null;
            }
            if (att.IsDownloaded) {
                return null;
            }
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (att.EmailMessageId);
            if (null == emailMessage) {
                return null;
            }
            var update = new McPending (Account.Id) {
                Operation = McPending.Operations.AttachmentDownload,
                ServerId = emailMessage.ServerId,
                AttachmentId = attId,
            };
            update.Insert ();
            att.PercentDownloaded = 1;
            att.Update ();
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDATT");
            }, "DnldAttCmd");
            return update.Token;
        }

        public override string CreateCalCmd (int calId, int folderId)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id, cal) {
                Operation = McPending.Operations.CalCreate,
                ParentId = folder.ServerId,
                ClientId = cal.ClientId,
            };
            pending.Insert ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECAL");
            }, "CreateCalCmd");
            return pending.Token;
        }

        public override string UpdateCalCmd (int calId)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd should not operate on client-owned folders");

            var pending = new McPending (Account.Id, cal) {
                Operation = McPending.Operations.CalUpdate,
                ParentId = primeFolder.ServerId,
                ServerId = cal.ServerId,
            };   
            pending.Insert ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCAL");
            }, "UpdateCalCmd");
            return pending.Token;
        }

        public override string DeleteCalCmd (int calId)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalDelete,
                ParentId = primeFolder.ServerId,
                ServerId = cal.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            cal.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCAL");
            }, "DeleteCalCmd");
            return pending.Token;
        }

        public override string MoveCalCmd (int calId, int destFolderId)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.CalMove, NcResult.SubKindEnum.Info_CalendarSetChanged,
                cal, srcFolder, destFolderId);
        }

        public override string DnldCalBodyCmd (int calId)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, -1, out folder)) {
                return null;
            }
            if (McAbstrItem.BodyStateEnum.Whole_0 == cal.BodyState) {
                return null;
            }
            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalBodyDownload,
                ServerId = cal.ServerId,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDCALBOD");
            }, "DnldCalBodyCmd");
            return pending.Token;
        }

        public override string CreateContactCmd (int contactId, int folderId)
        {
            McContact contact;
            McFolder folder;
            if (!GetItemAndFolder<McContact> (contactId, out contact, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id, contact) {
                Operation = McPending.Operations.ContactCreate,
                ParentId = folder.ServerId,
                ClientId = contact.ClientId,
            };
            pending.Insert ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECNT");
            }, "CreateContactCmd");
            return pending.Token;
        }

        public override string UpdateContactCmd (int contactId)
        {
            var contact = McAbstrObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd should not operate on client-owned folders");

            var pending = new McPending (Account.Id, contact) {
                Operation = McPending.Operations.ContactUpdate,
                ParentId = primeFolder.ServerId,
                ServerId = contact.ServerId,
            };   
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCTC");
            }, "UpdateContactCmd");
            return pending.Token;
        }

        public override string DeleteContactCmd (int contactId)
        {
            var contact = McAbstrObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactDelete,
                ParentId = primeFolder.ServerId,
                ServerId = contact.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            contact.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCTC");
            }, "DeleteContactCmd");
            return pending.Token;
        }

        public override string MoveContactCmd (int contactId, int destFolderId)
        {
            var contact = McAbstrObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.ContactMove, NcResult.SubKindEnum.Info_ContactSetChanged,
                contact, srcFolder, destFolderId);
        }

        public override string DnldContactBodyCmd (int contactId)
        {
            McContact contact;
            McFolder folder;
            if (!GetItemAndFolder<McContact> (contactId, out contact, -1, out folder)) {
                return null;
            }
            if (McAbstrItem.BodyStateEnum.Whole_0 == contact.BodyState) {
                return null;
            }
            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactBodyDownload,
                ServerId = contact.ServerId,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDCONBOD");
            }, "DnldContactBodyCmd");
            return pending.Token;
        }

        public override string CreateTaskCmd (int taskId, int folderId)
        {
            McTask task;
            McFolder folder;
            if (!GetItemAndFolder<McTask> (taskId, out task, folderId, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id, task) {
                Operation = McPending.Operations.TaskCreate,
                ParentId = folder.ServerId,
                ClientId = task.ClientId,
            };
            pending.Insert ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRETSK");
            }, "CreateTaskCmd");
            return pending.Token;
        }

        public override string UpdateTaskCmd (int taskId)
        {
            var task = McAbstrObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd should not operate on client-owned folders");

            var pending = new McPending (Account.Id, task) {
                Operation = McPending.Operations.TaskUpdate,
                ParentId = primeFolder.ServerId,
                ServerId = task.ServerId,
            };   
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGTSK");
            }, "UpdateTaskCmd");
            return pending.Token;
        }

        public override string DeleteTaskCmd (int taskId)
        {
            var task = McAbstrObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }

            var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
            if (null == folders || 0 == folders.Count) {
                return null;
            }

            var primeFolder = folders.First ();
            NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.ContactDelete,
                ParentId = primeFolder.ServerId,
                ServerId = task.ServerId,
            };   
            pending.Insert ();

            // Delete the actual item.
            task.Delete ();

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELTSK");
            }, "DeleteTaskCmd");
            return pending.Token;
        }

        public override string MoveTaskCmd (int taskId, int destFolderId)
        {
            var task = McAbstrObject.QueryById<McTask> (taskId);
            if (null == task) {
                return null;
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.TaskMove, NcResult.SubKindEnum.Info_TaskSetChanged,
                task, srcFolder, destFolderId);
        }

        public override string DnldTaskBodyCmd (int taskId)
        {
            McTask task;
            McFolder folder;
            if (!GetItemAndFolder<McTask> (taskId, out task, -1, out folder)) {
                return null;
            }
            if (McAbstrItem.BodyStateEnum.Whole_0 == task.BodyState) {
                return null;
            }
            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.TaskBodyDownload,
                ServerId = task.ServerId,
            };
            pending.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDNLDTBOD");
            }, "DnldTaskBodyCmd");
            return pending.Token;
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
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCRESPCAL");
            }, "RespondCalCmd");

            return pending.Token;
        }

        public override string CreateFolderCmd (int destFolderId, string displayName, 
                                                Xml.FolderHierarchy.TypeCode folderType)
        {
            var serverId = DateTime.UtcNow.Ticks.ToString ();
            string destFldServerId;

            if (0 > destFolderId) {
                // Root case.
                destFldServerId = "0";
            } else {
                // Sub-folder case.
                var destFld = McAbstrObject.QueryById<McFolder> (destFolderId);
                if (null == destFld) {
                    return null;
                }
                NcAssert.True (destFld.IsClientOwned == false, "BackEnd should not modify client-owned folders");
                destFldServerId = destFld.ServerId;
            }

            var folder = McFolder.Create (Account.Id,
                             false,
                             false,
                             destFldServerId,
                             serverId,
                             displayName,
                             folderType);
            folder.IsAwaitingCreate = true;
            folder.Insert ();
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

            var createFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderCreate,
                ServerId = serverId,
                ParentId = destFldServerId,
                DisplayName = displayName,
                Folder_Type = folderType,
                // Epoch intentionally not set.
            };

            createFolder.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFCRE");
            }, "CreateFolderCmd");

            return createFolder.Token;
        }

        public override string CreateFolderCmd (string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return CreateFolderCmd (-1, displayName, folderType);
        }

        public override string DeleteFolderCmd (int folderId)
        {
            var folder = McAbstrObject.QueryById<McFolder> (folderId);
            NcAssert.True (folder.IsClientOwned == false, "Should not delete folders in client-owned folders.");
            NcAssert.True (!folder.IsAwaitingDelete, "Should not try to delete folder that has been already deleted.");

            var delFolder = new McPending (Account.Id) {
                Operation = McPending.Operations.FolderDelete,
                ServerId = folder.ServerId,
                ParentId = folder.ParentId,
            };

            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
            MarkFoldersAwaitingDelete (folder);

            delFolder.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFDEL");
            }, "DeleteFolderCmd");

            return delFolder.Token;
        }

        // recursively mark param and its children with isAwaitingDelete == true
        public void MarkFoldersAwaitingDelete (McFolder folder)
        {
            folder.IsAwaitingDelete = true;
            folder.Update ();
            var children = McFolder.QueryByParentId (folder.AccountId, folder.ServerId);
            foreach (McFolder child in children) {
                MarkFoldersAwaitingDelete (child);
            }
        }

        public override string MoveFolderCmd (int folderId, int destFolderId)
        {
            var folder = McAbstrObject.QueryById<McFolder> (folderId);
            var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
            NcAssert.True (folder.IsClientOwned == false, "BackEnd should not move client-owned folders");
            NcAssert.True (destFolder.IsClientOwned == false, "BackEnd should not modify client-owned folders");

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
                Folder_Type = folder.Type,
            };

            upFolder.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP1");
            }, "MoveFolderCmd");

            return upFolder.Token;
        }

        public override string RenameFolderCmd (int folderId, string displayName)
        {
            var folder = McAbstrObject.QueryById<McFolder> (folderId);
            NcAssert.True (folder.IsClientOwned == false, "BackEnd cannot modify client-owned folders");

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
                Folder_Type = folder.Type,
            };

            upFolder.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP2");
            }, "RenameFolderCmd");
            return upFolder.Token;
        }
    }
}