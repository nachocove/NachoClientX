// # Copyright (C) 2013, 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
// TODO: this file should not reference ActiveSync.
using NachoCore.ActiveSync;

namespace NachoCore
{
    public enum BackEndStateEnum {
        NotYetStarted,
        Running, 
        CertAskWait, 
        ServerConfWait, 
        CredWait, 
        PostAutoDPreInboxSync, 
        PostAutoDPostInboxSync,
    };

    public enum AutoDInfoEnum {
        Unknown = 0,
        MXNotFound,
        MXFoundGoogle,
        MXFoundNonGoogle,
    };

    public interface IBackEnd
    {
        // This is the API Contract for the BackEnd object. The owner of the BackEnd
        // object is given these APIs.
        // for each account in the DB, fire up the protocol & start server interaction
        // (if it isn't already running).
        void Start ();
        // attempt to (re)start a specific account.
        void Start (int accountId);
        // stop all activity in the BE.
        void Stop ();
        // ... for a specific account.
        void Stop (int accountId);
        // for a single account to sync immediately.
        void QuickSync (int accountId);
        // let the BE know that the asked-about server cert is/not okay to trust.
        void CertAskResp (int accountId, bool isOkay);
        // let the BE know that the server info has been updated for this account.
        void ServerConfResp (int accountId, bool forceAutodiscovery);
        // let the BE know that the credentials have been updated for this account.
        void CredResp (int accountId);
        // cancel command/request associated with this token (if possible).
        void Cancel (int accountId, string token);
        // Move an operation to the head of the pending Q.
        void Prioritize (int accountId, string token);
        // user-block issue is resolved, try again.
        void UnblockPendingCmd (int accountId, int pendingId);
        // accept the fail. delete the pending obj.
        void DeletePendingCmd (int accountId, int pendingId);
        // search contacts. returns token that can be used to cancel the search and all eclipsed searches.
        string StartSearchContactsReq (int accountId, string prefix, uint? maxResults);
        // follow-on contacts search, using same token.
        void SearchContactsReq (int accountId, string prefix, uint? maxResults, string token);
        // send specified email (not in a synced folder). returns token that can be used to possibly cancel.
        string SendEmailCmd (int accountId, int emailMessageId);
        // BE will make sure that the operation that created calId is complete before sending the
        // email to the server. If the calId operation fails (hard), then the SendEmailCmd will too.
        string SendEmailCmd (int accountId, int emailMessageId, int calId);

        string ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                          int folderId, bool originalEmailIsEmbedded);

        string ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                        int folderId, bool originalEmailIsEmbedded);
        // delete an email from a synced folder. returns token that can be used to possibly cancel.
        string DeleteEmailCmd (int accountId, int emailMessageId);
        // move an email from one folder to another. returns token that can be used to possibly cancel.
        string MoveEmailCmd (int accountId, int emailMessageId, int destFolderId);
        // mark an email as read. returns token that can be used to possibly cancel.
        string MarkEmailReadCmd (int accountId, int emailMessageId);
        // set the flag value on the email.
        string SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                          DateTime start, DateTime utcStart, DateTime due, DateTime utcDue);
        // clear the flag value on the email.
        string ClearEmailFlagCmd (int accountId, int emailMessageId);
        // mark the flag as "done" for the server, and clear the values in the DB.
        string MarkEmailFlagDone (int accountId, int emailMessageId,
                            DateTime completeTime, DateTime dateCompleted);
        string DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDefer = false);
        // download an attachment. returns token that can be used to possibly cancel.
        string DnldAttCmd (int accountId, int attId, bool doNotDefer = false);
        string CreateCalCmd (int accountId, int calId, int folderId);
        string UpdateCalCmd (int accountId, int calId);
        string DeleteCalCmd (int accountId, int calId);
        string MoveCalCmd (int accountId, int calId, int destFolderId);
        string RespondCalCmd (int accountId, int calId, NcResponseType response);
        string DnldCalBodyCmd (int accountId, int calId);
        string CreateContactCmd (int accountId, int contactId, int folderId);
        string UpdateContactCmd (int accountId, int contactId);
        string DeleteContactCmd (int accountId, int contactId);
        string MoveContactCmd (int accountId, int contactId, int destFolderId);
        string DnldContactBodyCmd (int accountId, int contactId);
        string CreateTaskCmd (int accountId, int taskId, int folderId);
        string UpdateTaskCmd (int accountId, int taskId);
        string DeleteTaskCmd (int accountId, int taskId);
        string MoveTaskCmd (int accountId, int taskId, int destFolderId);
        string DnldTaskBodyCmd (int accountId, int taskId);
        // create a subordinate folder.
        string CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType);
        // create a root folder.
        string CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType);
        // delete a folder.
        string DeleteFolderCmd (int accountId, int folderId);
        // move a folder.
        string MoveFolderCmd (int accountId, int folderId, int destFolderId);
        // rename a folder.
        string RenameFolderCmd (int accountId, int folderId, string displayName);
        // validate account config.
        bool ValidateConfig (int accountId, McServer server, McCred cred);
        void CancelValidateConfig (int accountId);
        // state, including auto-d.
        BackEndStateEnum BackEndState (int accountId);
        AutoDInfoEnum AutoDInfo (int accountId);
        X509Certificate2 ServerCertToBeExamined (int accountId);
    }
}

