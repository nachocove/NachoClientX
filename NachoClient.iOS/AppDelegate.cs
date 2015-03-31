#define HA_AUTH_ANONYMOUS
//#define HA_AUTH_USER
//#define HA_AUTH_EMAIL

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CoreGraphics;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoPlatform;
using NachoClient.iOS;
using SQLite;
using Newtonsoft.Json;
using NachoCore.Wbxml;
using ObjCRuntime;
using NachoClient.Build;
using HockeyApp;
using NachoUIMonitorBinding;

namespace NachoClient.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the
    // User Interface of the application, as well as listening (and optionally responding) to
    // application events from iOS.
    // See http://iosapi.xamarin.com/?link=T%3aMonoTouch.UIKit.UIApplicationDelegate

    [Register ("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate
    {
        [DllImport ("libc")]
        private static extern int sigaction (Signal sig, IntPtr act, IntPtr oact);

        enum Signal
        {
            SIGBUS = 10,
            SIGSEGV = 11
        }
        // class-level declarations
        public override UIWindow Window { get; set; }

        private const UIUserNotificationType KNotificationSettings = 
            UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound;
        // iOS kills us after 30, so make sure we dont get there
        private const int KPerformFetchTimeoutSeconds = 25;
        private nint BackgroundIosTaskId = -1;
        // Don't use NcTimer here - use the raw timer to avoid any future chicken-egg issues.
        #pragma warning disable 414
        private Timer ShutdownTimer = null;
        // used to ensure that a race condition doesn't let the ShutdownTimer stop things after re-activation.
        private int ShutdownCounter = 0;

        #pragma warning restore 414
        private bool FinalShutdownHasHappened = false;
        private bool FirstLaunchInitialization = false;
        private bool DidEnterBackgroundCalled = false;
        private bool hasFirstSyncCompleted;

        private ulong UserNotificationSettings {
            get {
                return (ulong)UIApplication.SharedApplication.CurrentUserNotificationSettings.Types;
            }
        }

        private bool NotificationCanAlert {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Alert));
            }
        }

        private bool NotificationCanSound {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Sound));
            }
        }

        private bool NotificationCanBadge {
            get {
                return (0 != (UserNotificationSettings & (ulong)UIUserNotificationType.Badge));
            }
        }

        private void StartCrashReporting ()
        {
            if (Arch.SIMULATOR == Runtime.Arch) {
                // Xaramin does not produce .dSYM files. So, there is nothing to
                // upload to HockeyApp.
                //
                // For an explanation, see:
                // http://forums.xamarin.com/discussion/187/how-do-i-generate-dsym-for-simulator
                Log.Info (Log.LOG_LIFECYCLE, "Crash reporting is disabled on simulator");
                return;
            }

            if (Debugger.IsAttached) {
                Log.Info (Log.LOG_LIFECYCLE, "Crash reporting is disabled when debugger is attached");
                return;
            }

            //We MUST wrap our setup in this block to wire up
            // Mono's SIGSEGV and SIGBUS signals
            HockeyApp.Setup.EnableCustomCrashReporting (() => {

                //Get the shared instance
                var manager = BITHockeyManager.SharedHockeyManager;

                //Configure it to use our APP_ID
                manager.Configure (BuildInfo.HockeyAppAppId);

                // Enable automatic reporting
                manager.CrashManager.CrashManagerStatus = BITCrashManagerStatus.AutoSend;
                manager.CrashManager.EnableOnDeviceSymbolication = false;
                manager.CrashManager.Delegate = new HockeyAppCrashDelegate ();
                if (BuildInfo.Version.StartsWith ("DEV")) {
                    manager.DebugLogEnabled = true;
                }

                //Start the manager
                manager.StartManager ();

                //Authenticate (there are other authentication options)
                #if HA_AUTH_ANONYMOUS
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.Anonymous;
                #endif
                #if HA_AUTH_USER
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.HockeyAppUser;
                manager.Authenticator.Delegate = new HockeyAppAuthenticatorDelegate ();
                #endif
                #if HA_AUTH_EMAIL
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.HockeyAppEmail;
                manager.Authenticator.AuthenticationSecret = "fc041d7edcdd8b93951be3d4b9dd05d2";
                #endif
                manager.Authenticator.AuthenticateInstallation ();

                //Rethrow any unhandled .NET exceptions as native iOS
                // exceptions so the stack traces appear nicely in HockeyApp
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    try {
                        var ex = e.ExceptionObject as Exception;
                        if (null != ex) {
                            // See if we can get the part of the stack that is getting lost in ThrowExceptionAsNative().
                            Log.Error (Log.LOG_LIFECYCLE, "UnhandledException: {0}", ex);
                        }
                    } catch {
                    }
                    Setup.ThrowExceptionAsNative (e.ExceptionObject);
                };

                NcApplication.UnobservedTaskException += (sender, e) =>
                    Setup.ThrowExceptionAsNative (e.Exception);
            });
        }

        public override void RegisteredForRemoteNotifications (UIApplication application, NSData deviceToken)
        {
            var deviceTokenBytes = deviceToken.ToArray ();
            PushAssist.SetDeviceToken (Convert.ToBase64String (deviceTokenBytes));
            Log.Info (Log.LOG_LIFECYCLE, "RegisteredForRemoteNotifications: {0}", deviceToken.ToString ());
        }

        public override void FailedToRegisterForRemoteNotifications (UIApplication application, NSError error)
        {
            // null Value indicates token is lost.
            PushAssist.SetDeviceToken (null);
            Log.Info (Log.LOG_LIFECYCLE, "FailedToRegisterForRemoteNotifications: {0}", error.LocalizedDescription);
        }

        public override void DidRegisterUserNotificationSettings (UIApplication application, UIUserNotificationSettings notificationSettings)
        {
            Log.Info (Log.LOG_LIFECYCLE, "DidRegisteredUserNotificationSettings: 0x{0:X}", (ulong)notificationSettings.Types);
        }

        public override void DidReceiveRemoteNotification (UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_LIFECYCLE, "Got remote notification - {0}", userInfo);
            // Amazingly, it turns out the most programmatically simple way to convert a NSDictionary
            // to our own model objects.
            NSError error;
            var jsonData = NSJsonSerialization.Serialize (userInfo, NSJsonWritingOptions.PrettyPrinted, out error);
            var jsonStr = (string)NSString.FromData (jsonData, NSStringEncoding.UTF8);
            var notification = JsonConvert.DeserializeObject<Notification> (jsonStr);
            if (notification.HasPingerSection ()) {
                PushAssist.ProcessRemoteNotification (notification.pinger, (accountId) => {
                    if (NcApplication.Instance.IsForeground) {
                        var inbox = NcEmailManager.PriorityInbox ();
                        inbox.StartSync ();
                        completionHandler (UIBackgroundFetchResult.NoData);
                    } else {
                        if (doingPerformFetch) {
                            Log.Warn (Log.LOG_PUSH, "A perform fetch is already in progress. Do not start another one.");
                            completionHandler (UIBackgroundFetchResult.NoData);
                        } else {
                            StartFetch (application, completionHandler, "RN");
                            return; // completeHandler is called at the completion of perform fetch.
                        }
                    }
                });
            }
        }

        /// This is not a service but rather initialization of some native
        /// ObjC functions. It must be initialized before any UI object is
        /// created.
        private void StartUIMonitor ()
        {
            NachoUIMonitor.SetupUIButton (delegate(string description) {
                Telemetry.RecordUiButton (description);
            });

            NachoUIMonitor.SetupUISegmentedControl (delegate(string description, int index) {
                Telemetry.RecordUiSegmentedControl (description, index);
            });

            NachoUIMonitor.SetupUISwitch (delegate(string description, string onOff) {
                Telemetry.RecordUiSwitch (description, onOff);
            });

            NachoUIMonitor.SetupUIDatePicker (delegate(string description, string date) {
                Telemetry.RecordUiDatePicker (description, date);
            });

            NachoUIMonitor.SetupUITextField (delegate(string description) {
                Telemetry.RecordUiTextField (description);
            });

            NachoUIMonitor.SetupUIPageControl (delegate(string description, int page) {
                Telemetry.RecordUiPageControl (description, page);
            });

            NachoUIMonitor.SetupUIAlertView (delegate(string description, int index) {
                Telemetry.RecordUiAlertView (description, index);
            });

            NachoUIMonitor.SetupUIActionSheet (delegate(string description, int index) {
                Telemetry.RecordUiActionSheet (description, index);
            });

            NachoUIMonitor.SetupUITapGestureRecognizer (delegate(string description, int numTouches,
                                                                 PointF point1, PointF point2, PointF point3) {
                string touches = "";
                if (0 < numTouches) {
                    touches = String.Format ("({0},{1})", point1.X, point1.Y);
                    if (1 < numTouches) {
                        touches += String.Format (", ({0},{1})", point2.X, point2.Y);
                        if (2 < numTouches) {
                            touches += String.Format (", ({0},{1})", point3.X, point3.Y);
                        }
                    }
                }
                Telemetry.RecordUiTapGestureRecognizer (description, touches);
            });

//            NachoUIMonitor.SetupUITableView (delegate(string description, string operation) {
//                Telemetry.RecordUiTableView (description, operation);
//            });
        }

        // This method is common to both launching into the background and into the foreground.
        // It gets called once during the app lifecycle.
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            // One-time initialization that do not need to be shut down later.
            if (!FirstLaunchInitialization) {
                FirstLaunchInitialization = true;
                StartCrashReporting ();
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartCrashReporting complete");

                PushAssist.Initialize ();
                ServerCertificatePeek.Initialize ();
            }

            if (null == NcApplication.Instance.CrashFolder) {
                var cacheFolder = NSSearchPath.GetDirectories (NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User, true) [0];
                NcApplication.Instance.CrashFolder = Path.Combine (cacheFolder, "net.hockeyapp.sdk.ios");
                NcApplication.Instance.MarkStartup ();
            }

            NcApplication.Instance.ContinueRemoveAccountIfNeeded ();

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Called");
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;

            StartUIMonitor ();
            const uint MB = 1000 * 1000; // MB not MiB
            WebCache.Configure (1 * MB, 50 * MB);
            // end of one-time initialization

            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartBasalServices complete");

            NcApplication.Instance.AppStartupTasks ();

            application.SetStatusBarStyle (UIStatusBarStyle.LightContent, true);

            UINavigationBar.Appearance.BarTintColor = A.Color_NachoGreen;
            UIToolbar.Appearance.BackgroundColor = UIColor.White;
            UIBarButtonItem.Appearance.TintColor = A.Color_NachoBlue;

            var navigationTitleTextAttributes = new UITextAttributes ();
            navigationTitleTextAttributes.Font = A.Font_AvenirNextDemiBold17;
            navigationTitleTextAttributes.TextColor = UIColor.White;
            UINavigationBar.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes);
            UIBarButtonItem.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes, UIControlState.Normal);
            if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                // iOS 8 and after
                var settings = UIUserNotificationSettings.GetSettingsForTypes (KNotificationSettings, null);
                UIApplication.SharedApplication.RegisterUserNotificationSettings (settings);
                UIApplication.SharedApplication.RegisterForRemoteNotifications ();
            } else if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerForRemoteNotificationTypes:"))) {
                // iOS 7 and before
                // TODO: revist why we need the sound.
                UIApplication.SharedApplication.RegisterForRemoteNotificationTypes (
                    UIRemoteNotificationType.NewsstandContentAvailability | UIRemoteNotificationType.Sound);
            } else {
                Log.Error (Log.LOG_PUSH, "notification not registered!");
            }
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);
            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new ObjCRuntime.Class (typeof(CidImageProtocol)));

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: iOS Cocoa setup complete");

            NcApplication.Instance.CredReqCallback = CredReqCallback;
            NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication callbacks registered");

            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                Telemetry.SharedInstance.Throttling = false;
            };

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication Class4LateShowEvent registered");

            // If launch options is set with the local notification key, that means the app has been started
            // by a local notification.  ReceivedLocalNotification will be called and it wants to know if the
            // app has just be started of if it was already running. We set flags so ReceivedLocalNotification
            // knows that the app has just been started.
            if (launchOptions != null && launchOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: LaunchOptionsLocalNotificationKey");
                var localNotification = (UILocalNotification)launchOptions [UIApplication.LaunchOptionsLocalNotificationKey];
                var emailNotificationDictionary = localNotification.UserInfo.ObjectForKey (EmailNotificationKey);
                if (null != emailNotificationDictionary) {
                    var emailMessageId = ((NSNumber)emailNotificationDictionary).NIntValue;
                    SaveNotification ("FinishedLaunching", EmailNotificationKey, emailMessageId);
                }
                var eventNotificationDictionary = localNotification.UserInfo.ObjectForKey (EventNotificationKey);
                if (null != eventNotificationDictionary) {
                    var eventId = ((NSNumber)eventNotificationDictionary).NIntValue;
                    SaveNotification ("FinishedLaunching", EventNotificationKey, eventId);
                }
                if (localNotification != null) {
                    // reset badge
                    UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
                }
            }

            NcKeyboardSpy.Instance.Init ();

            if (NcApplication.Instance.IsUp () && "SegueToTabController" == StartupViewController.NextSegue ()) {
                var storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                var vc = storyboard.InstantiateViewController ("NachoTabBarController");
                Log.Info (Log.LOG_UI, "fast path to tab bar controller: {0}", vc);
                Window.RootViewController = (UIViewController)vc;
            }

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Exit");

            return true;
        }

        /// <Docs>Reference to the UIApplication that invoked this delegate method.</Docs>
        /// <summary>
        ///  Called when another app opens-in a document to nacho mail
        /// </summary>
        public override bool OpenUrl (UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
        {
            if (!url.IsFileUrl) {
                return false;
            }
            // We will be called here whether or not we were launched to Rx the file. So no need to handle in DFLwO.
            var document = McDocument.InsertSaveStart (McAccount.GetDeviceAccount ().Id);
            document.SetDisplayName (Path.GetFileName (url.Path));
            document.SourceApplication = sourceApplication;
            document.UpdateFileMove (url.Path);
            return true;
        }

        // OnActivated AND OnResignActivation ARE MIRROR IMAGE (except for BeginBackgroundTask).

        // Equivalent to applicationDidBecomeActive
        //
        // This method is called whenever the app is moved to the active state, whether on launch, from inactive, or
        // from the background.
        public override void OnActivated (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: Called");
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;
            BadgeNotifClear ();

            NcApplication.Instance.StartClass4Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: StartClass4Services complete");

            NcApplication.Instance.StatusIndEvent -= BgStatusIndReceiver;

            if (-1 != BackgroundIosTaskId) {
                UIApplication.SharedApplication.EndBackgroundTask (BackgroundIosTaskId);
            }
            BackgroundIosTaskId = UIApplication.SharedApplication.BeginBackgroundTask (() => {
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback time remaining: {0}", application.BackgroundTimeRemaining);
                FinalShutdown (null);
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback exit");
            });

            if (LoginHelpers.IsCurrentAccountSet () && LoginHelpers.HasFirstSyncCompleted (LoginHelpers.GetCurrentAccountId ())) {
                BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (LoginHelpers.GetCurrentAccountId ());

                int accountId = LoginHelpers.GetCurrentAccountId ();
                switch (backEndState) {
                case BackEndStateEnum.CertAskWait:
                    CertAskReqCallback (accountId, null);
                    Log.Info (Log.LOG_STATE, "OnActived: CERTASKCALLBACK ");
                    break;
                case BackEndStateEnum.CredWait:
                    CredReqCallback (accountId);
                    Log.Info (Log.LOG_STATE, "OnActived: CREDCALLBACK ");
                    break;
                case BackEndStateEnum.ServerConfWait:
                    ServConfReqCallback (accountId);
                    Log.Info (Log.LOG_STATE, "OnActived: SERVCONFCALLBACK ");
                    break;
                default:
                    LoginHelpers.SetDoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId (), false);
                    break;
                }
            }
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: Exit");
        }

        //
        // This method is invoked when the application is about to move from active to inactive state,
        // And also when moving from foreground to background.
        //
        // OpenGL applications should use this method to pause.
        //
        public override void OnResignActivation (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: time remaining: {0}", application.BackgroundTimeRemaining);
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            BadgeNotifGoInactive ();
            NcApplication.Instance.StatusIndEvent += BgStatusIndReceiver;

            NcApplication.Instance.StopClass4Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: StopClass4Services complete");

            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: Exit");
        }

        private void FinalShutdown (object opaque)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Called");
            if (null != opaque && (int)opaque != ShutdownCounter) {
                Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Stale");
                return;
            }
            NcApplication.Instance.StopBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: StopBasalServices complete");
            if (0 < BackgroundIosTaskId) {
                UIApplication.SharedApplication.EndBackgroundTask (BackgroundIosTaskId);
                BackgroundIosTaskId = -1;
            }
            FinalShutdownHasHappened = true;
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Exit");
        }

        private void ReverseFinalShutdown ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Called");
            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: StartBasalServices complete");
            FinalShutdownHasHappened = false;
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Exit");
        }

        // This method should be used to release shared resources and it should store the application state.
        // If your application supports background exection this method is called instead of WillTerminate
        // when the user quits.
        public override void DidEnterBackground (UIApplication application)
        {
            if (DidEnterBackgroundCalled) {
                Log.Warn (Log.LOG_LIFECYCLE, "DidEnterBackground: called more than once.");
                return;
            }
            DidEnterBackgroundCalled = true;
            var timeRemaining = application.BackgroundTimeRemaining;
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: time remaining: {0}", timeRemaining);
            // XAMMIT: sometimes BackgroundTimeRemaining reads as MAXFLOAT.
            if (25.0 > timeRemaining || 60 * 10 < timeRemaining) {
                FinalShutdown (null);
            } else {
                var secs = timeRemaining - 20.0;
                ShutdownTimer = new Timer ((opaque) => {
                    InvokeOnUIThread.Instance.Invoke (delegate {
                        FinalShutdown (opaque);
                    });
                }, ShutdownCounter, (int)(secs * 1000), Timeout.Infinite);
                Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: ShutdownTimer for {0}s", secs);
            }
            var imageView = new UIImageView (Window.Frame);
            imageView.Tag = 653;    // Give some decent tagvalue or keep a reference of imageView in self
            /* As A security Measure we may do something like this here
             * var imageView = new UIImageView(UIImage.FromBundle("Launch-BG.png"));
             * to keep the email image from being cached and potentially readable by 
             * others
             * imageView.BackgroundColor = UIColor.Red;
            */

            UIApplication.SharedApplication.KeyWindow.AddSubview (imageView);
            UIApplication.SharedApplication.KeyWindow.BringSubviewToFront (imageView);
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: Exit");
        }

        // This method is called as part of the transiton from background to active state.
        public override void WillEnterForeground (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: Called");
            DidEnterBackgroundCalled = false;
            if (doingPerformFetch) {
                CompletePerformFetchWithoutShutdown ();
            }
            Interlocked.Increment (ref ShutdownCounter);
            if (null != ShutdownTimer) {
                ShutdownTimer.Dispose ();
                ShutdownTimer = null;
            }
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
            }
            Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: Cleanup complete");

            var imageView = UIApplication.SharedApplication.KeyWindow.ViewWithTag (653);
            if (null != imageView) {
                imageView.RemoveFromSuperview ();
            } else {
                Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: failed to find red view");
            }
            Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: Exit");
        }

        // This method is called when the application is about to terminate. Save data, if needed.
        // There is no guarantee that this function gets called.
        public override void WillTerminate (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Called");
            FinalShutdown (null);
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Exit");
        }

        /// <summary>
        /// Code to implement iOS-7 background-fetch.
        /// </summary>/
        private bool doingPerformFetch = false;
        private Action<UIBackgroundFetchResult> CompletionHandler = null;
        private UIBackgroundFetchResult fetchResult;
        private Timer performFetchTimer = null;
        private bool fetchComplete;
        private bool pushAssistArmComplete;
        private string fetchCause;

        private void FetchStatusHandler (object sender, EventArgs e)
        {
            // TODO - need to wait for ALL accounts to complete, not just 1st!
            StatusIndEventArgs statusEvent = (StatusIndEventArgs)e;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_NewUnreadEmailMessageInInbox");
                fetchResult = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded");
                fetchComplete = true;
                BadgeNotifUpdate ();
                if (fetchComplete && pushAssistArmComplete) {
                    CompletePerformFetch ();
                }
                break;

            case NcResult.SubKindEnum.Info_PushAssistArmed:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_PushAssistArmed");
                pushAssistArmComplete = true;
                if (fetchComplete && pushAssistArmComplete) {
                    CompletePerformFetch ();
                }
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed");
                fetchResult = UIBackgroundFetchResult.Failed;
                CompletePerformFetch ();
                break;

            case NcResult.SubKindEnum.Error_SyncFailedToComplete:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailedToComplete");
                BadgeNotifUpdate ();
                CompletePerformFetch ();
                break;
            }
        }

        /// Status bar height can change when the user is on a call or using navigation
        public override void ChangedStatusBarFrame (UIApplication application, CGRect oldStatusBarFrame)
        {
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_StatusBarHeightChanged),
            });
        }

        protected void FinalizePerformFetch (UIBackgroundFetchResult result)
        {
            var handler = CompletionHandler;
            CompletionHandler = null;
            fetchCause = null;
            handler (result);
        }

        protected void CleanupPerformFetchStates ()
        {
            performFetchTimer.Dispose ();
            performFetchTimer = null;
            NcApplication.Instance.StatusIndEvent -= FetchStatusHandler;
            doingPerformFetch = false;
        }

        protected void CompletePerformFetchWithoutShutdown ()
        {
            CleanupPerformFetchStates ();
            FinalizePerformFetch (fetchResult);
        }

        protected void CompletePerformFetch ()
        {
            CleanupPerformFetchStates ();
            FinalShutdown (null);
            FinalizePerformFetch (fetchResult);
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_LIFECYCLE, "PerformFetch called.");
            StartFetch (application, completionHandler, "PF");
        }

        protected void StartFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler, string cause)
        {
            if (doingPerformFetch) {
                Log.Info (Log.LOG_LIFECYCLE, "PerformFetch was called while a previous PerformFetch was still running. This shouldn't happen.");
                CompletePerformFetchWithoutShutdown ();
            }
            CompletionHandler = completionHandler;
            fetchComplete = false;
            pushAssistArmComplete = false;
            fetchCause = cause;
            fetchResult = UIBackgroundFetchResult.NoData;
            // Need to set ExecutionContext before Start of BE so that strategy can see it.
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.QuickSync;
            NcApplication.Instance.UnmarkStartup ();
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
            }
            NcApplication.Instance.StatusIndEvent += FetchStatusHandler;
            // iOS only allows a limited amount of time to fetch data in the background.
            // Set a timer to force everything to shut down before iOS kills the app.
            performFetchTimer = new Timer (((object state) => {
                // When the timer expires, just fire an event.  The status callback will take
                // care of shutting everything down.
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Account = NcApplication.Instance.Account,
                    Status = NcResult.Error (NcResult.SubKindEnum.Error_SyncFailedToComplete)
                });
            }), null, KPerformFetchTimeoutSeconds * 1000, Timeout.Infinite);
            doingPerformFetch = true;
        }

        protected void SaveNotification (string traceMessage, string key, nint id)
        {
            Log.Info (Log.LOG_LIFECYCLE, "{0}: {1} id is {2}.", traceMessage, key, id);

            var devAccountId = McAccount.GetDeviceAccount ().Id;
            McMutables.Set (devAccountId, key, NcApplication.Instance.Account.Id.ToString (), id.ToString ());
        }

        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            var emailMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey);
            var eventMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey);

            var emailNotification = (NSNumber)notification.UserInfo.ObjectForKey (EmailNotificationKey);
            var eventNotification = (NSNumber)notification.UserInfo.ObjectForKey (EventNotificationKey);

            // The app is 'active' if it is already running when the local notification
            // arrives or if the app is started when a local notification is delivered.
            // When the app is started by a notification, FinishedLauching adds mutables.
            if (UIApplicationState.Active == UIApplication.SharedApplication.ApplicationState) {
                // If the app is started by FinishedLaunching, it adds some mutables
                if ((0 == emailMutables.Count) && (0 == eventMutables.Count)) {
                    // Now we know that the app was already running.  In this case,
                    // we notify the user of the upcoming event with an alert view.
                    if (null != eventNotification) {
                        var eventId = eventNotification.ToMcModelIndex ();
                        var eventItem = McEvent.QueryById<McEvent> (eventId);
                        if (null != eventItem) {
                            var calendarItem = McCalendar.QueryById<McCalendar> (eventItem.CalendarId);
                            if (null != calendarItem) {
                                var subject = Pretty.SubjectString (calendarItem.Subject);
                                if (!String.IsNullOrEmpty (subject)) {
                                    subject += " ";
                                } else {
                                    subject = "Event at ";
                                }
                                var msg = subject + Pretty.FullDateTimeString (eventItem.StartTime);
                                UIAlertView alert = new UIAlertView ("Reminder", msg, null, "OK", null);

                                alert.Show ();
                            }
                        }
                    }
                    return;
                }
            }

            // Look for the NachoTabBarController.  It's normally in Window.RootViewController.  Except when the
            // app was launched as a fresh install, in which case we have to look deeper.  And if a notification
            // arrives while database migration is in progress, the NachoTabBarController might not exist at all.
            NachoTabBarController nachoTabBarController = null;
            if (Window.RootViewController is NachoTabBarController) {
                nachoTabBarController = (NachoTabBarController)Window.RootViewController;
            } else if (null != Window.RootViewController) {
                if (Window.RootViewController.PresentedViewController is NachoTabBarController) {
                    nachoTabBarController = (NachoTabBarController)Window.RootViewController.PresentedViewController;
                } else if (null != Window.RootViewController.PresentedViewController && Window.RootViewController.PresentedViewController.TabBarController is NachoTabBarController) {
                    nachoTabBarController = (NachoTabBarController)Window.RootViewController.PresentedViewController.TabBarController;
                }
            }
            if (null == nachoTabBarController) {
                Log.Error (Log.LOG_LIFECYCLE, "The NachoTabBarController could not be found.  Handling of the notification will be delayed or skipped.");
            }
                
            if (null != emailNotification) {
                var emailMessageId = emailNotification.ToMcModelIndex ();
                SaveNotification ("ReceivedLocalNotification", EmailNotificationKey, emailMessageId);
                if (null != nachoTabBarController) {
                    nachoTabBarController.SwitchToNachoNow ();
                }
            }
            if (null != eventNotification) {
                var eventId = eventNotification.ToMcModelIndex ();
                SaveNotification ("ReceivedLocalNotification", EventNotificationKey, eventId);
                if (null != nachoTabBarController) {
                    nachoTabBarController.SwitchToNachoNow ();
                }
            }
            if ((null == emailNotification) && (null == eventNotification)) {
                Log.Error (Log.LOG_LIFECYCLE, "ReceivedLocalNotification: received unknown notification");
            }
        }

        public void BgStatusIndReceiver (object sender, EventArgs e)
        {
            StatusIndEventArgs ea = (StatusIndEventArgs)e;
            // Use Info_SyncSucceeded rather than Info_NewUnreadEmailMessageInInbox because
            // we want to remove a notification if the server marks a message as read.
            if (NcResult.SubKindEnum.Info_SyncSucceeded == ea.Status.SubKind) {
                BadgeNotifUpdate ();
            }
        }

        public void CredReqCallback (int accountId)
        {
            Log.Info (Log.LOG_UI, "CredReqCallback Called for account: {0}", accountId);

            hasFirstSyncCompleted = LoginHelpers.HasFirstSyncCompleted (accountId); 
            if (hasFirstSyncCompleted == false) {
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_CredReqCallback),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            } else {
                DisplayCredentialsFixView ();
            }
        }


        public void ServConfReqCallback (int accountId)
        {
            Log.Info (Log.LOG_UI, "ServConfReqCallback Called for account: {0}", accountId);

            // TODO Make use of the MX information that was gathered during auto-d.
            // It can be found at BackEnd.Instance.AutoDInfo(accountId).

            hasFirstSyncCompleted = LoginHelpers.HasFirstSyncCompleted (accountId); 
            if (hasFirstSyncCompleted == false) {
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Error_ServerConfReqCallback),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            } else {

                // called if server name is wrong
                // cancel should call "exit program, enter new server name should be updated server

                LoginHelpers.SetDoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId (), true);

                var Mo = NcModel.Instance;
                var Be = BackEnd.Instance;

                var credView = new UIAlertView ();

                credView.Title = "Need Correct Server Name";
                credView.AddButton ("Update");
                credView.AddButton ("Cancel");
                credView.AlertViewStyle = UIAlertViewStyle.PlainTextInput;
                credView.Show ();
                credView.Clicked += delegate(object a, UIButtonEventArgs b) {
                    var parent = (UIAlertView)a;
                    if (b.ButtonIndex == 0) {

                        LoginHelpers.SetDoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId (), false);

                        var txt = parent.GetTextField (0).Text;
                        // FIXME need to scan string to make sure it is of right format (regex).
                        if (txt != null && NachoCore.Utils.Uri_Helpers.IsValidHost (txt)) {
                            Log.Info (Log.LOG_LIFECYCLE, " New Server Name = " + txt);
                            NcModel.Instance.RunInTransaction (() => {
                                var tmpServer = McServer.QueryByAccountId<McServer> (accountId).SingleOrDefault ();
                                if (null == tmpServer) {
                                    tmpServer = new McServer () {
                                        Host = txt,
                                    };
                                    tmpServer.Insert ();
                                } else {
                                    tmpServer.Host = txt;
                                    tmpServer.Update ();
                                }
                            });
                            Be.ServerConfResp (accountId, false); 
                            credView.ResignFirstResponder ();
                        }
                        ;
                    }
                    ;

                    if (b.ButtonIndex == 1) {
                        var gonnaquit = new UIAlertView ();
                        gonnaquit.Title = "Are You Sure? \n No account information will be updated";

                        gonnaquit.AddButton ("Ok"); // continue exiting
                        gonnaquit.AddButton ("Go Back"); // enter info
                        gonnaquit.CancelButtonIndex = 1;
                        gonnaquit.Show ();
                        gonnaquit.Clicked += delegate(object sender, UIButtonEventArgs e) {
                            if (e.ButtonIndex == 1) {
                                ServConfReqCallback (accountId); // go again
                            }
                            gonnaquit.ResignFirstResponder ();
                        };
                    }
                    ;
                };
            }
        }

        public void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);

            hasFirstSyncCompleted = LoginHelpers.HasFirstSyncCompleted (accountId);
            if (hasFirstSyncCompleted == false) {
                Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Error_CertAskReqCallback),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            } else {
                // UI FIXME - ask user and call CertAskResp async'ly.
                DisplayCredentialsFixView ();
            }
        }

        protected void DisplayCredentialsFixView ()
        {
            LoginHelpers.SetDoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId (), true);

            UIStoryboard x = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            CredentialsAskViewController cvc = (CredentialsAskViewController)x.InstantiateViewController ("CredentialsAskViewController");
            cvc.SetTabBarController (Util.GetActiveTabBarOrNull ());
            this.Window.RootViewController.PresentViewController (cvc, true, null);
        }

        /* BADGE & NOTIFICATION LOGIC HERE.
         * - OnActivated clears ALL notifications and the badge.
         * - When we are not in the active state, and we get an indication of a new, hot, and unread email message:
         *   - we create a local notification for that message.
         *   - we set the badge number to the count of all new, hot, and unread email messages that have arrived 
         *     after we left the active state.
         * NOTE: This code will need to get a little smarter when we are doing many types of notification.
         */
        static public NSString EmailNotificationKey = new NSString ("McEmailMessage.Id");
        static public NSString EventNotificationKey = new NSString ("NotifiOS.handle");

        private bool BadgeNotifAllowed = true;

        private void BadgeNotifClear ()
        {
            UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
            BadgeNotifAllowed = false;
            Log.Info (Log.LOG_UI, "BadgeNotifClear: exit");
        }

        private void BadgeNotifGoInactive ()
        {
            McMutables.Set (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            BadgeNotifAllowed = true;
            Log.Info (Log.LOG_UI, "BadgeNotifGoInactive: exit");
        }

        private bool NotifyEmailMessage (McEmailMessage message, bool withSound)
        {
            if (null == message) {
                return false;
            }
            if (String.IsNullOrEmpty (message.From)) {
                // Don't notify or count in badge number from-me messages.
                Log.Info (Log.LOG_UI, "Not notifying on to-{0} message.", NcApplication.Instance.Account.EmailAddr);
                return false;
            }

            var fromString = Pretty.SenderString (message.From);
            var subjectString = Pretty.SubjectString (message.Subject);
            if (!String.IsNullOrEmpty (subjectString)) {
                subjectString += " ";
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                var cause = (null == fetchCause ? "BG" : fetchCause);
                subjectString += String.Format ("[{0}:{1:N1}s]", cause, latency);
            }

            if (NotificationCanAlert) {
                var notif = new UILocalNotification ();
                var className = NachoPlatformBinding.PlatformProcess.GetClassName (notif.Handle);
                if (("UIConcreteLocalNotification" != className) && ("UILocalNotification" != className)) {
                    Log.Error (Log.LOG_UI, "Get an object of unknown type {0}", className);
                    return true;
                }
                notif.AlertAction = null;
                notif.AlertTitle = fromString;
                notif.AlertBody = subjectString;
                notif.UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (message.Id), EmailNotificationKey);
                if (withSound) {
                    if (NotificationCanSound) {
                        notif.SoundName = UILocalNotification.DefaultSoundName;
                    } else {
                        Log.Warn (Log.LOG_UI, "No permission to play sound. (emailMessageId={0})", message.Id);
                    }
                }
                UIApplication.SharedApplication.ScheduleLocalNotification (notif);
            } else {
                Log.Warn (Log.LOG_UI, "No permission to badge. (emailMessageId={0})", message.Id);
            }

            return true;
        }

        // It is okay if this function is called more than it needs to be.
        private void BadgeNotifUpdate ()
        {
            Log.Info (Log.LOG_UI, "BadgeNotifUpdate: called");
            if (!BadgeNotifAllowed) {
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: early exit");
                return;
            }

            var datestring = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            var since = DateTime.Parse (datestring);
            var unreadAndHot = McEmailMessage.QueryUnreadAndHotAfter (since);
            var badgeCount = unreadAndHot.Count ();
            var soundExpressed = false;
            int remainingVisibleSlots = 10;

            foreach (var message in unreadAndHot) {
                if (message.HasBeenNotified) {
                    continue;
                }
                if (!NotifyEmailMessage (message, !soundExpressed)) {
                    --badgeCount;
                    continue;
                } else {
                    soundExpressed = true;
                }

                message.HasBeenNotified = true;
                message.Update ();
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: ScheduleLocalNotification");
                --remainingVisibleSlots;
                if (0 >= remainingVisibleSlots) {
                    break;
                }
            }

            if (NotificationCanBadge) {
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = badgeCount;
            } else {
                Log.Info (Log.LOG_UI, "Skip badging due to lack of user permission.");
            }
        }

        public static void TestScheduleEmailNotification ()
        {
            var list = NcEmailManager.PriorityInbox ();
            var thread = list.GetEmailThread (0);
            var message = thread.FirstMessageSpecialCase ();
            var notif = new UILocalNotification () {
                AlertAction = null,
                AlertBody = ((null == message.Subject) ? "(No Subject)" : message.Subject) + ", From " + message.From,
                UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (message.Id), EmailNotificationKey),
                FireDate = NSDate.FromTimeIntervalSinceNow (15),
            };
            notif.SoundName = UILocalNotification.DefaultSoundName;
            UIApplication.SharedApplication.ScheduleLocalNotification (notif);
        }


        public static void TestScheduleCalendarNotification ()
        {
            var e = NcModel.Instance.Db.Table<McEvent> ().Last ();
            var c = McCalendar.QueryById<McCalendar> (e.CalendarId);
            var notif = new UILocalNotification () {
                AlertAction = null,
                AlertBody = c.Subject + Pretty.ReminderTime (new TimeSpan (0, 7, 0)),
                UserInfo = NSDictionary.FromObjectAndKey (NSNumber.FromInt32 (c.Id), EventNotificationKey),
                FireDate = NSDate.FromTimeIntervalSinceNow (15),
            };
            notif.SoundName = UILocalNotification.DefaultSoundName;
            UIApplication.SharedApplication.ScheduleLocalNotification (notif);
        }

        public override void ReceiveMemoryWarning (UIApplication application)
        {
            Log.Info (Log.LOG_SYS, "ReceiveMemoryWarning;");
            Log.Info (Log.LOG_SYS, "Monitor: NSURLCache usage {0}", NSUrlCache.SharedCache.CurrentMemoryUsage);
            NcApplication.Instance.MonitorReport ();
        }

        public override void ApplicationSignificantTimeChange (UIApplication application)
        {
            // This is called in a variety of situations, including at midnight, when changing to or from
            // daylight saving time, or when the system time changes.  We are only interested in time zone
            // changes, so check for that before invoking the app's time zone change status indicator.
            var oldLocal = TimeZoneInfo.Local;
            TimeZoneInfo.ClearCachedData ();
            var newLocal = TimeZoneInfo.Local;
            if (oldLocal.Id != newLocal.Id || oldLocal.BaseUtcOffset != newLocal.BaseUtcOffset) {
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Account = ConstMcAccount.NotAccountSpecific,
                    Status = NcResult.Info (NcResult.SubKindEnum.Info_SystemTimeZoneChanged),
                });
            }
        }

    }

    public class HockeyAppCrashDelegate : BITCrashManagerDelegate
    {
        private bool IsDevelopmentBuild {
            get {
                return BuildInfo.Version.StartsWith ("DEV");
            }
        }

        public HockeyAppCrashDelegate () : base ()
        {
        }

        public override string ApplicationLogForCrashManager (BITCrashManager crashManager)
        {
            string launchTime = String.Format ("{0:O}", DateTime.UtcNow);
            string log = String.Format ("Version: {0}\nBuild Number: {1}\nLaunch Time: {2}\nDevice ID: {3}\n",
                             BuildInfo.Version, BuildInfo.BuildNumber, launchTime, Device.Instance.Identity ());
            if (IsDevelopmentBuild) {
                log += String.Format ("Build Time: {0}\nBuild User: {1}\n" +
                "Source: {2}\n", BuildInfo.Time, BuildInfo.User, BuildInfo.Source);
            }
            return log;
        }

        /// For some reason, UserName in HockeyApp web portal has a UUID prefixing the user name.
        /// On a narrow or normal browser window width, the user name is hidden. So, repeat it
        /// in contact again.
        private string UserName ()
        {
            string userName = null;
            if (IsDevelopmentBuild) {
                userName = BuildInfo.User;
            }
            return userName;
        }

        public override string UserEmailForCrashManager (BITCrashManager crashManager)
        {
            return UserName ();
        }

        public override string UserNameForCrashManager (BITCrashManager crashManager)
        {
            return UserName ();
        }
    }

    public class HockeyAppAuthenticatorDelegate : BITAuthenticatorDelegate
    {
        public override void WillShowAuthenticationController (BITAuthenticator authenticator, UIViewController viewController)
        {
            this.BeginInvokeOnMainThread (() => {
                bool done = false;

                UIAlertView av = new UIAlertView ();
                av.Title = "Authentication Required";
                av.Message = "In order to run this Nacho Mail beta client, you must authenticate with HockeyApp. " +
                "Please enter your HockeyApp credential in the next screen.";
                av.AddButton ("Continue");
                av.Clicked += (sender, buttonArgs) => {
                    done = true;
                };
                av.Show ();
                while (!done) {
                    NSRunLoop.Current.RunUntil (NSDate.FromTimeIntervalSinceNow (0.5));
                }
            });
        }
    }
}

