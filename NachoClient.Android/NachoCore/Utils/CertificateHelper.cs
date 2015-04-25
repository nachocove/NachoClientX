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
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1;

namespace NachoCore.Utils
{
    public class CertificateHelper
    {
        private const string kOidCrlDistributionPoint = "2.5.29.31";

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

        public static bool IsSelfSigned (X509Certificate2 certificate)
        {
            return certificate.Subject == certificate.Issuer;
        }

        public static HashSet<string> CrlDistributionPoint (X509Certificate2 certificate)
        {
            // Use hash set instead of list so that 
            var crlUrlSet = new HashSet<string> (); // use hash set instead of list in
            foreach (var ext in certificate.Extensions) {
                if (kOidCrlDistributionPoint == ext.Oid.Value) {
                    var asn1Bytes = new Asn1InputStream (ext.RawData);
                    var asn1Object = (Asn1Sequence)asn1Bytes.ReadObject ();
                    foreach (Asn1Sequence ans1distributionPoint in asn1Object) {
                        var distributionPoint = new CrlDistributionPoint (ans1distributionPoint);
                        crlUrlSet.Add (distributionPoint.Url);
                    }
                    return crlUrlSet;
                }
            }
            return null;
        }
    }

    // Bouncy Castle does not provide a X509 extension parser that return
    public class CrlDistributionPoint
    {
        public const int kDistributionPoint = 0;
        public const int kReason = 1;
        public const int kCrlIssuer = 2;

        // DistributionPointName
        public const int kFullName = 0;
        public const int kNameRelativeToCRLIssuer = 1;

        // GeneralNames
        public const int kUniformResourceIdentifier = 6;

        public string Url { get; protected set; }

        public CrlDistributionPoint (Asn1Sequence asn1DistributionPoint)
        {
            foreach (Asn1TaggedObject asn1Object in asn1DistributionPoint) {
                switch (asn1Object.TagNo) {
                case kDistributionPoint:
                    var dp = (Asn1TaggedObject)asn1Object.GetObject ();
                    if (kFullName != dp.TagNo) {
                        throw new ArgumentException (String.Format ("unexpected tag {0} in DistributionPoint", dp.TagNo));
                    }
                    var fn = (Asn1TaggedObject)dp.GetObject ();
                    if (kUniformResourceIdentifier != fn.TagNo) {
                        throw new ArgumentException (String.Format ("unexpected tag {0} in DistributionPoint.FullName", fn.TagNo));
                    }
                    var url = (Asn1OctetString)fn.GetObject ();
                    Url = System.Text.ASCIIEncoding.ASCII.GetString (url.GetOctets ());
                    break;
                case kReason:
                    break;
                case kCrlIssuer:
                    break;
                default:
                    Log.Warn (Log.LOG_UTILS, "CrlDistributionPoint: unknown tag {0}", asn1Object.TagNo);
                    break;
                }
            }
        }

        public static bool IsHttp (string dp, out string url)
        {
            if (dp.StartsWith ("URI:")) {
                url = dp.Substring (4);
            } else {
                url = dp;
            }
            return url.StartsWith ("http:");
        }
    }
}


