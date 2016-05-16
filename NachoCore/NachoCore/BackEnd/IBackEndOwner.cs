using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    // The owner is a delegate in the Cocoa sense. This API must be implemented by
    // the owner of a BackEnd object.
    public interface IBackEndOwner
    {
        ///
        /// CredRequest: When called, the callee must gather the credential for the specified 
        /// account and add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.
        ///
        void CredReq (int accountId);

        ///
        /// ServConfRequest: When called the callee must gather the server information for the 
        /// specified account and nd add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.
        ///
        void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg);

        ///
        /// CertAskReq: When called the callee must ask the user whether the passed server cert can
        /// be trusted for the specified account. 
        ///
        void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, X509Certificate2 certificate);

        void SearchContactsResp (int accountId, string prefix, string token);

        void SendEmailResp (int accountId, int emailMessageId, bool didSend);
    }
}

