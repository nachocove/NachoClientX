using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Model;

namespace NachoCore
{
    // The owner is a delegate in the Cocoa sense of the word. This API must be implemented by
    // the owner of a BackEnd object.
    public interface IBackEndOwner
    {
        /* CredRequest: When called, the callee must gather the credential for the specified 
         * account and add/update it to/in the DB. The callee must then update
         * the account record. The BE will act based on the update event for the
         * account record.
         */
        void CredReq (NcAccount account);

        /* ServConfRequest: When called the callee must gather the server information for the 
         * specified account and nd add/update it to/in the DB. The callee must then update
         * the account record. The BE will act based on the update event for the
         * account record.
         */
        void ServConfReq (NcAccount account);

        /* CertAskReq: When called the callee must ask the user whether the passed server cert can
         * be trusted for the specified account. 
         */
        void CertAskReq (NcAccount account, X509Certificate2 certificate);

        /* HardFailureIndication: Called to indicate to the callee that there is a failure
         * that will require some sort of intervention. The callee must call the BE method
         * Start(account) to get the BE going again (post intervention).
         */
        void HardFailInd (NcAccount account);

        /* SoftFailureIndication: Called to indicate that "it aint workin' right now." The
         * callee must call the BE method Start(account) to get the BE going again. We will
         * want to add some autorecovery here in the future.
         */
        void SoftFailInd (NcAccount account);

        bool RetryPermissionReq (NcAccount account, uint delaySeconds);

        void ServerOOSpaceInd (NcAccount account);
    }
}

