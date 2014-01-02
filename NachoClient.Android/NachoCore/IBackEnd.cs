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
        void Start (NcAccount account);
        // let the BE know that the asked-about server cert is/not okay to trust.
        void CertAskResp (NcAccount account, bool isOkay);
        // let the BE know that the server info has been updated for this account.
        void ServerConfResp (NcAccount account);
        // let the BE know that the credentials have been updated for this account.
        void CredResp (NcAccount account);
        // search contacts. returns token that can be used to cancel the search and all eclipsed searches.
        string StartSearchContactsReq (NcAccount account, string prefix, uint? maxResults);
        // follow-on contacts search.
        void SearchContactsReq (NcAccount account, string prefix, uint? maxResults, string token);
        // cancel contacts search. You don't need to call this if the latest search has completed.
        void CancelSearchContactsReq (NcAccount account, string token);
    }
}

