// # Copyright (C) 2013, 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
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
        // ask all accounts to sync immediately (does a Start if needed).
        void ForceSync ();
        // for a single account to sync immediately.
        void ForceSync (int accountId);
        // let the BE know that the asked-about server cert is/not okay to trust.
        void CertAskResp (int accountId, bool isOkay);
        // let the BE know that the server info has been updated for this account.
        void ServerConfResp (int accountId, bool forceAutodiscovery);
        // let the BE know that the credentials have been updated for this account.
        void CredResp (int accountId);
        // cancel command/request associated with this token (if possible).
        void Cancel (int accountId, string token);
        // event can be used to register for status indications.
        event EventHandler StatusIndEvent;
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
        string MoveItemCmd (int accountId, int emailMessageId, int destFolderId);
        // mark an email as read. returns token that can be used to possibly cancel.
        string MarkEmailReadCmd (int accountId, int emailMessageId, int folderId);
        // set the flag value on the email.
        string SetEmailFlagCmd (int accountId, int emailMessageId, int folderId, string flagType, 
                          DateTime start, DateTime utcStart, DateTime due, DateTime utcDue);
        // clear the flag value on the email.
        string ClearEmailFlagCmd (int accountId, int emailMessageId, int folderId);
        // mark the flag as "done" for the server, and clear the values in the DB.
        string MarkEmailFlagDone (int accountId, int emailMessageId, int folderId,
                            DateTime completeTime, DateTime dateCompleted);
        // download an attachment. returns token that can be used to possibly cancel.
        string DnldAttCmd (int accountId, int attId);
        string CreateCalCmd (int accountId, int calId, int folderId);
        string RespondCalCmd (int accountId, int calId, int folderId, NcResponseType response);
        // create a subordinate folder.
        string CreateFolderCmd (int accountId, int destFolderId, string displayName, uint folderType,
                          bool IsClientOwned, bool isHidden);
        // create a root folder.
        string CreateFolderCmd (int accountId, string DisplayName, uint folderType,
                          bool IsClientOwned, bool isHidden);
        // delete a folder.
        string DeleteFolderCmd (int accountId, int folderId);
        // move a folder.
        string MoveFolderCmd (int accountId, int folderId, int destFolderId);
        // rename a folder.
        string RenameFolderCmd (int accountId, int folderId, string displayName);
        //
        // in the BE for now, but moving to middleware/app-land someday:
        //
        McFolder GetOutbox (int accountId);

        McFolder GetGalCache (int accountId);

        McFolder GetGleaned (int accountId);
    }
}

