using SQLite;
using System;
using System.Security.Cryptography.X509Certificates;
using NachoCore.Utils;

namespace NachoCore
{
    public interface IProtoControlOwner
    {
        void StatusInd (ProtoControl sender, NcResult status);
        void StatusInd (ProtoControl sender, NcResult status, string[] tokens);
        void CredReq (ProtoControl sender);
        void ServConfReq (ProtoControl sender);
        void CertAskReq (ProtoControl sender, X509Certificate2 certificate);
        void SearchContactsResp (ProtoControl sender, string prefix, string token);
        void SendEmailResp (ProtoControl sender, int emailMessageId, bool didSend);
    }
}
