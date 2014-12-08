//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Wbxml;
using NachoClient.Build;
using NachoPlatform;
using NachoPlatformBinding;
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

        public enum ExecutionContextEnum
        {
            Foreground,
            Background,
            QuickSync,
        };

        private ExecutionContextEnum _ExecutionContext;

        public ExecutionContextEnum ExecutionContext {
            get { return _ExecutionContext; }
            set { 
                _ExecutionContext = value; 
                Log.Info (Log.LOG_LIFECYCLE, "ExecutionContext => {0}", value.ToString ());
                InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_ExecutionContextChanged),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }
        }

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

        public static event EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException;

        public int UiThreadId { get; set; }
        // event can be used to register for status indications.
        public event EventHandler StatusIndEvent;
        // when true, everything in the background needs to chill.
        public bool IsBackgroundAbateRequired { get; set; }

        public bool TestOnlyInvokeUseCurrentThread { get; set; }

        private bool IsXammit (Exception ex)
        {
            var message = ex.ToString ();
            if (ex is OperationCanceledException && message.Contains ("NcTask")) {
                Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: UnobservedTaskException from cancelled Task.");
                // XAMMIT. Known bug, Task should absorb OperationCanceledException when CancellationToken is bound to Task.
                return true;
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
            Log.Error (Log.LOG_SYS, "UnobservedTaskException: {0}", message);
            return false;
        }

        private NcApplication ()
        {
            ThreadPool.SetMinThreads (8, 6);
            TaskScheduler.UnobservedTaskException += (object sender, UnobservedTaskExceptionEventArgs eargs) => {
                NcAssert.True (eargs.Exception is AggregateException, "AggregateException check");
                var aex = (AggregateException)eargs.Exception;
                var faulted = NcTask.FindFaulted ();
                foreach (var name in faulted) {
                    Log.Error (Log.LOG_SYS, "Faulted task: {0}", name);
                }
                Exception nonXammit = null;
                foreach (var boom in aex.InnerExceptions) {
                    if (!IsXammit (boom)) {
                        nonXammit = boom;
                    }
                }
                if (null == nonXammit) {
                    aex.Handle ((ex) => true);
                } else {
                    if (null != UnobservedTaskException) {
                        UnobservedTaskException (sender, eargs);
                    } else {
                        aex.Handle ((ex) => false);
                    }
                }
            };
            UiThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            StatusIndEvent += (object sender, EventArgs ea) => {
                var siea = (StatusIndEventArgs)ea;

                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStarted) {
                    IsBackgroundAbateRequired = true;
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "NcApplication received Info_BackgroundAbateStarted {0} seconds", deliveryTime.ToString ());
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStopped) {
                    IsBackgroundAbateRequired = false;
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "NcApplication received Info_BackgroundAbateStopped {0} seconds", deliveryTime.ToString ());
                }
            };
        }

        private static volatile NcApplication instance;
        private static object syncRoot = new Object ();
        private NcTimer MonitorTimer;
        private NcTimer Class4EarlyShowTimer;
        private NcTimer Class4LateShowTimer;

        public event EventHandler Class4LateShowEvent;
        public event EventHandler MonitorEvent;

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
            NcModel.Instance.GarbageCollectFiles ();
            NcModel.Instance.Start ();
            EstablishService ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass1Services exited.");
        }

        public void StopClass1Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass1Services called.");
            NcModel.Instance.Stop ();
            NcTimer.StopService ();
            NcTask.StopService ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass1Services exited.");
        }

        public void StartClass2Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass2Services called.");
            NcModel.Instance.EngageRateLimiter ();
            if (ExecutionContextEnum.QuickSync != ExecutionContext) {
                NcBrain.StartService ();
                NcContactGleaner.Start ();
            }
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass2Services exited.");
        }

        public void StopClass2Services ()
        {
            // No need to turn off the model rate limiter.
            NcContactGleaner.Stop ();
            NcBrain.StopService ();
        }

        public void StartClass3Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass3Services called.");
            BackEnd.Instance.Owner = this;
            BackEnd.Instance.EstablishService ();
            BackEnd.Instance.Start ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass3Services exited.");
        }

        public void StopClass3Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass3Services called.");
            BackEnd.Instance.Stop ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass3Services exited.");
        }

        // ALL CLASS-4 STARTS ARE DEFERRED BASED ON TIME.
        public void StartClass4Services ()
        {
            ExecutionContext = ExecutionContextEnum.Foreground;
            MonitorStart (); // Has a deferred timer start inside.
            Log.Info (Log.LOG_LIFECYCLE, "{0} (build {1}) built at {2} by {3}",
                BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);
            Log.Info (Log.LOG_LIFECYCLE, "Device ID: {0}", Device.Instance.Identity ());
            if (0 < BuildInfo.Source.Length) {
                Log.Info (Log.LOG_LIFECYCLE, "Source Info: {0}", BuildInfo.Source);
            }
            Class4EarlyShowTimer = new NcTimer ("NcApplication:Class4EarlyShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer called.");
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer exited.");
            }, null, new TimeSpan (0, 0, KClass4EarlyShowSeconds), TimeSpan.Zero);
            Class4LateShowTimer = new NcTimer ("NcApplication:Class4LateShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer called.");
                NcModel.Instance.Info ();
                NcDeviceContacts.Run ();
                NcDeviceCalendars.Run ();
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
            ExecutionContext = ExecutionContextEnum.Background;
            MonitorStop ();
            if (Class4EarlyShowTimer.DisposeAndCheckHasFired ()) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer.DisposeAndCheckHasFired.");
            }

            if (Class4LateShowTimer.DisposeAndCheckHasFired ()) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer.DisposeAndCheckHasFired.");
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
            Log.Info (Log.LOG_SYS, "Monitor: Process Memory {0} MB", (int)PlatformProcess.GetUsedMemory () / (1024 * 1024));
            Log.Info (Log.LOG_SYS, "Monitor: GC Memory {0} MB", GC.GetTotalMemory (true) / (1024 * 1024));
            int workerThreads, completionPortThreads;
            ThreadPool.GetMinThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Min Threads {0}/{1}", workerThreads, completionPortThreads);
            ThreadPool.GetMaxThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Max Threads {0}/{1}", workerThreads, completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Comm Status {0}, Speed {1}", 
                NcCommStatus.Instance.Status, NcCommStatus.Instance.Speed);
            Log.Info (Log.LOG_SYS, "Monitor: Battery Level {0}, Plugged Status {1}",
                NachoPlatform.Power.Instance.BatteryLevel, NachoPlatform.Power.Instance.PowerState);
            Log.Info (Log.LOG_SYS, "Monitor: DB Connections {0}", NcModel.Instance.NumberDbConnections);
            if (100 < PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ()) {
                Log.DumpFileDescriptors ();
            }

            if (null != MonitorEvent) {
                MonitorEvent (this, EventArgs.Empty);
            }
        }

        public void EstablishService ()
        {
            // Create Device account if not yet there.
            McAccount deviceAccount = null;
            NcModel.Instance.RunInTransaction (() => {
                deviceAccount = McAccount.GetDeviceAccount ();
                if (null == deviceAccount) {
                    deviceAccount = new McAccount () {
                        AccountType = McAccount.AccountTypeEnum.Device,
                    };
                    deviceAccount.Insert ();
                }
            });
            // Create file directories.
            NcModel.Instance.InitalizeDirs (deviceAccount.Id);

            // Create Device contacts/calendars if not yet there.
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetDeviceContactsFolder ()) {
                    var freshMade = McFolder.Create (deviceAccount.Id, true, false, true, "0",
                                        McFolder.ClientOwned_DeviceContacts, "Device Contacts",
                                        NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14);
                    freshMade.Insert ();
                }
            });
            NcModel.Instance.RunInTransaction (() => {
                if (null == McFolder.GetDeviceCalendarsFolder ()) {
                    var freshMade = McFolder.Create (deviceAccount.Id, true, false, true, "0",
                                        McFolder.ClientOwned_DeviceCalendars, "Device Calendars",
                                        NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedCal_13);
                    freshMade.Insert ();
                }
            });
        }

        public void QuickSync ()
        {
            if (ExecutionContextEnum.QuickSync != ExecutionContext) {
                ExecutionContext = ExecutionContextEnum.QuickSync;
            }
            // Class 3 services must have already been set up.
            BackEnd.Instance.QuickSync ();
        }

        // method can be used to post to StatusIndEvent from outside NcApplication.
        public void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            if (TestOnlyInvokeUseCurrentThread) {
                if (null != StatusIndEvent) {
                    StatusIndEvent.Invoke (this, e);
                }
            } else {
                InvokeOnUIThread.Instance.Invoke (() => {
                    if (null != StatusIndEvent) {
                        StatusIndEvent.Invoke (this, e);
                    }
                });
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
            if (McMutables.GetBool (McAccount.GetDeviceAccount ().Id, "CERTAPPROVAL", certificate.Thumbprint)) {
                CertAskResp (accountId, true);
                return;
            }
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
            if (isOkay) {
                McMutables.GetOrCreateBool (McAccount.GetDeviceAccount ().Id, "CERTAPPROVAL", 
                    BackEnd.Instance.ServerCertToBeExamined (accountId).Thumbprint, true);
            }
            BackEnd.Instance.CertAskResp (accountId, isOkay);
        }
    }
}

