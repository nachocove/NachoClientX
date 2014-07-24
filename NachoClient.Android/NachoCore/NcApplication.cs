//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Wbxml;

using System.Security.Cryptography.X509Certificates;

namespace NachoCore
{
    // THIS IS THE INIT SEQUENCE FOR THE NON-UI ASPECTS OF THE APP ON ALL PLATFORMS.
    // IF YOUR INIT TAKES SIGNIFICANT TIME, YOU NEED TO HAVE A NcTask.Run() IN YOUR INIT
    // THAT DOES THE LONG DURATION STUFF ON A BACKGROUND THREAD. THIS METHOD IS CALLED
    // VIA THE UI THREAD ON STARTUP. ORDER MATTERS - KNOW BEFORE YOU MODIFY!

    public sealed class NcApplication : IBackEndOwner
    {
        private const int KClass4EarlyShowSeconds = 5;
        private const int KClass4LateShowSeconds = 15;

        public delegate void CredReqCallbackDele (int accountId);

        /// <summary>
        /// CredRequest: When called, the callee must gather the credential for the specified 
        /// account and add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.
        /// </summary>
        public CredReqCallbackDele CredReqCallback { set; get; }

        public delegate void ServConfReqCallbackDele (int accountId);

        /// <summary>
        /// ServConfRequest: When called the callee must gather the server information for the 
        /// specified account and nd add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.                
        /// </summary>
        public ServConfReqCallbackDele ServConfReqCallback { set; get; }

        public delegate void CertAskReqCallbackDele (int accountId, X509Certificate2 certificate);

        /// <summary>
        /// CertAskReq: When called the callee must ask the user whether the passed server cert can
        /// be trusted for the specified account. 
        /// </summary>
        public CertAskReqCallbackDele CertAskReqCallback { set; get; }

        public delegate void SearchContactsRespCallbackDele (int accountId, string prefix, string token);

        public SearchContactsRespCallbackDele SearchContactsRespCallback { set; get; }

        public int UiThreadId { get; set; }
        // event can be used to register for status indications.
        public event EventHandler StatusIndEvent;
        // when true, everything in the background needs to chill.
        public bool IsBackgroundAbateRequired { get; set; }

        private NcApplication ()
        {
            ThreadPool.SetMinThreads (8, 6);
            TaskScheduler.UnobservedTaskException += (object sender, UnobservedTaskExceptionEventArgs eargs) => {
                NcAssert.True (eargs.Exception is AggregateException, "AggregateException check");
                var aex = (AggregateException)eargs.Exception;
                aex.Handle ((ex) => {
                    var message = ex.ToString ();
                    Log.Error (Log.LOG_SYS, "UnobservedTaskException: {0}", message);
                    var faulted = NcTask.FindFaulted ();
                    foreach (var name in faulted) {
                        Log.Error (Log.LOG_SYS, "Faulted task: {0}", name);
                    }
                    if (ex is InvalidCastException &&
                        (message.Contains ("WriteRequestAsyncCB") || message.Contains ("GetResponseAsyncCB"))) {
                        Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: InvalidCastException with WriteRequestAsyncCB/GetResponseAsyncCB");
                        // XAMMIT. Known bug, AsHttpOperation will time-out and retry. No need to crash.
                        return true;
                    }

                    if (ex is System.Net.WebException && message.Contains ("EndGetResponse")) {
                        Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: WebException with EndGetResponse");
                        // XAMMIT. Known bug, AsHttpOperation will time-out and retry. No need to crash.
                        return true;
                    }
                    if (ex is System.IO.IOException && message.Contains ("Tls.RecordProtocol.BeginSendRecord")) {
                        Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: IOException with Tls.RecordProtocol.BeginSendRecord");
                        // XAMMIT. Known bug. AsHttpOperation will time-out and retry. No need to crash.
                        return true;
                    }
                    if (ex is System.OperationCanceledException && message.Contains ("Telemetry.Process")) {
                        Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: OperationCanceledException with Telemetry.Process");
                        // XAMMIT. Cancel exception should be caught by system when c-token is the Task's c-token.
                        return true;
                    }
                    return false;
                });
            };
            UiThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            StatusIndEvent += (object sender, EventArgs ea) => {
                var siea = (StatusIndEventArgs)ea;
                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStarted) {
                    IsBackgroundAbateRequired = true;
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStopped) {
                    IsBackgroundAbateRequired = false;
                }
            };
        }

        private static volatile NcApplication instance;
        private static object syncRoot = new Object ();
        private NcTimer MonitorTimer;
        private NcTimer Class4EarlyShowTimer;
        private NcTimer Class4LateShowTimer;

        public event EventHandler Class4LateShowEvent;

        public static NcApplication Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcApplication ();
                        }
                    }
                }
                return instance; 
            }
        }

        public void StartClass1Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass1Services called.");
            NcTask.StartService ();
            NcModel.Instance.Nop ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass1Services exited.");
        }

        public void StopClass1Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass1Services called.");
            NcTimer.StopService ();
            NcTask.StopService ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass1Services exited.");
        }

        public void StartClass2Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass2Services called.");
            NcModel.Instance.EngageRateLimiter ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass2Services exited.");
        }

        public void StopClass2Services ()
        {
            // Empty so far.
            // No need to turn off the model rate limiter.
        }

        public void StartClass3Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass3Services called.");
            BackEnd.Instance.Owner = this;
            BackEnd.Instance.EstablishService ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass3Services exited.");
        }

        public void StopClass3Services ()
        {
            // Empty so far. 
            // No harm in leaving inactive BackEnd data structures intact.
        }

        // ALL CLASS-4 STARTS ARE DEFERRED BASED ON TIME.
        public void StartClass4Services ()
        {
            MonitorStart (); // Has a deferred timer start inside.
            Class4EarlyShowTimer = new NcTimer ("NcApplication:Class4EarlyShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer called.");
                AsXmlFilterSet.Initialize ();
                BackEnd.Instance.Start ();
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer exited.");
            }, null, new TimeSpan (0, 0, KClass4EarlyShowSeconds), TimeSpan.Zero);
            Class4LateShowTimer = new NcTimer ("NcApplication:Class4LateShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer called.");
                NcModel.Instance.Info ();
                NcContactGleaner.Start ();
                NcCapture.ResumeAll ();
                NcTimeVariance.ResumeAll ();
                if (null != Class4LateShowEvent) {
                    Class4LateShowEvent (this, EventArgs.Empty);
                }
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer exited.");
            }, null, new TimeSpan (0, 0, KClass4LateShowSeconds), TimeSpan.Zero);
        }

        public void StopClass4Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass4Services called.");
            MonitorStop ();
            if (Class4EarlyShowTimer.DisposeAndCheckHasFired ()) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer.DisposeAndCheckHasFired.");
                BackEnd.Instance.Stop ();
            }

            if (Class4LateShowTimer.DisposeAndCheckHasFired ()) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer.DisposeAndCheckHasFired.");
                NcContactGleaner.Stop ();
                NcCapture.PauseAll ();
                NcTimeVariance.PauseAll ();
            }
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass4Services exited.");
        }

        public void MonitorStart ()
        {
            MonitorTimer = new NcTimer ("NcApplication:Monitor", (state) => {
                MonitorReport ();
            }, null, new TimeSpan (0, 0, 30), new TimeSpan (0, 0, 60));
            MonitorTimer.Stfu = true;
        }

        public void MonitorStop ()
        {
            MonitorTimer.Dispose ();
        }

        public void MonitorReport ()
        {
            Log.Info (Log.LOG_SYS, "Monitor: Total Memory {0} MB", GC.GetTotalMemory (true) / (1024 * 1024));
            int workerThreads, completionPortThreads;
            ThreadPool.GetMinThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Min Threads {0}/{1}", workerThreads, completionPortThreads);
            ThreadPool.GetMaxThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Max Threads {0}/{1}", workerThreads, completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Comm Status {0}, Speed {1}", 
                NcCommStatus.Instance.Status, NcCommStatus.Instance.Speed);
        }

        public void QuickCheck (uint seconds)
        {
            // Needs Class-3 Services up. Cause accounts to do a quick check for new messages.
            // If start is called while wating for the QuickCheck, the system keeps going after the QuickCheck completes.
            BackEnd.Instance.QuickCheck (seconds);
        }
        // method can be used to post to StatusIndEvent from outside NcApplication.
        public void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            if (null != StatusIndEvent) {
                StatusIndEvent.Invoke (this, e);
            }
        }
        // IBackEndOwner methods below.
        public void CredReq (int accountId)
        {
            if (null != CredReqCallback) {
                CredReqCallback (accountId);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication CredReqCallback.");
            }
        }

        public void ServConfReq (int accountId)
        {
            if (null != ServConfReqCallback) {
                ServConfReqCallback (accountId);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication ServConfReqCallback.");
            }
        }

        public void CertAskReq (int accountId, X509Certificate2 certificate)
        {
            if (null != CertAskReqCallback) {
                CertAskReqCallback (accountId, certificate);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication CertAskReqCallback.");
            }
        }

        public void SearchContactsResp (int accountId, string prefix, string token)
        {
            if (null != SearchContactsRespCallback) {
                SearchContactsRespCallback (accountId, prefix, token);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication SearchContactsRespCallback.");
            }
        }

        public void CertAskResp (int accountId, bool isOkay)
        {
            BackEnd.Instance.CertAskResp (accountId, isOkay);
        }


    }
}

