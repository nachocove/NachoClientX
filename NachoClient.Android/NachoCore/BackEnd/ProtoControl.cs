using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;

// TODO: this file should not reference ActiveSync.
using NachoCore.ActiveSync;

namespace NachoCore
{
    public class ProtoControl
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
                return McCred.QueryByAccountId<McCred> (Account.Id).SingleOrDefault ();
            }
        }

        public McServer Server { 
            get {
                return McServer.QueryByAccountId<McServer> (Account.Id).SingleOrDefault ();
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

        public virtual BackEndAutoDStateEnum AutoDState {
            get {
                return BackEndAutoDStateEnum.PostAutoDPostInboxSync;
            }
        }

        public virtual X509Certificate2 ServerCertToBeExamined {
            get {
                return null;
            }
        }

        public ProtoControl (IProtoControlOwner owner, int accountId)
        {
            Owner = owner;
            AccountId = accountId;
        }

        public NcStateMachine Sm { set; get; }
        // Interface to owner.
        public virtual void Execute ()
        {
        }

        public virtual void QuickSync ()
        {
        }

        public virtual void ForceStop ()
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

        public virtual void Cancel (string token)
        {
        }

        public virtual void UnblockPendingCmd (int pendingId)
        {
        }

        public virtual void DeletePendingCmd (int pendingId)
        {
        }

        public virtual string StartSearchContactsReq (string prefix, uint? maxResults)
        {
            return null;
        }

        public virtual void SearchContactsReq (string prefix, uint? maxResults, string token)
        {
        }

        public virtual string SendEmailCmd (int emailMessageId)
        {
            return null;
        }

        public virtual string SendEmailCmd (int emailMessageId, int calId)
        {
            return null;
        }

        public virtual string ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded)
        {
            return null;
        }

        public virtual string ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded)
        {
            return null;
        }

        public virtual string DeleteEmailCmd (int emailMessageId)
        {
            return null;
        }

        public virtual string MarkEmailReadCmd (int emailMessageId)
        {
            return null;
        }

        public virtual string MoveEmailCmd (int emailMessageId, int destFolderId)
        {
            return null;
        }

        public virtual string SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return null;
        }

        public virtual string ClearEmailFlagCmd (int emailMessageId)
        {
            return null;
        }

        public virtual string MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            return null;
        }

        public virtual string DnldEmailBodyCmd (int emailMessageId)
        {
            return null;
        }

        public virtual string DnldAttCmd (int attId)
        {
            return null;
        }

        public virtual string CreateCalCmd (int calId, int folderId)
        {
            return null;
        }

        public virtual string UpdateCalCmd (int calId)
        {
            return null;
        }

        public virtual string DeleteCalCmd (int calId)
        {
            return null;
        }
       
        public virtual string MoveCalCmd (int calId, int destFolderId)
        {
            return null;
        }

        public virtual string RespondCalCmd (int calId, NcResponseType response)
        {
            return null;
        }

        public virtual string DnldCalBodyCmd (int calId)
        {
            return null;
        }

        public virtual string CreateContactCmd (int contactId, int folderId)
        {
            return null;
        }

        public virtual string UpdateContactCmd (int contactId)
        {
            return null;
        }

        public virtual string DeleteContactCmd (int contactId)
        {
            return null;
        }

        public virtual string MoveContactCmd (int contactId, int destFolderId)
        {
            return null;
        }

        public virtual string DnldContactBodyCmd (int contactId)
        {
            return null;
        }

        public virtual string CreateTaskCmd (int taskId, int folderId)
        {
            return null;
        }

        public virtual string UpdateTaskCmd (int taskId)
        {
            return null;
        }

        public virtual string DeleteTaskCmd (int taskId)
        {
            return null;
        }

        public virtual string MoveTaskCmd (int taskId, int destFolderId)
        {
            return null;
        }

        public virtual string DnldTaskBodyCmd (int taskId)
        {
            return null;
        }

        public virtual string CreateFolderCmd (int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return null;
        }

        public virtual string CreateFolderCmd (string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return null;
        }

        public virtual string DeleteFolderCmd (int folderId)
        {
            return null;
        }

        public virtual string MoveFolderCmd (int folderId, int destFolderId)
        {
            return null;
        }

        public virtual string RenameFolderCmd (int folderId, string displayName)
        {
            return null;
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
