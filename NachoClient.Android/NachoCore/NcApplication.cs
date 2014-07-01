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
    public sealed class NcApplication : IBackEndOwner
    {
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

        private NcApplication ()
        {
            // THIS IS THE INIT SEQUENCE FOR THE NON-UI ASPECTS OF THE APP ON ALL PLATFORMS.
            // IF YOUR INIT TAKES SIGNIFICANT TIME, YOU NEED TO HAVE A NcTask.Run() IN YOUR INIT
            // THAT DOES THE LONG DURATION STUFF ON A BACKGROUND THREAD. THIS METHOD IS CALLED
            // VIA THE UI THREAD ON STARTUP. ORDER MATTERS - KNOW BEFORE YOU MODIFY!
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
                        // FIXME XAMMIT. Known bug, AsHttpOperation will time-out and retry. No need to crash.
                        return true;
                    }

                    if (ex is System.Net.WebException && message.Contains ("EndGetResponse")) {
                        Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: WebException with EndGetResponse");
                        // FIXME XAMMIT. Known bug, AsHttpOperation will time-out and retry. No need to crash.
                        return true;
                    }
                    return false;
                });
            };
            NcTask.StartService ();
            NcModel.Instance.Nop ();
            AsXmlFilterSet.Initialize ();
            BackEnd.Instance.Owner = this;
            UiThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        private static volatile NcApplication instance;
        private static object syncRoot = new Object ();
        private NcTimer MonitorTimer;

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

        public void Nop ()
        {
        }

        public void Start ()
        {
            // THIS IS THE BEST PLACE TO PUT Start FUNCTIONS - WHEN SERVICE NEEDS TO BE TURNED ON AFTER INIT.
            MonitorStart ();
            Log.Info (Log.LOG_LIFECYCLE, "MonitorStarted");
            NcModel.Instance.Info ();
            NcModel.Instance.EngageRateLimiter ();
            Log.Info (Log.LOG_LIFECYCLE, "NcModel.Instance Started");
            BackEnd.Instance.Start ();
            Log.Info (Log.LOG_LIFECYCLE, "BackEnd.Instance.Started");
            NcContactGleaner.Start ();
            Log.Info (Log.LOG_LIFECYCLE, "NcContactGleaner.Started");
            NcCapture.ResumeAll ();
            Log.Info (Log.LOG_LIFECYCLE, "NcCapture.ResumeAll'd");
            NcTimeVariance.ResumeAll ();
            Log.Info (Log.LOG_LIFECYCLE, "NcTimeVariance.ResumeAll'd");
        }

        public void Stop ()
        {
            // THIS IS THE BEST PLADE TO PUT Stop FUNCTIONS - WHEN SERVICE NEEDS TO BE SHUTDOWN BEFORE SLEEP/EXIT.
            MonitorStop ();
            BackEnd.Instance.Stop ();
            NcContactGleaner.Stop ();
            NcCapture.PauseAll ();
            NcTimeVariance.PauseAll ();
            // NcTask/Timer.StopService () should go last.
            NcTimer.StopService ();
            NcTask.StopService ();
        }

        public void MonitorStart ()
        {
            MonitorTimer = new NcTimer ("NcApplication:Monitor", (state) => {
                MonitorReport ();
            }, null, TimeSpan.Zero, new TimeSpan (0, 0, 60));
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
        }

        public void QuickCheck (uint seconds)
        {
            // Typically called after Stop(). Cause accounts to do a quick check for new messages.
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

