//#define CRASHLYTICS
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
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
#if (CRASHLYTICS)
using CrashlyticsBinding;
#endif
using NachoCore.Wbxml;
using MonoTouch.ObjCRuntime;
using ParseBinding;
using NachoClient.Build;
#if (!CRASHLYTICS)
using HockeyApp;
#endif
namespace NachoClient.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the
    // User Interface of the application, as well as listening (and optionally responding) to
    // application events from iOS.
    [Register ("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate
    {
        [DllImport ("libc")]private static extern int sigaction (Signal sig, IntPtr act, IntPtr oact);

        enum Signal
        {
            SIGBUS = 10,
            SIGSEGV = 11
        }
        // class-level declarations
        public override UIWindow Window { get; set; }

        public McAccount Account { get; set; }
        // constants for managing timers
        // iOS kills us after 30, so make sure we dont get there
        private const int KDefaultTimeoutSeconds = 25;

        #if (CRASHLYTICS)
        private void StartCrashReporting ()
        {
            if (Arch.SIMULATOR == Runtime.Arch) {
                // Xaramin does not produce .dSYM files. So, there is nothing to
                // upload to Crashlytics which does not show any crash report
                // that it cannot symbolicate.
                //
                // For an explanation, see:
                // http://forums.xamarin.com/discussion/187/how-do-i-generate-dsym-for-simulator
                Log.Info (Log.LOG_INIT, "Crashlytics is disabled on simulator");
                return;
            }

            // Debugger is causing Crashlytics to crash the app. See the following as a solution
            // http://stackoverflow.com/questions/14499334/how-to-prevent-ios-crash-reporters-from-crashing-monotouch-apps
            IntPtr sigbus = Marshal.AllocHGlobal (512);
            IntPtr sigsegv = Marshal.AllocHGlobal (512);

            // Store Mono SIGSEGV and SIGBUS handlers
            sigaction (Signal.SIGBUS, IntPtr.Zero, sigbus);
            sigaction (Signal.SIGSEGV, IntPtr.Zero, sigsegv);

            // Start Crashlytics
            Crashlytics crash = Crashlytics.SharedInstance ();
            crash.DebugMode = true;
            crash = Crashlytics.StartWithAPIKey ("5aff8dc5f7ff465089df2453cd07d6cd21880b74", 10.0);

            // Restore Mono SIGSEGV and SIGBUS handlers
            sigaction (Signal.SIGBUS, sigbus, IntPtr.Zero);
            sigaction (Signal.SIGSEGV, sigsegv, IntPtr.Zero);

            Marshal.FreeHGlobal (sigbus);
            Marshal.FreeHGlobal (sigsegv);
        }
        #endif

        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            application.SetStatusBarStyle (UIStatusBarStyle.LightContent, true);

            UINavigationBar.Appearance.BarTintColor = UIColor.FromRGB (0x11, 0x46, 0x4F);

            var navigationTitleTextAttributes = new UITextAttributes ();
            navigationTitleTextAttributes.Font = A.Font_AvenirNextDemiBold17;
            navigationTitleTextAttributes.TextColor = A.Color_FFFFFF;
            UINavigationBar.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes);
            UIBarButtonItem.Appearance.SetTitleTextAttributes (navigationTitleTextAttributes, UIControlState.Normal);

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint A");

            NcApplication.Instance.CredReqCallback = CredReqCallback;
            NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);

            application.ApplicationIconBadgeNumber = 0;

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint B");

            #if (CRASHLYTICS)
            StartCrashReporting ();
            #else
            //We MUST wrap our setup in this block to wire up
            // Mono's SIGSEGV and SIGBUS signals
            HockeyApp.Setup.EnableCustomCrashReporting (() => {

                //Get the shared instance
                var manager = BITHockeyManager.SharedHockeyManager;

                //Configure it to use our APP_ID
                manager.Configure ("5f7134267a5c73933420a1f0efbdfcbf");

                // Enable automatic reporting
                manager.CrashManager.CrashManagerStatus = BITCrashManagerStatus.AutoSend;
                manager.CrashManager.EnableOnDeviceSymbolication = true;

                //Start the manager
                manager.StartManager ();

                //Authenticate (there are other authentication options)
                manager.Authenticator.AuthenticateInstallation ();

                //Rethrow any unhandled .NET exceptions as native iOS
                // exceptions so the stack traces appear nicely in HockeyApp
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    Setup.ThrowExceptionAsNative(e.ExceptionObject);

                TaskScheduler.UnobservedTaskException += (sender, e) =>
                    Setup.ThrowExceptionAsNative(e.Exception);
            });
            #endif

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint C");

            Telemetry.SharedInstance.Start<TelemetryBEParse> ();

            Log.Info (Log.LOG_INIT, "{0} (build {1}) built at {2} by {3}",
                BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);

            Account = NcModel.Instance.Db.Table<McAccount> ().FirstOrDefault ();

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint D");

            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                // Watch for changes from the back end
                var s = (StatusIndEventArgs)e;
                this.StatusInd (s.Account.Id, s.Status, s.Tokens);
            };

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint E");

            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof(CidImageProtocol)));

            Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint F");

            if (launchOptions != null) {
                // we got some launch options from the OS, probably launched from a localNotification
                if (launchOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                    Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint G");
                    var localNotification = launchOptions [UIApplication.LaunchOptionsLocalNotificationKey] as UILocalNotification;
                    Log.Info (Log.LOG_LIFECYCLE, "Launched from local notification");
                    if (localNotification.HasAction) {
                        // something supposed to happen
                        //FIXME - for now we'll pop up an alert saying we got new mail
                        // what we will do in future is show the email or calendar invite body
                        localNotification.ApplicationIconBadgeNumber = 0;
                        var alert = new UIAlertView (localNotification.AlertAction, localNotification.AlertBody, null, null);
                        alert.PerformSelector (new Selector ("show"), null, 0.1); // http://stackoverflow.com/questions/9040896
                   
                        Log.Info (Log.LOG_INIT, "FinishedLaunching: checkpoint H");
                    }
                }
            }

            Log.Info (Log.LOG_LIFECYCLE, "AppDelegate FinishedLaunching done.");
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
            // FIXME - need to wait for ALL accounts to complete, not just 1st!
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
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed");
                BackEnd.Instance.Stop ();
                UnhookFetchStatusHandler ();
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailedToComplete:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailedToComplete");
                // BE calls Stop () itself.
                UnhookFetchStatusHandler ();
                CompletionHandler (FetchResult);
                break;
            }
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_LIFECYCLE, "PerformFetch Called");
            CompletionHandler = completionHandler;
            FetchResult = UIBackgroundFetchResult.Failed;
            NcApplication.Instance.StatusIndEvent += FetchStatusHandler;
            NcApplication.Instance.QuickCheck (KDefaultTimeoutSeconds);
        }
        //
        // This method is invoked when the application is about to move from active to inactive state.
        //
        // OpenGL applications should use this method to pause.
        //
        public override void OnResignActivation (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "App Resign Activation: time remaining: " + application.BackgroundTimeRemaining);
        }
        // This method should be used to release shared resources and it should store the application state.
        // If your application supports background exection this method is called instead of WillTerminate
        // when the user quits.
        public override void DidEnterBackground (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "App Did Enter Background");
            NcApplication.Instance.Stop ();
            var imageView = new UIImageView (Window.Frame);
            imageView.Tag = 653;    // Give some decent tagvalue or keep a reference of imageView in self
            imageView.BackgroundColor = UIColor.Red;
            UIApplication.SharedApplication.KeyWindow.AddSubview (imageView);
            UIApplication.SharedApplication.KeyWindow.BringSubviewToFront (imageView);
        }
        // This method is called as part of the transiton from background to active state.
        public override void WillEnterForeground (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "App Will Enter Foreground");
            var imageView = UIApplication.SharedApplication.KeyWindow.ViewWithTag (653);
            if (null != imageView) {
                imageView.RemoveFromSuperview ();
            } else {
                Log.Info (Log.LOG_LIFECYCLE, "App Will Enter Foreground failed to find red view");
            }
        }
        // Equivalent to applicationDidBecomeActive
        public override void OnActivated (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "OnActiviated: Checkpoint A - Enter");
            UnhookFetchStatusHandler ();
            Log.Info (Log.LOG_LIFECYCLE, "OnActiviated: Checkpoint B");
            NcApplication.Instance.Start ();
            Log.Info (Log.LOG_LIFECYCLE, "OnActiviated: Checkpoint C - Exit");
        }
        // This method is called when the application is about to terminate. Save data, if needed.
        public override void WillTerminate (UIApplication application)
        {
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Checkpoint A - Enter");
            NcApplication.Instance.Stop ();
            Log.Info (Log.LOG_LIFECYCLE, "WillTerminate: Checkpoint B - Exit");
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

        public void StatusInd (int accountId, NcResult status, string[] tokens)
        {
            //Assert MCAccount != null;
            // with code change - what is  corect access to DB?
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

            var account = Mo.Db.Table<McAccount> ().Single (rec => rec.Id == accountId);
            var tmpServer = Mo.Db.Table<McServer> ().Single (rec => rec.Id == account.ServerId);

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
}

