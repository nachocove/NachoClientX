//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Text;
using Org.BouncyCastle.X509;
using System.IO;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.Security;
using NachoPlatform;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.Utils
{
    public static class CrlMonitor
    {
        // Keep track of all registered monitors
        private static Dictionary<string, CrlMonitorItem> Monitors = new Dictionary<string, CrlMonitorItem> ();

        private static int NextId = 0;

        // For synchronized access of monitors dictionary
        private static object LockObj = new object ();

        static CancellationTokenSource Cts;

        public static void StartService ()
        {
            lock (LockObj) {
                if (Cts != null) {
                    Cts.Cancel ();
                }
                Cts = new CancellationTokenSource ();
                foreach (var monitor in Monitors.Values) {
                    monitor.StartTimer (Cts.Token);
                }
            }
        }

        public static void StopService ()
        {
            lock (LockObj) {
                Cts.Cancel ();
            }
        }

        public static void Register (X509Certificate2 cert, X509Certificate2 signerCert = null)
        {
            lock (LockObj) {
                // need to extract the CRLDP's, and find the root cert that signed them
                var crlUrls = CertificateHelper.CrlDistributionPoint (cert);
                if (null == crlUrls) {
                    return;
                }
                foreach (var dp in crlUrls) {
                    string url;
                    if (!CrlDistributionPoint.IsHttp (dp, out url)) {
                        Log.Error (Log.LOG_PUSH, "Non-HTTP CRL distribution point - {0}", url);
                        continue;
                    }
                    if (Monitors.ContainsKey (url)) {
                        continue;
                    }
                    var monitor = new CrlMonitorItem (Interlocked.Increment (ref NextId), url, cert, signerCert ?? cert);
                    monitor.StartTimer (Cts.Token);
                    Monitors.Add (url, monitor);
                    Log.Info (Log.LOG_PUSH, "CRL Monitor: register {0}", url);
                }
            }
        }

        public static bool Deregister (string url)
        {
            lock (LockObj) {
                CrlMonitorItem monitor;
                if (!Monitors.TryGetValue (url, out monitor)) {
                    return false;
                }
                monitor.StopTimer ();
                var removed = Monitors.Remove (url);
                NcAssert.True (removed);
                return true;
            }
        }

        public static bool IsRevoked (X509Certificate2 cert)
        {
            lock (LockObj) {
                foreach (var monitor in Monitors.Values) {
                    if (monitor.IsRevoked (cert)) {
                        return true;
                    }
                }
                return false;
            }
        }

    }

    public class CrlMonitorItem
    {
        string Name { get; set; }

        const long RetryInterval = 15 * 1000;

        const int DefaultTimeoutSecs = 10;

        string Url { get; set; }

        protected HashSet<string> Revoked { get; set; }

        DateTime? NextUpdate { get; set; }

        // I don't think the number of CRLs will be a large numbers. So, I am not using
        // NcTimerPool now. But if that ever becomes a problem, we can switch easily.
        NcTimer Timer;

        X509Crl Crl { get; set; }

        /// <summary>
        /// The issuer of certs. May not be the same as the CrlSignerCert.Subject, which is why it's separate.
        /// </summary>
        public X500DistinguishedName Issuer;

        /// <summary>
        /// The crl signer cert. May not be the same as the CA certificate, though is most cases it will be (delegate
        /// signing certificates are allowed by RFC but rarely used)
        /// </summary>
        X509Certificate2 CrlSignerCert;

        /// <summary>
        /// Initializes a new instance of the <see cref="NachoCore.Utils.CrlMonitorItem"/> class.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="url">URL for the CRL</param>
        /// <param name="cacert">The CA Certificate</param>
        /// <param name="delegateSigner">The CRL delegate signing certificate, if one is used (rare)</param>
        public CrlMonitorItem (int id, string url, X509Certificate2 cacert, X509Certificate2 delegateSigner = null)
        {
            Name = String.Format ("CrlMonitorItem[{0}]", id);
            Url = url;
            Issuer = cacert.IssuerName;
            CrlSignerCert = delegateSigner ?? cacert;
            Revoked = new HashSet<string> ();
        }

        public void StopTimer ()
        {
            if (null != Timer) {
                Timer.Dispose ();
                Timer = null;
            }
        }

        /// <summary>
        /// Starts the timer with a time of 0, i.e. immediately. The underlying code will reset the timer to run when needed.
        /// TODO: Get rid of the timers, and instead have the CrlMonitor static class periodically inspect all CrlMonitorItem's
        /// to see if a new CRL needs to be fetched.
        /// </summary>
        public void StartTimer (CancellationToken cToken)
        {
            StopTimer ();
            cToken.Register (StopTimer);
            Timer = new NcTimer (Name, (state) => {
                NcTask.Run (() => {
                    ValidateCrl (cToken);
                }, (string)state);
            }, null, 0, 0);
        }

        public virtual INcHttpClient HttpClient {
            get {
                return NcHttpClient.Instance;
            }
        }

        void ValidateCrl (CancellationToken cToken)
        {
            var request = new NcHttpRequest (HttpMethod.Get, Url);
            NextUpdate = null;
            HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, cToken);
        }

        void DownloadSuccess (NcHttpResponse response, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            long retryIn = RetryInterval;
            try {
                NcAssert.True (null != response, "response should not be null");
                if (HttpStatusCode.OK == response.StatusCode) {
                    NcAssert.True (null != response.Content, "content should not be null");
                    if (ExtractCrl (response.Content as FileStream)) {
                        if (token.IsCancellationRequested) {
                            return;
                        }
                        CrlGetRevoked ();
                        Log.Info (Log.LOG_PUSH, "{0}: CRL pull response: statusCode={1}", Name, response.StatusCode);
                    }
                } else {
                    Log.Warn (Log.LOG_PUSH, "{0}: CRL pull response: statusCode={1}", Name, response.StatusCode);
                }
                if (NextUpdate.HasValue) {
                    Log.Info (Log.LOG_SYS, "{0}: NextUpdate {1}", Name, NextUpdate.Value);
                    var ms = (NextUpdate.Value - DateTime.UtcNow).TotalMilliseconds;
                    NcAssert.True (ms > 0);
                    retryIn = (long)ms;
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "{0}: Exception processing response: {1}", Name, ex);
            } finally {
                ResetTimer (retryIn);
            }
        }

        void DownloadError (Exception ex, CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }
            try {
                if (ex is OperationCanceledException) {
                    Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: canceled", Name);
                } else if (ex is WebException) {
                    var webex = ex as WebException;
                    Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: Caught network exception: {1} - {2}", Name, webex.Status, webex.Message);
                } else {
                    Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: Caught unexpected http exception - {1}", Name, ex);
                }
            } finally {
                ResetTimer (RetryInterval);
            }
        }

        void ResetTimer (long nextRetry)
        {
            // Check that the timer has not been disposed because the client goes to background.
            if (null != Timer) {
                Timer.Change (nextRetry, 0);
            }
        }

        /// <summary>
        /// Given a CRL, check whether it's still valid (i.e. If NextUpdate is still in the future)
        /// and whether the CRL signature can be validated with the CrlSignerCert.
        /// </summary>
        /// <returns><c>true</c>, if crl was extracted, <c>false</c> otherwise.</returns>
        /// <param name="crl">Crl.</param>
        /// <param name="failOnExpired">If set to <c>true</c> fail on expired.</param>
        protected bool ExtractCrl (Stream crl, bool failOnExpired = true)
        {
            X509CrlParser parser = new X509CrlParser (true);
            var theCrl = parser.ReadCrl (crl);
            if (theCrl == null) {
                Log.Info (Log.LOG_SYS, "Could not convert crl");
                return false;
            }
            var now = DateTime.UtcNow;
            if (theCrl.NextUpdate.Value <= now) {
                // For dev, we hardly ever update the CRL, so just set it to once a day
                if (failOnExpired || !BuildInfoHelper.IsDev) {
                    Log.Info (Log.LOG_SYS, "{0}: CRL is already expired: {1}", Name, theCrl.NextUpdate.Value);
                    return false;
                } else {
                    NextUpdate = DateTime.UtcNow.AddDays (1);
                }
            } else {
                NextUpdate = theCrl.NextUpdate.Value;
            }

            var crlIssuerDn = new X500DistinguishedName (theCrl.IssuerDN.GetDerEncoded ());
            if (crlIssuerDn.Name != CrlSignerCert.SubjectName.Name) {
                Log.Info (Log.LOG_SYS, "CRL issuer {0} does not match SignerCert {1}", crlIssuerDn.Name, CrlSignerCert.SubjectName.Name);
                return false;
            }
            try {
                var certStream = new MemoryStream (CrlSignerCert.Export (X509ContentType.Cert));
                X509CertificateParser x509Parser = new X509CertificateParser ();
                var cert = x509Parser.ReadCertificate (certStream);
                theCrl.Verify (cert.GetPublicKey ());
            } catch (CrlException ex) {
                Log.Error (Log.LOG_SYS, "CrlException: {0}", ex.Message);
                return false;
            } catch (SignatureException ex) {
                Log.Error (Log.LOG_SYS, "SignatureException: {0}", ex.Message);
                return false;
            }
            Crl = theCrl;
            return true;
        }

        protected void CrlGetRevoked ()
        {
            NcAssert.NotNull (Crl);

            var revokedSet = new HashSet<string> ();
            var revokedList = Crl.GetRevokedCertificates ();
            if (revokedList != null) {
                foreach (X509CrlEntry revoked in revokedList) {
                    revokedSet.Add (revoked.SerialNumber.LongValue.ToString ("X"));
                }
            }
            Revoked = revokedSet;
        }

        /// <summary>
        /// Checks to see if this certificate is revoked. Both Issuer and SerialNumber must be checked.
        /// </summary>
        /// <returns><c>true</c> if this certificate is revoked; otherwise, <c>false</c>.</returns>
        /// <param name="cert">Cert.</param>
        public bool IsRevoked (X509Certificate2 cert)
        {
            return cert.IssuerName == Issuer && Revoked.Contains (cert.SerialNumber.ToUpperInvariant ());
        }

        public static string ExportToPEM (X509Certificate2 cert)
        {
            StringBuilder builder = new StringBuilder ();            

            builder.AppendLine ("-----BEGIN CERTIFICATE-----");
            builder.AppendLine (Convert.ToBase64String (cert.Export (X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine ("-----END CERTIFICATE-----");

            return builder.ToString ();
        }
    }
}

