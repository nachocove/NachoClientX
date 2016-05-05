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
using System.Linq;

namespace NachoCore.Utils
{
    /// <summary>
    /// The CRL monitor. Uses an Instance pattern.
    /// </summary>
    public class CrlMonitor
    {
        /// <summary>
        /// Keep track of all registered monitors. The key is the canonical SubjectName of the Certificate.
        /// The value is a CrlMonitorItem.
        /// </summary>
        protected Dictionary<string, CrlMonitorItem> Monitors = new Dictionary<string, CrlMonitorItem> ();

        protected int NextId = 0;

        /// <summary>
        /// For synchronized access of monitors dictionary
        /// </summary>
        private static object LockObj = new object ();

        protected CancellationTokenSource Cts;

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

        /// <summary>
        /// The monitor timer, which is set to the smallest expiration date across all CrlMonitorItem's.
        /// Polling kills battery life, and we KNOW when CRL's expire. A CA doesn't issue a new CRL until
        /// the NextUpdate time expires. So there's no need to constantly poll, unless a CRL has expired(*).
        /// 
        /// (*)This is one of the downsides of CRL's. If a more immediate validation is required, OCSP must be used.
        /// </summary>
        /// <value>The monitor timer.</value>
        public NcTimer MonitorTimer { get; set; }

        /// <summary>
        /// Path where we cache CRL's (again, since a CRL won't be reissued until it expires, it's safe to cache
        /// the downloaded CRL's).
        /// </summary>
        /// <value>The crl document path.</value>
        public static string CrlDocumentPath { get; protected set; }

        private CrlMonitor ()
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            CrlDocumentPath = Path.Combine (documentsPath, "crl");
            Directory.CreateDirectory (CrlDocumentPath); // checks first, so no need to check here.

            // find all the cached CRL's, and delete the CRL if it is expired already.
            foreach (var crl in Directory.EnumerateFiles (CrlDocumentPath)) {
                NcCrl theCrl;
                bool deleteMe = false;
                try {
                    theCrl = new NcCrl (new FileStream (crl, FileMode.Open, FileAccess.Read, FileShare.Read));
                    if (theCrl.IsExpired ()) {
                        deleteMe = true;
                    }
                } catch (ArgumentException ex) {
                    // it's a badly formatted CRL apparently. Delete it and let upper layers register a new one.
                    Log.Info (Log.LOG_SYS, "{0}: Could not convert crl: {1}", crl, ex);
                    deleteMe = true;
                }
                if (deleteMe) {
                    File.Delete (crl);
                }
            }
        }

        public void StartService ()
        {
            lock (LockObj) {
                StopService ();
                Cts = new CancellationTokenSource ();
                SetMonitorTimer ();
            }
        }

        public void StopService ()
        {
            lock (LockObj) {
                if (Cts != null) {
                    Cts.Cancel ();
                    Cts = null;
                }
                StopTimer ();
            }
        }

        /// <summary>
        /// Register a list of certificates with the CRL Monitoring service.
        /// NOTE: It is the caller's responsibility to validate all certs passed in.
        /// </summary>
        /// <param name="signerCerts">Signer certs.</param>
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

        void StopTimer ()
        {
            if (MonitorTimer != null) {
                MonitorTimer.Dispose ();
                MonitorTimer = null;
            }
        }

        /// <summary>
        /// Find the soonest expiry time over all CrlMonitorItem's and set the timer that that.
        /// </summary>
        public void SetMonitorTimer ()
        {
            lock (LockObj) {
                StopTimer ();
                try {
                    var soonestExpiry = SoonestCrlExpiry ();
                    if (soonestExpiry == DateTime.MaxValue) {
                        // no new expiry time, so default to 1 day.
                        soonestExpiry = DateTime.UtcNow.AddDays (1);
                    }
                    if (soonestExpiry < DateTime.UtcNow) {
                        StartUpdates ();
                        return;
                    }
                    var dueIn = soonestExpiry - DateTime.UtcNow;
                    MonitorTimer = new NcTimer ("CrlMonitorTimer", (state) => {
                        StartUpdates ();
                    }, null, dueIn, TimeSpan.Zero);
                } catch (CrlMonitorNoItems) {
                    return;
                }
            }
        }

        class CrlMonitorNoItems : ArgumentOutOfRangeException
        {
        }

        /// <summary>
        /// Finds the soonest any of the CRL's expires.
        /// </summary>
        /// <exception cref="CrlMonitorNoItems">Throws CrlMonitorNoItems if no CRL's are currently registered.</exception>
        /// <returns>The crl expiry.</returns>
        DateTime SoonestCrlExpiry ()
        {
            if (!Monitors.Any ()) {
                throw new CrlMonitorNoItems ();
            }
            DateTime soonest = DateTime.MaxValue;
            foreach (var item in Monitors) {
                var crlNextupdate = item.Value.CrlNextUpdate ();
                if (crlNextupdate < soonest) {
                    soonest = crlNextupdate;
                }
            }
            return soonest;
        }

        void StartUpdates ()
        {
            lock (LockObj) {
                foreach (var monitor in Monitors.Values) {
                    if (monitor.NeedsUpdate ()) {
                        monitor.StartUpdate (Cts.Token); // spawns a task
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the certificate is revoked. Uses the cert's issuer-name to find the relevant
        /// CRL (or rather CrlMonitorItem), and looks to see if the cert is revoked within that item.
        /// </summary>
        /// <returns><c>true</c> if this cert is revoked; otherwise, <c>false</c>.</returns>
        /// <param name="cert">Cert.</param>
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
    /// Crl monitor item. This represents one CA Certificate, and will monitor the CDP Url's contained therein, 
    /// fetch the CRL, validate it, and extract the list of revoked certificates.
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
    ///   Again the RFC's don't really say. We will treat them as the same CRL, because that's the most common use-case
    ///   out there.
    /// </summary>
    public class CrlMonitorItem
    {
        public string Name { get; set; }

        const long RetryInterval = 15 * 1000;

        const int DefaultTimeoutSecs = 10;

        List<string> Urls { get; set; }

        int UrlIndex { get; set; }

        DateTime? NextUpdate { get; set; }

        NcCrl Crl { get; set; }

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
            cToken.Register (() => {
                UpdateRunning = false;
            });
            Retries = 0;
            UrlIndex = 0;
            UpdateRunning = true;
            FetchCRL (cToken);
        }

        void FinishUpdate ()
        {
            CrlMonitor.Instance.SetMonitorTimer ();
            UpdateRunning = false;
        }

        string _crlPath;

        string crlPath {
            get {
                if (string.IsNullOrEmpty (_crlPath)) {
                    _crlPath = Path.Combine (CrlMonitor.CrlDocumentPath, HashHelper.Sha256 (Cert.SubjectName.Name));
                }
                return _crlPath;
            }
        }

        /// <summary>
        /// Fetchs the CRL. Given a list of URL's in a certificate, try them all in turn until one works.
        /// If we've tried them all MaxRetries times, give up and fail. Remember to check the cached CRL first.
        /// </summary>
        /// <param name="cToken">Cancellation token.</param>
        void FetchCRL (CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }

            // do we have another URL to check? If not, increase retries, and start with the first URL again.
            if (UrlIndex >= Urls.Count) {
                UrlIndex = 0;
                Retries++;
            }

            if (Retries < MaxRetries) {
                NextUpdate = null;
                // there may be a cached file from the last time we fetched one. Use it as if this was the download
                if (File.Exists (crlPath)) {
                    NcTask.Run (() => {
                        var fs = new FileStream (crlPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (!ProcessCrl (fs, cToken)) {
                            File.Delete (crlPath);
                            DownloadCrl (cToken);
                        } else {
                            FinishUpdate ();
                        }
                    }, "ProcessCrl");
                } else {
                    // there was no file. Just do a download.
                    DownloadCrl (cToken);
                }
            } else {
                Log.Info (Log.LOG_SYS, "{0}: Could not fetch CRL. Stopping.", Name);
                var timer = new NcTimer (Name + "Timer", (state) => StartUpdate (cToken), null, RetryInterval, 0);
                cToken.Register (timer.Dispose);
                FinishUpdate ();
                Revoked = null;
            }
        }

        /// <summary>
        /// Starts a download request to fetch the CRL. On success, the request calls DownloadSuccess, otherwise DownloadError.
        /// </summary>
        /// <param name="cToken">Cancellation token.</param>
        void DownloadCrl (CancellationToken cToken)
        {
            var request = new NcHttpRequest (HttpMethod.Get, Urls [UrlIndex]);
            Log.Info (Log.LOG_SYS, "{0}: Updating crl url {1}:{2} (retry {3})", Name, UrlIndex, Urls [UrlIndex], Retries);
            HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, cToken);
        }

        /// <summary>
        /// Called if the request was successfull (note the response code may still not be a 200).
        /// First, write the downloaded data to the Cache, then process and check it.
        /// </summary>
        /// <param name="response">Response.</param>
        /// <param name="token">Token.</param>
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

                    if (File.Exists (crlPath)) {
                        File.Delete (crlPath);
                    }

                    // copy the downloaded data to cache crlPath
                    using (var fs = new FileStream (crlPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                        response.Content.CopyTo (fs);
                    }

                    // Process the CRL, checking its validity period as well as checking the signature.
                    bool processSuccess;
                    using (var fs = new FileStream (crlPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        processSuccess = ProcessCrl (fs, token);
                    }
                    if (!processSuccess) {
                        File.Delete (crlPath);
                        Log.Warn (Log.LOG_PUSH, "{0}: CRL failed to process crl", Name);
                        DownloadError (null, token);
                    } else {
                        FinishUpdate ();
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

        /// <summary>
        /// Connection failed. Log the errors and start a new request
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="cToken">token.</param>
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
        /// Process the CRL.
        /// </summary>
        /// <returns><c>true</c>, if crl was processed, <c>false</c> otherwise.</returns>
        /// <param name="crl">Crl.</param>
        /// <param name="cToken">token.</param>
        protected bool ProcessCrl (Stream crl, CancellationToken cToken)
        {
            if (ExtractCrl (crl)) {
                if (cToken.IsCancellationRequested) {
                    return false;
                }
                CrlGetRevoked ();
                return true;
            }

            Log.Warn (Log.LOG_PUSH, "{0}: CRL failed to extract", Name);
            return false;
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
            NcCrl theCrl;
            try {
                theCrl = new NcCrl (crl);
            } catch (ArgumentException ex) {
                Log.Info (Log.LOG_SYS, "{0}: Could not convert crl: {1}", Name, ex);
                return false;
            }
            if (theCrl.IsExpired ()) {
                // For dev, we hardly ever update the CRL, so just set it to once a day
                if (failOnExpired && !BuildInfoHelper.IsDev) {
                    Log.Info (Log.LOG_SYS, "{0}: CRL is already expired: {1}", Name, theCrl.NextUpdate);
                    return false;
                } else {
                    NextUpdate = DateTime.UtcNow.AddDays (1);
                }
            } else {
                NextUpdate = theCrl.NextUpdate;
            }

            var crlIssuerDn = new X500DistinguishedName (theCrl.IssuerDnDer);
            X509Certificate2 signerCert = null;
            // find the signer certificate. It'll have a subject-name that matches the CRl IssuerDn,
            // which we decoded above.
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

            // verify it.
            string errorMsg;
            if (!theCrl.Verify (signerCert, out errorMsg)) {
                Log.Error (Log.LOG_SYS, "Could not validate crl {0}: {1}", Name, errorMsg);
                return false;
            }
            Log.Info (Log.LOG_SYS, "{0}: CRL Validated Successfully", Name);
            Crl = theCrl;
            return true;
        }

        /// <summary>
        /// Copy the revoked certificate information into Revoked for later use.
        /// </summary>
        protected void CrlGetRevoked ()
        {
            NcAssert.NotNull (Crl);
            Revoked = Crl.ExpiredCerts;
            Log.Info (Log.LOG_SYS, "{0}: Extracted {1} Revoked certificates", Name, Revoked.Count);
        }

        /// <summary>
        /// Checks to see if this certificate is revoked. Issuer MUST match the subject-name of the issuing certificate,
        /// and SerialNumber must be checked against the list of revoked certs.
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

        public DateTime CrlNextUpdate ()
        {
            if (null != Crl) {
                return Crl.NextUpdate;
            }
            return DateTime.MaxValue;
        }
    }

    /// <summary>
    /// An abstraction so we can replace the CRL decoding functions as we need to (i.e. from BC to Openssl, for example)
    /// </summary>
    public class NcCrl
    {
        protected object LockObj = new object ();

        /// <summary>
        /// The internal copy of the CRL. In this case, this is a BouncyCastle structure, but could be anything.
        /// </summary>
        protected X509Crl Crl;

        /// <summary>
        /// The expired certs, indexed by serialnumber (as a string).
        /// </summary>
        protected HashSet<string> _ExpiredCerts;

        public NcCrl (Stream crlStream)
        {
            Crl = ParseCrl (crlStream);
            if (null == Crl) {
                throw new ArgumentException ("Could not parse CRL");
            }
        }

        /// <summary>
        /// Gets the expired cert information in a HashSet
        /// </summary>
        /// <value>The expired certs.</value>
        public HashSet<string> ExpiredCerts {
            get {
                if (null == _ExpiredCerts) {
                    lock (LockObj) {
                        if (null == _ExpiredCerts) {
                            _ExpiredCerts = new HashSet<string> ();
                            var revokedList = Crl.GetRevokedCertificates ();
                            if (revokedList != null) {
                                foreach (X509CrlEntry revoked in revokedList) {
                                    _ExpiredCerts.Add (revoked.SerialNumber.LongValue.ToString ("X"));
                                }
                            }
                        }
                    }
                }
                return _ExpiredCerts;
            }
        }

        /// <summary>
        /// Is this CRL expired?
        /// </summary>
        /// <returns><c>true</c> if this instance is expired; otherwise, <c>false</c>.</returns>
        public bool IsExpired ()
        {
            return IsExpired (Crl);
        }

        /// <summary>
        /// Expiration time and date of this CRL.
        /// </summary>
        /// <value>The next update as a DateTime object.</value>
        public DateTime NextUpdate {
            get {
                return Crl.NextUpdate != null ? Crl.NextUpdate.Value : DateTime.MaxValue;
            }
        }

        /// <summary>
        /// The issuer x.500 DN in DER format byte array.
        /// </summary>
        public byte[] IssuerDnDer {
            get {
                return Crl.IssuerDN.GetDerEncoded ();
            }
        }

        /// <summary>
        /// Verify the CRL using the specified cert and return an error message, if needed.
        /// </summary>
        /// <param name="cert">Cert.</param>
        /// <param name="errorMsg">Error message.</param>
        /// <returns>true if the crl validated, false otherwise.</returns>
        public bool Verify (X509Certificate2 cert, out string errorMsg)
        {
            errorMsg = null;
            var certStream = new MemoryStream (cert.Export (X509ContentType.Cert));
            X509CertificateParser x509Parser = new X509CertificateParser ();
            var bccert = x509Parser.ReadCertificate (certStream);
            try {
                Crl.Verify (bccert.GetPublicKey ());
                return true;
            } catch (CrlException ex) {
                errorMsg = ex.Message;
                return false;
            } catch (SignatureException ex) {
                errorMsg = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Parses the crl from a stream
        /// </summary>
        /// <returns>The crl.</returns>
        /// <param name="stream">Stream.</param>
        public static X509Crl ParseCrl (Stream stream)
        {
            X509CrlParser parser = new X509CrlParser (true);
            var theCrl = parser.ReadCrl (stream);
            return theCrl;
        }

        /// <summary>
        /// is the CRL expired? This is a static function.
        /// </summary>
        /// <returns><c>true</c> if is expired the specified crl; otherwise, <c>false</c>.</returns>
        /// <param name="crl">Crl.</param>
        public static bool IsExpired (X509Crl crl)
        {
            return crl.NextUpdate.Value <= DateTime.UtcNow;
        }
    }
}

