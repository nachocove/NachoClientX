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

        private const string NotificationActionIdentifierReply = "reply";
        private const string NotificationActionIdentifierMark = "mark";
        private const string NotificationActionIdentifierDelete = "delete";
        private const string NotificationActionIdentifierArchive = "archive";
        private const string NotificationCategoryIdentifierMessage = "message";
        private const string NotificationCategoryIdentifierChat = "chat";

        private ulong UserNotificationSettings {
            get {
                if (UIDevice.CurrentDevice.CheckSystemVersion (8, 0)) {
                    return (ulong)UIApplication.SharedApplication.CurrentUserNotificationSettings.Types;
                }
                // Older iOS does not have this property. So, just assume it's ok and let 
                // iOS to reject it.
                return (ulong)KNotificationSettings;
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

        private DateTime foregroundTime = DateTime.MinValue;

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
                manager.Configure (BuildInfo.HockeyAppAppId, new HockeyAppCrashDelegate ());

                // Enable automatic reporting
                manager.CrashManager.CrashManagerStatus = BITCrashManagerStatus.AutoSend;
                manager.CrashManager.EnableOnDeviceSymbolication = false;
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
            hasRegisteredForRemoteNotifications = true;
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
            Log.Info (Log.LOG_LIFECYCLE, "[PA] Got remote notification - {0}", userInfo);

            // Amazingly, it turns out the most programmatically simple way to convert a NSDictionary
            // to our own model objects.
            NSError error;
            var jsonData = NSJsonSerialization.Serialize (userInfo, NSJsonWritingOptions.PrettyPrinted, out error);
            var jsonStr = (string)NSString.FromData (jsonData, NSStringEncoding.UTF8);
            var notification = JsonConvert.DeserializeObject<Notification> (jsonStr);
            if (notification.HasPingerSection ()) {
                if (!PushAssist.ProcessRemoteNotification (notification.pinger, (accountId) => {
                    if (NcApplication.Instance.IsForeground) {
                        var inbox = NcEmailManager.PriorityInbox (accountId);
                        inbox.StartSync ();
                    }
                })) {
                    // Can't find any account matching those contexts. Abort immediately
                    completionHandler (UIBackgroundFetchResult.NoData);
                    return;
                }
                if (NcApplication.Instance.IsForeground) {
                    completionHandler (UIBackgroundFetchResult.NewData);
                } else {
                    if (doingPerformFetch) {
                        Log.Warn (Log.LOG_PUSH, "A perform fetch is already in progress. Do not start another one.");
                        completionHandler (UIBackgroundFetchResult.NewData);
                    } else {
                        StartFetch (application, completionHandler, "RN");
                    }
                }
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

            // Alert views are monitored inside NcAlertView

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

            NachoUIMonitor.SetupUITableView (delegate(string description, string operation) {
                Telemetry.RecordUiTableView (description, operation);
            });
        }

        // This method is common to both launching into the background and into the foreground.
        // It gets called once during the app lifecycle.
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            // move data files to Documents/Data if needed
            NachoPlatform.DataFileMigration.MigrateDataFilesIfNeeded ();
            // One-time initialization that do not need to be shut down later.
            if (!FirstLaunchInitialization) {
                FirstLaunchInitialization = true;
                StartCrashReporting ();
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartCrashReporting complete");

                ServerCertificatePeek.Initialize ();
                StartUIMonitor ();

                NcApplication.Instance.CredReqCallback = CredReqCallback;
                NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
                NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;
                MdmConfig.Instance.ExtractValues ();
            }

            CopyResourcesToDocuments ();

            if ((null != launchOptions) && launchOptions.ContainsKey (UIApplication.LaunchOptionsRemoteNotificationKey)) {
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Remote notification");
            }

            if (null == NcApplication.Instance.CrashFolder) {
                var cacheFolder = NSSearchPath.GetDirectories (NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User, true) [0];
                NcApplication.Instance.CrashFolder = Path.Combine (cacheFolder, "net.hockeyapp.sdk.ios");
                NcApplication.Instance.MarkStartup ();
            }

            NcApplication.Instance.ContinueRemoveAccountIfNeeded ();

            NcTimeStamp.Add ("Before Log");
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Called");
            NcTimeStamp.Add ("After Log, before PlatformIndication");
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            NcTimeStamp.Add ("After PlatformIndication");
            NcTimeStamp.Dump ();

            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartBasalServices complete");

            NcApplication.Instance.AppStartupTasks ();

            application.SetStatusBarStyle (UIStatusBarStyle.LightContent, true);

            UINavigationBar.Appearance.BarTintColor = A.Color_NachoGreen;
            UINavigationBar.Appearance.ShadowImage = new UIImage ();
            UIToolbar.Appearance.BackgroundColor = UIColor.White;
            UIBarButtonItem.Appearance.TintColor = A.Color_NachoBlue;

            var navigationTitleTextAttributes = new UITextAttributes ();
            navigationTitleTextAttributes.Font = A.Font_AvenirNextDemiBold17;
            navigationTitleTextAttributes.TextColor = UIColor.White;
            UINavigationBar.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes);
            using (var arrow = UIImage.FromFile ("nav-backarrow")) {
                UINavigationBar.Appearance.BackIndicatorImage = arrow;
                UINavigationBar.Appearance.BackIndicatorTransitionMaskImage = arrow;
            }
            UIBarButtonItem.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes, UIControlState.Normal);
            if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerUserNotificationSettings:"))) {
                // iOS 8 and after
                var replyAction = new UIMutableUserNotificationAction ();
                replyAction.ActivationMode = UIUserNotificationActivationMode.Foreground;
                replyAction.Identifier = NotificationActionIdentifierReply;
                replyAction.Title = "Reply";
                var markAction = new UIMutableUserNotificationAction ();
                markAction.ActivationMode = UIUserNotificationActivationMode.Background;
                markAction.Identifier = NotificationActionIdentifierMark;
                markAction.Title = "Mark as Read";
                var archiveAction = new UIMutableUserNotificationAction ();
                archiveAction.ActivationMode = UIUserNotificationActivationMode.Background;
                archiveAction.Identifier = NotificationActionIdentifierArchive;
                archiveAction.Title = "Archive";
                var deleteAction = new UIMutableUserNotificationAction ();
                deleteAction.ActivationMode = UIUserNotificationActivationMode.Background;
                deleteAction.Identifier = NotificationActionIdentifierDelete;
                deleteAction.Title = "Delete";
                deleteAction.Destructive = true;
                var defaultActions = new UIUserNotificationAction[] { replyAction, markAction, archiveAction, deleteAction };
                var minimalActions = new UIUserNotificationAction[] { replyAction, markAction };
                var messageCategory = new UIMutableUserNotificationCategory ();
                messageCategory.Identifier = NotificationCategoryIdentifierMessage;
                messageCategory.SetActions (defaultActions, UIUserNotificationActionContext.Default);
                messageCategory.SetActions (minimalActions, UIUserNotificationActionContext.Minimal);
                var categories = new NSSet (messageCategory);
                var settings = UIUserNotificationSettings.GetSettingsForTypes (KNotificationSettings, categories);
                UIApplication.SharedApplication.RegisterUserNotificationSettings (settings);
                UIApplication.SharedApplication.RegisterForRemoteNotifications ();
            } else if (UIApplication.SharedApplication.RespondsToSelector (new Selector ("registerForRemoteNotificationTypes:"))) {
                UIApplication.SharedApplication.RegisterForRemoteNotificationTypes (UIRemoteNotificationType.NewsstandContentAvailability);
            } else {
                Log.Error (Log.LOG_PUSH, "notification not registered!");
            }
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);
            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new ObjCRuntime.Class (typeof(CidImageProtocol)));

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: iOS Cocoa setup complete");

            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                Telemetry.SharedInstance.Throttling = false;
            };

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication Class4LateShowEvent registered");

            // If launch options is set with the local notification key, that means the app has been started
            // by a local notification.  ReceivedLocalNotification will be called and it wants to know if the
            // app has just be started or if it was already running. We set flags so ReceivedLocalNotification
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
                var chatNotificationDictionary = (NSArray)localNotification.UserInfo.ObjectForKey (ChatNotificationKey);
                if (null != chatNotificationDictionary) {
                    var chatId = (chatNotificationDictionary.GetItem<NSNumber> (0)).NIntValue;
                    var messageId = (chatNotificationDictionary.GetItem<NSNumber> (1)).NIntValue;
                    SaveNotification ("FinishedLaunching", ChatNotificationKey, new nint[] {chatId, messageId});
                }
                if (localNotification != null) {
                    // reset badge
                    UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
                }
            }

            NcKeyboardSpy.Instance.Init ();

//            if (application.RespondsToSelector (new ObjCRuntime.Selector ("shortcutItems"))) {
//                application.ShortcutItems = new UIApplicationShortcutItem[] {
//                    new UIApplicationShortcutItem ("com.nachocove.nachomail.newmessage", "New Message", null, UIApplicationShortcutIcon.FromTemplateImageName ("shortcut-compose"), null)
//                };
//            }

            // I don't know where to put this.  It can't go in NcApplication.MonitorReport(), because
            // C#'s TimeZoneInfo.Local has an ID and name of "Local", which is not helpful.  It has
            // to be in iOS-specific code.
            Log.Info (Log.LOG_LIFECYCLE, "Current time zone: {0}", NSTimeZone.LocalTimeZone.Description);

            var firstStoryboardName = NcApplication.ReadyToStartUI () ? "MainStoryboard_iPhone" : "Startup";
            var mainStoryboard = UIStoryboard.FromName (firstStoryboardName, null);
            var appViewController = mainStoryboard.InstantiateInitialViewController ();

            Window = new UIWindow (UIScreen.MainScreen.Bounds);
            Window.RootViewController = appViewController;
            Window.MakeKeyAndVisible ();

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Exit");

            return true;
        }

        public void CopyResourcesToDocuments ()
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            string[] resources = { "nacho.html", "nacho.css", "nacho.js", "chat-email.html" };
            foreach (var resourceName in resources) {
                var resourcePath = NSBundle.MainBundle.PathForResource (resourceName, null);
                var destinationPath = Path.Combine (documentsPath, resourceName);
                if (!File.Exists (destinationPath) || File.GetLastWriteTime (destinationPath) < File.GetLastWriteTime (resourcePath)) {
                    if (File.Exists (destinationPath)) {
                        File.Delete (destinationPath);
                    }
                    File.Copy (resourcePath, destinationPath);
                    NcFileHandler.Instance.MarkFileForSkipBackup (destinationPath);
                }
            }
        }

        public override void PerformActionForShortcutItem (UIApplication application, UIApplicationShortcutItem shortcutItem, UIOperationHandler completionHandler)
        {
            if (shortcutItem.Type.Equals ("com.nachocove.nachomail.newmessage")) {
                // TODO: verify that we're not setting up an account
                // TODO: check if another compose is already up
                var composeViewController = new MessageComposeViewController (NcApplication.Instance.DefaultEmailAccount);
                composeViewController.Present (false, () => {
                    completionHandler (true);
                });
            } else {
                Log.Error (Log.LOG_UI, "Application received unknown shortcut action: {0}", shortcutItem.Type);
                completionHandler (false);
            }
        }

        /// <Docs>Reference to the UIApplication that invoked this delegate method.</Docs>
        /// <summary>
        ///  Called when another app opens-in a document to nacho mail
        /// </summary>
        public override bool OpenUrl (UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
        {
            Log.Info (Log.LOG_LIFECYCLE, "OpenUrl: {0} {1} {2}", application, url, annotation);
            var nachoSchemeObject = NSBundle.MainBundle.InfoDictionary.ObjectForKey (new NSString ("CFBundleIdentifier"));
            var nachoScheme = nachoSchemeObject.ToString ();

            if (url.IsFileUrl) {
                OpenFiles (new string[] { url.Path }, sourceApplication);
                return true;
            } else if (url.Scheme.Equals (nachoScheme)) {
                var components = url.PathComponents;
                if (components.Length > 1) {
                    if (components [1].Equals ("share") && components.Length > 2) {
                        var stashName = components [2];
                        var containerUrl = NSFileManager.DefaultManager.GetContainerUrl (BuildInfo.AppGroup);
                        if (containerUrl != null) {
                            var stashUrl = containerUrl.Append (stashName, true);
                            var paths = Directory.GetFiles (stashUrl.Path);
                            OpenFiles (paths, sourceApplication);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        void OpenFiles (string[] paths, string source = null)
        {
            if (NcApplication.ReadyToStartUI ()) {
                var account = NcApplication.Instance.DefaultEmailAccount;
                var attachments = new List<McAttachment> ();
                foreach (var path in paths) {
                    // We will be called here whether or not we were launched to Rx the file. So no need to handle in DFLwO.
                    var document = McDocument.InsertSaveStart (McAccount.GetDeviceAccount ().Id);
                    document.SetDisplayName (Path.GetFileName (path));
                    document.SourceApplication = source;
                    document.UpdateFileMove (path);
                    var attachment = McAttachment.InsertSaveStart (account.Id);
                    attachment.SetDisplayName (document.DisplayName);
                    attachment.UpdateFileCopy (document.GetFilePath ());
                    attachments.Add (attachment);
                }
                if (attachments.Count > 0) {
                    var composeViewController = new MessageComposeViewController (account);
                    composeViewController.Composer.InitialAttachments = attachments;
                    composeViewController.Present ();
                }
            }
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
            NotifyAllowed = false;
            UpdateBadge ();
            if (doingPerformFetch) {
                CompletePerformFetchWithoutShutdown ();
            }
            foregroundTime = DateTime.UtcNow;
            NcApplication.Instance.StatusIndEvent -= BgStatusIndReceiver;

            if (-1 != BackgroundIosTaskId) {
                UIApplication.SharedApplication.EndBackgroundTask (BackgroundIosTaskId);
            }
            BackgroundIosTaskId = UIApplication.SharedApplication.BeginBackgroundTask (() => {
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback time remaining: {0:n2}", application.BackgroundTimeRemaining);
                FinalShutdown (null);
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback exit");
            });

            NcApplication.Instance.ContinueOnActivation ();

            NcTask.Run (NcApplication.Instance.CheckNotified, "CheckNotified");
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
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: Called");
            bool isInitializing = NcApplication.Instance.IsInitializing;
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            UpdateGoInactiveTime ();
            UpdateBadge ();
            NotifyAllowed = true;
            NcApplication.Instance.StatusIndEvent += BgStatusIndReceiver;

            if (DateTime.MinValue != foregroundTime) {
                // Log the duration of foreground for usage analytics
                var duration = (int)(DateTime.UtcNow - foregroundTime).TotalMilliseconds;
                Telemetry.RecordIntTimeSeries ("Client.Foreground.Duration", foregroundTime, duration);
                foregroundTime = DateTime.MinValue;
            }

            if (!isInitializing) {
                NcApplication.Instance.StopClass4Services ();
            }
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
            Log.Info (Log.LOG_PUSH, "[PA] finalshutdown: client_id={0}", NcApplication.Instance.ClientId);
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
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: time remaining: {0:n2}", timeRemaining);
            if (25.0 > timeRemaining) {
                FinalShutdown (null);
            } else {
                var didShutdown = false;
                ShutdownTimer = new Timer ((opaque) => {
                    InvokeOnUIThread.Instance.Invoke (delegate {
                        // check remaining background time. If too little, shut us down.
                        // iOS caveat: BackgroundTimeRemaining can be MAX_DOUBLE early on.
                        // It also seems to return to MAX_DOUBLE value after we call EndBackgroundTask().
                        var remaining = application.BackgroundTimeRemaining;
                        Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer: time remaining: {0:n2}", remaining);
                        if (!didShutdown && 25.0 > remaining) {
                            didShutdown = true;
                            FinalShutdown (opaque);
                            try {
                                // This seems to work, but we do get some extra callbacks after Change().
                                ShutdownTimer.Change (Timeout.Infinite, Timeout.Infinite);
                            } catch (Exception ex) {
                                // Wrapper to protect against unknown C# timer stupidity.
                                Log.Error (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer exception: {0}", ex);
                            }
                        }
                    });
                }, ShutdownCounter, 1000, 1000);
                Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: ShutdownTimer");
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
            Interlocked.Increment (ref ShutdownCounter);
            if (null != ShutdownTimer) {
                ShutdownTimer.Dispose ();
                ShutdownTimer = null;
            }
            if (doingPerformFetch) {
                CompletePerformFetchWithoutShutdown ();
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

        private bool doingPerformFetch = false;
        private Action<UIBackgroundFetchResult> CompletionHandler = null;
        private UIBackgroundFetchResult fetchResult;
        private Timer performFetchTimer = null;
        private string fetchCause;
        // A list of all account ids that are waiting to be synced.
        private List<int> fetchAccounts;
        // A list of all accounts ids that are waiting for push assist to set up
        private List<int> pushAccounts;
        // PushAssist is active only when the app is registered for remote notifications
        private bool hasRegisteredForRemoteNotifications = false;

        private bool fetchComplete {
            get {
                return (0 == fetchAccounts.Count);
            }
        }

        private bool pushAssistArmComplete {
            get {
                return !hasRegisteredForRemoteNotifications || (0 == pushAccounts.Count);
            }
        }

        private void FetchStatusHandler (object sender, EventArgs e)
        {
            StatusIndEventArgs statusEvent = (StatusIndEventArgs)e;
            int accountId = (null != statusEvent.Account) ? statusEvent.Account.Id : -1;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_NewUnreadEmailMessageInInbox account {0}", accountId);
                fetchResult = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded for unspecified account {0}", accountId);
                }
                bool fetchWasComplete = fetchComplete;
                fetchAccounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded account {0}. {1} accounts and {2} push assists remaining.",
                    accountId, fetchAccounts.Count, pushAccounts.Count);
                if (fetchComplete) {
                    // There will sometimes be duplicate Info_SyncSucceeded for an account.
                    // Only call BadgeNotifUpdate once.
                    if (!fetchWasComplete) {
                        BadgeNotifUpdate ();
                    }
                    if (pushAssistArmComplete) {
                        CompletePerformFetch ();
                    }
                }
                break;

            case NcResult.SubKindEnum.Info_PushAssistArmed:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_PushAssistArmed for unspecified account {0}", accountId);
                }
                pushAccounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_PushAssistArmed account {0}. {1} accounts and {2} push assists remaining.",
                    accountId, fetchAccounts.Count, pushAccounts.Count);
                if (fetchComplete && pushAssistArmComplete) {
                    CompletePerformFetch ();
                }
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed for unspecified account {0}", accountId);
                }
                fetchAccounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed account {0}. {1} accounts and {2} push assists remaining.",
                    accountId, fetchAccounts.Count, pushAccounts.Count);
                // If one account found some new messages and a different account failed to sync,
                // return a successful result.
                if (UIBackgroundFetchResult.NoData == fetchResult) {
                    fetchResult = UIBackgroundFetchResult.Failed;
                }
                if (fetchComplete) {
                    BadgeNotifUpdate ();
                    if (pushAssistArmComplete) {
                        CompletePerformFetch ();
                    }
                }
                break;

            case NcResult.SubKindEnum.Error_SyncFailedToComplete:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailedToComplete");
                // Stop the back end first, so that any accounts still running will give up the CPU
                // as soon as possible.
                BackEnd.Instance.Stop ();
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
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
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

            // Crashes while launching in the background shouldn't increment the safe mode counter.
            // (It would be nice if background launches could simply not increment the counter rather
            // than clear it completely, but that is not worth the effort.)
            NcApplication.Instance.UnmarkStartup ();

            CompletionHandler = completionHandler;
            // check to see if migrations need to run. If so, we shouldn't let the PerformFetch proceed!
            NcMigration.Setup ();
            if (NcMigration.WillStartService ()) {
                Log.Error (Log.LOG_SYS, "PerformFetch called while migrations still need to run.");
                FinalizePerformFetch (UIBackgroundFetchResult.NoData); // or UIBackgroundFetchResult.Failed?
                return;
            }
            fetchCause = cause;
            fetchResult = UIBackgroundFetchResult.NoData;

            fetchAccounts = McAccount.GetAllConfiguredNormalAccountIds ();
            if (hasRegisteredForRemoteNotifications) {
                pushAccounts = McAccount.GetAllConfiguredNormalAccountIds ();
            } else {
                pushAccounts = new List<int> ();
            }
            // Need to set ExecutionContext before Start of BE so that strategy can see it.
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.QuickSync;
            NcApplication.Instance.UnmarkStartup ();
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
                BackEnd.Instance.Start ();
                NcApplicationMonitor.Instance.Start (1, 60);
            } else {
                NcCommStatus.Instance.Reset ("StartFetch");
            }
            NcApplication.Instance.StatusIndEvent += FetchStatusHandler;
            // iOS only allows a limited amount of time to fetch data in the background.
            // Set a timer to force everything to shut down before iOS kills the app.
            performFetchTimer = new Timer (((object state) => {
                // Just fire an event.  The listener for the event will take care of
                // shutting things down.  (The UI thread is the synchronization method
                // for lifecycle events, so the timer expiration needs to be channelled
                // through the UI thread.)
                Log.Info (Log.LOG_LIFECYCLE, "PerformFetch timer fired. Shutting down the app.");
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

            var devAccount = McAccount.GetDeviceAccount ();
            if (null != devAccount) {
                McMutables.Set (devAccount.Id, key, key, id.ToString ());
            }
        }

        protected void SaveNotification (string traceMessage, string key, nint[] id)
        {
            Log.Info (Log.LOG_LIFECYCLE, "{0}: {1} id is {2}.", traceMessage, key, id);

            var devAccount = McAccount.GetDeviceAccount ();
            if (null != devAccount) {
                McMutables.Set (devAccount.Id, key, key, String.Join<nint>(",", id));
            }
        }

        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            var emailMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey);
            var eventMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey);
            var chatMutables = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.ChatNotificationKey);

            var emailNotification = (NSNumber)notification.UserInfo.ObjectForKey (EmailNotificationKey);
            var eventNotification = (NSNumber)notification.UserInfo.ObjectForKey (EventNotificationKey);
            var chatNotification = (NSArray)notification.UserInfo.ObjectForKey (ChatNotificationKey);

            // The app is 'active' if it is already running when the local notification
            // arrives or if the app is started when a local notification is delivered.
            // When the app is started by a notification, FinishedLauching adds mutables.
            if (UIApplicationState.Active == UIApplication.SharedApplication.ApplicationState) {
                // If the app is started by FinishedLaunching, it adds some mutables
                if ((0 == emailMutables.Count) && (0 == eventMutables.Count) && (0 == chatMutables.Count)) {
                    // Now we know that the app was already running.  In this case,
                    // we notify the user of the upcoming event with an alert view.
                    string title = notification.AlertTitle;
                    string body = notification.AlertBody;
                    if (string.IsNullOrEmpty (title)) {
                        title = "Reminder";
                    } else if (body.StartsWith (title + ": ")) {
                        body = body.Substring (title.Length + 2);
                    }
                    new UIAlertView (title, body, null, "OK").Show ();
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
            if (null != chatNotification) {
                var chatId = ((NSNumber)chatNotification.GetItem<NSNumber>(0)).ToMcModelIndex ();
                var messageId = ((NSNumber)chatNotification.GetItem<NSNumber>(1)).ToMcModelIndex ();
                SaveNotification ("ReceivedLocalNotification", ChatNotificationKey, new nint[]{chatId, messageId});
                if (null != nachoTabBarController) {
                    nachoTabBarController.SwitchToNachoNow ();
                }
            }
            if ((null == emailNotification) && (null == eventNotification) && (null == chatNotification)) {
                Log.Error (Log.LOG_LIFECYCLE, "ReceivedLocalNotification: received unknown notification");
            }
        }

        public override void HandleAction (UIApplication application, string actionIdentifier, UILocalNotification localNotification, Action completionHandler)
        {
            var emailNotification = (NSNumber)localNotification.UserInfo.ObjectForKey (EmailNotificationKey);
            if (emailNotification != null) {
                var emailMessageId = emailNotification.ToMcModelIndex ();
                var thread = new McEmailMessageThread ();
                thread.FirstMessageId = emailMessageId;
                thread.MessageCount = 1;
                var message = thread.FirstMessageSpecialCase ();
                if (null != message) {
                    if (actionIdentifier == NotificationActionIdentifierReply) {
                        if (Window.RootViewController is NachoTabBarController) {
                            var account = McAccount.EmailAccountForMessage (message);
                            EmailHelper.MarkAsRead (thread, force: true);
                            var composeViewController = new MessageComposeViewController (account);
                            composeViewController.Composer.RelatedThread = thread;
                            composeViewController.Composer.Kind = EmailHelper.Action.Reply;
                            composeViewController.Present (false, null);
                        }
                    } else if (actionIdentifier == NotificationActionIdentifierArchive) {
                        NcEmailArchiver.Archive (message);
                        BadgeNotifUpdate ();
                    } else if (actionIdentifier == NotificationActionIdentifierMark) {
                        EmailHelper.MarkAsRead (thread, force: true);
                        BadgeNotifUpdate ();
                    } else if (actionIdentifier == NotificationActionIdentifierDelete) {
                        NcEmailArchiver.Delete (message);
                        BadgeNotifUpdate ();
                    } else {
                        NcAssert.CaseError ("Unknown notification action");
                    }
                }
            }
            completionHandler ();
        }

        public void BgStatusIndReceiver (object sender, EventArgs e)
        {
            StatusIndEventArgs ea = (StatusIndEventArgs)e;
            // Use Info_SyncSucceeded rather than Info_NewUnreadEmailMessageInInbox because
            // we want to remove a notification if the server marks a message as read.
            // When the app is in QuickSync mode, BadgeNotifUpdate will be called when
            // QuickSync is done.  There isn't a need to call it when each account's sync
            // completes.
            if (NcResult.SubKindEnum.Info_SyncSucceeded == ea.Status.SubKind && NcApplication.ExecutionContextEnum.QuickSync != NcApplication.Instance.ExecutionContext) {
                BadgeNotifUpdate ();
            }
        }

        public void CredReqCallback (int accountId)
        {
            Log.Info (Log.LOG_UI, "CredReqCallback Called for account: {0}", accountId);
            LoginHelpers.UserInterventionStateChanged (accountId);
        }

        public void ServConfReqCallback (int accountId, McAccount.AccountCapabilityEnum capabilities, object arg = null)
        {
            Log.Info (Log.LOG_UI, "ServConfReqCallback Called for account: {0} with arg {1}", accountId, arg);

            // TODO Make use of the MX information that was gathered during auto-d.
            // It can be found at BackEnd.Instance.AutoDInfo(accountId).

//            NcResult.WhyEnum why = NcResult.WhyEnum.NotSpecified;
//            switch ((uint)arg) {
//            case (uint) AsAutodiscoverCommand.AutoDFailureReason.CannotFindServer:
//                why = NcResult.WhyEnum.InvalidDest;
//                break;
//            case (uint) AsAutodiscoverCommand.AutoDFailureReason.CannotConnectToServer:
//                why = NcResult.WhyEnum.ServerError;
//                break;
//            default:
//                why = NcResult.WhyEnum.NotSpecified;
//                break;
//            }

            // called if server name is wrong
            // cancel should call "exit program, enter new server name should be updated server

            LoginHelpers.UserInterventionStateChanged (accountId);
        }

        public void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
            LoginHelpers.UserInterventionStateChanged (accountId);
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
        static public NSString ChatNotificationKey = new NSString ("McChat.Id,McChatMessage.MessageId");
        static public NSString EventNotificationKey = new NSString ("NotifiOS.handle");

        private bool NotifyAllowed = true;

        private void UpdateGoInactiveTime ()
        {
            McMutables.Set (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            Log.Info (Log.LOG_UI, "UpdateGoInactiveTime: exit");
        }

        private bool NotifyEmailMessage (McEmailMessage message, McAccount account, bool withSound)
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
            var previewString = Pretty.PreviewString (message.BodyPreview);
            if (!String.IsNullOrEmpty (subjectString)) {
                subjectString += " ";
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                var cause = (null == fetchCause ? "BG" : fetchCause);
                subjectString += String.Format ("[{0}:{1:N1}s]", cause, latency);
                Log.Info (Log.LOG_PUSH, "[PA] notify email message: client_id={0}, message_id={1}, cause={2}, delay={3}",
                    NcApplication.Instance.ClientId, message.Id, cause, latency);
            }

            if (NotificationCanAlert) {
                var notif = new UILocalNotification ();
                notif.Category = NotificationCategoryIdentifierMessage;
                notif.AlertBody = String.Format ("{0}\n{1}\n{2}", fromString, subjectString, previewString);
                if (notif.RespondsToSelector (new Selector ("setAlertTitle:"))) {
                    notif.AlertTitle = "New Email";
                }
                notif.AlertAction = null;
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

        private bool NotifyChatMessage (NcChatMessage message, McChat chat, McAccount account, bool withSound)
        {
            var fromString = Pretty.SenderString (message.From);
            var bundle = new NcEmailMessageBundle (message);
            string preview = bundle.TopText;
            if (String.IsNullOrWhiteSpace (preview)) {
                preview = message.BodyPreview;
            }
            if (String.IsNullOrWhiteSpace (preview)) {
                return false;
            }

            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                // Add debugging info for dev & alpha
                var latency = (DateTime.UtcNow - message.DateReceived).TotalSeconds;
                var cause = (null == fetchCause ? "BG" : fetchCause);
                preview = String.Format ("[{0}:{1:N1}s] {2}", cause, latency, preview);

                Log.Info (Log.LOG_PUSH, "[PA] notify email message: client_id={0}, message_id={1}, cause={2}, delay={3}",
                    NcApplication.Instance.ClientId, message.Id, cause, latency);
            }

            if (NotificationCanAlert) {
                var notif = new UILocalNotification ();
                notif.Category = NotificationCategoryIdentifierChat;
                notif.AlertBody = String.Format ("{0}\n{1}", fromString, preview);
                if (notif.RespondsToSelector (new Selector ("setAlertTitle:"))) {
                    notif.AlertTitle = "New Chat Message";
                }
                notif.AlertAction = null;
                notif.UserInfo = NSDictionary.FromObjectAndKey (NSArray.FromNSObjects(NSNumber.FromInt32 (message.ChatId), NSNumber.FromInt32 (message.Id)), ChatNotificationKey);
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

        public void UpdateBadge ()
        {
            bool shouldClearBadge = EmailHelper.HowToDisplayUnreadCount () == EmailHelper.ShowUnreadEnum.RecentMessages && !NotifyAllowed;
            if (shouldClearBadge) {
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
            } else {
                int badgeCount = EmailHelper.GetUnreadMessageCountForBadge();
                badgeCount += McChat.UnreadMessageCountForBadge ();
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: badge count = {0}", badgeCount);
                UIApplication.SharedApplication.ApplicationIconBadgeNumber = badgeCount;
            }
        }

        // It is okay if this function is called more than it needs to be.
        private void BadgeNotifUpdate ()
        {
            if (NotificationCanBadge) {
                UpdateBadge ();
            } else {
                Log.Info (Log.LOG_UI, "Skip badging due to lack of user permission.");
            }

            Log.Info (Log.LOG_UI, "BadgeNotifUpdate: called");
            if (!NotifyAllowed) {
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: early exit");
                return;
            }

            var datestring = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            var since = DateTime.Parse (datestring);
            var unreadAndHot = McEmailMessage.QueryUnreadAndHotAfter (since);
            var unreadChatMessages = McChat.UnreadMessagesSince (since);
            var soundExpressed = false;
            int remainingVisibleSlots = 10;
            var accountTable = new Dictionary<int, McAccount> ();
            var chatTable = new Dictionary<int, McChat> ();
            McAccount account = null;
            McChat chat = null;

            var notifiedMessageIDs = new HashSet<string> ();

            foreach (var message in unreadAndHot) {
                if (!string.IsNullOrEmpty (message.MessageID) && notifiedMessageIDs.Contains (message.MessageID)) {
                    Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Skipping message {0} because a message with that message ID has already been processed", message.Id);
                    message.MarkHasBeenNotified (true);
                    continue;
                }
                if (message.HasBeenNotified) {
                    if (message.ShouldNotify && !string.IsNullOrEmpty (message.MessageID)) {
                        notifiedMessageIDs.Add (message.MessageID);
                    }
                    continue;
                }
                if (!accountTable.TryGetValue (message.AccountId, out account)) {
                    var newAccount = McAccount.QueryById<McAccount> (message.AccountId);
                    if (null == newAccount) {
                        Log.Warn (Log.LOG_PUSH,
                            "Will not notify email message from an unknown account (accoundId={0}, emailMessageId={1})",
                            message.AccountId, message.Id);
                    }
                    accountTable.Add (message.AccountId, newAccount);
                    account = newAccount;
                }
                if ((null == account) || !NotificationHelper.ShouldNotifyEmailMessage (message)) {
                    message.MarkHasBeenNotified (false);
                    continue;
                }
                if (!NotifyEmailMessage (message, account, !soundExpressed)) {
                    Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification attempt for message {0} failed.", message.Id);
                    continue;
                } else {
                    soundExpressed = true;
                }

                var updatedMessage = message.MarkHasBeenNotified (true);
                if (!string.IsNullOrEmpty (updatedMessage.MessageID)) {
                    notifiedMessageIDs.Add (updatedMessage.MessageID);
                }
                Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification for message {0}", updatedMessage.Id);
                --remainingVisibleSlots;
                if (0 >= remainingVisibleSlots) {
                    break;
                }
            }

            if (remainingVisibleSlots > 0){
                foreach (var message in unreadChatMessages) {
                    if (!accountTable.TryGetValue (message.AccountId, out account)) {
                        account = McAccount.QueryById<McAccount> (message.AccountId);
                        if (null == account) {
                            Log.Warn (Log.LOG_PUSH, "Will not notify chat message from an unknown account (accoundId={0}, emailMessageId={1})", message.AccountId, message.Id);
                        }
                        accountTable.Add (message.AccountId, account);
                    }
                    if (!chatTable.TryGetValue (message.ChatId, out chat)) {
                        chat = McChat.QueryById<McChat> (message.ChatId);
                        if (null == chat) {
                            Log.Warn (Log.LOG_PUSH, "Will not notify chat message from an unknown chat (chatId={0}, emailMessageId={1})", message.ChatId, message.Id);
                        }
                        chatTable.Add (message.ChatId, chat);
                    }
                    if (message.HasBeenNotified) {
                        continue;
                    }
                    if ((null == account) || (null == chat) || !NotificationHelper.ShouldNotifyChatMessage (message)) {
                        // Have to re-query as McEmailMessage or else UpdateWithOCApply complains of a type mismatch
                        McEmailMessage.QueryById<McEmailMessage>(message.Id).MarkHasBeenNotified (false);
                        continue;
                    }
                    if (!NotifyChatMessage (message, chat, account, !soundExpressed)) {
                        Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification attempt for message {0} failed.", message.Id);
                        continue;
                    } else {
                        soundExpressed = true;
                    }
                    // Have to re-query as McEmailMessage or else UpdateWithOCApply complains of a type mismatch
                    McEmailMessage.QueryById<McEmailMessage>(message.Id).MarkHasBeenNotified (true);
                    Log.Info (Log.LOG_UI, "BadgeNotifUpdate: Notification for message {0}", message.Id);
                    --remainingVisibleSlots;
                    if (0 >= remainingVisibleSlots) {
                        break;
                    }
                }
            }

            accountTable.Clear ();
        }

        public static void TestScheduleEmailNotification ()
        {
            var list = NcEmailManager.PriorityInbox (2);
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
            NcApplicationMonitor.Instance.Report ();
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
        public HockeyAppCrashDelegate () : base ()
        {
        }

        public override string ApplicationLogForCrashManager (BITCrashManager crashManager)
        {
            return NcApplication.ApplicationLogForCrashManager ();
        }

        /// For some reason, UserName in HockeyApp web portal has a UUID prefixing the user name.
        /// On a narrow or normal browser window width, the user name is hidden. So, repeat it
        /// in contact again.
        private string UserName ()
        {
            string userName = null;
            if (BuildInfoHelper.IsDev) {
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

