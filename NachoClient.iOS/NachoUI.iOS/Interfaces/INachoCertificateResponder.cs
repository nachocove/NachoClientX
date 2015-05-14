using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoCertificateResponderParent
    {
        void DontAcceptCertificate(int accountId);
        void AcceptCertificate(int accountId);
    }
}