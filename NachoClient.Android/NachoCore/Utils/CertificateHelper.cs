//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using NachoCore.Model;
using NachoClient;
using NachoCore;
using NachoCore.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Utils
{
    public class CertificateHelper
    {
        public CertificateHelper ()
        {
        }

        public string formatCertificateData (X509Certificate2 certificate)
        {
            string subject = certificate.Subject;
            string issuer = certificate.Issuer;
            string notValidBefore = certificate.NotBefore.ToString ();
            string notValidAfter = certificate.NotAfter.ToString ();
            string thumbprint = certificate.Thumbprint;
            string serialNumber = certificate.SerialNumber;
            string version = certificate.Version.ToString ();
            string signatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName;

            string certificateToken = 

                "Subject: \n" + 
                subject.Trim().Replace("\"", "") + "\n\n" +

                "Issuer: \n" +
                issuer.Trim().Replace("\"", "") + "\n\n" + 

                "Serial Number: " + serialNumber + "\n" +
                "Version: " + version + "\n\n" +

                "Signature Algorithm: " + signatureAlgorithm + "\n\n" +

                "Not Valid Before: \n" +
                notValidBefore + "\n\n" +
                "Not Valid After: \n" +
                notValidAfter;

            return  certificateToken;
        }
    }
}


