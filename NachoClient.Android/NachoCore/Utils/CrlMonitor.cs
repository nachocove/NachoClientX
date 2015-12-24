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
    public class CrlMonitor
    {
        // Keep track of all registered monitors
        protected Dictionary<string, CrlMonitorItem> Monitors = new Dictionary<string, CrlMonitorItem> ();

        protected int NextId = 0;

        // For synchronized access of monitors dictionary
        private static object LockObj = new object ();

        protected CancellationTokenSource Cts;

        const int DefaultRecheckTimeSecs = 60 * 60;

        protected static CrlMonitor _Instance;

        public static CrlMonitor Instance {
            get {
                if (null == _Instance) {
                    lock (LockObj) {
                        if (null == _Instance) {
                            Log.Info (Log.LOG_SYS, "CrlMonitor: Creating Instance");
                            _Instance = new CrlMonitor ();
                        }
                    }
                }
                return _Instance;
            }
        }

        public NcTimer MonitorTimer { get; set; }

        public CrlMonitor ()
        {
        }

        public void StartService ()
        {
            StopService ();
            Cts = new CancellationTokenSource ();
            MonitorTimer = new NcTimer ("CrlMonitorTimer", (state) => {
                foreach (var monitor in Monitors.Values) {
                    if (monitor.NeedsUpdate ()) {
                        monitor.StartUpdate (Cts.Token); // spawns a task
                    }
                }
            }, null, 0, DefaultRecheckTimeSecs * 1000);
        }

        public void StopService ()
        {
            lock (LockObj) {
                if (Cts != null) {
                    Cts.Cancel ();
                    Cts = null;
                }
                if (MonitorTimer != null) {
                    MonitorTimer.Dispose ();
                    MonitorTimer = null;
                }
            }
        }

        public void Register (X509Certificate2Collection signerCerts)
        {
            lock (LockObj) {
                foreach (var cert in signerCerts) {
                    if (Monitors.ContainsKey (cert.SubjectName.Name)) {
                        continue;
                    }
                    var urls = CDPUrls (cert);
                    if (urls.Count == 0) {
                        continue;
                    }
                    var monitor = new CrlMonitorItem (Interlocked.Increment (ref NextId), cert, urls, signerCerts);
                    Monitors.Add (cert.SubjectName.Name, monitor);
                    monitor.StartUpdate (Cts.Token);
                }
            }
        }

        public bool Deregister (X509Certificate2 cert)
        {
            lock (LockObj) {
                CrlMonitorItem monitor;
                if (!Monitors.TryGetValue (cert.SubjectName.Name, out monitor)) {
                    return false;
                }
                var removed = Monitors.Remove (cert.SubjectName.Name);
                NcAssert.True (removed);
                return true;
            }
        }

        public bool IsRevoked (X509Certificate2 cert)
        {
            lock (LockObj) {
                CrlMonitorItem monitor;
                if (!Monitors.TryGetValue (cert.IssuerName.Name, out monitor)) {
                    return false;
                }

                return monitor.IsRevoked (cert);
            }
        }

        public static List<string> CDPUrls (X509Certificate2 cert)
        {
            var ret = new List<string> ();
            var crlUrls = CertificateHelper.CrlDistributionPoint (cert);
            if (null != crlUrls) {
                foreach (var dp in crlUrls) {
                    string url;
                    if (!CrlDistributionPoint.IsHttp (dp, out url)) {
                        Log.Error (Log.LOG_PUSH, "Non-HTTP CRL distribution point (ignoring) - {0}", url);
                        continue;
                    }
                    ret.Add (url);
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// Crl monitor item. This represents one URL found in the list of CDP's (CRL Distribution Points), and will
    /// monitor that URL periodically and fetch the CRL, validate it, and extract the list of revoked certificates.
    /// 
    /// Note this approach has a few flaws, but it works for our current environment:
    /// 1) CRL's can be signed by any valid CA certificate in the chain for a given cert. For example our pinger
    ///   certs for officetaco.com (alphapinger.officetaco.com) are signed by the D2 Sub-CA, which in turn is signed
    ///   by Digicert's Top-level Root CA Certificate (alphapinger.officetaco.com -> D2 -> Digicert Root). The D2
    ///   Certificate has a list of CDP's, that point to CRL's that are signed by the ROOT CERT, and not by the D2 cert.
    ///   The RFC's don't specify how CRL's are supposed to be signed, just that they have to be. So every CA is free
    ///   to do it in whatever way they chose.
    /// 
    /// 2) The CRL's listed in a CDP-list may be the same CRL (for fault tolerance), or they could be different CRL's.
    ///   Again the RFC's don't really say.
    /// </summary>
    public class CrlMonitorItem
    {
        public string Name { get; set; }

        const long RetryInterval = 15 * 1000;

        const int DefaultTimeoutSecs = 10;

        List<string> Urls { get; set; }

        int UrlIndex { get; set; }

        DateTime? NextUpdate { get; set; }

        X509Crl Crl { get; set; }

        /// <summary>
        /// The Issuer cert. May not be the same as the CRL signer certificate
        /// </summary>
        X509Certificate2 Cert;

        X509Certificate2Collection CrlSignerCerts;

        protected HashSet<string> Revoked { get; set; }

        protected bool UpdateRunning { get; set; }

        int Retries;

        const int MaxRetries = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="NachoCore.Utils.CrlMonitorItem"/> class.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="cert">The Certificate</param>
        /// <param name="urls">The CRL distribution point URLs</param>
        /// <param name="signerCerts">List of possible CRL signing certificates. The CRL is usually signed by the Top-level CA certificate, but not always.</param>
        public CrlMonitorItem (int id, X509Certificate2 cert, List<string> urls, X509Certificate2Collection signerCerts)
        {
            Name = String.Format ("CrlMonitorItem[{0}]", id);
            Urls = urls;
            UrlIndex = 0;
            Cert = cert;

            // Copy the certs
            List<string> names = new List<string> ();
            CrlSignerCerts = new X509Certificate2Collection ();
            foreach (var c in signerCerts) {
                names.Add (c.SubjectName.Name);
                CrlSignerCerts.Add (c);
            }
        }

        public virtual INcHttpClient HttpClient {
            get {
                return NcHttpClient.Instance;
            }
        }

        public bool NeedsUpdate ()
        {
            if (NextUpdate.HasValue && NextUpdate.Value <= DateTime.UtcNow) {
                return true;
            }
            return false;
        }

        public void StartUpdate (CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }

            if (UpdateRunning) {
                return;
            }
            cToken.Register (FinishUpdate);
            Retries = 0;
            UrlIndex = 0;
            UpdateRunning = true;
            FetchCRL (cToken);
        }

        void FinishUpdate ()
        {
            UpdateRunning = false;
        }

        void FetchCRL (CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }

            if (UrlIndex >= Urls.Count) {
                UrlIndex = 0;
                Retries++;
            }

            if (Retries < MaxRetries) {
                var request = new NcHttpRequest (HttpMethod.Get, Urls [UrlIndex]);
                NextUpdate = null;
                Log.Info (Log.LOG_SYS, "{0}: Updating crl url {1} (retry {2})", Name, UrlIndex, Retries);
                HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, cToken);
            } else {
                Log.Info (Log.LOG_SYS, "{0}: Could not fetch CRL. Stopping.", Name);
                var timer = new NcTimer (Name + "Timer", (state) => {
                    StartUpdate (cToken);
                }, null, RetryInterval, 0);
                cToken.Register (timer.Dispose);
                FinishUpdate ();
                Revoked = null;
            }
        }

        void DownloadSuccess (NcHttpResponse response, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            try {
                NcAssert.True (null != response, "response should not be null");
                if (HttpStatusCode.OK == response.StatusCode) {
                    NcAssert.True (null != response.Content, "content should not be null");
                    Log.Info (Log.LOG_PUSH, "{0}: CRL pull response: statusCode={1}", Name, response.StatusCode);
                    if (ExtractCrl (response.Content as FileStream)) {
                        if (token.IsCancellationRequested) {
                            return;
                        }
                        CrlGetRevoked ();
                        FinishUpdate ();
                    } else {
                        Log.Warn (Log.LOG_PUSH, "{0}: CRL failed to extract", Name);
                        DownloadError (null, token);
                    }
                } else {
                    Log.Warn (Log.LOG_PUSH, "{0}: CRL pull response: statusCode={1}", Name, response.StatusCode);
                    DownloadError (null, token);
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "{0}: Exception processing response: {1}", Name, ex);
                DownloadError (ex, token);
            }
        }

        void DownloadError (Exception ex, CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }
            try {
                if (ex != null) {
                    if (ex is OperationCanceledException) {
                        Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: cancelled", Name);
                    } else if (ex is WebException) {
                        var webex = ex as WebException;
                        Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: Caught network exception: {1} - {2}", Name, webex.Status, webex.Message);
                    } else {
                        Log.Warn (Log.LOG_PUSH, "{0}: CRL pull: Caught unexpected http exception - {1}", Name, ex);
                    }
                }
            } finally {
                UrlIndex++;
                FetchCRL (cToken); // try the next one
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
                Log.Info (Log.LOG_SYS, "{0}: Could not convert crl", Name);
                return false;
            }
            var now = DateTime.UtcNow;
            if (theCrl.NextUpdate.Value <= now) {
                // For dev, we hardly ever update the CRL, so just set it to once a day
                if (failOnExpired && !BuildInfoHelper.IsDev) {
                    Log.Info (Log.LOG_SYS, "{0}: CRL is already expired: {1}", Name, theCrl.NextUpdate.Value);
                    return false;
                } else {
                    NextUpdate = DateTime.UtcNow.AddDays (1);
                }
            } else {
                NextUpdate = theCrl.NextUpdate.Value;
            }

            var crlIssuerDn = new X500DistinguishedName (theCrl.IssuerDN.GetDerEncoded ());
            X509Certificate2 signerCert = null;
            foreach (var cert in CrlSignerCerts) {
                if (crlIssuerDn.Name == cert.SubjectName.Name) {
                    signerCert = cert;
                    break;
                }
            }
            if (signerCert == null) {
                Log.Info (Log.LOG_SYS, "{0}: No CRL signer certificate found");
                return false;
            }
            try {
                var certStream = new MemoryStream (signerCert.Export (X509ContentType.Cert));
                X509CertificateParser x509Parser = new X509CertificateParser ();
                var cert = x509Parser.ReadCertificate (certStream);
                theCrl.Verify (cert.GetPublicKey ());
                Log.Info (Log.LOG_SYS, "{0}: CRL Validated Successfully", Name);
            } catch (CrlException ex) {
                Log.Error (Log.LOG_SYS, "CrlException/{0}: {1}", Name, ex.Message);
                return false;
            } catch (SignatureException ex) {
                Log.Error (Log.LOG_SYS, "SignatureException/{0}: {1}", Name, ex.Message);
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
            Log.Info (Log.LOG_SYS, "{0}: Extracted {1} Revoked certificates", Name, revokedSet.Count);
            Revoked = revokedSet;
        }

        /// <summary>
        /// Checks to see if this certificate is revoked. Both Issuer and SerialNumber must be checked.
        /// </summary>
        /// <returns><c>true</c> if this certificate is revoked; otherwise, <c>false</c>.</returns>
        /// <param name="cert">Cert.</param>
        public bool IsRevoked (X509Certificate2 cert)
        {
            if (null == Revoked) {
                // TODO Should we reject any certs if the CRL hasn't been fetched yet or could not be fetched/Updated?
                // It might be a vulnerability one way (attacker could prevent us from fetching it), and a DoS attack in the other way (.
                return true;
            }
            return cert.IssuerName.Name == Cert.SubjectName.Name && Revoked.Contains (cert.SerialNumber.ToUpperInvariant ());
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

