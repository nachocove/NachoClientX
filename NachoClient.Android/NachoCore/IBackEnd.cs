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
        void Start (McAccount account);
        // let the BE know that the asked-about server cert is/not okay to trust.
        void CertAskResp (McAccount account, bool isOkay);
        // let the BE know that the server info has been updated for this account.
        void ServerConfResp (McAccount account);
        // let the BE know that the credentials have been updated for this account.
        void CredResp (McAccount account);
        // cancel command/request associated with this token (if possible).
        bool Cancel (McAccount account, string token);
        // search contacts. returns token that can be used to cancel the search and all eclipsed searches.
        string StartSearchContactsReq (McAccount account, string prefix, uint? maxResults);
        // follow-on contacts search, using same token.
        void SearchContactsReq (McAccount account, string prefix, uint? maxResults, string token);
        // send specified email (not in a synced folder). returns token that can be used to possibly cancel.
        string SendEmailCmd (McAccount account, int emailMessageId);
        // delete an email from a synced folder. returns token that can be used to possibly cancel.
        string DeleteEmailCmd (McAccount account, int emailMessageId);
        // move an email from one folder to another. returns token that can be used to possibly cancel.
        string MoveItemCmd (McAccount account, int emailMessageId, int destFolderId);
        // mark an email as read. returns token that can be used to possibly cancel.
        string MarkEmailReadCmd (McAccount account, int emailMessageId);
        // download an attachment. returns token that can be used to possibly cancel.
        string DnldAttCmd (McAccount account, int attId);
        //
        // in the BE for now, but moving to middleware/app-land someday:
        //
        McFolder GetOutbox (int accountId);
        McFolder GetGalCache (int accountId);
        McFolder GetGleaned (int accountId);
    }
}

