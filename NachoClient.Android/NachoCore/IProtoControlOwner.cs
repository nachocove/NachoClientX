using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Utils;

namespace NachoCore
{
    public interface IProtoControlOwner
    {
        SQLiteConnectionWithEvents Db { set; get; }
        string AttachmentsDir { set; get; }

        void CredReq (ProtoControl sender);
        void ServConfReq (ProtoControl sender);
        void CertAskReq (ProtoControl sender, X509Certificate2 certificate);
        void HardFailInd (ProtoControl sender);
        void TempFailInd (ProtoControl sender);
        bool RetryPermissionReq (ProtoControl sender, uint delaySeconds);
        void ServerOOSpaceInd (ProtoControl sender);
    }
}
