using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoCertificateResponder
    {
        void SetOwner (INachoCertificateResponderParent o);
        void SetCertificateInfo ();
    }

    public interface INachoCertificateResponderParent
    {
        void DontAcceptCertificate();
        void AcceptCertificate();
    }
}