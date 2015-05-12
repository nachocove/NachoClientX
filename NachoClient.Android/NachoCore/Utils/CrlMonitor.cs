//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Text;
using ModernHttpClient;

namespace NachoCore.Utils
{
    public class CrlMonitor
    {
        private static Type HttpClientType = typeof(MockableHttpClient);

        private const long PollingPeriod = 2 * 3600 * 1000;

        private const long RetryInterval = 15 * 1000;

        private static int NextId = 0;

        // Keep track of all registered monitors
        private static Dictionary<string, CrlMonitor> Monitors = new Dictionary<string, CrlMonitor> ();

        // For synchronized access of monitors dictionary
        private static object LockObj = new object ();

        public int Id { get; protected set; }

        public string Url { get; protected set; }

        public String Crl { get; protected set; }

        public HashSet<string> Revoked { get; protected set; }

        public DateTime LastUpdated { get; protected set; }

        private IHttpClient Client;

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
            var handler = new NativeMessageHandler (false, true);
            Client = (IHttpClient)Activator.CreateInstance (HttpClientType, handler, true);
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

        public async void Download (CancellationToken cToken)
        {
            var request = new HttpRequestMessage (HttpMethod.Get, Url);
            try {
                var response = await Client
                    .SendAsync (request, HttpCompletionOption.ResponseContentRead, cToken)
                    .ConfigureAwait (false);
                NcAssert.True (null != response, "response should not be null");
                if (HttpStatusCode.OK == response.StatusCode) {
                    NcAssert.True (null != response.Content, "content should not be null");
                    var crlBytes = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
                    try {
                        Crl = Encoding.ASCII.GetString (crlBytes);
                        // This looks like an ASCII string. Does it look like a PEM object? (Is this simple way sufficient?)
                        if (!Crl.StartsWith ("-----BEGIN")) {
                            Crl = null;
                        }
                    } catch (ArgumentException) {
                        // Will end up here if the CRL is in DER format and has non-ASCII characters
                        Crl = null;
                    }
                    if (null == Crl) {
                        // Looks like we get DER CRL. Convert it to PEM as our OpenSSL binding requires PEM objects.
                        Crl = "-----BEGIN X509 CRL-----\n" + Convert.ToBase64String (crlBytes, Base64FormattingOptions.InsertLineBreaks) + "\n-----END X509 CRL-----\n";
                    }

                    LastUpdated = DateTime.UtcNow;
                    var revoked = new HashSet<string> ();
                    // FIXME - Need a different signing scheme so we can present the signing cert to verify the CRL.
                    var snList = NachoPlatformBinding.Crypto.CrlGetRevoked (Crl);
                    if (null != snList) {
                        foreach (var sn in snList) {
                            revoked.Add (sn);
                        }
                        Revoked = revoked;
                    } else {
                        Log.Error (Log.LOG_PUSH, "Unable to parse CRL for {0}", Url);
                    }
                    Log.Info (Log.LOG_PUSH, "CRL pull response: statusCode={0}, content={1}", response.StatusCode, Crl);
                    return;
                } else {
                    Log.Warn (Log.LOG_PUSH, "CRL pull response: statusCode={0}", response.StatusCode);
                }
            } catch (OperationCanceledException) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: canceled");
                if (cToken.IsCancellationRequested) {
                    throw;
                }
            } catch (WebException e) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught network exception - {0}", e);
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught unexpected http exception - {0}", e);
            }

            // Check that the timer has not been disposed because the client goes to background.
            if (null != Timer) {
                // Something went wrong and we cannot get a new CRL. Poll again at a shorter interval.
                Timer.Change (RetryInterval, PollingPeriod);
            }
        }
    }
}

