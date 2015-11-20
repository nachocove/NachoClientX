//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
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

    public class UserIdFile
    {
        private const string FileName = "user_id";
        private const string OldFileName = "client_id";

        public string FilePath { get; protected set; }

        private static UserIdFile _Instance;

        public static UserIdFile SharedInstance {
            get {
                if (null == _Instance) {
                    // Check if there is a file with the old file name (client_id). Rename it
                    var dirPath = NcApplication.GetDataDirPath ();
                    var oldFilePath = Path.Combine (dirPath, OldFileName);
                    var newFilePath = Path.Combine (dirPath, FileName);
                    if (File.Exists (oldFilePath)) {
                        File.Move (oldFilePath, newFilePath);
                    }

                    _Instance = new UserIdFile ();

                    // Check if the there is a new file (user_id). If yes, migrate
                    // the user ID to keychain
                    if (_Instance.Exists ()) {
                        _Instance.Write (_Instance.ReadFile ());
                        File.Delete (_Instance.FilePath);
                    }
                }
                return _Instance;
            }
        }

        public UserIdFile ()
        {
            FilePath = Path.Combine (NcApplication.GetDataDirPath (), FileName);
        }

        public bool Exists ()
        {
            return File.Exists (FilePath);
        }

        protected string ReadFile ()
        {
            try {
                using (var stream = new FileStream (FilePath, FileMode.Open, FileAccess.Read)) {
                    using (var reader = new StreamReader (stream)) {
                        return reader.ReadLine ();
                    }
                }
            } catch (IOException) {
                return null;
            }
        }

        public string Read ()
        {
            return Keychain.Instance.GetUserId ();
        }

        public void Write (string userId)
        {
            Keychain.Instance.SetUserId (userId);
        }
    }

    // THIS IS THE INIT SEQUENCE FOR THE NON-UI ASPECTS OF THE APP ON ALL PLATFORMS.
    // IF YOUR INIT TAKES SIGNIFICANT TIME, YOU NEED TO HAVE A NcTask.Run() IN YOUR INIT
    // THAT DOES THE LONG DURATION STUFF ON A BACKGROUND THREAD.

    public sealed class NcApplication : IBackEndOwner, IStatusIndEvent
    {
        private const int KClass4LateShowSeconds = 15;
        private const int KSafeModeMaxSeconds = 120;
        private const string KDataPathSegment = "Data";

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

        private bool SafeMode = false;
        private bool SafeModeStarted = false;

        private static string Documents;
        private static string DataDir;

        public double UpTimeSec { 
            get {
                return (DateTime.UtcNow - _LaunchTimeUTc).TotalSeconds;
            }
        }

        public ExecutionContextEnum ExecutionContext {
            get { return _ExecutionContext; }
            private set { 
                if (value != _ExecutionContext) {
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
        }

        public bool IsBackground {
            get {
                return (ExecutionContextEnum.Background == ExecutionContext);
            }
        }

        public bool IsForeground {
            get {
                return (ExecutionContextEnum.Foreground == ExecutionContext);
            }
        }

        public bool IsForegroundOrBackground {
            get {
                return IsBackground || IsForeground;
            }
        }

        public bool IsMigrating {
            get {
                return (ExecutionContextEnum.Migrating == ExecutionContext);
            }
        }

        public bool IsInitializing {
            get {
                return (ExecutionContextEnum.Initializing == ExecutionContext);
            }
        }

        public bool IsQuickSync {
            get {
                return (ExecutionContextEnum.QuickSync == ExecutionContext);
            }
        }

        // This string needs to be filled out by platform-dependent code when the app is first launched.
        public string CrashFolder { get; set; }

        private string _StartupLog;
        public string StartupLog {
            get {
                if (null == _StartupLog) {
                    _StartupLog = Path.Combine (GetDataDirPath (), "startup.log");
                }
                return _StartupLog;
            }
        }

        // Client Id is a string that uniquely identifies a NachoMail client on
        // all cloud servers (telemetry, pinger, etc.)
        private string _UserId;

        public string UserId {
            get {
                return _UserId;
            }
            set {
                if (value != _UserId) {
                    _UserId = value;
                    UserIdFile.SharedInstance.Write (_UserId);
                    InvokeStatusIndEventInfo (null, NcResult.SubKindEnum.Info_UserIdChanged, _UserId);
                }
            }
        }

        public string ClientId {
            get {
                return Device.Instance.Identity ();
            }
        }

        public static string ApplicationLogForCrashManager ()
        {
            // TODO: UtcNow isn't really the launch-time, nor is it really what we want.
            // For convenience what we REALLY want here is the time of the crash for 
            // easy-cut-n-pasting from HA to TeleViewer. What this really is is the
            // upload-time, i.e. when we upload this sucker to HA.
            string launchTime = String.Format ("{0:O}", DateTime.UtcNow);
            string log = String.Format ("Version: {0}\nBuild Number: {1}\nLaunch Time: {2}\nDevice ID: {3}\n",
                             BuildInfo.Version, BuildInfo.BuildNumber, launchTime, Device.Instance.Identity ());
            if (BuildInfoHelper.IsDev) {
                log += String.Format ("Build Time: {0}\nBuild User: {1}\n" +
                "Source: {2}\n", BuildInfo.Time, BuildInfo.User, BuildInfo.Source);
            }
            return log;
        }

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
        public McAccount Account {
            get {
                return _Account;
            }
            set {
                _Account = value;
                InvokeStatusIndEventInfo (null, NcResult.SubKindEnum.Info_AccountChanged);
            }
        }

        private McAccount _Account;

        public delegate void CredReqCallbackDele (int accountId);

        /// <summary>
        /// CredRequest: When called, the callee must gather the credential for the specified 
        /// account and add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.
        /// </summary>
        public CredReqCallbackDele CredReqCallback { set; get; }

        public delegate void ServConfReqCallbackDele (int accountId, McAccount.AccountCapabilityEnum capabilities, object arg = null);

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

        public delegate void SendEmailRespCallbackDele (int accountId, int emailMessageId, bool didSend);

        public SendEmailRespCallbackDele SendEmailRespCallback { set; get; }

        public static event EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException;

        public int UiThreadId { get; set; }
        // event can be used to register for status indications.
        public event EventHandler StatusIndEvent;
        // when true, everything in the background needs to chill.
        public bool IsBackgroundAbateRequired { get; set; }

        public bool TestOnlyInvokeUseCurrentThread { get; set; }

        private bool serviceHasBeenEstablished = false;

        private NcSamples ProcessMemory;

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

        private void UpdateUserIdFromFile (string clientIdFile)
        {
            UserId = UserIdFile.SharedInstance.Read ();
            string cloudUserId = CloudHandler.Instance.GetUserId ();
            if ((cloudUserId != null) && (cloudUserId != UserId)) {
                UserId = cloudUserId;
            }
        }

        private NcApplication ()
        {
            // Install test mode handlers
            InitTestMode ();

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
                    foreach (var ex in aex.InnerExceptions) { 
                        if (ex is System.IO.IOException && ex.Message.Contains ("Too many open files")) {
                            Log.Error (Log.LOG_SYS, "UnobservedTaskException:{0}: Dumping File Descriptors", ex.Message);
                            Log.DumpFileDescriptors ();
                            NcModel.Instance.DumpLastAccess ();
                        }
                    }
                    if (null != UnobservedTaskException) {
                        UnobservedTaskException (sender, eargs);
                    } else {
                        aex.Handle ((ex) => false);
                    }
                }
            };
            UiThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            if (UserIdFile.SharedInstance.Exists ()) {
                UpdateUserIdFromFile (UserIdFile.SharedInstance.FilePath);
            }
            StatusIndEvent += (object sender, EventArgs ea) => {
                var siea = (StatusIndEventArgs)ea;
                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStarted) {
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "NcApplication received Info_BackgroundAbateStarted {0} seconds", deliveryTime.ToString ());
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStopped) {
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "NcApplication received Info_BackgroundAbateStopped {0} seconds", deliveryTime.ToString ());
                }
            };
            ProcessMemory = new NcSamples ("Monitor.ProcessMemory");
            ProcessMemory.MinInput = 0;
            ProcessMemory.MaxInput = 100000;
            ProcessMemory.LimitInput = true;
            ProcessMemory.ReportThreshold = 4;
        }

        private static volatile NcApplication instance;
        private static object syncRoot = new Object ();
        private NcTimer MonitorTimer;
        private NcTimer Class4LateShowTimer;
        private NcTimer StartupUnmarkTimer;

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

        public void ContinueRemoveAccountIfNeeded ()
        {
            // if not done removing account, finish up 
            int AccountId = NcModel.Instance.GetRemovingAccountIdFromFile ();
            if (AccountId > 0) {
                Log.Info (Log.LOG_UI, "RemoveAccount: Continuing to remove data for account {0} after restart", AccountId);
                NcAccountHandler.Instance.RemoveAccountDBAndFilesForAccountId (AccountId);
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
            EmailHelper.Setup ();
            BackEnd.Instance.Owner = this;
            BackEnd.Instance.CreateServices ();
            BackEnd.Instance.Start ();
            ExecutionContext = _PlatformIndication; 
            ContinueOnActivation ();
            // load products from app store
            StoreHandler.Instance.Start ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServicesCompletion exited.");
        }

        public void StartBasalServices ()
        {
            Log.Info (Log.LOG_SYS, "{0}-bit App", 8 * IntPtr.Size);
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices called.");
            NcTask.StartService ();
            CloudHandler.Instance.Start ();
            if (UserId == null) {
                // this can be null if cloud is not accessible or if this the first time
                UserId = CloudHandler.Instance.GetUserId (); 
            }
            Telemetry.StartService ();

            // Pick most recently used account
            Account = LoginHelpers.PickStartupAccount ();

            // NcMigration does one query. So db must be initialized. Currently, db can be and is 
            // lazy initialized. So, we don't need pay any attention. But if that changes in the future,
            // we need to sequence these properly.
            NcMigration.Setup ();
            if (ShouldEnterSafeMode ()) {
                ExecutionContext = ExecutionContextEnum.Initializing;
                SafeMode = true;
                Telemetry.SharedInstance.Throttling = false;
                NcTask.Run (() => {
                    if (!MonitorUploads ()) {
                        Log.Info (Log.LOG_LIFECYCLE, "NcApplication: safe mode canceled");
                        return;
                    }
                    if (!NcMigration.WillStartService ()) {
                        StartBasalServicesCompletion ();
                    } else {
                        Log.Info (Log.LOG_LIFECYCLE, "Starting migration after exiting safe mode");
                        ExecutionContext = ExecutionContextEnum.Migrating;
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
                    SafeMode = false;
                }, "SafeMode");

                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartBasalServices exited (safe mode).");
                return;
            } else {
                SafeMode = false;
            }

            // Start Migrations, if any.
            if (NcMigration.WillStartService ()) {
                ExecutionContext = ExecutionContextEnum.Migrating;
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
            StoreHandler.Instance.Stop (); 
            CloudHandler.Instance.Stop (); 

            if (null != StartupUnmarkTimer) {
                StartupUnmarkTimer.Dispose ();
                StartupUnmarkTimer = null;
            }
            NcTimer.StopService ();
            NcTask.StopService ();
            UnmarkStartup ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopBasalServices exited.");
        }

        // ALL CLASS-4 STARTS ARE DEFERRED BASED ON TIME.
        public void StartClass4Services ()
        { 
            // Make sure the scheduled notifications are up to date.
            LocalNotificationManager.ScheduleNotifications ();

            MonitorStart (); // Has a deferred timer start inside.
            CrlMonitor.StartService ();
            Log.Info (Log.LOG_LIFECYCLE, "{0} (build {1}) built at {2} by {3}",
                BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);
            Log.Info (Log.LOG_LIFECYCLE, "Device ID: {0}", Device.Instance.Identity ());
            Log.Info (Log.LOG_LIFECYCLE, "Culture: {0} ({1})", CultureInfo.CurrentCulture.Name, CultureInfo.CurrentCulture.DisplayName);
            if (0 < BuildInfo.Source.Length) {
                Log.Info (Log.LOG_LIFECYCLE, "Source Info: {0}", BuildInfo.Source);
            }
            Class4LateShowTimer = new NcTimer ("NcApplication:Class4LateShowTimer", (state) => {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer called.");
                NcModel.Instance.Info ();
                NcCapture.ResumeAll ();
                NcTimeVariance.ResumeAll ();
                if (null != Class4LateShowEvent) {
                    Class4LateShowEvent (this, EventArgs.Empty);
                }
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer exited.");
            }, null, new TimeSpan (0, 0, KClass4LateShowSeconds + (SafeMode ? KSafeModeMaxSeconds : 0)), TimeSpan.Zero);
        }

        public void StopClass4Services ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass4Services called.");
            MonitorStop ();
            CrlMonitor.StopService ();
            if ((null != Class4LateShowTimer) && Class4LateShowTimer.DisposeAndCheckHasFired ()) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Class4LateShowTimer.DisposeAndCheckHasFired.");
                NcCapture.PauseAll ();
                NcTimeVariance.PauseAll ();
            }
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StopClass4Services exited.");
        }

        public void ContinueOnActivation ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: ContinueOnActivation called");
            if (IsMigrating) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: Migration in process. Will run ContinueOnActivation after migration is complete.");
                return;
            } else if (!IsForeground) {
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: App is still not in the foreground. Will run ContinueOnActivation later.");
                return;
            }
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: ContinueOnActivation running...");

            NcApplication.Instance.StartClass4Services ();
            Log.Info (Log.LOG_LIFECYCLE, "NcApplication: StartClass4Services complete");
        }

        bool DoesBackEndStateIndicateAnIssue (int accountId, McAccount.AccountCapabilityEnum capabiliity)
        {
            var backEndState = BackEnd.Instance.BackEndState (accountId, capabiliity);

            switch (backEndState) {
            case BackEndStateEnum.CertAskWait:
                Log.Info (Log.LOG_STATE, "NcApplication: CERTASKCALLBACK ");
                return true;
            case BackEndStateEnum.CredWait:
                Log.Info (Log.LOG_STATE, "NcApplication: CREDCALLBACK ");
                return true;
            case BackEndStateEnum.ServerConfWait:
                Log.Info (Log.LOG_STATE, "NcApplication: SERVCONFCALLBACK ");
                return true;
            default:
                Log.Info (Log.LOG_LIFECYCLE, "NcApplication: ContinueOnActivation exited");
                return false;
            }
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
            if (null != MonitorTimer) {
                MonitorTimer.Dispose ();
                MonitorTimer = null;
            }
        }

        public void MonitorReport (string moniker = null, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!String.IsNullOrEmpty (moniker)) {
                Log.Info (Log.LOG_SYS, "Monitor: {0} from line {1} of {2}", moniker, sourceLineNumber, sourceFilePath);
            }
            var processMemory = PlatformProcess.GetUsedMemory () / (1024 * 1024);
            ProcessMemory.AddSample ((int)processMemory);
            Log.Info (Log.LOG_SYS, "Monitor: Memory: Process {0} MB, GC {1} MB",
                processMemory, GC.GetTotalMemory (true) / (1024 * 1024));
            int minWorker, maxWorker, minCompletion, maxCompletion;
            ThreadPool.GetMinThreads (out minWorker, out minCompletion);
            ThreadPool.GetMaxThreads (out maxWorker, out maxCompletion);
            int systemThreads = PlatformProcess.GetNumberOfSystemThreads ();
            string message = string.Format ("Monitor: Threads: Min {0}/{1}, Max {2}/{3}, System {4}",
                                 minWorker, minCompletion, maxWorker, maxCompletion, systemThreads);
            if (50 > systemThreads) {
                Log.Info (Log.LOG_SYS, message);
            } else {
                Log.Warn (Log.LOG_SYS, message);
            }
            Log.Info (Log.LOG_SYS, "Monitor: Status: Comm {0}, Speed {1}, Battery {2:00}% {3}",
                NcCommStatus.Instance.Status, NcCommStatus.Instance.Speed,
                NachoPlatform.Power.Instance.BatteryLevel * 100.0, NachoPlatform.Power.Instance.PowerState);
            Log.Info (Log.LOG_SYS, "Monitor: DB Connections {0}", NcModel.Instance.NumberDbConnections);
            Log.Info (Log.LOG_SYS, "Monitor: Files: Max {0}, Currently open {1}",
                PlatformProcess.GetCurrentNumberOfFileDescriptors (), PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ());
            if (100 < PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ()) {
                Log.DumpFileDescriptors ();
            }
            NcModel.Instance.DumpLastAccess ();
            NcTask.Dump ();

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
                    deviceAccount = new McAccount ();
                    deviceAccount.DisplayName = "Device";
                    deviceAccount.SetAccountType (McAccount.AccountTypeEnum.Device);
                    deviceAccount.Insert ();
                }
            });
            // Create file directories.
            NcModel.Instance.InitializeDirs (deviceAccount.Id);

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
                    var freshMade = McFolder.Create (deviceAccount.Id, true, true, true, "0",
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

                //////////////////////////////////////////////////////////////////////////////////////
                // Actions that shouldn't be cancelled.  These need to run to completion, even if that
                // means the task survives across a shutdown.

                NcEventManager.Initialize ();
                LocalNotificationManager.InitializeLocalNotifications ();

                /////////////////////////////////////////////////////////////////////////////////////
                // Actions that can be cancelled.  These are not necessary for the correctness of the
                // running app, or they can be delayed until the next time the app starts.

                NcTask.Cts.Token.ThrowIfCancellationRequested ();

                // Clean up old McPending tasks that have been abandoned.
                DateTime cutoff = DateTime.UtcNow - new TimeSpan (2, 0, 0, 0); // Two days ago
                foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                    foreach (var pending in McPending.QueryOlderThanByState (account.Id, cutoff, McPending.StateEnum.Failed)) {
                        NcTask.Cts.Token.ThrowIfCancellationRequested ();
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

        public void InvokeStatusIndEventInfo (McAccount account, NcResult.SubKindEnum subKind, object value = null)
        {
            var siea = new StatusIndEventArgs () {
                Account = (null != account ? account : ConstMcAccount.NotAccountSpecific),
                Status = NcResult.Info (subKind)
            };
            if (null != value) {
                siea.Status.Value = value;
            }
            InvokeStatusIndEvent (siea);
        }

        // IBackEndOwner methods below.
        public void CredReq (int accountId)
        {
            if (null != CredReqCallback) {
                CredReqCallback (accountId);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication CredReqCallback.");
            }
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_CredReqCallback),
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }

        public void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg)
        {
            if (null != ServConfReqCallback) {
                ServConfReqCallback (accountId, capabilities, arg);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication ServConfReqCallback.");
            }
            var status = NcResult.Info (NcResult.SubKindEnum.Info_ServerConfReqCallback);
            status.Value = new Tuple<McAccount.AccountCapabilityEnum, BackEnd.AutoDFailureReasonEnum> (capabilities, arg);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = status,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }

        public void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, X509Certificate2 certificate)
        {
            if (McMutables.GetBool (McAccount.GetDeviceAccount ().Id, "CERTAPPROVAL", certificate.Thumbprint)) {
                CertAskResp (accountId, capabilities, true);
                return;
            }
            if (null != CertAskReqCallback) {
                CertAskReqCallback (accountId, certificate);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication CertAskReqCallback.");
            }
            var status = NcResult.Info (NcResult.SubKindEnum.Info_CertAskReqCallback);
            status.Value = new Tuple<McAccount.AccountCapabilityEnum, X509Certificate2> (capabilities, certificate);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = status,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }

        public void SearchContactsResp (int accountId, string prefix, string token)
        {
            if (null != SearchContactsRespCallback) {
                SearchContactsRespCallback (accountId, prefix, token);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication SearchContactsRespCallback.");
            }
        }

        public void SendEmailResp (int accountId, int emailMessageId, bool didSend)
        {
            if (null != SendEmailRespCallback) {
                SendEmailRespCallback (accountId, emailMessageId, didSend);
            } else {
                Log.Error (Log.LOG_UI, "Nothing registered for NcApplication SendEmailRespCallback.");
            }
        }

        public bool CertAskReqPreApproved (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var certificate = BackEnd.Instance.ServerCertToBeExamined (accountId, capabilities);
            if (null != certificate) {
                return (McMutables.GetBool (McAccount.GetDeviceAccount ().Id, "CERTAPPROVAL", certificate.Thumbprint));
            } else {
                return false;
            }
        }

        public void CertAskResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool isOkay)
        {
            if (isOkay) {
                var certificate = BackEnd.Instance.ServerCertToBeExamined (accountId, capabilities);
                if (null != certificate) {
                    McMutables.GetOrCreateBool (McAccount.GetDeviceAccount ().Id, "CERTAPPROVAL", certificate.Thumbprint, true);
                }
            }
            BackEnd.Instance.CertAskResp (accountId, capabilities, isOkay);
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

        public bool InSafeMode ()
        {
            return SafeMode;
        }

        private bool ShouldEnterSafeMode ()
        {
            if (Debugger.IsAttached) {
                return false;
            }
            if (SafeModeStarted) {
                return false;
            }
            if (!File.Exists (StartupLog)) {
                return false;
            }
            if (new FileInfo(StartupLog).Length > 2) {
                Telemetry.JsonFileTable.FinalizeAll (); // close of all JSON files
                return true;
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
            SafeModeStarted = true;
            int numTelemetryEvents, numCrashes;
            while (KSafeModeMaxSeconds > UpTimeSec) { // safe mode can only run up to 2 min
                numTelemetryEvents = 0;
                numCrashes = 0;

                if (ExecutionContextEnum.QuickSync == ExecutionContext) {
                    Log.Info (Log.LOG_LIFECYCLE, "MonitorUploads: early exit in quick sync.");
                    break;
                }

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
                    numTelemetryEvents = McTelemetryEvent.QueryCount () + McTelemetrySupportEvent.QueryCount ();
                    if ((0 == numTelemetryEvents) && (null == Telemetry.JsonFileTable.GetNextReadFile ())) {
                        telemetryDone = true;
                    }
                }

                // Check if HockeyApp has any queued crash reports
                if (!crashReportingDone) {
                    numCrashes = NumberOfCrashReports ();
                    if (0 == numCrashes) {
                        crashReportingDone = true;
                    }
                }

                Log.Info (Log.LOG_LIFECYCLE, "MonitorUploads: telemetryEvents={0}, crashes={1}", numTelemetryEvents, numCrashes);

                if (crashReportingDone && telemetryDone) {
                    break;
                }
                if (!NcTask.CancelableSleep (1000)) {
                    return false;
                }
            }
            if (KSafeModeMaxSeconds > UpTimeSec) {
                // Safe mode does not use up all allowed time. Reschedule class 4 timer to an earlier time.
                if ((null != Class4LateShowTimer) && !Class4LateShowTimer.IsExpired ()) {
                    Class4LateShowTimer.Change (new TimeSpan (0, 0, KClass4LateShowSeconds), TimeSpan.Zero);
                }
            }
            return true;
        }


        public void MarkStartup ()
        {
            if (null != StartupUnmarkTimer) {
                StartupUnmarkTimer.Dispose ();
            }
            StartupUnmarkTimer = new NcTimer ("StartupUnmark",
                (state) => {
                    UnmarkStartup ();
                }, null, 20 * 1000, 0);
            using (var fileStream = File.Open (StartupLog, FileMode.OpenOrCreate | FileMode.Append)) {
                fileStream.WriteByte ((byte)0);
            }
        }

        public void UnmarkStartup ()
        {
            if (File.Exists (StartupLog)) {
                try {
                    File.Delete (StartupLog);
                } catch (Exception e) {
                    Log.Warn (Log.LOG_LIFECYCLE, "fail to delete startup log (file={0}, exception={1})", StartupLog, e.Message);
                }
            }
        }

        public void CheckNotified ()
        {
            var latestMigration = McMigration.QueryLatestMigration ();
            var version = (new NcMigration15 ()).Version ();
            if ((null == latestMigration) ||
                (latestMigration.Version < version) ||
                ((version == latestMigration.Version) && (!latestMigration.Finished))) {
                // We have not run NcMigration15. Wait till it is finished.
                return;
            }
            foreach (var message in McEmailMessage.QueryUnnotified ()) {
                if (NcTask.Cts.Token.IsCancellationRequested) {
                    return;
                }
                Log.Warn (Log.LOG_PUSH, "Unnotified email message (id={0}, dateReceived={1}, createdAt={2})",
                    message.Id, message.DateReceived, message.CreatedAt);
                message.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.HasBeenNotified = true;
                    return true;
                });
            }
        }

        public static string GetDocumentsPath ()
        {
            if (Documents == null) {
                Documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            }
            return Documents;
        }

        public static string GetDataDirPath ()
        {
            if (DataDir == null) {
                DataDir = Path.Combine (GetDocumentsPath (), KDataPathSegment);
                if (!Directory.Exists (DataDir)) {
                    Directory.CreateDirectory (DataDir);
                }
            }
            return DataDir;
        }

        public static string GetVersionString ()
        {
            return String.Format ("{0} ({1})", BuildInfo.Version, BuildInfo.BuildNumber);
        }

        // Fast track to UI
        static public bool ReadyToStartUI ()
        {
            if (!NcApplication.Instance.IsUp ()) {
                return false;
            }
            if (!LoginHelpers.HasViewedTutorial ()) {
                return false;
            }
            var account = NcApplication.Instance.Account;
            if (null == account) {
                return false;
            }
            if (McAccount.AccountTypeEnum.Device == account.AccountType) {
                return false;
            }
            var configAccount = McAccount.GetAccountBeingConfigured ();
            if (null != configAccount) {
                return false;
            }
            if (LoginHelpers.GetGoogleSignInCallbackArrived ()) {
                return false;
            }
            return true;
        }

        public static void InitTestMode ()
        {
            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                TestMode.Instance.Add ("markhoton", (parameters) => {
                    ScoringHelpers.SetTestMode (true);
                    Console.WriteLine ("!!!!! ENTER MARKHOT TEST MODE !!!!!");
                });
                TestMode.Instance.Add ("markhotoff", (parameters) => {
                    ScoringHelpers.SetTestMode (false);
                    Console.WriteLine ("!!!!! EXIT MARKHOT TEST MODE !!!!!");
                });
                TestMode.Instance.Add ("searchon", (parameters) => {
                    Log.SharedInstance.Settings.Debug.EnableConsole (Log.LOG_SEARCH);
                    Log.SharedInstance.Settings.Debug.EnableTelemetry (Log.LOG_SEARCH);
                    Console.WriteLine ("!!!!! START SEARCH DEBUG LOGGING  !!!!!");
                });
                TestMode.Instance.Add ("searchoff", (parameters) => {
                    Log.SharedInstance.Settings.Debug.DisableConsole (Log.LOG_SEARCH);
                    Log.SharedInstance.Settings.Debug.DisableTelemetry (Log.LOG_SEARCH);
                    Console.WriteLine ("!!!!! STOP SEARCH DEBUG LOGGING !!!!!");
                });
            }
        }

    }
}

