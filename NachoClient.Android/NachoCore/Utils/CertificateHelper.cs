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

namespace NachoCore.Utils
{
    public class CertificateHelper
    {
        string subjectCountry = "";
        string subjectOrganization = "";
        string subjectOrgainizationalUnit = "";
        string subjectCommonName = "";
        string issuerCountry = "";
        string issuerOrganization = "";
        string issuerOrganizationalUnit = "";
        string issuerCommonName = "";
        char[] delimeters = { ',', '=' };

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

            parseSubjectItems (subject);
            parseIssuerItems (issuer);

            string certificateToken = 

                "Subject \n" +
                "Common Name: " + subjectCommonName + "\n" +
                "Organization Unit: " + subjectOrgainizationalUnit + "\n" +
                "Organization: " + subjectOrganization + "\n" +
                "Country: " + subjectCountry + "\n\n" +

                "Issuer \n" +
                "Country: " + issuerCountry + "\n" +
                "Organization: " + issuerOrganization + "\n" +
                "Organizational Unit: " + issuerOrganizationalUnit + "\n" +
                "Common Name: " + issuerCommonName + "\n\n" +

                "Serial Number: " + serialNumber + "\n" +
                "Version: " + version + "\n\n" +

                "Signature Algorithm: " + signatureAlgorithm + "\n\n" +

                "Not Valid Before: \n" +
                notValidBefore + "\n\n" +
                "Not Valid After: \n" +
                notValidAfter;

            return  certificateToken;
        }

        public void parseSubjectItems (string subject)
        {
            string[] subjectTokens = subject.Split (delimeters);
            List<KeyValuePair<string, string>> subjectComponentKVPs = new List<KeyValuePair<string,string>> ();
            for (int i = 0; i < subjectTokens.Length;) {
                string key = subjectTokens [i].Trim ();
                string value = subjectTokens [i + 1].Trim ();
                subjectComponentKVPs.Add (new KeyValuePair<string, string> (key, value));
                i = i + 2;
            }

            foreach (KeyValuePair<string,string> kvp in subjectComponentKVPs) {
                if (kvp.Key == "CN") {
                    subjectCommonName = kvp.Value;
                }
                if (kvp.Key == "OU") {
                    subjectOrgainizationalUnit = kvp.Value;
                }
                if (kvp.Key == "O") {
                    subjectOrganization = kvp.Value;
                }
                if (kvp.Key == "C") {
                    subjectCountry = kvp.Value;
                }
            }
        }

        public void parseIssuerItems (string issuer)
        {
            string[] issuerTokens = issuer.Split (delimeters);
            List<KeyValuePair<string, string>> issuerComponentKVPs = new List<KeyValuePair<string,string>> ();
            for (int i = 0; i < issuerTokens.Length;) {
                string key = issuerTokens [i].Trim ();
                string value = issuerTokens [i + 1].Trim ();
                issuerComponentKVPs.Add (new KeyValuePair<string, string> (key, value));
                i = i + 2;
            }

            foreach (KeyValuePair<string,string> kvp in issuerComponentKVPs) {
                if (kvp.Key == "CN") {
                    issuerCommonName = kvp.Value;
                }
                if (kvp.Key == "OU") {
                    issuerOrganizationalUnit = kvp.Value;
                }
                if (kvp.Key == "O") {
                    issuerOrganization = kvp.Value;
                }
                if (kvp.Key == "C") {
                    issuerCountry = kvp.Value;
                }
            }
        }
    }
}


