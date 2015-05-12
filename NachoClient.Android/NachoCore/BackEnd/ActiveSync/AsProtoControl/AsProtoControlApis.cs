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
        public override McPending UnblockPendingCmd (int pendingId)
        {
            McPending retval = null;
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (Account.Id == pending.AccountId);
                    NcAssert.True (McPending.StateEnum.UserBlocked == pending.State);
                    retval = pending.UpdateWithOCApply<McPending>((record) => {
                        var target = (McPending)record;
                        target.BlockReason = McPending.BlockReasonEnum.NotBlocked;
                        target.State = McPending.StateEnum.Eligible;
                        return true;
                    });
                    NcTask.Run (delegate {
                        Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCUNBLK");
                    }, "UnblockPendingCmd");
                }
            });
            return retval;
        }

        public override McPending DeletePendingCmd (int pendingId)
        {
            McPending retval = null;
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (Account.Id == pending.AccountId);
                    retval = pending.ResolveAsCancelled (false);
                }
            });
            return retval;
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

        public override bool Cancel (string token)
        {
            var retval = false;
            NcModel.Instance.RunInTransaction (() => {
                var pendings = McPending.QueryByToken (Account.Id, token);
                foreach (var iterPending in pendings) {
                    var pending = iterPending;
                    switch (pending.State) {
                    case McPending.StateEnum.Eligible:
                        pending.ResolveAsCancelled (false);
                        retval = true;
                        break;

                    case McPending.StateEnum.Deferred:
                    case McPending.StateEnum.Failed:
                    case McPending.StateEnum.PredBlocked:
                    case McPending.StateEnum.UserBlocked:
                        if (McPending.Operations.ContactSearch == pending.Operation || 
                            McPending.Operations.EmailSearch == pending.Operation) {
                            McPending.ResolvePendingSearchReqs (Account.Id, token, false);
                        } else {
                            pending.ResolveAsCancelled (false);
                        }
                        retval = true;
                        break;

                    case McPending.StateEnum.Dispatched:
                        // Prevent any more high-level attempts after Cancel().
                        // TODO - need method to find executing Op/Cmd so we can prevent HTTP retries.
                        pending.UpdateWithOCApply<McPending> ((record) => {
                            var target = (McPending)record;
                            target.DefersRemaining = 0;
                            return true;
                        });
                        retval = false;
                        break;

                    case McPending.StateEnum.Deleted:
                    // Nothing to do.
                        retval = true;
                        break;

                    default:
                        NcAssert.CaseError (string.Format ("Unknown State {0}", pending.State));
                        break;
                    }
                }
            });
            return retval;
        }

        public override NcResult StartSearchEmailReq (string keywords, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchEmailReq (keywords, maxResults, token);
            return NcResult.OK (token);
        }

        public override NcResult SearchEmailReq (string keywords, uint? maxResults, string token)
        {
            McPending.ResolvePendingSearchReqs (Account.Id, token, true);
            var newSearch = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSearch,
                Search_Prefix = keywords,
                Search_MaxResults = (null == maxResults) ? 20 : (uint)maxResults,
                Token = token
            };
            newSearch.DoNotDelay ();
            newSearch.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSRCHE");
            }, "SearchEmailReq");
            return NcResult.OK (token);
        }

        public override NcResult StartSearchContactsReq (string prefix, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchContactsReq (prefix, maxResults, token);
            return NcResult.OK (token);
        }

        public override NcResult SearchContactsReq (string prefix, uint? maxResults, string token)
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
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSRCHC");
            }, "SearchContactsReq");
            return NcResult.OK (token);
        }

        public override NcResult SendEmailCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_AS, "SendEmailCmd({0})", emailMessageId);
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var pending = new McPending (Account.Id, emailMessage) {
                    Operation = McPending.Operations.EmailSend,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSEND");
            }, "SendEmailCmd");
            Log.Info (Log.LOG_AS, "SendEmailCmd({0}) returning {1}", emailMessageId, result.Value as string);
            return result;
        }

        public override NcResult SendEmailCmd (int emailMessageId, int calId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_AS, "SendEmailCmd({0},{1})", emailMessageId, calId);
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == cal || null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
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
                        pending = pending.MarkPredBlocked (pendingCalCreId);
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
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSENDCAL");
            }, "SendEmailCmd(cal)");
            Log.Info (Log.LOG_AS, "SendEmailCmd({0},{1}) returning {2}", emailMessageId, calId, result.Value as string);
            return result;
        }

        private NcResult SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
                                      int folderId, bool originalEmailIsEmbedded)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_AS, "SmartEmailCmd({0},{1},{2},{3},{4})", Op, newEmailMessageId, refdEmailMessageId, folderId, originalEmailIsEmbedded);
            if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
                return SendEmailCmd (newEmailMessageId);
            }
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                var refdEmailMessage = McAbstrObject.QueryById<McEmailMessage> (refdEmailMessageId);
                var newEmailMessage = McAbstrObject.QueryById<McEmailMessage> (newEmailMessageId);
                folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == refdEmailMessage || null == newEmailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var pending = new McPending (Account.Id, newEmailMessage) {
                    Operation = Op,
                    ServerId = refdEmailMessage.ServerId,
                    ParentId = folder.ServerId,
                    Smart_OriginalEmailIsEmbedded = originalEmailIsEmbedded,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCSMF");
            }, "SmartEmailCmd");
            Log.Info (Log.LOG_AS, "SmartEmailCmd({0},{1},{2},{3},{4}) returning {5}", Op, newEmailMessageId, refdEmailMessageId, folderId, originalEmailIsEmbedded, result.Value as string);
            return result;
        }

        public override NcResult ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded)
        {
            Log.Info (Log.LOG_AS, "ReplyEmailCmd({0},{1},{2},{3})", newEmailMessageId, repliedToEmailMessageId, folderId, originalEmailIsEmbedded);
            return SmartEmailCmd (McPending.Operations.EmailReply,
                newEmailMessageId, repliedToEmailMessageId, folderId, originalEmailIsEmbedded);
        }

        public override NcResult ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded)
        {
            Log.Info (Log.LOG_AS, "ForwardEmailCmd({0},{1},{2},{3})", newEmailMessageId, forwardedEmailMessageId, folderId, originalEmailIsEmbedded);
            if (originalEmailIsEmbedded) {
                var attachments = McAttachment.QueryByItemId (AccountId, forwardedEmailMessageId, McAbstrFolderEntry.ClassCodeEnum.Email);
                Log.Info (Log.LOG_AS, "ForwardEmailCmd: attachments = {0}", attachments.Count);
                foreach (var attach in attachments) {
                    if (McAbstrFileDesc.FilePresenceEnum.None == attach.FilePresence) {
                        var token = DnldAttCmd (attach.Id);
                        if (null == token) {
                            // FIXME - is this correct behavior in this case?
                            return NcResult.Error (NcResult.SubKindEnum.Error_TaskBodyDownloadFailed);
                        }
                    }
                }
            }
            return SmartEmailCmd (McPending.Operations.EmailForward,
                newEmailMessageId, forwardedEmailMessageId, folderId, originalEmailIsEmbedded);
        }

        public override NcResult DeleteEmailCmd (int emailMessageId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McEmailMessage emailMessage = null;
            NcModel.Instance.RunInTransaction (() => {
                emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }

                var folders = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }

                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                McPending pending;
                var trash = McFolder.GetDefaultDeletedFolder (Account.Id);
                if (null == trash || trash.Id == primeFolder.Id) {
                    pending = new McPending (Account.Id) {
                        Operation = McPending.Operations.EmailDelete,
                        ParentId = primeFolder.ServerId,
                        ServerId = emailMessage.ServerId,
                    };
                    emailMessage.Delete ();
                } else {
                    pending = new McPending (Account.Id) {
                        Operation = McPending.Operations.EmailMove,
                        ServerId = emailMessage.ServerId,
                        ParentId = primeFolder.ServerId,
                        DestParentId = trash.ServerId,
                    };
                    trash.Link (emailMessage);
                    primeFolder.Unlink (emailMessage);
                }
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            if (null != emailMessage && result.isOK ()) {
                Log.Info (Log.LOG_AS, "DeleteEmailCmd: Id {0}/ServerId {1} => Token {2}",
                    emailMessage.Id, emailMessage.ServerId, result.GetValue<string> ());
                if (lastInSeq) {
                    StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                    Log.Debug (Log.LOG_AS, "DeleteEmailCmd:Info_EmailMessageSetChanged sent.");
                    NcTask.Run (delegate {
                        Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELMSG");
                    }, "DeleteEmailCmd");
                }
            }
            return result;
        }

        private NcResult MoveItemCmd (McPending.Operations op, NcResult.SubKindEnum subKind,
            McAbstrItem item, McFolder srcFolder, int destFolderId, bool lastInSeq)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            if (null == srcFolder) {
                return NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
            }
            if (srcFolder.IsClientOwned) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
            }

            if (srcFolder.Id == destFolderId) {
                return NcResult.OK ();
            }

            NcModel.Instance.RunInTransaction (() => {
                var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
                if (null == destFolder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }

                if (destFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }
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
                result = NcResult.OK (pending.Token);
                destFolder.Link (item);
                srcFolder.Unlink (item);
            });
            if (lastInSeq) {
                StatusInd (NcResult.Info (subKind));
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMOVMSG");
                }, "MoveItemCmd");
            }
            return result;
        }

        public override NcResult MoveEmailCmd (int emailMessageId, int destFolderId, bool lastInSeq = true)
        {
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.EmailMove, NcResult.SubKindEnum.Info_EmailMessageSetChanged,
                emailMessage, srcFolder, destFolderId, lastInSeq);
        }

        private bool GetItemAndFolder<T> (int itemId, 
            out T item,
            int folderId,
            out McFolder folder,
            out NcResult.SubKindEnum subKind) where T : McAbstrItem, new()
        {
            folder = null;
            item = McAbstrObject.QueryById<T> (itemId);
            if (null == item) {
                subKind = NcResult.SubKindEnum.Error_ItemMissing;
                return false;
            }

            var folders = McFolder.QueryByFolderEntryId<T> (Account.Id, itemId);
            foreach (var maybe in folders) {
                if (maybe.IsClientOwned) {
                    subKind = NcResult.SubKindEnum.Error_ClientOwned;
                    return false;
                }
                if (-1 == folderId || maybe.Id == folderId) {
                    folder = maybe;
                    subKind = NcResult.SubKindEnum.NotSpecified;
                    return true;
                }
            }
            subKind = NcResult.SubKindEnum.Error_FolderMissing;
            return false;
        }

        public override NcResult MarkEmailReadCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailMarkRead,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                emailMessage.IsRead = true;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCMRMSG");
            }, "MarkEmailReadCmd");
            return result;
        }

        public override NcResult SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
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
                result = NcResult.OK (pending.Token);
                // Set the Flag info in the DB item.
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
                emailMessage.FlagType = flagType;
                emailMessage.FlagStartDate = start;
                emailMessage.FlagUtcStartDate = utcStart;
                emailMessage.FlagDue = due;
                emailMessage.FlagUtcDue = utcDue;
                emailMessage.Update ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCSF");
            }, "SetEmailFlagCmd");
            return result;
        }

        public override NcResult ClearEmailFlagCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.EmailClearFlag,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                emailMessage.Update ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "ClearEmailFlagCmd");
            return result;
        }

        public override NcResult MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
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
                result = NcResult.OK (pending.Token);
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
                emailMessage.FlagCompleteTime = completeTime;
                emailMessage.FlagDateCompleted = dateCompleted;
                emailMessage.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCF");
            }, "MarkEmailFlagDone");
            return result;
        }

        public override NcResult DnldEmailBodyCmd (int emailMessageId, bool doNotDelay = false)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var body = emailMessage.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FilePresenceIsComplete);
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
                    result = NcResult.OK (dup.Token);
                    return;
                }
                if (doNotDelay) {
                    pending.DoNotDelay ();
                }
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDEBOD");
            }, "DnldEmailBodyCmd");
            return result;
        }

        public override NcResult DnldAttCmd (int attId, bool doNotDelay = false)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var att = McAbstrObject.QueryById<McAttachment> (attId);
                if (null == att) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_AttMissing);
                    return;
                }
                if (McAbstrFileDesc.FilePresenceEnum.None != att.FilePresence) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FilePresenceNotNone);
                    return;
                }
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (att.ItemId);
                if (null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
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
                    result = NcResult.OK (dup.Token);
                    return;
                }

                if (doNotDelay) {
                    pending.DoNotDelay ();
                }
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                att.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
                att.Update ();
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDATT");
            }, "DnldAttCmd");
            return result;
        }

        public override NcResult CreateCalCmd (int calId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McCalendar cal;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McCalendar> (calId, out cal, folderId, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (Account.Id, cal) {
                    Operation = McPending.Operations.CalCreate,
                    ParentId = folder.ServerId,
                    ClientId = cal.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECAL");
            }, "CreateCalCmd");
            return result;
        }

        public override NcResult UpdateCalCmd (int calId, bool sendBody)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                if (null == cal) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (Account.Id, cal) {
                    Operation = McPending.Operations.CalUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = cal.ServerId,
                    CalUpdate_SendBody = sendBody,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCAL");
            }, "UpdateCalCmd");
            return result;
        }

        public override NcResult DeleteCalCmd (int calId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var cal = McAbstrObject.QueryById<McCalendar> (calId);
                if (null == cal) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.CalDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = cal.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                cal.Delete ();
            });
            if (lastInSeq) {
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCAL");
                }, "DeleteCalCmd");
            }
            return result;
        }

        public override NcResult MoveCalCmd (int calId, int destFolderId, bool lastInSeq = true)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.CalMove, NcResult.SubKindEnum.Info_CalendarSetChanged,
                cal, srcFolder, destFolderId, lastInSeq);
        }

        public override NcResult DnldCalBodyCmd (int calId)
        {
            // I can't get this command to work.  The server always responds with "Bad or malformed request."
            // Return an error immediately.  The code to run the command is left here, commented out, in case
            // we want to try again.
            Log.Error (Log.LOG_AS, "DnldCalBody command is not supported.");
            return NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            #if false
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McCalendar cal;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McCalendar> (calId, out cal, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.CalBodyDownload,
                    ServerId = cal.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDCALBOD");
            }, "DnldCalBodyCmd");
            return result;
            #endif
        }

        public override NcResult ForwardCalCmd (int newEmailMessageId, int forwardedCalId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var refdCalEvent = McAbstrObject.QueryById<McCalendar> (forwardedCalId);
                var newEmailMessage = McAbstrObject.QueryById<McEmailMessage> (newEmailMessageId);
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == refdCalEvent || null == newEmailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var pending = new McPending (Account.Id, newEmailMessage) {
                    Operation = McPending.Operations.CalForward,
                    ServerId = refdCalEvent.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCCALF");
            }, "ForwardCalCmd");
            return result;
        }

        public override NcResult CreateContactCmd (int contactId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McContact contact;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McContact> (contactId, out contact, folderId, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (Account.Id, contact) {
                    Operation = McPending.Operations.ContactCreate,
                    ParentId = folder.ServerId,
                    ClientId = contact.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRECNT");
            }, "CreateContactCmd");
            return result;
        }

        public override NcResult UpdateContactCmd (int contactId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var contact = McAbstrObject.QueryById<McContact> (contactId);
                if (null == contact) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (Account.Id, contact) {
                    Operation = McPending.Operations.ContactUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = contact.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGCTC");
            }, "UpdateContactCmd");
            return result;
        }

        public override NcResult DeleteContactCmd (int contactId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var contact = McAbstrObject.QueryById<McContact> (contactId);
                if (null == contact) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = contact.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                contact.Delete ();
            });
            if (lastInSeq) {
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELCTC");
                }, "DeleteContactCmd");
            }
            return result;
        }

        public override NcResult MoveContactCmd (int contactId, int destFolderId, bool lastInSeq = true)
        {
            var contact = McAbstrObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.ContactMove, NcResult.SubKindEnum.Info_ContactSetChanged,
                contact, srcFolder, destFolderId, lastInSeq);
        }

        public override NcResult DnldContactBodyCmd (int contactId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McContact contact;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McContact> (contactId, out contact, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var body = contact.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsNontruncatedBodyComplete);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactBodyDownload,
                    ServerId = contact.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDCONBOD");
            }, "DnldContactBodyCmd");
            return result;
        }

        public override NcResult CreateTaskCmd (int taskId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McTask> (taskId, out task, folderId, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (Account.Id, task) {
                    Operation = McPending.Operations.TaskCreate,
                    ParentId = folder.ServerId,
                    ClientId = task.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCRETSK");
            }, "CreateTaskCmd");
            return result;
        }

        public override NcResult UpdateTaskCmd (int taskId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var task = McAbstrObject.QueryById<McTask> (taskId);
                if (null == task) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (Account.Id, task) {
                    Operation = McPending.Operations.TaskUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCCHGTSK");
            }, "UpdateTaskCmd");
            return result;
        }

        public override NcResult DeleteTaskCmd (int taskId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var task = McAbstrObject.QueryById<McTask> (taskId);
                if (null == task) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.ContactDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                task.Delete ();
            });
            if (lastInSeq) {
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCDELTSK");
                }, "DeleteTaskCmd");
            }
            return result;
        }

        public override NcResult MoveTaskCmd (int taskId, int destFolderId, bool lastInSeq = true)
        {
            var task = McAbstrObject.QueryById<McTask> (taskId);
            if (null == task) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McTask> (Account.Id, taskId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.TaskMove, NcResult.SubKindEnum.Info_TaskSetChanged,
                task, srcFolder, destFolderId, lastInSeq);
        }

        public override NcResult DnldTaskBodyCmd (int taskId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McTask> (taskId, out task, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var body = task.GetBody ();
                if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsNontruncatedBodyComplete);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.TaskBodyDownload,
                    ServerId = task.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDTBOD");
            }, "DnldTaskBodyCmd");
            return result;
        }

        public override NcResult RespondEmailCmd (int emailMessageId, NcResponseType response)
        {
            return RespondItemCmd<McEmailMessage> (emailMessageId, response);
        }

        public override NcResult RespondCalCmd (int calId, NcResponseType response, DateTime? instance = null)
        {
            return RespondItemCmd<McCalendar> (calId, response, instance);
        }

        private NcResult RespondItemCmd<T> (int itemId, NcResponseType response, DateTime? instance = null)
            where T : McAbstrItem, new ()
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McPending pending;
            NcModel.Instance.RunInTransaction (() => {
                T item;
                McFolder folder;
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<T> (itemId, out item, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
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
                        result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
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
                        result = NcResult.Error (NcResult.SubKindEnum.Error_InvalidResponseType);
                        return;
                    }
                    cal.ResponseTypeIsSet = true;
                    cal.Update ();
                    pending = new McPending (Account.Id, cal) {
                        Operation = McPending.Operations.CalUpdate,
                        ParentId = folder.ServerId,
                        ServerId = cal.ServerId,
                        CalUpdate_SendBody = false,
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
                        result = NcResult.Error (NcResult.SubKindEnum.Error_InvalidResponseType);
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
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCRESPCAL");
            }, "RespondItemCmd");
            return result;
        }

        public override NcResult CreateFolderCmd (int destFolderId, string displayName, 
                                                Xml.FolderHierarchy.TypeCode folderType)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
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
                        result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                        return;
                    }
                    if (destFld.IsClientOwned) {
                        result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                        return;
                    }
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
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFCRE");
            }, "CreateFolderCmd");

            return result;
        }

        public override NcResult CreateFolderCmd (string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return CreateFolderCmd (-1, displayName, folderType);
        }

        public override NcResult DeleteFolderCmd (int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (folder.IsDistinguished) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsDistinguished);
                    return;
                }
                if (folder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }
                if (folder.IsAwaitingDelete) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsAwaitingDelete);
                    return;
                }

                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.FolderDelete,
                    ServerId = folder.ServerId,
                    ParentId = folder.ParentId,
                };
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                MarkFoldersAwaitingDelete (folder);

                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFDEL");
            }, "DeleteFolderCmd");
            return result;
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

        public override NcResult MoveFolderCmd (int folderId, int destFolderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var destFolder = McAbstrObject.QueryById<McFolder> (destFolderId);
                if (null == destFolder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                if (folder.IsDistinguished) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsDistinguished);
                    return;
                }
                if (folder.IsClientOwned || destFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }
                folder = folder.UpdateSet_ParentId (destFolder.ServerId);
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                if (folder.IsClientOwned) {
                    result = NcResult.OK (McPending.KSynchronouslyCompleted);
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
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP1");
            }, "MoveFolderCmd");
            return result;
        }

        public override NcResult RenameFolderCmd (int folderId, string displayName)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var folder = McAbstrObject.QueryById<McFolder> (folderId);
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                if (folder.IsDistinguished) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsDistinguished);
                    return;
                }
                if (folder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                folder = folder.UpdateSet_DisplayName (displayName);

                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));

                if (folder.IsClientOwned) {
                    result = NcResult.OK (McPending.KSynchronouslyCompleted);
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
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQ, "ASPCFUP2");
            }, "RenameFolderCmd");
            return result;
        }

        public override NcResult SyncCmd (int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                folder = McFolder.QueryById<McFolder> (folderId);
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var pending = new McPending (Account.Id) {
                    Operation = McPending.Operations.Sync,
                    ServerId = folder.ServerId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    Log.Info (Log.LOG_AS, "SyncCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
                    result = NcResult.OK (dup.Token);
                    return;
                }
                pending.DoNotDelay ();
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDEBOD");
            }, "SyncCmd");
            return result;
        }
    }
}