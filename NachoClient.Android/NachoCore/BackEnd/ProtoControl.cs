using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

// TODO: this file should not reference ActiveSync.
using NachoCore.ActiveSync;

namespace NachoCore
{
    public abstract class ProtoControl
    {
        public int AccountId;

        public IProtoControlOwner Owner { get; set; }

        public McAccount Account {
            get {
                return NcModel.Instance.Db.Table<McAccount> ().Where (acc => acc.Id == AccountId).Single ();
            }
        }

        public McCred Cred {
            get {
                // Note the lack of join :-(.
                var account = Account;
                return NcModel.Instance.Db.Table<McCred> ().Where (crd => crd.Id == Account.CredId).Single ();
            }
        }

        public McServer Server { 
            get {
                var account = Account;
                return NcModel.Instance.Db.Table<McServer> ().Where (srv => srv.Id == Account.ServerId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ServerId;
                update.Update ();
            }
        }

        public McProtocolState ProtocolState { 
            get {
                var account = Account;
                return NcModel.Instance.Db.Table<McProtocolState> ().Where (pcs => pcs.Id == Account.ProtocolStateId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ProtocolStateId;
                update.Update ();
            }
        }

        public NcStateMachine Sm { set; get; }
        // Interface to owner.
        public abstract void Execute ();

        public abstract void ForceSync ();

        public abstract void ForceStop ();

        public abstract void CertAskResp (bool isOkay);

        public abstract void ServerConfResp (bool forceAutodiscovery);

        public abstract void CredResp ();

        public abstract void Cancel (string token);

        public abstract void UnblockPendingCmd (int pendingId);

        public abstract void DeletePendingCmd (int pendingId);

        public abstract string StartSearchContactsReq (string prefix, uint? maxResults);

        public abstract void SearchContactsReq (string prefix, uint? maxResults, string token);

        public abstract string SendEmailCmd (int emailMessageId);

        public abstract string SendEmailCmd (int emailMessageId, int calId);

        public abstract string ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded);

        public abstract string ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded);

        public abstract string DeleteEmailCmd (int emailMessageId);

        public abstract string MarkEmailReadCmd (int emailMessageId);

        public abstract string MoveEmailCmd (int emailMessageId, int destFolderId);

        public abstract string SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue);

        public abstract string ClearEmailFlagCmd (int emailMessageId);

        public abstract string MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted);

        public abstract string DnldEmailBodyCmd (int emailMessageId);

        public abstract string DnldAttCmd (int attId);

        public abstract string CreateCalCmd (int calId, int folderId);

        public abstract string UpdateCalCmd (int calId);

        public abstract string DeleteCalCmd (int calId);

        public abstract string MoveCalCmd (int calId, int destFolderId);

        public abstract string RespondCalCmd (int calId, NcResponseType response);

        public abstract string DnldCalBodyCmd (int calId);

        public abstract string CreateContactCmd (int contactId, int folderId);

        public abstract string UpdateContactCmd (int contactId);

        public abstract string DeleteContactCmd (int contactId);

        public abstract string MoveContactCmd (int contactId, int destFolderId);

        public abstract string DnldContactBodyCmd (int contactId);

        public abstract string CreateTaskCmd (int taskId, int folderId);

        public abstract string UpdateTaskCmd (int taskId);

        public abstract string DeleteTaskCmd (int taskId);

        public abstract string MoveTaskCmd (int taskId, int destFolderId);

        public abstract string DnldTaskBodyCmd (int taskId);

        public abstract string CreateFolderCmd (int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType);

        public abstract string CreateFolderCmd (string DisplayName, Xml.FolderHierarchy.TypeCode folderType);

        public abstract string DeleteFolderCmd (int folderId);

        public abstract string MoveFolderCmd (int folderId, int destFolderId);

        public abstract string RenameFolderCmd (int folderId, string displayName);

        public abstract void ValidateConfig (McServer server, McCred cred);

        public abstract void CancelValidateConfig ();
        //
        // Interface to controllers.
        public abstract void StatusInd (NcResult status);

        public abstract void StatusInd (NcResult status, string[] tokens);
    }
}
