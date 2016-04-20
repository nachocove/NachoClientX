using System.Security.Cryptography.X509Certificates;
using NachoCore.Utils;

namespace NachoCore
{
    public interface INcProtoControlOwner
    {
        void StatusInd (NcProtoControl sender, NcResult status);
        void StatusInd (NcProtoControl sender, NcResult status, string[] tokens);
        void CredReq (NcProtoControl sender);
        void ServConfReq (NcProtoControl sender, BackEnd.AutoDFailureReasonEnum arg);
        void CertAskReq (NcProtoControl sender, X509Certificate2 certificate);
        void SearchContactsResp (NcProtoControl sender, string prefix, string token);
        void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend);
    }
}
