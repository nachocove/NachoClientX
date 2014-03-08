using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public abstract class ProtoControl
    {
        public int AccountId;

        public IProtoControlOwner Owner { get; set; }

        public McAccount Account {
            get {
                return BackEnd.Instance.Db.Table<McAccount> ().Where (acc => acc.Id == AccountId).Single ();
            }
        }

        public McCred Cred {
            get {
                // Note the lack of join :-(.
                var account = Account;
                return BackEnd.Instance.Db.Table<McCred> ().Where (crd => crd.Id == Account.CredId).Single ();
            }
        }

        public McServer Server { 
            get {
                var account = Account;
                return BackEnd.Instance.Db.Table<McServer> ().Where (srv => srv.Id == Account.ServerId).Single ();
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
                return BackEnd.Instance.Db.Table<McProtocolState> ().Where (pcs => pcs.Id == Account.ProtocolStateId).Single ();
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

        public abstract bool Cancel (string token);

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

        public abstract string MoveItemCmd (int emailMessageId, int destFolderId);

        public abstract string SetEmailFlagCmd (int emailMessageId, string flagType, 
                                          DateTime start, DateTime utcStart, DateTime due, DateTime utcDue);

        public abstract string ClearEmailFlagCmd (int emailMessageId);

        public abstract string MarkEmailFlagDone (int emailMessageId,
                                            DateTime completeTime, DateTime dateCompleted);

        public abstract string DnldAttCmd (int attId);

        public abstract string CreateCalCmd (int calId);

        public abstract string RespondCalCmd (int calId, RespondCalEnum response);

        public abstract string CreateFolderCmd (int destFolderId, string displayName, uint folderType,
                                          bool IsClientOwned, bool isHidden);

        public abstract string CreateFolderCmd (string DisplayName, uint folderType,
                                          bool IsClientOwned, bool isHidden);

        public abstract string DeleteFolderCmd (int folderId);

        public abstract string MoveFolderCmd (int folderId, int destFolderId);

        public abstract string RenameFolderCmd (int folderId, string displayName);
        //
        // Interface to controllers.
        public abstract void StatusInd (NcResult status);

        public abstract void StatusInd (NcResult status, string[] tokens);
    }
}
