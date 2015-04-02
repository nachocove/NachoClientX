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

        public virtual void Prioritize (string token)
        {
        }

        public virtual bool Cancel (string token)
        {
            return false;
        }

        public virtual void UnblockPendingCmd (int pendingId)
        {
        }

        public virtual void DeletePendingCmd (int pendingId)
        {
        }

        public virtual NcResult StartSearchEmailReq (string keywords, uint? maxResults)
        {
            return null;
        }

        public virtual NcResult SearchEmailReq (string keywords, uint? maxResults, string token)
        {
            return null;
        }

        public virtual NcResult StartSearchContactsReq (string prefix, uint? maxResults)
        {
            return null;
        }

        public virtual NcResult SearchContactsReq (string prefix, uint? maxResults, string token)
        {
            return null;
        }

        public virtual NcResult SendEmailCmd (int emailMessageId)
        {
            return null;
        }

        public virtual NcResult SendEmailCmd (int emailMessageId, int calId)
        {
            return null;
        }

        public virtual NcResult ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
                                                int folderId, bool originalEmailIsEmbedded)
        {
            return null;
        }

        public virtual NcResult ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
                                              int folderId, bool originalEmailIsEmbedded)
        {
            return null;
        }

        public virtual NcResult DeleteEmailCmd (int emailMessageId)
        {
            return null;
        }

        public virtual NcResult MarkEmailReadCmd (int emailMessageId)
        {
            return null;
        }

        public virtual NcResult MoveEmailCmd (int emailMessageId, int destFolderId)
        {
            return null;
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
            return null;
        }

        public virtual NcResult DnldAttCmd (int attId, bool doNotDelay = false)
        {
            return null;
        }

        public virtual NcResult CreateCalCmd (int calId, int folderId)
        {
            return null;
        }

        public virtual NcResult UpdateCalCmd (int calId)
        {
            return null;
        }

        public virtual NcResult DeleteCalCmd (int calId)
        {
            return null;
        }
       
        public virtual NcResult MoveCalCmd (int calId, int destFolderId)
        {
            return null;
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

        /// <summary>
        /// Forward a calendar event.
        /// </summary>
        /// <returns>The token for the pending operation.</returns>
        /// <param name="newEmailMessageId">ID of the outgoing e-mail message.</param>
        /// <param name="forwardedCalId">ID of the McCalendar event being forwarded.</param>
        /// <param name="folderId">ID of the folder that is the parent of the event being forwarded.</param>
        public virtual NcResult ForwardCalCmd (int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return null;
        }

        public virtual NcResult CreateContactCmd (int contactId, int folderId)
        {
            return null;
        }

        public virtual NcResult UpdateContactCmd (int contactId)
        {
            return null;
        }

        public virtual NcResult DeleteContactCmd (int contactId)
        {
            return null;
        }

        public virtual NcResult MoveContactCmd (int contactId, int destFolderId)
        {
            return null;
        }

        public virtual NcResult DnldContactBodyCmd (int contactId)
        {
            return null;
        }

        public virtual NcResult CreateTaskCmd (int taskId, int folderId)
        {
            return null;
        }

        public virtual NcResult UpdateTaskCmd (int taskId)
        {
            return null;
        }

        public virtual NcResult DeleteTaskCmd (int taskId)
        {
            return null;
        }

        public virtual NcResult MoveTaskCmd (int taskId, int destFolderId)
        {
            return null;
        }

        public virtual NcResult DnldTaskBodyCmd (int taskId)
        {
            return null;
        }

        public virtual NcResult CreateFolderCmd (int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return null;
        }

        public virtual NcResult CreateFolderCmd (string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return null;
        }

        public virtual NcResult DeleteFolderCmd (int folderId)
        {
            return null;
        }

        public virtual NcResult MoveFolderCmd (int folderId, int destFolderId)
        {
            return null;
        }

        public virtual NcResult RenameFolderCmd (int folderId, string displayName)
        {
            return null;
        }

        public virtual NcResult SyncCmd (int folderId)
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
