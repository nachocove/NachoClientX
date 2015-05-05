// # Copyright (C) 2013, 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;
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
        GoogleForbids,
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
        // remove service for an account.
        void Remove (int accountId);
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
        McPending UnblockPendingCmd (int accountId, int pendingId);
        // accept the fail. delete the pending obj.
        McPending DeletePendingCmd (int accountId, int pendingId);
        // search email. returns token that can be used to cancel the search and all eclipsed searches.
        NcResult StartSearchEmailReq (int accountId, string prefix, uint? maxResults);
        // follow-on email search, using same token.
        NcResult SearchEmailReq (int accountId, string prefix, uint? maxResults, string token);
        // search contacts. returns token that can be used to cancel the search and all eclipsed searches.
        NcResult StartSearchContactsReq (int accountId, string prefix, uint? maxResults);
        // follow-on contacts search, using same token.
        NcResult SearchContactsReq (int accountId, string prefix, uint? maxResults, string token);
        // send specified email (not in a synced folder). returns token that can be used to possibly cancel.
        NcResult SendEmailCmd (int accountId, int emailMessageId);
        // BE will make sure that the operation that created calId is complete before sending the
        // email to the server. If the calId operation fails (hard), then the SendEmailCmd will too.
        NcResult SendEmailCmd (int accountId, int emailMessageId, int calId);

        NcResult ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                          int folderId, bool originalEmailIsEmbedded);

        NcResult ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                        int folderId, bool originalEmailIsEmbedded);
        // delete an email from a synced folder. returns token that can be used to possibly cancel.
        NcResult DeleteEmailCmd (int accountId, int emailMessageId);
        // move an email from one folder to another. returns token that can be used to possibly cancel.
        NcResult MoveEmailCmd (int accountId, int emailMessageId, int destFolderId);
        // mark an email as read. returns token that can be used to possibly cancel.
        NcResult MarkEmailReadCmd (int accountId, int emailMessageId);
        // set the flag value on the email.
        NcResult SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                          DateTime start, DateTime utcStart, DateTime due, DateTime utcDue);
        // clear the flag value on the email.
        NcResult ClearEmailFlagCmd (int accountId, int emailMessageId);
        // mark the flag as "done" for the server, and clear the values in the DB.
        NcResult MarkEmailFlagDone (int accountId, int emailMessageId,
                            DateTime completeTime, DateTime dateCompleted);
        NcResult DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDelay = false);
        // download an attachment. returns token that can be used to possibly cancel.
        NcResult DnldAttCmd (int accountId, int attId, bool doNotDelay = false);
        NcResult CreateCalCmd (int accountId, int calId, int folderId);
        NcResult UpdateCalCmd (int accountId, int calId, bool sendBody);
        NcResult DeleteCalCmd (int accountId, int calId);
        NcResult MoveCalCmd (int accountId, int calId, int destFolderId);
        NcResult RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response);
        NcResult RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null);
        NcResult DnldCalBodyCmd (int accountId, int calId);

        /// <summary>
        /// Forward a calendar event.
        /// </summary>
        /// <returns>The token for the pending operation.</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="newEmailMessageId">ID of the outgoing e-mail message.</param>
        /// <param name="forwardedCalId">ID of the McCalendar event being forwarded.</param>
        /// <param name="folderId">ID of the folder that is the parent of the event being forwarded.</param>
        NcResult ForwardCalCmd (int accountId, int newEmailMessageId, int forwardedCalId, int folderId);

        NcResult CreateContactCmd (int accountId, int contactId, int folderId);
        NcResult UpdateContactCmd (int accountId, int contactId);
        NcResult DeleteContactCmd (int accountId, int contactId);
        NcResult MoveContactCmd (int accountId, int contactId, int destFolderId);
        NcResult DnldContactBodyCmd (int accountId, int contactId);
        NcResult CreateTaskCmd (int accountId, int taskId, int folderId);
        NcResult UpdateTaskCmd (int accountId, int taskId);
        NcResult DeleteTaskCmd (int accountId, int taskId);
        NcResult MoveTaskCmd (int accountId, int taskId, int destFolderId);
        NcResult DnldTaskBodyCmd (int accountId, int taskId);
        // create a subordinate folder.
        NcResult CreateFolderCmd (int accountId, int destFolderId, string displayName, ProtoControl.FolderHierarchy.TypeCode folderType);
        // create a root folder.
        NcResult CreateFolderCmd (int accountId, string DisplayName, ProtoControl.FolderHierarchy.TypeCode folderType);
        // delete a folder.
        NcResult DeleteFolderCmd (int accountId, int folderId);
        // move a folder.
        NcResult MoveFolderCmd (int accountId, int folderId, int destFolderId);
        // rename a folder.
        NcResult RenameFolderCmd (int accountId, int folderId, string displayName);
        // Sync the contents of a folder.
        NcResult SyncCmd (int accountId, int folderId);
        // validate account config.
        NcResult ValidateConfig (int accountId, McServer server, McCred cred);
        void CancelValidateConfig (int accountId);
        // state, including auto-d.
        BackEndStateEnum BackEndState (int accountId);
        AutoDInfoEnum AutoDInfo (int accountId);
        X509Certificate2 ServerCertToBeExamined (int accountId);
    }
}

