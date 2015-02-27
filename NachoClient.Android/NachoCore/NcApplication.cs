//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Wbxml;
using NachoClient.Build;
using NachoPlatform;
using NachoPlatformBinding;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;

namespace NachoCore
{
    // THIS IS THE INIT SEQUENCE FOR THE NON-UI ASPECTS OF THE APP ON ALL PLATFORMS.
    // IF YOUR INIT TAKES SIGNIFICANT TIME, YOU NEED TO HAVE A NcTask.Run() IN YOUR INIT
    // THAT DOES THE LONG DURATION STUFF ON A BACKGROUND THREAD.

    public sealed class NcApplication : IBackEndOwner
    {
        private const int KClass4EarlyShowSeconds = 5;
        private const int KClass4LateShowSeconds = 15;

        public enum ExecutionContextEnum
        {
            Migrating,
            Initializing,
            Foreground,
            Background,
            QuickSync,
        };

        private ExecutionContextEnum _ExecutionContext = ExecutionContextEnum.Migrating;
        private ExecutionContextEnum _PlatformIndication = ExecutionContextEnum.Background;

        private DateTime _LaunchTimeUTc = DateTime.UtcNow;

        public double UpTimeSec { 
            get {
                return (DateTime.UtcNow - _LaunchTimeUTc).TotalSeconds;
            }
        }

        public ExecutionContextEnum ExecutionContext {
            get { return _ExecutionContext; }
            private set { 
                _ExecutionContext = value; 
                Log.Info (Log.LOG_LIFECYCLE, "ExecutionContext => {0}", _ExecutionContext.ToString ());
                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_ExecutionContextChanged);
                result.Value = _ExecutionContext;
                InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = result,
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }
        }

        // This string needs to be filled out by platform-dependent code when the app is first launched.
        public string CrashFolder { get; set; }

        public string StartupLog {
            get {
                var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
                return Path.Combine (documents, "startup.log");
            }
        }

        private bool StartupMarked;

        public bool IsUp ()
        {
            return (ExecutionContextEnum.Migrating != ExecutionContext) && (ExecutionContextEnum.Initializing != ExecutionContext);
        }

        public ExecutionContextEnum PlatformIndication {
            get { return _PlatformIndication; }
            set { 
                _PlatformIndication = value;
                Log.Info (Log.LOG_LIFECYCLE, "PlatformIndication => {0}", _PlatformIndication.ToString ());
                if (ExecutionContextEnum.Migrating != ExecutionContext &&
                    ExecutionContextEnum.Initializing != ExecutionContext) {
                    ExecutionContext = _PlatformIndication;
                }
            }
        }

        // This is sucky, but for now it is better than having it in AppDelegate.
        public McAccount Account { get; set; }

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

        private bool serviceHasBeenEstablished = false;

        private bool IsXammit (Exception ex)
        {
            var message = ex.ToString ();
            if (ex is OperationCanceledException && message.Contains ("NcTask")) {
                Log.Warn (Log.LOG_SYS, "XAMMIT AggregateException: UnobservedTaskException from cancelled Task.");
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
            if (message.Contains ("Amazon.Runtime")) {
                Log.Error (Log.LOG_SYS, "AMAXAMMIT Unobserved Exception: {0}", message);
                // Don't let AWS SDK exception brain damage take down the app.
                return true;
            }
            Log.Error (Log.LOG_SYS, "UnobservedTaskException: {0}", message);
            return false;
        }

        private NcApplication ()
        {
            // SetMaxThreads needs to be called before SetMinThreads, because on some devices (such as
            // iPhone 4s running iOS 7) the default maximum is less than the minimums we are trying to set.
            // NcAssert.True can't be called here, because logging hasn't been set up yet, meaning a
            // failure can't be properly reported.
            if (!ThreadPool.SetMaxThreads (50, 16)) {
                Console.WriteLine ("ERROR: ThreadPool maximums could not be set.");
            }
            if (!ThreadPool.SetMinThreads (8, 6)) {
                Console.WriteLine ("ERROR: ThreadPool minimums could not be set.");
            }
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

        public event EventHandler Class4EarlyShowEvent;
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

        private void StartBasalServicesCompletion ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServicesCompletion called.");
            ExecutionContext = ExecutionContextEnum.Initializing;
            NcModel.Instance.GarbageCollectFiles ();
            NcModel.Instance.Start ();
            EstablishService ();
            NcModel.Instance.EngageRateLimiter ();
            NcBrain.StartService ();
            NcContactGleaner.Start ();
            BackEnd.Instance.Owner = this;
            BackEnd.Instance.EstablishService ();
            BackEnd.Instance.Start ();
            ExecutionContext = _PlatformIndication;
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServicesCompletion exited.");
        }

        public void StartBasalServices ()
        {
            Log.Info (Log.LOG_SYS, "{0}-bit App", 8 * IntPtr.Size);
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices called.");
            NcTask.StartService ();
            Telemetry.StartService ();
            Account = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            // NcMigration does one query. So db must be initialized. Currently, db can be and is 
            // lazy initialized. So, we don't need pay any attention. But if that changes in the future,
            // we need to sequence these properly.
            NcMigration.Setup ();

            if (ShouldEnterSafeMode ()) {
                if (!NcMigration.WillStartService ()) {
                    ExecutionContext = ExecutionContextEnum.Initializing;
                }
                NcTask.Run (() => {
                    if (!MonitorUploads ()) {
                        Log.Info (Log.LOG_LIFECYCLE, "NcApplication: safe mode canceled");
                        return;
                    }
                    if (!NcMigration.WillStartService ()) {
                        StartBasalServicesCompletion ();
                    } else {
                        Log.Info (Log.LOG_LIFECYCLE, "Starting migration after exiting safe mode");
                        NcMigration.StartService (
                            StartBasalServicesCompletion,
                            (percentage) => {
                                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_MigrationProgress);
                                result.Value = percentage;
                                InvokeStatusIndEvent (new StatusIndEventArgs () { 
                                    Status = result,
                                    Account = ConstMcAccount.NotAccountSpecific,
                                });
                            },
                            (description) => {
                                var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_MigrationDescription);
                                result.Value = description;
                                InvokeStatusIndEvent (new StatusIndEventArgs () { 
                                    Status = result,
                                    Account = ConstMcAccount.NotAccountSpecific,
                                });
                            });
                    }
                }, "SafeMode");

                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices exited (safe mode).");
                return;
            }

            // Start Migrations, if any.
            if (NcMigration.WillStartService ()) {
                NcMigration.StartService (
                    StartBasalServicesCompletion,
                    (percentage) => {
                        var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_MigrationProgress);
                        result.Value = percentage;
                        InvokeStatusIndEvent (new StatusIndEventArgs () { 
                            Status = result,
                            Account = ConstMcAccount.NotAccountSpecific,
                        });
                    },
                    (description) => {
                        var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_MigrationDescription);
                        result.Value = description;
                        InvokeStatusIndEvent (new StatusIndEventArgs () { 
                            Status = result,
                            Account = ConstMcAccount.NotAccountSpecific,
                        });
                    });
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices exited (w/migration).");
                return;
            }

            StartBasalServicesCompletion ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices exited (w/out migration).");
        }

        public void StopBasalServices ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopBasalServices called.");
            BackEnd.Instance.Stop ();
            NcContactGleaner.Stop ();
            NcBrain.StopService ();
            NcModel.Instance.Stop ();
            NcTimer.StopService ();
            NcTask.StopService ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopBasalServices exited.");
        }

        // ALL CLASS-4 STARTS ARE DEFERRED BASED ON TIME.
        public void StartClass4Services ()
        {
            // Make sure the scheduled notifications are up to date.
            LocalNotificationManager.ScheduleNotifications ();

            MonitorStart (); // Has a deferred timer start inside.
            Log.Info (Log.LOG_LIFECYCLE, "{0} (build {1}) built at {2} by {3}",
                BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);
            Log.Info (Log.LOG_LIFECYCLE, "Device ID: {0}", Device.Instance.Identity ());
            if (0 < BuildInfo.Source.Length) {
                Log.Info (Log.LOG_LIFECYCLE, "Source Info: {0}", BuildInfo.Source);
            }
            Class4EarlyShowTimer = new NcTimer ("NcApplication:Class4EarlyShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4EarlyShowTimer called.");
                if (null != Class4EarlyShowEvent) {
                    Class4EarlyShowEvent (this, EventArgs.Empty);
                }
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

        public void MonitorReport (string moniker = null, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!String.IsNullOrEmpty (moniker)) {
                Log.Info (Log.LOG_SYS, "Monitor: {0} from line {1} of {2}", moniker, sourceLineNumber, sourceFilePath);
            }
            Log.Info (Log.LOG_SYS, "Monitor: Process Memory {0} MB", (int)PlatformProcess.GetUsedMemory () / (1024 * 1024));
            Log.Info (Log.LOG_SYS, "Monitor: GC Memory {0} MB", GC.GetTotalMemory (true) / (1024 * 1024));
            int workerThreads, completionPortThreads;
            ThreadPool.GetMinThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Min Threads {0}/{1}", workerThreads, completionPortThreads);
            ThreadPool.GetMaxThreads (out workerThreads, out completionPortThreads);
            Log.Info (Log.LOG_SYS, "Monitor: Max Threads {0}/{1}", workerThreads, completionPortThreads);
            var numThreads = PlatformProcess.GetNumberOfSystemThreads ();
            var message = String.Format ("Monitor: System Threads {0}", numThreads);
            if (50 > numThreads) {
                Log.Info (Log.LOG_SYS, message);
            } else {
                Log.Warn (Log.LOG_SYS, message);
            }
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
            // It is not a big deal if this function is run twice, so don't bother
            // protecting the flag variable with a lock.
            if (serviceHasBeenEstablished) {
                return;
            }
            serviceHasBeenEstablished = true;

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

        /// <summary>
        /// Tasks that should happen just once when the app starts.
        /// </summary>
        public void AppStartupTasks ()
        {
            // Things that don't need to run on the UI thread and don't need
            // to happen right away should go into a background task.
            NcTask.Run (delegate {

                NcEventManager.Initialize ();

                LocalNotificationManager.InitializeLocalNotifications ();

                // Clean up old McPending tasks that have been abandoned.
                DateTime cutoff = DateTime.UtcNow - new TimeSpan (2, 0, 0, 0); // Two days ago
                foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                    foreach (var pending in McPending.QueryOlderThanByState (account.Id, cutoff, McPending.StateEnum.Failed)) {
                        // TODO Expand this to clean up more than just downloads.
                        if (McPending.Operations.EmailBodyDownload == pending.Operation ||
                            McPending.Operations.CalBodyDownload == pending.Operation ||
                            McPending.Operations.AttachmentDownload == pending.Operation) {
                            pending.Delete ();
                        }
                    }
                }

            }, "NcAppStartup");
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

        public string GetClientId ()
        {
            // TODO - the client id is currently obtained thru telemetry. Create an API
            //        now and if / when we move the client id registration mechanism
            //        out of telemetry, only this API needs to be changed.
            return Telemetry.SharedInstance.GetUserName ();
        }

        public string GetPlatformName ()
        {
            #if __IOS__
            return "ios";
            #elif __ANDROID__
            return "android";
            #else
            throw new PlatformNotSupportedException ();
            #endif
        }

        public string GetPushService ()
        {
            #if __IOS__
            return "APNS";
            #elif __ANDROID__
            return "GCM";
            #else
            throw new PlatformNotSupportedException ();
            #endif
        }

        private bool ShouldEnterSafeMode ()
        {
            var startupLog = StartupLog;
            if (!File.Exists (StartupLog)) {
                return false;
            }
            var bytes = new byte[128];
            int numBytes = 0;
            using (var fileStream = File.Open (startupLog, FileMode.Open, FileAccess.Read)) {
                numBytes = fileStream.Read (bytes, 0, bytes.Length);
            }
            int numTimestamps = 0;
            for (int n = 0; n < numBytes; n++) {
                if ('\n' == bytes [n]) {
                    numTimestamps += 1;
                    if (numTimestamps > 2) {
                        return true;
                    }
                }
            }

            return false;
        }

        private int NumberOfCrashReports ()
        {
            if (!Directory.Exists (CrashFolder)) {
                return 0;
            }
            return Directory.GetFiles (CrashFolder).Where (x => x.EndsWith (".meta")).Count ();
        }

        private bool MonitorUploads ()
        {
            bool telemetryDone = false;
            bool crashReportingDone = false;
            while (120 > UpTimeSec) { // safe mode can only run up to 2 min
                if (15 < UpTimeSec) {
                    // If we have no network connectivity or cellular only, we wait or
                    // upload up to 15 sec. The cellular part is to avoid running up
                    // data charges.
                    NcCommStatus comStatus = NcCommStatus.Instance;
                    if ((NetStatusStatusEnum.Down == comStatus.Status) ||
                        (NetStatusSpeedEnum.WiFi_0 != comStatus.Speed)) {
                        break;
                    }
                }

                // Check if we have caught up in telemetry upload
                if (!telemetryDone) {
                    int numTelemetryEvents = McTelemetryEvent.QueryCount () + McTelemetrySupportEvent.QueryCount ();
                    if (0 == numTelemetryEvents) {
                        telemetryDone = true;
                    }
                }

                // Check if HockeyApp has any queued crash reports
                if (!crashReportingDone && (0 == NumberOfCrashReports ())) {
                    crashReportingDone = true;
                }

                if (crashReportingDone && telemetryDone) {
                    break;
                }
                if (!NcTask.CancelableSleep (100)) {
                    return false;
                }
            }
            return true;
        }


        public void MarkStartup ()
        {
            using (var fileStream = File.Open (StartupLog, FileMode.OpenOrCreate | FileMode.Append)) {
                var timestamp = String.Format ("{0}\n", DateTime.UtcNow);
                var bytes = Encoding.ASCII.GetBytes (timestamp);
                fileStream.Write (bytes, 0, bytes.Length);
                StartupMarked = true;
            }
        }

        public void UnmarkStartup ()
        {
            if (!StartupMarked) {
                return; // already unmarked
            }
            if (30 > (DateTime.UtcNow - _LaunchTimeUTc).TotalSeconds) {
                return; // wait 30 seconds before unmark. should give enough time to upload crash log and some telemetry
            }
            var startupLog = StartupLog;
            if (File.Exists (startupLog)) {
                try {
                    File.Delete (startupLog);
                    StartupMarked = false;
                } catch (Exception e) {
                    Log.Warn (Log.LOG_LIFECYCLE, "fail to delete starutp log (file={0}, exception={1})", startupLog, e.Message);
                }
            }
        }
    }
}

