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
        public NotificationsHandler NotificationsHandler { get; private set; } = new NotificationsHandler ();
        BackgroundController BackgroundController = new BackgroundController ();

        // class-level declarations
        public override UIWindow Window { get; set; }

        private bool FirstLaunchInitialization = false;
        private bool DidEnterBackgroundCalled = false;

        private DateTime ForegroundTime = DateTime.MinValue;

        public AppDelegate () : base ()
        {
            NotificationsHandler.BackgroundController = BackgroundController;
            BackgroundController.NotificationsHandler = NotificationsHandler;
        }

        #region Application Lifecycle

        // This method is common to both launching into the background and into the foreground.
        // It gets called once during the app lifecycle.
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Called");

            NcApplication.GuaranteeGregorianCalendar ();

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

            Theme.Active = new NachoTheme ();
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
            BackgroundController.EnterForeground ();
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
            ForegroundTime = DateTime.UtcNow;

            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;

            NotificationsHandler.BecomeActive ();
            BackgroundController.BecomeActive ();
            CallDirectory.Instance.BecomeActive ();

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
            NotificationsHandler.BecomeInactive ();
            CallDirectory.Instance.BecomeInactive ();

            if (DateTime.MinValue != ForegroundTime) {
                // Log the duration of foreground for usage analytics
                var duration = (int)(DateTime.UtcNow - ForegroundTime).TotalMilliseconds;
                NcApplication.Instance.TelemetryService.RecordIntTimeSeries ("Client.Foreground.Duration", ForegroundTime, duration);
                ForegroundTime = DateTime.MinValue;
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

            BackgroundController.EnterBackground ();

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
            BackgroundController.StartFetch (completionHandler, BackgroundController.FetchCause.PerformFetch);
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
            BackgroundController.FinalShutdown (null);
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
                OpenFiles (new string [] { url.Path }, sourceApplication);
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
                    } else if (components [1].Equals ("compose")) {
                        var query = System.Web.HttpUtility.ParseQueryString (url.Query ?? "", System.Text.Encoding.UTF8);
                        var account = NcApplication.Instance.Account;
                        var message = McEmailMessage.MessageWithSubject (account, query.Get ("subject") ?? "");
                        message.To = string.Join (", ", query.GetValues ("to") ?? new string [] { });
                        message.Cc = string.Join (", ", query.GetValues ("cc") ?? new string [] { });
                        message.Bcc = string.Join (", ", query.GetValues ("bcc") ?? new string [] { });
                        var composeViewController = new MessageComposeViewController (account);
                        composeViewController.Composer.Message = message;
                        composeViewController.Composer.InitialText = query.Get ("body") ?? "";
                        var container = query.Get ("container") ?? BuildInfo.AppGroup;
                        var stash = query.Get ("attachments");
                        if (container != null && stash != null) {
                            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl (container);
                            if (containerUrl != null) {
                                var paths = Directory.GetFiles (Path.Combine (containerUrl.Path, stash));
                                var attachments = McAttachment.AttachmentsFromPaths (account, paths);
                                if (attachments.Count > 0) {
                                    composeViewController.Composer.InitialAttachments = attachments;
                                }
                            }
                        }
                        composeViewController.Present ();
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

        void OpenFiles (string [] paths, string source = null)
        {
            if (NcApplication.ReadyToStartUI ()) {
                var account = NcApplication.Instance.DefaultEmailAccount;
                var attachments = McAttachment.AttachmentsFromPaths (account, paths, source);
                if (attachments.Count > 0) {
                    var composeViewController = new MessageComposeViewController (account);
                    composeViewController.Composer.InitialAttachments = attachments;
                    composeViewController.Present ();
                }
            }
        }

        #endregion

        #region Notifications

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
            string [] resources = { "nacho.html", "nacho.css", "nacho.js", "chat-email.html" };
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

        private void UpdateGoInactiveTime ()
        {
            McMutables.Set (McAccount.GetDeviceAccount ().Id, "IOS", "GoInactiveTime", DateTime.UtcNow.ToString ());
            Log.Info (Log.LOG_UI, "UpdateGoInactiveTime: exit");
        }

        #endregion

    }

}
