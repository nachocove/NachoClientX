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

        public static string FormatCertificateData (X509Certificate2 certificate)
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
                subject.Trim ().Replace ("\"", "") + "\n\n" +

                "Issuer: \n" +
                issuer.Trim ().Replace ("\"", "") + "\n\n" +

                "Serial Number: " + serialNumber + "\n" +
                "Version: " + version + "\n\n" +

                "Signature Algorithm: " + signatureAlgorithm + "\n\n" +

                "Not Valid Before: \n" +
                notValidBefore + "\n\n" +
                "Not Valid After: \n" +
                notValidAfter;

            return  certificateToken;
        }

        public static string GetField (X509Certificate2 certificate, string[] matches)
        {
            string multiline = certificate.SubjectName.Format (true);
            string[] parsedFields = multiline.Split (new char[] { '\n' });
        
            foreach (var field in parsedFields) {
                if (!string.IsNullOrEmpty (field)) {
                    foreach (var match in matches) {
                        var target = match + "=";
                        if (field.StartsWith (target)) {
                            return field.Substring (target.Length);
                        }
                    }
                }
            }
            return "";
        }

        public static string GetCommonName (X509Certificate2 certificate)
        {
            return GetField (certificate, new String[] { "CN" });
        }

        public static string GetOrganizationname (X509Certificate2 certificate)
        {
            return GetField (certificate, new String[] { "O", "OU" });
        }

        public static string DebugInfo (X509Certificate2 certificate)
        {
            return String.Format (
                "Subject: {0}\n" +
                "Issuer: {1}\n" +
                "Version: {2}\n" +
                "Serial #: {3}\n" +
                "Not Valid Before: {4}\n" +
                "Not Valid After: {5}\n", certificate.Subject, certificate.Issuer, certificate.Version,
                certificate.SerialNumber, certificate.NotBefore, certificate.NotAfter);
        }
    }
}


