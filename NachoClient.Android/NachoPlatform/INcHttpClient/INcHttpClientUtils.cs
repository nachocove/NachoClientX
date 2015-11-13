//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace NachoPlatform
{
    public static class NcHttpCertificateValidation
    {
        static string SubjectAltNameOid = "2.5.29.17";

        static readonly Regex cnRegex = new Regex (@"CN\s*=\s*([^,]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public static bool CertValidation (Uri Url, X509Certificate2 root, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors != SslPolicyErrors.None) {
                goto sslErrorVerify;
            }

            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            try {
                if (!chain.Build (root)) {
                    errors = SslPolicyErrors.RemoteCertificateChainErrors;
                    goto sslErrorVerify;
                }
            } catch (System.Security.Cryptography.CryptographicException) {
                // As best we can tell, a XAMMIT (spurious).
                errors = SslPolicyErrors.RemoteCertificateChainErrors;
                goto sslErrorVerify;
            }

            var subject = root.Subject;
            var subjectCn = cnRegex.Match (subject).Groups [1].Value;

            if (String.IsNullOrWhiteSpace (subjectCn) || !MatchHostnameToPattern (Url.Host, subjectCn)) {
                bool found = false;
                foreach (var ext in root.Extensions) {
                    if (ext.Oid.Value == SubjectAltNameOid) {
                        // TODO Quite the hack. Need to figure out how to get the raw data rather than string splitting
                        foreach (var line in ext.Format (true).Split (new [] {'\n'})) {
                            var parts = line.Split (new []{ '=' });
                            if (parts [0] == "DNS Name") {
                                if (MatchHostnameToPattern (Url.Host, parts [1])) {
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (found)
                        break;
                }
                if (!found) {
                    errors = SslPolicyErrors.RemoteCertificateNameMismatch;
                    goto sslErrorVerify;
                }
            }

        sslErrorVerify:
            // NachoCove: Add this to make it look like other HTTP client
            var request = new HttpWebRequest (Url);
            // End of NachoCove
            return ServicePointManager.ServerCertificateValidationCallback (request, root, chain, errors);
        }

        static bool MatchHostnameToPattern (string hostname, string pattern)
        {
            // check if this is a pattern
            int index = pattern.IndexOf ('*');
            if (index == -1) {
                // not a pattern, do a direct case-insensitive comparison
                return (String.Compare (hostname, pattern, true, CultureInfo.InvariantCulture) == 0);
            }

            // check pattern validity
            // A "*" wildcard character MAY be used as the left-most name component in the certificate.

            // unless this is the last char (valid)
            if (index != pattern.Length - 1) {
                // then the next char must be a dot .'.
                if (pattern [index + 1] != '.') {
                    return false;
                }
            }

            // only one (A) wildcard is supported
            int i2 = pattern.IndexOf ('*', index + 1);
            if (i2 != -1)
                return false;

            // match the end of the pattern
            string end = pattern.Substring (index + 1);
            int length = hostname.Length - end.Length;
            // no point to check a pattern that is longer than the hostname
            if (length <= 0)
                return false;

            if (String.Compare (hostname, length, end, 0, end.Length, true, CultureInfo.InvariantCulture) != 0) {
                return false;
            }

            // special case, we start with the wildcard
            if (index == 0) {
                // ensure we hostname non-matched part (start) doesn't contain a dot
                int i3 = hostname.IndexOf ('.');
                return ((i3 == -1) || (i3 >= (hostname.Length - end.Length)));
            }

            // match the start of the pattern
            string start = pattern.Substring (0, index);
            return (String.Compare (hostname, 0, start, 0, start.Length, true, CultureInfo.InvariantCulture) == 0);
        }
    }
}

