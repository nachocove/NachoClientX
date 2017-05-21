using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CoreGraphics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoPlatform;
using NachoClient.iOS;
using Newtonsoft.Json;
using ObjCRuntime;
using NachoClient.Build;

namespace NachoClient.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the
    // User Interface of the application, as well as listening (and optionally responding) to
    // application events from iOS.
    // See http://iosapi.xamarin.com/?link=T%3aMonoTouch.UIKit.UIApplicationDelegate

    [Register ("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate
    {

        UiMonitor UiMonitor = new UiMonitor ();
        CrashReporter CrashReporter = new CrashReporter ();

        // class-level declarations
        public override UIWindow Window { get; set; }

        private nint BackgroundIosTaskId = -1;

        // Don't use NcTimer here - use the raw timer to avoid any future chicken-egg issues.
        #pragma warning disable 414
        private Timer ShutdownTimer = null;
        #pragma warning restore 414

        // used to ensure that a race condition doesn't let the ShutdownTimer stop things after re-activation.
        private int ShutdownCounter = 0;

        private bool FinalShutdownHasHappened = false;
        private bool FirstLaunchInitialization = false;
        private bool DidEnterBackgroundCalled = false;

        private DateTime foregroundTime = DateTime.MinValue;

        #region Application Lifecycle

        // This method is common to both launching into the background and into the foreground.
        // It gets called once during the app lifecycle.
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Called");

            NcApplication.GuaranteeGregorianCalendar ();

            // move data files to Documents/Data if needed
            NachoPlatform.DataFileMigration.MigrateDataFilesIfNeeded ();

            // One-time initialization that do not need to be shut down later.
            if (!FirstLaunchInitialization) {
                FirstLaunchInitialization = true;

                CrashReporter.Start ();
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartCrashReporting complete");

                ServerCertificatePeek.Initialize ();

                UiMonitor.Start ();

                NcApplication.Instance.CredReqCallback = CredReqCallback;
                NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
                NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;

                MdmConfig.Instance.ExtractValues ();
            }

            CopyResourcesToDocuments ();

            if ((null != launchOptions) && launchOptions.ContainsKey (UIApplication.LaunchOptionsRemoteNotificationKey)) {
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Remote notification");
            }

            CrashReporter.SetCrashFolder ();

            NcApplication.Instance.ContinueRemoveAccountIfNeeded ();
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartBasalServices complete");

            NcApplication.Instance.AppStartupTasks ();

            application.SetStatusBarStyle (UIStatusBarStyle.LightContent, true);

            Theme.Active = new NachoTheme();
            Theme.Active.DefineAppearance ();

            NotificationsHandler.RegisterForNotifications ();

            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: iOS Cocoa setup complete");

            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                NcApplication.Instance.TelemetryService.Throttling = false;
            };

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication Class4LateShowEvent registered");

            // If launch options is set with the local notification key, that means the app has been started
            // by a local notification.  ReceivedLocalNotification will be called and it wants to know if the
            // app has just be started or if it was already running. We set flags so ReceivedLocalNotification
            // knows that the app has just been started.
            if (launchOptions != null && launchOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: LaunchOptionsLocalNotificationKey");
                var localNotification = (UILocalNotification)launchOptions [UIApplication.LaunchOptionsLocalNotificationKey];
                NotificationsHandler.HandleLaunchNotification (localNotification);
            }

            NcKeyboardSpy.Instance.Init ();

           // if (application.RespondsToSelector (new ObjCRuntime.Selector ("shortcutItems"))) {
           //     application.ShortcutItems = new UIApplicationShortcutItem[] {
           //         new UIApplicationShortcutItem ("com.nachocove.nachomail.newmessage", "New Message", null, UIApplicationShortcutIcon.FromTemplateImageName ("shortcut-compose"), null)
           //     };
           // }

            // I don't know where to put this.  It can't go in NcApplication.MonitorReport(), because
            // C#'s TimeZoneInfo.Local has an ID and name of "Local", which is not helpful.  It has
            // to be in iOS-specific code.
            Log.Info (Log.LOG_LIFECYCLE, "Current time zone: {0}", NSTimeZone.LocalTimeZone.Description);

            Window = new UIWindow (UIScreen.MainScreen.Bounds);
            if (NcApplication.ReadyToStartUI ()) {
                Window.RootViewController = new NachoTabBarController ();
            } else {
                var storyboard = UIStoryboard.FromName ("Startup", null);
                Window.RootViewController = storyboard.InstantiateInitialViewController ();
            }
            Window.MakeKeyAndVisible ();

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Exit");

            return true;
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

        // OnActivated AND OnResignActivation ARE MIRROR IMAGE (except for BeginBackgroundTask).

        // Equivalent to applicationDidBecomeActive
        //
        // This method is called whenever the app is moved to the active state, whether on launch, from inactive, or
        // from the background.
        public override void OnActivated (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: Called");
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;
            NotificationsHandler.BecomeActive ();
            if (doingPerformFetch) {
                CompletePerformFetchWithoutShutdown ();
            }
            foregroundTime = DateTime.UtcNow;

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
            NotificationsHandler.BecomeInactive ();

            if (DateTime.MinValue != foregroundTime) {
                // Log the duration of foreground for usage analytics
                var duration = (int)(DateTime.UtcNow - foregroundTime).TotalMilliseconds;
                NcApplication.Instance.TelemetryService.RecordIntTimeSeries ("Client.Foreground.Duration", foregroundTime, duration);
                foregroundTime = DateTime.MinValue;
            }

            if (!isInitializing) {
                NcApplication.Instance.StopClass4Services ();
            }
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: StopClass4Services complete");

            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: Exit");
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
                TimeSpan initialTimerDelay = TimeSpan.FromSeconds (1);
                if (35 < timeRemaining && timeRemaining < 1000) {
                    initialTimerDelay = TimeSpan.FromSeconds (timeRemaining - 30);
                }
                ShutdownTimer = new Timer ((opaque) => {
                    InvokeOnUIThread.Instance.Invoke (delegate {
                        // check remaining background time. If too little, shut us down.
                        // iOS caveat: BackgroundTimeRemaining can be MAX_DOUBLE early on.
                        // It also seems to return to MAX_DOUBLE value after we call EndBackgroundTask().
                        var remaining = application.BackgroundTimeRemaining;
                        Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer: time remaining: {0:n2}", remaining);
                        if (!didShutdown && 25.0 > remaining) {
                            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer: Background time is running low. Shutting down the app.");
                            try {
                                // This seems to work, but we do get some extra callbacks after Change().
                                ShutdownTimer.Change (Timeout.Infinite, Timeout.Infinite);
                            } catch (Exception ex) {
                                // Wrapper to protect against unknown C# timer stupidity.
                                Log.Error (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer exception: {0}", ex);
                            }
                            didShutdown = true;
                            FinalShutdown (opaque);
                        }
                    });
                }, ShutdownCounter, initialTimerDelay, TimeSpan.FromSeconds (1));
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

            NcTask.Run (() => {
                NcModel.Instance.CleanupOldDbConnections (TimeSpan.FromMinutes (10), 20);
            }, "CleanupOldDbConnections");

            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: Exit");
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
        	Log.Info (Log.LOG_LIFECYCLE, "PerformFetch called.");
        	StartFetch (application, completionHandler, BackgroundController.FetchCause.PerformFetch);
        }

        public override void ReceiveMemoryWarning (UIApplication application)
        {
            Log.Info (Log.LOG_SYS, "ReceiveMemoryWarning;");
            Log.Info (Log.LOG_SYS, "Monitor: NSURLCache usage {0}", NSUrlCache.SharedCache.CurrentMemoryUsage);
            NcApplicationMonitor.Instance.Report ();
        }

        // This method is called when the application is about to terminate. Save data, if needed.
        // There is no guarantee that this function gets called.
        public override void WillTerminate (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Called");
            FinalShutdown (null);
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Exit");
        }

        #endregion

        #region Sepecial Open Requests

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

        #endregion

        #region Notifications

        NotificationsHandler NotificationsHandler = new NotificationsHandler ();

        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            NotificationsHandler.HandleLocalNotification (notification);
        }

        public override void HandleAction (UIApplication application, string actionIdentifier, UILocalNotification localNotification, Action completionHandler)
        {
            NotificationsHandler.HandleAction (actionIdentifier, localNotification, completionHandler);
        }

        public override void RegisteredForRemoteNotifications (UIApplication application, NSData deviceToken)
        {
            NotificationsHandler.HandleRemoteNoficationRegistration (deviceToken);
        }

        public override void FailedToRegisterForRemoteNotifications (UIApplication application, NSError error)
        {
            NotificationsHandler.HandleRemoteNotificationRegistrationError (error);
        }

        public override void DidRegisterUserNotificationSettings (UIApplication application, UIUserNotificationSettings notificationSettings)
        {
            Log.Info (Log.LOG_LIFECYCLE, "DidRegisteredUserNotificationSettings: 0x{0:X}", (ulong)notificationSettings.Types);
        }

        public override void DidReceiveRemoteNotification (UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
        {
            NotificationsHandler.HandleRemoteNotification (userInfo, completionHandler);
        }

        #endregion

        #region UI Related Events

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

        /// Status bar height can change when the user is on a call or using navigation
        public override void ChangedStatusBarFrame (UIApplication application, CGRect oldStatusBarFrame)
        {
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_StatusBarHeightChanged),
            });
        }

        #endregion

        #region NcApplication Events

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

        #endregion

        #region Private Helpers

        private void CopyResourcesToDocuments ()
        {
            var documentsPath = NcApplication.GetDocumentsPath ();
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
            Log.Info (Log.LOG_PUSH, "[PA] finalshutdown: client_id={0}", NcApplication.Instance.ClientId);
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Exit");
        }

        private void ReverseFinalShutdown ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Called");
            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: StartBasalServices complete");
            FinalShutdownHasHappened = false;
            NcTask.Run (() => NcModel.Instance.CleanupOldDbConnections (TimeSpan.FromMinutes (10), 20), "ReverseFinalShutdownCleanupOldDbConnections");
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Exit");
        }

        #endregion

        #region Test/Debug Helpers

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

        #endregion

    }

}
