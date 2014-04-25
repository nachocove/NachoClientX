using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoClient.iOS;
using SQLite;
using CrashlyticsBinding;
using NachoCore.Wbxml;

namespace NachoClient.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the
    // User Interface of the application, as well as listening (and optionally responding) to
    // application events from iOS.
    [Register ("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate, IBackEndOwner
    {
        // class-level declarations
        public override UIWindow Window { get; set; }

        public McAccount Account { get; set; }


        // constants for managing timers
        private const uint KDefaultDelaySeconds = 10;
        private const int KDefaultTimeoutSeconds = 25;
        // iOS kills us after 30, so make sure we dont get there
        private TimeSpan Timeout = new TimeSpan (0, 0, KDefaultTimeoutSeconds);
        private NcTimer TimeoutTimer;
        // These DisposedXxx are used to avoid eliminating a reference while still in a callback.
        #pragma warning disable 414

        private NcTimer DisposedTimeoutTimer;
        #pragma warning restore 414
        // end timer constants
        private bool launchBe ()
        {
            // Initialize all Xml filters
            NcXmlFilterSet.Requests.Add (new AsXmlFilterPingRequest ());
            NcXmlFilterSet.Responses.Add (new AsXmlFilterPingResponse ());

            // There is one back-end object covering all protocols and accounts. It does not go in the DB.
            // It manages everything while the app is running.
            var Be = BackEnd.Instance;
            Be.Owner = this;
            Be.StatusIndEvent += (object sender, EventArgs e) => {
                // Watch for changes from the back end
                var s = (StatusIndEventArgs)e;
                this.StatusInd (s.Account.Id, s.Status, s.Tokens);
            };
            if (0 == Be.Db.Table<McAccount> ().Count ()) {
                Log.Info (Log.LOG_UI, "Empty Table");
            } else {
                // FIXME - this is wrong. Need to handle multiple accounts in future
                this.Account = Be.Db.Table<McAccount> ().ElementAt (0);
            }
            Be.Start ();
            NcContactGleaner.Start ();
            return true;
        }

        public override bool FinishedLaunching (UIApplication application, NSDictionary launcOptions)
        {
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);

            application.ApplicationIconBadgeNumber = 0;
           
            #if (!DEBUG)
            Crashlytics crash = Crashlytics.SharedInstance ();
            crash.DebugMode = true;
            crash = Crashlytics.StartWithAPIKey ("5aff8dc5f7ff465089df2453cd07d6cd21880b74", 10.0);
            #endif

            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof(CidImageProtocol)));
            if (launcOptions != null) {
                // we got some launch options from the OS, probably launched from a localNotification
                if (launcOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                    var localNotification = launcOptions [UIApplication.LaunchOptionsLocalNotificationKey] as UILocalNotification;
                    Log.Info (Log.LOG_UI, "Launched from local notification");
                    if (localNotification.HasAction) {
                        // something supposed to happen
                        //FIXME - for now we'll pop up an alert saying we got new mail
                        // what we will do in future is show the email or calendar invite body
                        localNotification.ApplicationIconBadgeNumber = 0;
                        new UIAlertView (localNotification.AlertAction, localNotification.AlertBody, null, null).Show ();
                   
                        //localNotification.ApplicationIconBadgeNumber = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false);// need to find accountID here
                    }
                }
            }

            // FIXME - should this get started before AlertView above, to ensure the BE is active?
            launchBe ();
            Log.Info (Log.LOG_UI, "AppDelegate FinishedLaunching done.");

            return true;

        }
        /* 
         * Code to implement iOS-7 background-fetch.
         */
        private Action<UIBackgroundFetchResult> CompletionHandler;
        private UIBackgroundFetchResult FetchResult;

        private void StatusHandler (object sender, EventArgs e)
        {
            // FIXME - need to wait for ALL accounts to complete, not just 1st!
            StatusIndEventArgs statusEvent = (StatusIndEventArgs)e;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:
                Log.Info (Log.LOG_UI, "StatusHandler:Info_NewUnreadEmailMessageInInbox");
                FetchResult = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                Log.Info (Log.LOG_UI, "StatusHandler:Info_SyncSucceeded");
                if (UIBackgroundFetchResult.Failed == FetchResult) {
                    FetchResult = UIBackgroundFetchResult.NoData;
                }
                // We rely on the fact that Info_NewUnreadEmailMessageInInbox will
                // preceed Info_SyncSucceeded.
                BackEnd.Instance.Stop ();
                BackEnd.Instance.StatusIndEvent -= StatusHandler;
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                Log.Info (Log.LOG_UI, "StatusHandler:Error_SyncFailed");
                BackEnd.Instance.Stop ();
                BackEnd.Instance.StatusIndEvent -= StatusHandler;
                CompletionHandler (FetchResult);
                break;
            }
        }

        private void CancelFetchCallback (object State)
        {
            // here's where we check to see if any status
            // * kill backgroundfetch
            Log.Info (Log.LOG_UI, "Cancel BackgroundFetch");
           
            BackEnd.Instance.StatusIndEvent -= StatusHandler;
            CompletionHandler (UIBackgroundFetchResult.Failed); // should stop fetch occuring
            CancelTimeoutTimer ();
        }

        private void CancelTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                DisposedTimeoutTimer = TimeoutTimer;
                TimeoutTimer = null;
            }
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_UI, "PerformFetch Called");
            // Set up a Timer to kill BG after 25 Secs
            TimeoutTimer = new NcTimer (CancelFetchCallback, null, Timeout, System.Threading.Timeout.InfiniteTimeSpan);
            CompletionHandler = completionHandler;
            // DEBUG - comment this if you want to just return as soon as we enter BG
            // completion handler call to just immediately return from BG perform fetch call
            // CompletionHandler (UIBackgroundFetchResult.NewData);
           
            FetchResult = UIBackgroundFetchResult.Failed;
            BackEnd.Instance.StatusIndEvent += StatusHandler;
            Console.WriteLine ("ForceSync START 1");
            BackEnd.Instance.ForceSync ();
            Console.WriteLine ("ForceSync END 1");
        }
        //
        // This method is invoked when the application is about to move from active to inactive state.
        //
        // OpenGL applications should use this method to pause.
        //
        public override void OnResignActivation (UIApplication application)
        {

            Log.Info (Log.LOG_UI, "App Resign Activation: time remaining: " + application.BackgroundTimeRemaining);
            BackEnd.Instance.Stop ();
        }
        // This method should be used to release shared resources and it should store the application state.
        // If your application supports background exection this method is called instead of WillTerminate
        // when the user quits.
        public override void DidEnterBackground (UIApplication application)
        {
            Log.Info (Log.LOG_UI, "App Did Enter Background");
            var imageView = new UIImageView (Window.Frame);
            imageView.Tag = 101;    // Give some decent tagvalue or keep a reference of imageView in self
            imageView.BackgroundColor = UIColor.Red;
            UIApplication.SharedApplication.KeyWindow.AddSubview (imageView);
            UIApplication.SharedApplication.KeyWindow.BringSubviewToFront (imageView);
        }
        // This method is called as part of the transiton from background to active state.
        public override void WillEnterForeground (UIApplication application)
        {
            Log.Info (Log.LOG_UI, "App Will Enter Foreground");
            Console.WriteLine ("ForceSync START 2");
            BackEnd.Instance.ForceSync ();
            Console.WriteLine ("ForceSync END 2");
            var imageView = UIApplication.SharedApplication.KeyWindow.ViewWithTag (101);
            if (null != imageView) {
                imageView.RemoveFromSuperview ();
            }
        }
        // Equivalent to applicationDidBecomeActive
        public override void OnActivated (UIApplication application)
        {
            Log.Info (Log.LOG_UI, "App Did Become Active");
        }
        // This method is called when the application is about to terminate. Save data, if needed.
        public override void WillTerminate (UIApplication application)
        {
            Log.Info (Log.LOG_UI, "App Will Terminate");
        }

        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            // Overwrite stuff  if we are "woken up"  from a LocalNotificaton out 
            Log.Info (Log.LOG_UI, "Recieved Local Notification");
            if (notification.UserInfo != null) {
                Log.Info (Log.LOG_UI, "User Info from localnotifocation");
                // in future, we'll use this to open to right screen if we got launched from a notification
            }
        }

        public void StatusInd (int accountId, NcResult status, string[] tokens)
        {
            {

                //Assert MCAccount != null;
                // with code change - what is  corect access to DB?
                UILocalNotification badgeNotification;
                UILocalNotification notification = new UILocalNotification ();
                var countunread = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false && x.AccountId == accountId);


                switch (status.SubKind) {
                case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:

                    notification.AlertAction = "Taco Mail";
                    notification.AlertBody = "You have new mail.";

                    notification.ApplicationIconBadgeNumber = countunread;
                    notification.SoundName = UILocalNotification.DefaultSoundName;
                    notification.FireDate = DateTime.Now;

                    UIApplication.SharedApplication.ScheduleLocalNotification (notification);

                    break;
                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:

                    notification.AlertAction = "Taco Mail"; 
                    // no AlertBody should prevent message form being shown            
                    notification.HasAction = false;  // no alert to show on screen
                    notification.SoundName = UILocalNotification.DefaultSoundName;
                    notification.ApplicationIconBadgeNumber = countunread;
                    notification.FireDate = DateTime.Now;
                    UIApplication.SharedApplication.ScheduleLocalNotification (notification);
                    break;
                case NcResult.SubKindEnum.Info_CalendarSetChanged:
                    //UILocalNotification notification = new UILocalNotification ();
                    notification.AlertAction = "Taco Mail";
                    notification.AlertBody = "Your Calendar has Changed";
                    notification.FireDate = DateTime.Now;
                    UIApplication.SharedApplication.ScheduleLocalNotification (notification);

                    break;
                case NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded:
                    // need to find way to pop badge number without alert on app popping up
                    badgeNotification = new UILocalNotification ();
                    badgeNotification.AlertAction = "Taco Mail";
                    //badgeNotification.AlertBody = "Message Read"; // null body means don't show
                    badgeNotification.HasAction = false;  // no alert to show on screen
                    badgeNotification.FireDate = DateTime.Now;
                    var count2 = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false && x.AccountId == accountId);
                    badgeNotification.ApplicationIconBadgeNumber = count2;

                    UIApplication.SharedApplication.ScheduleLocalNotification (badgeNotification);
                    break;
                }
            }
        }

        public void CredReq (int accountId)
        {
            var Be = BackEnd.Instance;

            Log.Info (Log.LOG_UI, "Asking for Credentials");
            InvokeOnMainThread (delegate {
                var credView = new UIAlertView ();
                var account = Be.Db.Table<McAccount> ().Single (rec => rec.Id == accountId);
                var tmpCred = Be.Db.Table<McCred> ().Single (rec => rec.Id == account.CredId);

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
                        Be.Db.Update (tmpCred); //  update with new username/password

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
                                CredReq (accountId);    // call to get credentials
                            }
                            ;

                            DoitYadummy.ResignFirstResponder ();
                           
                        };
                    }
                    ;
                    credView.ResignFirstResponder (); // might want this moved
                };
            }); // end invokeonMain
        }

        public void ServConfReq (int accountId)
        {
            // called if server name is wrong
            // cancel should call "exit program, enter new server name should be updated server
            var Be = BackEnd.Instance;

            Log.Info (Log.LOG_UI, "Asking for Config Info");
            InvokeOnMainThread (delegate {  // lock on main thread
                var account = Be.Db.Table<McAccount> ().Single (rec => rec.Id == accountId);
                var tmpServer = Be.Db.Table<McServer> ().Single (rec => rec.Id == account.ServerId);

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
                            Log.Info (Log.LOG_UI, " New Server Name = " + txt);
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
                                ServConfReq (accountId); // go again
                            }
                            gonnaquit.ResignFirstResponder ();
                        };
                    }
                    ;
                };
            }); // end invoke MainThread
        }

        public void CertAskReq (int accountId, X509Certificate2 certificate)
        {
            var Be = BackEnd.Instance;

            // UI FIXME - ask user and call CertAskResp async'ly.
            Be.CertAskResp (accountId, true);
        }

        public void SearchContactsResp (int accountId, string prefix, string token)
        {
            // FIXME.
        }
    }
}

