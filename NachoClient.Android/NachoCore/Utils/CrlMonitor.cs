//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Http;
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
                if (HttpStatusCode.OK == response.StatusCode) {
                    Crl = await response.Content.ReadAsStringAsync ().ConfigureAwait (false);
                    LastUpdated = DateTime.UtcNow;
                    var revoked = new HashSet<string> ();
                    var snList = NachoPlatformBinding.Crypto.CrlGetRevoked (Crl, null);
                    foreach (var sn in snList) {
                        revoked.Add (sn);
                    }
                    Revoked = revoked;
                    Log.Info (Log.LOG_PUSH, "CRL pull response: statusCode={0}, content={1}", response.StatusCode, Crl);
                    return;
                } else {
                    Log.Warn (Log.LOG_PUSH, "CRL pull response: statusCode={0}", response.StatusCode);
                }
            } catch (OperationCanceledException) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: canceled");
                throw;
            } catch (WebException e) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught network exception - {0}", e);
            } catch (Exception e) {
                Log.Warn (Log.LOG_PUSH, "CRL pull: Caught unexpected http exception - {0}", e);
            }
            // Something went wrong and we cannot get a new CRL. Poll again at a shorter interval.
            Timer.Change (RetryInterval, PollingPeriod);
        }
    }
}

