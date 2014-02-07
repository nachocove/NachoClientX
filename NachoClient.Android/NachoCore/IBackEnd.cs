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
        void ServerConfResp (int accountId);
        // let the BE know that the credentials have been updated for this account.
        void CredResp (int accountId);
        // cancel command/request associated with this token (if possible).
        bool Cancel (int accountId, string token);
        // event can be used to register for status indications.
        event EventHandler StatusIndEvent;
        // search contacts. returns token that can be used to cancel the search and all eclipsed searches.
        string StartSearchContactsReq (int accountId, string prefix, uint? maxResults);
        // follow-on contacts search, using same token.
        void SearchContactsReq (int accountId, string prefix, uint? maxResults, string token);
        // send specified email (not in a synced folder). returns token that can be used to possibly cancel.
        string SendEmailCmd (int accountId, int emailMessageId);
        // delete an email from a synced folder. returns token that can be used to possibly cancel.
        string DeleteEmailCmd (int accountId, int emailMessageId);
        // move an email from one folder to another. returns token that can be used to possibly cancel.
        string MoveItemCmd (int accountId, int emailMessageId, int destFolderId);
        // mark an email as read. returns token that can be used to possibly cancel.
        string MarkEmailReadCmd (int accountId, int emailMessageId);
        // set the flag value on the email.
        string SetEmailFlagCmd (int accountId, int emailMessageId, string flagMessage, DateTime utcStart, DateTime utcDue);
        // clear the flag value on the email.
        string ClearEmailFlagCmd (int accountId, int emailMessageId);
        // mark the flag as "done" for the server, and clear the values in the DB.
        string MarkEmailFlagDone (int accountId, int emailMessageId);
        // download an attachment. returns token that can be used to possibly cancel.
        string DnldAttCmd (int accountId, int attId);
        //
        // in the BE for now, but moving to middleware/app-land someday:
        //
        McFolder GetOutbox (int accountId);
        McFolder GetGalCache (int accountId);
        McFolder GetGleaned (int accountId);
    }
}

