// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
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
    }
}

