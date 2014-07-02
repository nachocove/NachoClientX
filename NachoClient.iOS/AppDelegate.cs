using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoPlatform;
using NachoClient.iOS;
using SQLite;
using NachoCore.Wbxml;
using MonoTouch.ObjCRuntime;
using ParseBinding;
using NachoClient.Build;
using HockeyApp;

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

        public McAccount Account { get; set; }

        // iOS kills us after 30, so make sure we dont get there
        private const int KPerformFetchTimeoutSeconds = 25;
        private int BackgroundIosTaskId = -1;
        // Don't use NcTimer here - use the raw timer to avoid any future chicken-egg issues.
        #pragma warning disable 414
        private Timer ShutdownTimer = null;
        #pragma warning restore 414
        private bool FinalShutdownHasHappened = false;

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

            //We MUST wrap our setup in this block to wire up
            // Mono's SIGSEGV and SIGBUS signals
            HockeyApp.Setup.EnableCustomCrashReporting (() => {

                //Get the shared instance
                var manager = BITHockeyManager.SharedHockeyManager;

                //Configure it to use our APP_ID
                manager.Configure ("b22a505d784d64901ab1abde0728df67");

                // Enable automatic reporting
                manager.CrashManager.CrashManagerStatus = BITCrashManagerStatus.AutoSend;
                manager.CrashManager.EnableOnDeviceSymbolication = true;
                manager.CrashManager.Delegate = new HockeyAppCrashDelegate ();
                if (BuildInfo.Version.StartsWith ("DEV")) {
                    manager.DebugLogEnabled = true;
                }

                //Start the manager
                manager.StartManager ();

                //Authenticate (there are other authentication options)
                manager.Authenticator.AuthenticateInstallation ();

                //Rethrow any unhandled .NET exceptions as native iOS
                // exceptions so the stack traces appear nicely in HockeyApp
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    Setup.ThrowExceptionAsNative (e.ExceptionObject);

                TaskScheduler.UnobservedTaskException += (sender, e) =>
                    Setup.ThrowExceptionAsNative (e.Exception);
            });
        }

        // This method is common to both launching into the background and into the foreground.
        // It gets called once during the app lifecycle.
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Called");

            NcApplication.Instance.StartClass1Services ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartClass1Services complete");

            NcApplication.Instance.StartClass2Services ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartClass2Services complete");

            application.SetStatusBarStyle (UIStatusBarStyle.LightContent, true);

            UINavigationBar.Appearance.BarTintColor = UIColor.FromRGB (0x11, 0x46, 0x4F);
            UIToolbar.Appearance.BackgroundColor = UIColor.White;
            UIBarButtonItem.Appearance.TintColor = A.Color_29CCBE;

            var navigationTitleTextAttributes = new UITextAttributes ();
            navigationTitleTextAttributes.Font = A.Font_AvenirNextDemiBold17;
            navigationTitleTextAttributes.TextColor = A.Color_FFFFFF;
            UINavigationBar.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes);
            UIBarButtonItem.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes, UIControlState.Normal);

            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);
            application.ApplicationIconBadgeNumber = 0;
            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof(CidImageProtocol)));

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: iOS Cocoa setup complete");

            NcApplication.Instance.CredReqCallback = CredReqCallback;
            NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication callbacks registered");

            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                InvokeOnUIThread.Instance.Invoke (delegate {
                    StartCrashReporting ();
                });
                Telemetry.SharedInstance.Start<TelemetryBEParse> ();
                Log.Info (Log.LOG_LIFECYCLE, "{0} (build {1}) built at {2} by {3}",
                    BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);
                if (0 < BuildInfo.Source.Length) {
                    Log.Info (Log.LOG_LIFECYCLE, "Source Info: {0}", BuildInfo.Source);
                }
            };

            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: NcApplication Class4LateShowEvent registered");

            if (launchOptions != null) {
                // we got some launch options from the OS, probably launched from a localNotification
                if (launchOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                    Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: LaunchOptionsLocalNotificationKey");
                    var localNotification = launchOptions [UIApplication.LaunchOptionsLocalNotificationKey] as UILocalNotification;
                    if (localNotification.HasAction) {
                        // something supposed to happen
                        //FIXME - for now we'll pop up an alert saying we got new mail
                        // what we will do in future is show the email or calendar invite body
                        localNotification.ApplicationIconBadgeNumber = 0;
                        var alert = new UIAlertView (localNotification.AlertAction, localNotification.AlertBody, null, null);
                        alert.PerformSelector (new Selector ("show"), null, 0.1); // http://stackoverflow.com/questions/9040896
                        Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: Alert done");
                    }
                }
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
            var file = new McFile ();
            file.DisplayName = Path.GetFileName (url.Path);
            file.SourceApplication = sourceApplication;
            file.Insert ();
            var destDirectory = Path.Combine (NcModel.Instance.FilesDir, file.Id.ToString ());
            Directory.CreateDirectory (destDirectory);
            var destFile = Path.Combine (destDirectory, Path.GetFileName (url.Path));
            File.Move (url.Path, destFile);
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
           
            NcApplication.Instance.StartClass3Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: StartClass3Services complete");

            NcApplication.Instance.StartClass4Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnActivated: StartClass4Services complete");

            Account = NcModel.Instance.Db.Table<McAccount> ().FirstOrDefault ();
            NcApplication.Instance.StatusIndEvent += StatusIndReceiver;

            BackgroundIosTaskId = UIApplication.SharedApplication.BeginBackgroundTask (() => {
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback time remaining: {0}", application.BackgroundTimeRemaining);
                FinalShutdown (null);
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback exit");
            });
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

            NcApplication.Instance.StatusIndEvent -= StatusIndReceiver;

            NcApplication.Instance.StopClass4Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: StopClass4Services complete");

            NcApplication.Instance.StopClass3Services ();
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: StopClass3Services complete");
            Log.Info (Log.LOG_LIFECYCLE, "OnResignActivation: Exit");

        }

        private void FinalShutdown (object dontCare)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Called");
            NcApplication.Instance.StopClass2Services ();
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: StopClass2Services complete");
            NcApplication.Instance.StopClass1Services ();
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: NcApplication.Instance.Dispose complete");
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
            NcApplication.Instance.StartClass1Services ();
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: StartClass1Services complete");
            NcApplication.Instance.StartClass2Services ();
            FinalShutdownHasHappened = false;
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Exit");
        }

        // This method should be used to release shared resources and it should store the application state.
        // If your application supports background exection this method is called instead of WillTerminate
        // when the user quits.
        public override void DidEnterBackground (UIApplication application)
        {
            var timeRemaining = application.BackgroundTimeRemaining;
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: time remaining: {0}", timeRemaining);
            if (25.0 > timeRemaining) {
                FinalShutdown (null);
            } else {
                var secs = timeRemaining - 20.0;
                ShutdownTimer = new Timer (FinalShutdown, null, (int)(secs * 1000), Timeout.Infinite);
                Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: ShutdownTimer for {0}s", secs);
            }
            var imageView = new UIImageView (Window.Frame);
            imageView.Tag = 653;    // Give some decent tagvalue or keep a reference of imageView in self
            imageView.BackgroundColor = UIColor.Red;
            UIApplication.SharedApplication.KeyWindow.AddSubview (imageView);
            UIApplication.SharedApplication.KeyWindow.BringSubviewToFront (imageView);
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: Exit");
        }

        // This method is called as part of the transiton from background to active state.
        public override void WillEnterForeground (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: Called");
            UnhookFetchStatusHandler ();
            Log.Info (Log.LOG_LIFECYCLE, "WillEnterForeground: UnhookFetchStatusHandler complete");

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
        private Action<UIBackgroundFetchResult> CompletionHandler;
        private UIBackgroundFetchResult FetchResult;

        private void UnhookFetchStatusHandler ()
        {
            NcApplication.Instance.StatusIndEvent -= FetchStatusHandler;
        }

        private void FetchStatusHandler (object sender, EventArgs e)
        {
            // TODO - need to wait for ALL accounts to complete, not just 1st!
            StatusIndEventArgs statusEvent = (StatusIndEventArgs)e;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_NewUnreadEmailMessageInInbox");
                FetchResult = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded");
                if (UIBackgroundFetchResult.Failed == FetchResult) {
                    FetchResult = UIBackgroundFetchResult.NoData;
                }
                // We rely on the fact that Info_NewUnreadEmailMessageInInbox will
                // preceed Info_SyncSucceeded.
                BackEnd.Instance.Stop ();
                UnhookFetchStatusHandler ();
                FinalShutdown (null);
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed");
                BackEnd.Instance.Stop ();
                UnhookFetchStatusHandler ();
                FinalShutdown (null);
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailedToComplete:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailedToComplete");
                // BE calls Stop () itself.
                UnhookFetchStatusHandler ();
                FinalShutdown (null);
                CompletionHandler (FetchResult);
                break;
            }
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_LIFECYCLE, "PerformFetch Called");
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
            }
            CompletionHandler = completionHandler;
            FetchResult = UIBackgroundFetchResult.Failed;
            NcApplication.Instance.StatusIndEvent += FetchStatusHandler;
            NcApplication.Instance.QuickCheck (KPerformFetchTimeoutSeconds);
        }


        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            // Overwrite stuff  if we are "woken up"  from a LocalNotificaton out 
            Log.Info (Log.LOG_LIFECYCLE, "Recieved Local Notification");
            if (notification.UserInfo != null) {
                Log.Info (Log.LOG_LIFECYCLE, "User Info from localnotifocation");
                // in future, we'll use this to open to right screen if we got launched from a notification
            }
        }

        public void StatusIndReceiver (object sender, EventArgs e)
        {
            StatusIndEventArgs ea = (StatusIndEventArgs)e;
            var accountId = ea.Account.Id;
            var status = ea.Status;
            NcAssert.True (ConstMcAccount.NotAccountSpecific.Id == accountId || accountId == Account.Id);
            UILocalNotification badgeNotification;
            UILocalNotification notification = new UILocalNotification ();
            var countunread = NcModel.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false && x.AccountId == accountId);

            switch (status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:

                notification.AlertAction = "Nacho Mail";
                notification.AlertBody = "You have new mail.";

                notification.ApplicationIconBadgeNumber = countunread;
                notification.SoundName = UILocalNotification.DefaultSoundName;
                notification.FireDate = DateTime.Now;

                UIApplication.SharedApplication.ScheduleLocalNotification (notification);

                break;
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:

                notification.AlertAction = "Nacho Mail"; 
                    // no AlertBody should prevent message form being shown            
                notification.HasAction = false;  // no alert to show on screen
                notification.SoundName = UILocalNotification.DefaultSoundName;
                notification.ApplicationIconBadgeNumber = countunread;
                notification.FireDate = DateTime.Now;
                UIApplication.SharedApplication.ScheduleLocalNotification (notification);
                break;
            case NcResult.SubKindEnum.Info_CalendarSetChanged:
                    //UILocalNotification notification = new UILocalNotification ();
                notification.AlertAction = "Nacho Mail";
                notification.AlertBody = "Your Calendar has Changed";
                notification.FireDate = DateTime.Now;
                UIApplication.SharedApplication.ScheduleLocalNotification (notification);

                break;
            case NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded:
                    // need to find way to pop badge number without alert on app popping up
                badgeNotification = new UILocalNotification ();
                badgeNotification.AlertAction = "Nacho Mail";
                    //badgeNotification.AlertBody = "Message Read"; // null body means don't show
                badgeNotification.HasAction = false;  // no alert to show on screen
                badgeNotification.FireDate = DateTime.Now;
                var count2 = NcModel.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false && x.AccountId == accountId);
                badgeNotification.ApplicationIconBadgeNumber = count2;

                UIApplication.SharedApplication.ScheduleLocalNotification (badgeNotification);
                break;
            }
        }

        public void CredReqCallback (int accountId)
        {
            var Mo = NcModel.Instance;
            var Be = BackEnd.Instance;

            var credView = new UIAlertView ();
            var account = Mo.Db.Table<McAccount> ().Single (rec => rec.Id == accountId);
            var tmpCred = Mo.Db.Table<McCred> ().Single (rec => rec.Id == account.CredId);

            credView.Title = "Need to update Login Credentials";
            credView.AddButton ("Update");
            credView.AlertViewStyle = UIAlertViewStyle.LoginAndPasswordInput;
            credView.Show ();

            credView.Clicked += delegate(object sender, UIButtonEventArgs b) {
                var parent = (UIAlertView)sender;
                // FIXME - need  to display the login id they used in first login attempt
                var tmplog = parent.GetTextField (0).Text; // login id
                var tmppwd = parent.GetTextField (1).Text; // password
                if ((tmplog != String.Empty) && (tmppwd != String.Empty)) {
                    tmpCred.Username = (string)tmplog;
                    tmpCred.Password = (string)tmppwd;
                    Mo.Db.Update (tmpCred); //  update with new username/password

                    Be.CredResp (accountId);
                    credView.ResignFirstResponder ();
                } else {
                    var DoitYadummy = new UIAlertView ();
                    DoitYadummy.Title = "You need to enter fields for Login ID and Password";
                    DoitYadummy.AddButton ("Go Back");
                    DoitYadummy.AddButton ("Exit - Do Not Care");
                    DoitYadummy.CancelButtonIndex = 1;
                    DoitYadummy.Show ();
                    DoitYadummy.Clicked += delegate(object silly, UIButtonEventArgs e) {

                        if (e.ButtonIndex == 0) { // I want to actually enter login data
                            CredReqCallback (accountId);    // call to get credentials
                        }

                        DoitYadummy.ResignFirstResponder ();

                    };
                }
                ;
                credView.ResignFirstResponder (); // might want this moved
            };
        }

        public void ServConfReqCallback (int accountId)
        {
            // called if server name is wrong
            // cancel should call "exit program, enter new server name should be updated server
            var Mo = NcModel.Instance;
            var Be = BackEnd.Instance;

            var account = McAccount.QueryById<McAccount> (accountId);
            var tmpServer = McServer.QueryById<McServer> (account.ServerId);

            var credView = new UIAlertView ();

            credView.Title = "Need Correct Server Name";
            credView.AddButton ("Update");
            credView.AddButton ("Cancel");
            credView.AlertViewStyle = UIAlertViewStyle.PlainTextInput;
            credView.Show ();
            credView.Clicked += delegate(object a, UIButtonEventArgs b) {
                var parent = (UIAlertView)a;
                if (b.ButtonIndex == 0) {
                    var txt = parent.GetTextField (0).Text;
                    // FIXME need to scan string to make sure it is of right format
                    if (txt != null) {
                        Log.Info (Log.LOG_LIFECYCLE, " New Server Name = " + txt);
                        tmpServer.Host = txt;
                        tmpServer.Update ();
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

        public void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            // UI FIXME - ask user and call CertAskResp async'ly.
            NcApplication.Instance.CertAskResp (accountId, true);
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
            string log = String.Format ("Version: {0}\nBuild Number: {1}\nLaunch Time: {2}\n",
                             BuildInfo.Version, BuildInfo.BuildNumber, launchTime);
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
}

