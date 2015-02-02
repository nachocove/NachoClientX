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
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (Account.Id == pending.AccountId);
                    NcAssert.True (McPending.StateEnum.UserBlocked == pending.State);
                    pending.BlockReason = McPending.BlockReasonEnum.NotBlocked;
                    pending.State = McPending.StateEnum.Eligible;
                    pending.Update ();
                    NcTask.Run (delegate {
                        Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCUNBLK");
                    }, "UnblockPendingCmd");
                }
            });
        }

        public override void DeletePendingCmd (int pendingId)
        {
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (Account.Id == pending.AccountId);
                    pending.ResolveAsCancelled (false);
                }
            });
        }

        public override void Prioritize (string token)
        {
            NcModel.Instance.RunInTransaction (() => {
                var pendings = McPending.QueryByToken (Account.Id, token);
                foreach (var pending in pendings) {
                    pending.Prioritize ();
                }
            });
        }

        public override void Cancel (string token)
        {
            NcModel.Instance.RunInTransaction (() => {
                var pendings = McPending.QueryByToken (Account.Id, token);
                foreach (var iterPending in pendings) {
                    var pending = iterPending;
                    switch (pending.State) {
                    case McPending.StateEnum.Eligible:
                        pending.Delete ();
                        break;

                    case McPending.StateEnum.Deferred:
                    case McPending.StateEnum.Failed:
                    case McPending.StateEnum.PredBlocked:
                    case McPending.StateEnum.UserBlocked:
                        if (McPending.Operations.ContactSearch == pending.Operation) {
                            McPending.ResolvePendingSearchReqs (Account.Id, token, false);
                        } else {
                            pending.ResolveAsCancelled ();
                        }
                        break;

                    case McPending.StateEnum.Dispatched:
                    // TODO: find a way to prevent re-try, and at least mark as do-not-delay.
                        break;

                    case McPending.StateEnum.Deleted:
                    // Nothing to do.
                        break;

                    default:
                        NcAssert.CaseError (string.Format ("Unknown State {0}", pending.State));
                        break;
                    }
                }
            });
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
            newSearch.DoNotDelay ();
            newSearch.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSRCH");
            }, "SearchContactsReq");
        }

        public override string SendEmailCmd (int emailMessageId)
        {
            string token = null;
            Log.Info (Log.LOG_AS, "SendEmailCmd({0})", emailMessageId);
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    return;
                }
                var pending = new McPending (Account.Id, emailMessage) {
                    Operation = McPending.Operations.EmailSend,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSEND");
            }, "SendEmailCmd");
            Log.Info (Log.LOG_AS, "SendEmailCmd({0}) returning", emailMessageId);
            return token;
        }

        public override string SendEmailCmd (int emailMessageId, int calId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == cal || null == emailMessage) {
                    return;
                }

                var pendingCalCre = NcModel.Instance.Db.Table<McPending> ().LastOrDefault (x => calId == x.ItemId);
                var pendingCalCreId = (null == pendingCalCre) ? 0 : pendingCalCre.Id;

                var pending = new McPending (Account.Id, emailMessage) {
                    Operation = McPending.Operations.EmailSend,
                };
                pending.Insert ();

                // TODO consider unifying this dependency code with that in McPending.
                // 0 means pending has already been completed & deleted.
                if (0 != pendingCalCreId) {
                    switch (pendingCalCre.State) {
                    case McPending.StateEnum.Deferred:
                    case McPending.StateEnum.Dispatched:
                    case McPending.StateEnum.Eligible:
                    case McPending.StateEnum.PredBlocked:
                    case McPending.StateEnum.UserBlocked:
                        pending.MarkPredBlocked (pendingCalCreId);
                        break;

                    case McPending.StateEnum.Failed:
                        pending.Delete ();
                        return;

                    case McPending.StateEnum.Deleted:
                        // On server already.
                        break;

                    default:
                        NcAssert.True (false);
                        break;
                    }
                }
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSENDCAL");
            }, "SendEmailCmd(cal)");
            return token;
        }

        private string SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
                                      int folderId, bool originalEmailIsEmbedded)
        {
            string token = null;
            if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
                return SendEmailCmd (newEmailMessageId);
            }
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                var refdEmailMessage = McAbstrObject.QueryById<McEmailMessage> (refdEmailMessageId);
                var newEmailMessage = McAbstrObject.QueryById<McEmailMessage> (newEmailMessageId);
                folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == refdEmailMessage || null == newEmailMessage || null == folder) {
                    return;
                }

                var pending = new McPending (Account.Id, newEmailMessage) {
                    Operation = Op,
                    ServerId = refdEmailMessage.ServerId,
                    ParentId = folder.ServerId,
                    Smart_OriginalEmailIsEmbedded = originalEmailIsEmbedded,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSMF");
            }, "SmartEmailCmd");
            return token;
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
            if (originalEmailIsEmbedded) {
                var attachments = McAttachment.QueryByItemId (AccountId, forwardedEmailMessageId, McAbstrFolderEntry.ClassCodeEnum.Email);
                foreach (var attach in attachments) {
                    if (McAbstrFileDesc.FilePresenceEnum.None == attach.FilePresence) {
                        var token = DnldAttCmd (attach.Id);
                        if (null == token) {
                            // FIXME - is this correct behavior in this case?
                            return null;
                        }
                    }
                }
            }
            return SmartEmailCmd (McPending.Operations.EmailForward,
                newEmailMessageId, forwardedEmailMessageId, folderId, originalEmailIsEmbedded);
        }

        public override string DeleteEmailCmd (int emailMessageId)
        {
            string token = null;
            McEmailMessage emailMessage = null;
            NcModel.Instance.RunInTransaction (() => {
                emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    return;
                }

                var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }

                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = emailMessage.ServerId
                };   
                pending.Insert ();
                token = pending.Token;
                emailMessage.Delete ();
            });
            if (null != emailMessage && null != token) {
                Log.Info (Log.LOG_AS, "DeleteEmailCmd: Id {0}/ServerId {1} => Token {2}",
                    emailMessage.Id, emailMessage.ServerId, token);

                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                Log.Debug (Log.LOG_AS, "DeleteEmailCmd:Info_EmailMessageSetChanged sent.");
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELMSG");
                }, "DeleteEmailCmd");
            }
            return token;
        }

        private string MoveItemCmd (McPending.Operations op, NcResult.SubKindEnum subKind,
                                    McAbstrItem item, McFolder srcFolder, int destFolderId)
        {
            string token = null;
            if (null == srcFolder) {
                return null;
            }
            NcAssert.True (srcFolder.IsClientOwned != true, "Back end must not modify client-owned folders");
            NcModel.Instance.RunInTransaction (() => {
                var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
                if (null == destFolder) {
                    return;
                }

                NcAssert.True (destFolder.IsClientOwned != true, "Back end must not modify client-owned folders");
                McPending markUpdate = null;
                if (McPending.Operations.EmailMove == op && Server.HostIsGMail ()) {
                    // Need to make sure the email is marked read to get it out of GFE Inbox.
                    var emailMessage = item as McEmailMessage;
                    if (null != emailMessage && !emailMessage.IsRead) {
                        markUpdate = new McPending (Account.Id) {
                            Operation = McPending.Operations.EmailMarkRead,
                            ServerId = emailMessage.ServerId,
                            ParentId = srcFolder.ServerId,
                        };   
                        markUpdate.Insert ();

                        // Mark the actual item.
                        emailMessage.IsRead = true;
                        emailMessage.Update ();
                    }
                }
                var pending = new McPending (Account.Id) {
                    Operation = op,
                    ServerId = item.ServerId,
                    ParentId = srcFolder.ServerId,
                    DestParentId = destFolder.ServerId,
                };

                pending.Insert ();
                token = pending.Token;
                destFolder.Link (item);
                srcFolder.Unlink (item);
            });
            StatusInd (NcResult.Info (subKind));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMOVMSG");
            }, "MoveItemCmd");
            return token;
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
                NcAssert.True (maybe.IsClientOwned == false, "BackEnd must not operate on client-owned folders");
                if (-1 == folderId || maybe.Id == folderId) {
                    folder = maybe;
                    return true;
                }
            }
            return false;
        }

        public override string MarkEmailReadCmd (int emailMessageId)
        {
            string token = null;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailMarkRead,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
                emailMessage.IsRead = true;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMRMSG");
            }, "MarkEmailReadCmd");
            return token;
        }

        public override string SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            string token = null;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailSetFlag,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                    EmailSetFlag_FlagType = flagType,
                    EmailSetFlag_Start = start,
                    EmailSetFlag_UtcStart = utcStart,
                    EmailSetFlag_Due = due,
                    EmailSetFlag_UtcDue = utcDue,
                };
                pending.Insert ();
                token = pending.Token;
                // Set the Flag info in the DB item.
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
                emailMessage.FlagType = flagType;
                emailMessage.FlagStartDate = start;
                emailMessage.FlagUtcStartDate = utcStart;
                emailMessage.FlagDue = due;
                emailMessage.FlagUtcDue = utcDue;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSF");
            }, "SetEmailFlagCmd");
            return token;
        }

        public override string ClearEmailFlagCmd (int emailMessageId)
        {
            string token = null;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailClearFlag,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                token = pending.Token;
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "ClearEmailFlagCmd");
            return token;
        }

        public override string MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            string token = null;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailMarkFlagDone,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                    EmailMarkFlagDone_CompleteTime = completeTime,
                    EmailMarkFlagDone_DateCompleted = dateCompleted,
                };
                pending.Insert ();
                token = pending.Token;
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
                emailMessage.FlagCompleteTime = completeTime;
                emailMessage.FlagDateCompleted = dateCompleted;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "MarkEmailFlagDone");
            return token;
        }

        public override string DnldEmailBodyCmd (int emailMessageId, bool doNotDefer = false)
        {
            string token = null;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder)) {
                    Log.Error (Log.LOG_AS, "DnldEmailBodyCmd: can't find McEmailMessage and/or McFolder");
                    return;
                }
                var body = emailMessage.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    Log.Error (Log.LOG_AS, "DnldEmailBodyCmd: FilePresence is Complete");
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailBodyDownload,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    // TODO: Insert but have the result of the 1st duplicate trigger the same result events for all duplicates.
                    Log.Info (Log.LOG_AS, "DnldEmailBodyCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
                    token = dup.Token;
                    return;
                }
                if (doNotDefer) {
                    pending.DoNotDelay ();
                }
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDEBOD");
            }, "DnldEmailBodyCmd");
            return token;
        }

        public override string DnldAttCmd (int attId, bool doNotDefer = false)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var att = McAbstrObject.QueryById<McAttachment> (attId);
                if (null == att) {
                    return;
                }
                if (McAbstrFileDesc.FilePresenceEnum.None != att.FilePresence) {
                    return;
                }
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (att.ItemId);
                if (null == emailMessage) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.AttachmentDownload,
                    ServerId = emailMessage.ServerId,
                    AttachmentId = attId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    // TODO: Insert but have the result of the 1st duplicate trigger the same result events for all duplicates.
                    Log.Info (Log.LOG_AS, "DnldAttCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
                    token = dup.Token;
                    return;
                }

                if (doNotDefer) {
                    pending.DoNotDelay ();
                }
                pending.Insert ();
                token = pending.Token;
                att.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
                att.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDATT");
            }, "DnldAttCmd");
            return token;
        }

        public override string CreateCalCmd (int calId, int folderId)
        {
            string token = null;
            McCalendar cal;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McCalendar> (calId, out cal, folderId, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id, cal) {
                    Operation = McPending.Operations.CalCreate,
                    ParentId = folder.ServerId,
                    ClientId = cal.ClientId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECAL");
            }, "CreateCalCmd");
            return token;
        }

        public override string UpdateCalCmd (int calId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                if (null == cal) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd should not operate on client-owned folders");

                var pending = new McPending (Account.Id, cal) {
                    Operation = McPending.Operations.CalUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = cal.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCAL");
            }, "UpdateCalCmd");
            return token;
        }

        public override string DeleteCalCmd (int calId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                if (null == cal) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "Must not delete items in client-owned folders");

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.CalDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = cal.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
                cal.Delete ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCAL");
            }, "DeleteCalCmd");
            return token;
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
            string token = null;
            McCalendar cal;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McCalendar> (calId, out cal, -1, out folder)) {
                    return;
                }
                var body = cal.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.CalBodyDownload,
                    ServerId = cal.ServerId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDCALBOD");
            }, "DnldCalBodyCmd");
            return token;
        }

        public override string ForwardCalCmd (int newEmailMessageId, int forwardedCalId, int folderId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var refdCalEvent = McAbstrObject.QueryById<McCalendar> (forwardedCalId);
                var newEmailMessage = McAbstrObject.QueryById<McEmailMessage> (newEmailMessageId);
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == refdCalEvent || null == newEmailMessage || null == folder) {
                    return;
                }

                var pending = new McPending (Account.Id, newEmailMessage) {
                    Operation = McPending.Operations.CalForward,
                    ServerId = refdCalEvent.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCCALF");
            }, "ForwardCalCmd");
            return token;
        }

        public override string CreateContactCmd (int contactId, int folderId)
        {
            string token = null;
            McContact contact;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McContact> (contactId, out contact, folderId, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id, contact) {
                    Operation = McPending.Operations.ContactCreate,
                    ParentId = folder.ServerId,
                    ClientId = contact.ClientId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECNT");
            }, "CreateContactCmd");
            return token;
        }

        public override string UpdateContactCmd (int contactId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var contact = McAbstrObject.QueryById<McContact> (contactId);
                if (null == contact) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd must not operate on client-owned folders");

                var pending = new McPending (Account.Id, contact) {
                    Operation = McPending.Operations.ContactUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = contact.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCTC");
            }, "UpdateContactCmd");
            return token;
        }

        public override string DeleteContactCmd (int contactId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var contact = McAbstrObject.QueryById<McContact> (contactId);
                if (null == contact) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "Should not delete items in client-owned folders");
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = contact.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
                contact.Delete ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCTC");
            }, "DeleteContactCmd");
            return token;
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
            string token = null;
            McContact contact;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McContact> (contactId, out contact, -1, out folder)) {
                    return;
                }
                var body = contact.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactBodyDownload,
                    ServerId = contact.ServerId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDCONBOD");
            }, "DnldContactBodyCmd");
            return token;
        }

        public override string CreateTaskCmd (int taskId, int folderId)
        {
            string token = null;
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McTask> (taskId, out task, folderId, out folder)) {
                    return;
                }
                var pending = new McPending (Account.Id, task) {
                    Operation = McPending.Operations.TaskCreate,
                    ParentId = folder.ServerId,
                    ClientId = task.ClientId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRETSK");
            }, "CreateTaskCmd");
            return token;
        }

        public override string UpdateTaskCmd (int taskId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var task = McAbstrObject.QueryById<McTask> (taskId);
                if (null == task) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "BackEnd should not operate on client-owned folders");

                var pending = new McPending (Account.Id, task) {
                    Operation = McPending.Operations.TaskUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGTSK");
            }, "UpdateTaskCmd");
            return token;
        }

        public override string DeleteTaskCmd (int taskId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var task = McAbstrObject.QueryById<McTask> (taskId);
                if (null == task) {
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
                if (null == folders || 0 == folders.Count) {
                    return;
                }
                var primeFolder = folders.First ();
                NcAssert.True (primeFolder.IsClientOwned == false, "Must not delete items in client-owned folders");

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                token = pending.Token;
                task.Delete ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELTSK");
            }, "DeleteTaskCmd");
            return token;
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
            string token = null;
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McTask> (taskId, out task, -1, out folder)) {
                    return;
                }
                var body = task.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.TaskBodyDownload,
                    ServerId = task.ServerId,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDTBOD");
            }, "DnldTaskBodyCmd");
            return token;
        }

        public override string RespondEmailCmd (int emailMessageId, NcResponseType response)
        {
            return RespondItemCmd<McEmailMessage> (emailMessageId, response);
        }

        public override string RespondCalCmd (int calId, NcResponseType response, DateTime? instance = null)
        {
            return RespondItemCmd<McCalendar> (calId, response, instance);
        }

        private string RespondItemCmd<T> (int itemId, NcResponseType response, DateTime? instance = null)
            where T : McAbstrItem, new ()
        {
            string token = null;
            McPending pending;
            NcModel.Instance.RunInTransaction (() => {
                T item;
                McFolder folder;
                if (!GetItemAndFolder<T> (itemId, out item, -1, out folder)) {
                    return;
                }
                // From MS-ASCMD:
                // When protocol versions 2.5, 12.0, 12.1, or 14.0 are used, the MeetingResponse command cannot be used to modify meeting requests in the Calendar folder.
                // AND
                // In Exchange 2007, the MeetingResponse command is used to accept, tentatively accept, or decline a meeting request only in the user's Inbox folder.
                // 
                // In this (these?) scenarios, update the Cal item in the DB, and Sync the change to the server.
                //
                if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 != folder.Type &&
                14.1 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
                    var cal = item as McCalendar;
                    if (null == cal) {
                        Log.Error (Log.LOG_AS, "Cannot respond to an email-invite message not in Inbox for older EAS ({0})", folder.Type);
                        return;
                    }
                    switch (response) {
                    case NcResponseType.Accepted:
                        cal.ResponseType = NcResponseType.Accepted;
                        break;
                    case NcResponseType.Tentative:
                        cal.ResponseType = NcResponseType.Tentative;
                        break;
                    case NcResponseType.Declined:
                        cal.ResponseType = NcResponseType.Declined;
                        break;
                    default:
                        return;
                    }
                    cal.ResponseTypeIsSet = true;
                    cal.Update ();
                    pending = new McPending (Account.Id, cal) {
                        Operation = McPending.Operations.CalUpdate,
                        ParentId = folder.ServerId,
                        ServerId = cal.ServerId,
                    }; 
                } else {
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
                        return;
                    }

                    pending = new McPending (Account.Id) {
                        Operation = McPending.Operations.CalRespond,
                        ServerId = item.ServerId,
                        ParentId = folder.ServerId,
                        CalResponse = apiResponse,
                    };
                    if (null != instance) {
                        pending.CalResponseInstance = (DateTime)instance;
                    }
                }
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCRESPCAL");
            }, "RespondItemCmd");
            return token;
        }

        public override string CreateFolderCmd (int destFolderId, string displayName, 
                                                Xml.FolderHierarchy.TypeCode folderType)
        {
            string token = null;
            var serverId = DateTime.UtcNow.Ticks.ToString ();
            string destFldServerId;
            NcModel.Instance.RunInTransaction (() => {
                if (0 > destFolderId) {
                    // Root case.
                    destFldServerId = "0";
                } else {
                    // Sub-folder case.
                    var destFld = McAbstrObject.QueryById<McFolder> (destFolderId);
                    if (null == destFld) {
                        return;
                    }
                    NcAssert.True (destFld.IsClientOwned == false, "BackEnd should not modify client-owned folders");
                    destFldServerId = destFld.ServerId;
                }
                var folder = McFolder.Create (Account.Id,
                             false,
                             false,
                             false,
                             destFldServerId,
                             serverId,
                             displayName,
                             folderType);
                folder.IsAwaitingCreate = true;
                folder.Insert ();
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.FolderCreate,
                    ServerId = serverId,
                    ParentId = destFldServerId,
                    DisplayName = displayName,
                    Folder_Type = folderType,
                    // Epoch intentionally not set.
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFCRE");
            }, "CreateFolderCmd");

            return token;
        }

        public override string CreateFolderCmd (string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return CreateFolderCmd (-1, displayName, folderType);
        }

        public override string DeleteFolderCmd (int folderId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                NcAssert.False (folder.IsDistinguished, "BackEnd must not delete distinguished folders");
                NcAssert.False (folder.IsClientOwned, "BackEnd must not delete folders in client-owned folders.");
                NcAssert.False (folder.IsAwaitingDelete, "BackEnd must not try to delete folder that has been already deleted.");

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.FolderDelete,
                    ServerId = folder.ServerId,
                    ParentId = folder.ParentId,
                };
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                MarkFoldersAwaitingDelete (folder);

                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFDEL");
            }, "DeleteFolderCmd");
            return token;
        }

        // recursively mark param and its children with isAwaitingDelete == true
        public void MarkFoldersAwaitingDelete (McFolder folder)
        {
            folder = folder.UpdateSet_IsAwaitingDelete (true);
            var children = McFolder.QueryByParentId (folder.AccountId, folder.ServerId);
            foreach (McFolder child in children) {
                MarkFoldersAwaitingDelete (child);
            }
        }

        public override string MoveFolderCmd (int folderId, int destFolderId)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
                NcAssert.False (folder.IsDistinguished, "BackEnd must not move distinguished folders");
                NcAssert.False (folder.IsClientOwned, "BackEnd must not move client-owned folders");
                NcAssert.False (destFolder.IsClientOwned, "BackEnd must not modify client-owned folders");
                folder = folder.UpdateSet_ParentId (destFolder.ServerId);
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                if (folder.IsClientOwned) {
                    token = McPending.KSynchronouslyCompleted;
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.FolderUpdate,
                    ServerId = folder.ServerId,
                    ParentId = folder.ParentId,
                    DestParentId = destFolder.ServerId,
                    DisplayName = folder.DisplayName,
                    Folder_Type = folder.Type,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP1");
            }, "MoveFolderCmd");
            return token;
        }

        public override string RenameFolderCmd (int folderId, string displayName)
        {
            string token = null;
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                NcAssert.False (folder.IsDistinguished, "BackEnd must not delete distinguished folders");
                NcAssert.False (folder.IsClientOwned, "BackEnd must not modify client-owned folders");

                folder = folder.UpdateSet_DisplayName (displayName);

                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

                if (folder.IsClientOwned) {
                    token = McPending.KSynchronouslyCompleted;
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.FolderUpdate,
                    ServerId = folder.ServerId,
                    ParentId = folder.ParentId,
                    DestParentId = folder.ParentId, // Set only because Move & Rename map to the same EAS command.
                    DisplayName = displayName,
                    Folder_Type = folder.Type,
                };
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP2");
            }, "RenameFolderCmd");
            return token;
        }

        public override string SyncCmd (int folderId)
        {
            string token = null;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                folder = McFolder.QueryById<McFolder> (folderId);
                if (null == folder) {
                    Log.Error (Log.LOG_AS, "SyncCmd: can't find McFolder");
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.Sync,
                    ServerId = folder.ServerId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    Log.Info (Log.LOG_AS, "SyncCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
                    token = dup.Token;
                    return;
                }
                pending.DoNotDelay ();
                pending.Insert ();
                token = pending.Token;
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDEBOD");
            }, "SyncCmd");
            return token;
        }
    }
}