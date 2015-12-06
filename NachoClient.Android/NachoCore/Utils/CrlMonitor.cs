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

namespace NachoCore.Utils
{
    public class CrlMonitor
    {
        private const long PollingPeriod = 2 * 3600 * 1000;

        private const long RetryInterval = 15 * 1000;

        private static int NextId = 0;

        private const int DefaultTimeoutSecs = 10;

        // Keep track of all registered monitors
        private static Dictionary<string, CrlMonitor> Monitors = new Dictionary<string, CrlMonitor> ();

        // For synchronized access of monitors dictionary
        private static object LockObj = new object ();

        public int Id { get; protected set; }

        public string Url { get; protected set; }

        public String Crl { get; protected set; }

        public HashSet<string> Revoked { get; protected set; }

        public DateTime LastUpdated { get; protected set; }

        // I don't think the number of CRLs will be a large numbers. So, I am not using
        // NcTimerPool now. But if that ever becomes a problem, we can switch easily.
        public NcTimer Timer;

        public static void StartService ()
        {
            lock (LockObj) {
                foreach (var monitor in Monitors.Values) {
                    monitor.StartTimer ();
                }
            }
        }

        public static void StopService ()
        {
            lock (LockObj) {
                foreach (var monitor in Monitors.Values) {
                    monitor.StopTimer ();
                }
            }
        }

        public static bool Register (string url)
        {
            lock (LockObj) {
                if (Monitors.ContainsKey (url)) {
                    return false;
                }
                var monitor = new CrlMonitor (url);
                Monitors.Add (monitor.Url, monitor);
                return true;
            }
        }

        public static void Register (HashSet<string> distributionPoints)
        {
            if (null == distributionPoints) {
                return;
            }
            foreach (var dp in distributionPoints) {
                string url;
                if (!CrlDistributionPoint.IsHttp (dp, out url)) {
                    Log.Error (Log.LOG_PUSH, "Non-HTTP CRL distribution point - {0}", url);
                    continue;
                }
                Log.Info (Log.LOG_PUSH, "CRL Monitor: register {0}", url);
                CrlMonitor.Register (url);
            }
        }

        public static bool Deregister (string url)
        {
            lock (LockObj) {
                CrlMonitor monitor;
                if (!Monitors.TryGetValue (url, out monitor)) {
                    return false;
                }
                monitor.StopTimer ();
                var removed = Monitors.Remove (url);
                NcAssert.True (removed);
                return true;
            }
        }

        public static bool IsRevoked (string serialNumber)
        {
            lock (LockObj) {
                foreach (var monitor in Monitors.Values) {
                    if (monitor.Revoked.Contains (serialNumber)) {
                        return true;
                    }
                }
                return false;
            }
        }

        public CrlMonitor (string url)
        {
            Id = Interlocked.Increment (ref NextId);
            Url = url;
            Revoked = new HashSet<string> ();
        }

        private void StopTimer ()
        {
            if (null != Timer) {
                Timer.Dispose ();
                Timer = null;
            }
        }

        private void StartTimer ()
        {
            StopTimer ();
            var name = String.Format ("Crl[{0}]", Id);
            long sinceLast = (long)((DateTime.UtcNow - LastUpdated).TotalMilliseconds);
            long duration = (sinceLast >= PollingPeriod ? 0 : PollingPeriod - sinceLast);
            Timer = new NcTimer (name, (state) => {
                NcTask.Run (() => {
                    Download (NcTask.Cts.Token);
                }, (string)state);
            }, null, duration, PollingPeriod);
        }

        public virtual INcHttpClient HttpClient {
            get {
                return NcHttpClient.Instance;
            }
        }

        public void Download (CancellationToken cToken)
        {
            var request = new NcHttpRequest (HttpMethod.Get, Url);
            HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, cToken);
        }

        void DownloadSuccess (NcHttpResponse response, CancellationToken token)
        {
            NcAssert.True (null != response, "response should not be null");
            if (HttpStatusCode.OK == response.StatusCode) {
                NcAssert.True (null != response.Content, "content should not be null");
                LastUpdated = DateTime.UtcNow;
                // FIXME - Need a different signing scheme so we can present the signing cert to verify the CRL.
                Revoked = CrlGetRevoked (response.Content as FileStream);
                Log.Info (Log.LOG_PUSH, "CRL pull response: statusCode={0}, content={1}", response.StatusCode, Crl);
                return;
            } else {
                Log.Warn (Log.LOG_PUSH, "CRL pull response: statusCode={0}", response.StatusCode);
            }
        }

        void DownloadError (Exception ex, CancellationToken cToken)
        {
            if (ex is OperationCanceledException) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: canceled");
                if (cToken.IsCancellationRequested) {
                    return;
                }
            } else if (ex is WebException) {
                var webex = ex as WebException;
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught network exception: {0} - {1}", webex.Status, webex.Message);
            } else {
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught unexpected http exception - {0}", ex);
            }

            // Check that the timer has not been disposed because the client goes to background.
            if (null != Timer) {
                // Something went wrong and we cannot get a new CRL. Poll again at a shorter interval.
                Timer.Change (RetryInterval, PollingPeriod);
            }
        }

        #region CRLParsing

        public class InvalidCrl : Exception
        {
            public InvalidCrl (string message) : base(message)
            {}
        }

        public static HashSet<string> CrlGetRevoked (string crl, string signingCert)
        {
            var crlStream = new MemoryStream (Encoding.ASCII.GetBytes (crl));
            var certStream = new MemoryStream (Encoding.ASCII.GetBytes (signingCert));
            return CrlGetRevoked (crlStream, certStream);
        }

        public static HashSet<string> CrlGetRevoked (Stream crl, Stream signingCert)
        {
            var sigCert = ParseX509Pem (signingCert);
            var theCrl = ParseX509CrlPem (crl);
            return CrlGetRevoked (theCrl, sigCert);
        }

        public static HashSet<string> CrlGetRevoked (string crl)
        {
            var crlStream = new MemoryStream (Encoding.ASCII.GetBytes (crl));
            return CrlGetRevoked (crlStream);
        }

        public static HashSet<string> CrlGetRevoked (Stream crl)
        {
            var theCrl = ParseX509CrlPem (crl);
            return CrlGetRevoked (theCrl, null);
        }

        protected static HashSet<string> CrlGetRevoked (X509Crl crl, X509Certificate signingCert)
        {
            if (signingCert != null) {
                try {
                    crl.Verify (signingCert.GetPublicKey ());
                } catch (CrlException ex) {
                    throw new InvalidCrl (ex.ToString ());
                } catch (SignatureException ex) {
                    throw new InvalidCrl (ex.ToString ());
                }
            }
            var ret = new HashSet<string> ();
            var revokedList = crl.GetRevokedCertificates ();
            if (revokedList != null) {
                foreach (X509CrlEntry revoked in revokedList) {
                    ret.Add (revoked.SerialNumber.LongValue.ToString ("X"));
                }
            }
            return ret;
        }

        static X509Certificate ParseX509Pem (Stream certStream)
        {
            X509CertificateParser x509Parser = new X509CertificateParser ();
            var theCert = x509Parser.ReadCertificate (certStream);
            if (theCert == null) {
                throw new ArgumentException ("Could not convert signingCert");
            }
            return theCert;
        }

        static X509Crl ParseX509CrlPem (Stream crlStream)
        {
            X509CrlParser parser = new X509CrlParser (true);
            var theCrl = parser.ReadCrl (crlStream);
            if (theCrl == null) {
                throw new ArgumentException ("Could not convert crl");
            }
            return theCrl;
        }

        string ConvertCrl (byte[] crlBytes)
        {
            string crl;
            try {
                crl = Encoding.ASCII.GetString (crlBytes);
                // This looks like an ASCII string. Does it look like a PEM object? (Is this simple way sufficient?)
                if (!crl.StartsWith ("-----BEGIN")) {
                    crl = null;
                }
            } catch (ArgumentException) {
                // Will end up here if the CRL is in DER format and has non-ASCII characters
                crl = null;
            }
            if (null == Crl) {
                // Looks like we get DER CRL. Convert it to PEM as our OpenSSL binding requires PEM objects.
                crl = "-----BEGIN X509 CRL-----\n" + Convert.ToBase64String (crlBytes, Base64FormattingOptions.InsertLineBreaks) + "\n-----END X509 CRL-----\n";
            }
            return crl;
        }
        #endregion
    }
}

