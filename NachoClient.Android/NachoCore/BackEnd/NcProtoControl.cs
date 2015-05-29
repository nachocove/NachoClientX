using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync; // For XML code values for now (Jan, I know...)

namespace NachoCore
{
    public enum PickActionEnum { Sync, Ping, QOop, HotQOp, Fetch, Wait, FSync };

    public class NcProtoControl
    {
        public class PcEvt : SmEvt
        {
            // Every subclass of ProtoControl must be able to handle these events. Generic code uses them.
            new public enum E : uint
            {
                PendQ = (SmEvt.E.Last + 1),
                PendQHot,
                Park,
                Last = Park,
            };
        }

        public int AccountId;

        public INcProtoControlOwner Owner { get; set; }

        public NcProtoControl ProtoControl { set; get; }

        public McAccount.AccountCapabilityEnum Capabilities { protected set; get; }

        public McAccount Account {
            get {
                return NcModel.Instance.Db.Table<McAccount> ().Where (acc => acc.Id == AccountId).Single ();
            }
        }

        public McCred Cred {
            get {
                return McCred.QueryByAccountId<McCred> (Account.Id).SingleOrDefault ();
            }
        }

        public McServer Server { 
            get {
                return McServer.QueryByAccountIdAndCapabilities (Account.Id, Capabilities);
            }
            set {
                var update = value;
                update.Update ();
            }
        }

        public McProtocolState ProtocolState { 
            get {
                return McProtocolState.QueryByAccountId<McProtocolState> (Account.Id).SingleOrDefault ();
            }
            set {
                var update = value;
                update.Update ();
            }
        }

        public virtual BackEndStateEnum BackEndState {
            get {
                return BackEndStateEnum.PostAutoDPostInboxSync;
            }
        }

        private AutoDInfoEnum _autoDInfo = AutoDInfoEnum.Unknown;
        public virtual AutoDInfoEnum AutoDInfo {
            get {
                return _autoDInfo;
            }
            set {
                _autoDInfo = value;
            }
        }

        public virtual X509Certificate2 ServerCertToBeExamined {
            get {
                return null;
            }
        }

        public NcProtoControl (INcProtoControlOwner owner, int accountId)
        {
            Owner = owner;
            AccountId = accountId;
            // TODO - change ResolveAllDispatchedAsDeferred to be per-controller (capabilities).
            McPending.ResolveAllDispatchedAsDeferred (this, AccountId);
        }

        protected void SetupAccount ()
        {
            // Hang our records off Account.
            NcModel.Instance.RunInTransaction (() => {
                var policy = McPolicy.QueryByAccountId<McPolicy> (AccountId).SingleOrDefault ();
                if (null == policy) {
                    policy = new McPolicy () {
                        AccountId = AccountId,
                    };
                    policy.Insert ();
                }
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (AccountId).SingleOrDefault ();
                if (null == protocolState) {
                    protocolState = new McProtocolState () {
                        AccountId = AccountId,
                    };
                    protocolState.Insert ();
                }
            });

            // Make the application-defined folders.
            McFolder freshMade;
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetClientOwnedOutboxFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, false, true, "0",
                        McFolder.ClientOwned_Outbox, "On-Device Outbox",
                        Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetClientOwnedDraftsFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, false, true, "0",
                        McFolder.ClientOwned_EmailDrafts, "On-Device Drafts",
                        Xml.FolderHierarchy.TypeCode.UserCreatedMail_12);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetCalDraftsFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, true, "0",
                        McFolder.ClientOwned_CalDrafts, "On-Device Calendar Drafts",
                        Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetGalCacheFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, true, "0",
                        McFolder.ClientOwned_GalCache, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetGleanedFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, true, "0",
                        McFolder.ClientOwned_Gleaned, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetLostAndFoundFolder (AccountId)) {
                    freshMade = McFolder.Create (AccountId, true, true, true, "0",
                        McFolder.ClientOwned_LostAndFound, string.Empty,
                        Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1);
                    freshMade.Insert ();
                }
            });
            // Create file directories.
            NcModel.Instance.InitializeDirs (AccountId);
        }

        public NcStateMachine Sm { set; get; }

        // recursively mark param and its children with isAwaitingDelete == true
        protected void MarkFoldersAwaitingDelete (McFolder folder)
        {
            folder = folder.UpdateSet_IsAwaitingDelete (true);
            var children = McFolder.QueryByParentId (folder.AccountId, folder.ServerId);
            foreach (McFolder child in children) {
                MarkFoldersAwaitingDelete (child);
            }
        }

        protected bool GetItemAndFolder<T> (int itemId, 
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

        protected NcResult MoveItemCmd (McPending.Operations op, McAccount.AccountCapabilityEnum capability,
            NcResult.SubKindEnum subKind,
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
                        markUpdate = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                var pending = new McPending (Account.Id, capability) {
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
                    Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCMOVMSG");
                }, "MoveItemCmd");
            }
            return result;
        }

        // Interface to owner.
        // Returns false if sub-class override should not continue.
        public virtual bool Execute ()
        {
            if (NachoPlatform.NetStatusStatusEnum.Up != NcCommStatus.Instance.Status) {
                Log.Warn (Log.LOG_BACKEND, "Execute called while network is down.");
                return false;
            }
            // TODO - extract more from the EAS class and stuff here.
            return true;
        }

        public virtual void ForceStop ()
        {
        }

        public virtual void Remove ()
        {
        }

        public virtual void CertAskResp (bool isOkay)
        {
        }

        public virtual void ServerConfResp (bool forceAutodiscovery)
        {
        }

        public virtual void CredResp ()
        {
        }

        public virtual NcResult StartSearchEmailReq (string keywords, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchEmailReq (keywords, maxResults, token);
            return NcResult.OK (token);
        }

        public virtual NcResult SearchEmailReq (string keywords, uint? maxResults, string token)
        {
            McPending.ResolvePendingSearchReqs (Account.Id, token, true);
            var newSearch = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                Operation = McPending.Operations.EmailSearch,
                Search_Prefix = keywords,
                Search_MaxResults = (null == maxResults) ? 20 : (uint)maxResults,
                Token = token
            };
            newSearch.DoNotDelay ();
            newSearch.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCSRCHE");
            }, "SearchEmailReq");
            return NcResult.OK (token);
        }

        public virtual NcResult StartSearchContactsReq (string prefix, uint? maxResults)
        {
            var token = Guid.NewGuid ().ToString ();
            SearchContactsReq (prefix, maxResults, token);
            return NcResult.OK (token);
        }

        public virtual NcResult SearchContactsReq (string prefix, uint? maxResults, string token)
        {
            McPending.ResolvePendingSearchReqs (Account.Id, token, true);
            var newSearch = new McPending (Account.Id, McAccount.AccountCapabilityEnum.ContactReader) {
                Operation = McPending.Operations.ContactSearch,
                Search_Prefix = prefix,
                Search_MaxResults = (null == maxResults) ? 50 : (uint)maxResults,
                Token = token
            };
            newSearch.DoNotDelay ();
            newSearch.Insert ();

            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCSRCHC");
            }, "SearchContactsReq");
            return NcResult.OK (token);
        }

        public virtual NcResult SendEmailCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_BACKEND, "SendEmailCmd({0})", emailMessageId);
            NcModel.Instance.RunInTransaction (() => {
                var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
                if (null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailSender, emailMessage) {
                    Operation = McPending.Operations.EmailSend,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCSEND");
            }, "SendEmailCmd");
            Log.Info (Log.LOG_BACKEND, "SendEmailCmd({0}) returning {1}", emailMessageId, result.Value as string);
            return result;
        }

        public virtual NcResult SendEmailCmd (int emailMessageId, int calId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            Log.Info (Log.LOG_BACKEND, "SendEmailCmd({0},{1})", emailMessageId, calId);
            NcModel.Instance.RunInTransaction (() => {
                var cal = McCalendar.QueryById<McCalendar> (calId);
                var emailMessage = McEmailMessage.QueryById<McEmailMessage> (emailMessageId);
                if (null == cal || null == emailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }

                var pendingCalCre = NcModel.Instance.Db.Table<McPending> ().LastOrDefault (x => calId == x.ItemId);
                var pendingCalCreId = (null == pendingCalCre) ? 0 : pendingCalCre.Id;

                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailSender, emailMessage) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCSENDCAL");
            }, "SendEmailCmd(cal)");
            Log.Info (Log.LOG_BACKEND, "SendEmailCmd({0},{1}) returning {2}", emailMessageId, calId, result.Value as string);
            return result;
        }

        public virtual NcResult ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded)
        {
            return SendEmailCmd (newEmailMessageId);
        }

        public virtual NcResult ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded)
        {
            return SendEmailCmd (newEmailMessageId);
        }

        public virtual NcResult DeleteEmailCmd (int emailMessageId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McEmailMessage emailMessage = null;
            NcModel.Instance.RunInTransaction (() => {
                emailMessage = McEmailMessage.QueryById<McEmailMessage> (emailMessageId);
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
                    pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                        Operation = McPending.Operations.EmailDelete,
                        ParentId = primeFolder.ServerId,
                        ServerId = emailMessage.ServerId,
                    };
                    emailMessage.Delete ();
                } else {
                    pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Log.Info (Log.LOG_BACKEND, "DeleteEmailCmd: Id {0}/ServerId {1} => Token {2}",
                    emailMessage.Id, emailMessage.ServerId, result.GetValue<string> ());
                if (lastInSeq) {
                    StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                    Log.Debug (Log.LOG_BACKEND, "DeleteEmailCmd:Info_EmailMessageSetChanged sent.");
                    NcTask.Run (delegate {
                        Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCDELMSG");
                    }, "DeleteEmailCmd");
                }
            }
            return result;
        }

        public virtual NcResult MarkEmailReadCmd (int emailMessageId)
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
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCMRMSG");
            }, "MarkEmailReadCmd");
            return result;
        }

        public virtual NcResult MoveEmailCmd (int emailMessageId, int destFolderId, bool lastInSeq = true)
        {
            var emailMessage = McAbstrObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == emailMessage) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McEmailMessage> (Account.Id, emailMessageId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.EmailMove, McAccount.AccountCapabilityEnum.EmailReaderWriter,
                NcResult.SubKindEnum.Info_EmailMessageSetChanged,
                emailMessage, srcFolder, destFolderId, lastInSeq);
        }


        public virtual NcResult SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return null;
        }

        public virtual NcResult ClearEmailFlagCmd (int emailMessageId)
        {
            return null;
        }

        public virtual NcResult MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            return null;
        }

        public virtual NcResult DnldEmailBodyCmd (int emailMessageId, bool doNotDelay = false)
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
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.EmailBodyDownload,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    // TODO: Insert but have the result of the 1st duplicate trigger the same result events for all duplicates.
                    Log.Info (Log.LOG_BACKEND, "DnldEmailBodyCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
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
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCDNLDEBOD");
            }, "DnldEmailBodyCmd");
            return result;
        }

        public virtual NcResult DnldAttCmd (int attId, bool doNotDelay = false)
        {
            return null;
        }

        public virtual NcResult CreateCalCmd (int calId, int folderId)
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
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.CalWriter, cal) {
                    Operation = McPending.Operations.CalCreate,
                    ParentId = folder.ServerId,
                    ClientId = cal.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_CalendarSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCCRECAL");
            }, "CreateCalCmd");
            return result;
        }

        public virtual NcResult UpdateCalCmd (int calId, bool sendBody)
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

                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.CalWriter, cal) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCCHGCAL");
            }, "UpdateCalCmd");
            return result;
        }

        public virtual NcResult DeleteCalCmd (int calId, bool lastInSeq = true)
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

                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.CalWriter) {
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
                    Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCDELCAL");
                }, "DeleteCalCmd");
            }
            return result;
        }
       
        public virtual NcResult MoveCalCmd (int calId, int destFolderId, bool lastInSeq = true)
        {
            var cal = McAbstrObject.QueryById<McCalendar> (calId);
            if (null == cal) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McCalendar> (Account.Id, calId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.CalMove, McAccount.AccountCapabilityEnum.CalWriter,
                NcResult.SubKindEnum.Info_CalendarSetChanged,
                cal, srcFolder, destFolderId, lastInSeq);
        }

        public virtual NcResult RespondEmailCmd (int emailMessageId, NcResponseType response)
        {
            return null;
        }

        public virtual NcResult RespondCalCmd (int calId, NcResponseType response, DateTime? instance = null)
        {
            return null;
        }

        public virtual NcResult DnldCalBodyCmd (int calId)
        {
            return null;
        }

        public virtual NcResult ForwardCalCmd (int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return null;
        }

        public virtual NcResult CreateContactCmd (int contactId, int folderId)
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
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.ContactWriter, contact) {
                    Operation = McPending.Operations.ContactCreate,
                    ParentId = folder.ServerId,
                    ClientId = contact.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCCRECNT");
            }, "CreateContactCmd");
            return result;
        }

        public virtual NcResult UpdateContactCmd (int contactId)
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

                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.ContactWriter, contact) {
                    Operation = McPending.Operations.ContactUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = contact.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ContactSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCCHGCTC");
            }, "UpdateContactCmd");
            return result;
        }

        public virtual NcResult DeleteContactCmd (int contactId, bool lastInSeq = true)
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
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.ContactWriter) {
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
                    Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCDELCTC");
                }, "DeleteContactCmd");
            }
            return result;
        }

        public virtual NcResult MoveContactCmd (int contactId, int destFolderId, bool lastInSeq = true)
        {
            var contact = McAbstrObject.QueryById<McContact> (contactId);
            if (null == contact) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McContact> (Account.Id, contactId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.ContactMove, McAccount.AccountCapabilityEnum.ContactWriter,
                NcResult.SubKindEnum.Info_ContactSetChanged,
                contact, srcFolder, destFolderId, lastInSeq);
        }

        public virtual NcResult DnldContactBodyCmd (int contactId)
        {
            return null;
        }

        // Let the task stuff continue to live in ActiveSync until we have a use-case.
        public virtual NcResult CreateTaskCmd (int taskId, int folderId)
        {
            return null;
        }

        public virtual NcResult UpdateTaskCmd (int taskId)
        {
            return null;
        }

        public virtual NcResult DeleteTaskCmd (int taskId, bool lastInSeq = true)
        {
            return null;
        }

        public virtual NcResult MoveTaskCmd (int taskId, int destFolderId, bool lastInSeq = true)
        {
            return null;
        }

        public virtual NcResult DnldTaskBodyCmd (int taskId)
        {
            return null;
        }

        public virtual NcResult CreateFolderCmd (int destFolderId, string displayName, 
            NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode folderType)
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
                // TODO - base capabilities on folder type.
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCFCRE");
            }, "CreateFolderCmd");

            return result;
        }

        public virtual NcResult CreateFolderCmd (string displayName, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode folderType)
        {
            return CreateFolderCmd (-1, displayName, folderType);
        }

        public virtual NcResult DeleteFolderCmd (int folderId)
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
                // TODO - base capabilities on folder type.
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCFDEL");
            }, "DeleteFolderCmd");
            return result;
        }

        public virtual NcResult MoveFolderCmd (int folderId, int destFolderId)
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
                // TODO - base capability on folder type.
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCFUP1");
            }, "MoveFolderCmd");
            return result;
        }

        public virtual NcResult RenameFolderCmd (int folderId, string displayName)
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
                // TODO - determine appropriate capability based on type of folder.
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
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
                Sm.PostEvent ((uint)PcEvt.E.PendQ, "PCPCFUP2");
            }, "RenameFolderCmd");
            return result;
        }

        public virtual NcResult SyncCmd (int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                folder = McFolder.QueryById<McFolder> (folderId);
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var pending = new McPending (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.Sync,
                    ServerId = folder.ServerId,
                };
                McPending dup;
                if (pending.IsDuplicate (out dup)) {
                    Log.Info (Log.LOG_BACKEND, "SyncCmd: IsDuplicate of Id/Token {0}/{1}", dup.Id, dup.Token);
                    result = NcResult.OK (dup.Token);
                    return;
                }
                pending.DoNotDelay ();
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "PCPCDNLDEBOD");
            }, "SyncCmd");
            return result;
        }

        public virtual void ValidateConfig (McServer server, McCred cred)
        {
        }

        public virtual void CancelValidateConfig ()
        {
        }
        //
        // Interface to controllers.
        public virtual void StatusInd (NcResult status)
        {
            Owner.StatusInd (this, status);
        }

        public virtual void StatusInd (NcResult status, string[] tokens)
        {
            Owner.StatusInd (this, status, tokens);
        }
    }
}
