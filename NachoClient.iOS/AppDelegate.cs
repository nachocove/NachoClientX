using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoClient.iOS;
using SQLite;

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

        public EKEventStore EventStore {
            get { return eventStore; }
        }
        protected EKEventStore eventStore;

        private bool launchBe()
        {
            // There is one back-end object covering all protocols and accounts. It does not go in the DB.
            // It manages everything while the app is running.
            var Be = BackEnd.Instance;
            Be.Owner = this;
            if (0 == Be.Db.Table<McAccount> ().Count ()) {
                Log.Info(Log.LOG_UI, "Empty Table");
            } else {
                // FIXME - this is wrong. Need to handle multiple accounts in future
                this.Account = Be.Db.Table<McAccount>().ElementAt(0);
            }
            Be.Start ();
            NcContactGleaner.Start ();
            return true;
        }
        public override bool FinishedLaunching (UIApplication application, NSDictionary launcOptions)
        {
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval (UIApplication.BackgroundFetchIntervalMinimum);
            // An instance of the EKEventStore class represents the iOS Calendar database.
            eventStore = new EKEventStore ( );
            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof (CidImageProtocol)));
            if (launcOptions != null) {
                // we got some launch options from the OS, probably launched from a localNotification
                if (launcOptions.ContainsKey (UIApplication.LaunchOptionsLocalNotificationKey)) {
                    var localNotification = launcOptions[UIApplication.LaunchOptionsLocalNotificationKey] as UILocalNotification;
                    if (localNotification.HasAction) {
                        // something supposed to happen
                        //FIXME - for now we'll pop up an alert saying we got new mail
                        // what we will do in future is show the email or calendar invite body
                        new UIAlertView (localNotification.AlertAction, localNotification.AlertBody, null, null).Show ();
                        localNotification.ApplicationIconBadgeNumber = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false);
                    }
                }
            }

            launchBe();
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
                Log.Info(Log.LOG_UI,"StatusHandler:Info_NewUnreadEmailMessageInInbox");
                FetchResult = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                Log.Info (Log.LOG_UI , "StatusHandler:Info_SyncSucceeded");
                if (UIBackgroundFetchResult.Failed == FetchResult) {
                    FetchResult = UIBackgroundFetchResult.NoData;
                }
                // We rely on the fact that Info_NewUnreadEmailMessageInInbox will
                // preceed Info_SyncSucceeded.
                BackEnd.Instance.StatusIndEvent -= StatusHandler;
                CompletionHandler (FetchResult);
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                Log.Info (Log.LOG_UI, "StatusHandler:Error_SyncFailed");
                BackEnd.Instance.StatusIndEvent -= StatusHandler;
                CompletionHandler (FetchResult);
                break;
            }
        }

        public override void PerformFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            Log.Info (Log.LOG_UI, "PerformFetch Called");
            CompletionHandler = completionHandler;
            FetchResult = UIBackgroundFetchResult.Failed;
            BackEnd.Instance.StatusIndEvent += StatusHandler;
            BackEnd.Instance.ForceSync ();
        }
        //
        // This method is invoked when the application is about to move from active to inactive state.
        //
        // OpenGL applications should use this method to pause.
        //
        public override void OnResignActivation (UIApplication application)
        {
        }
        // This method should be used to release shared resources and it should store the application state.
        // If your application supports background exection this method is called instead of WillTerminate
        // when the user quits.
        public override void DidEnterBackground (UIApplication application)
        {
        }
        // This method is called as part of the transiton from background to active state.
        public override void WillEnterForeground (UIApplication application)
        {
        }
        // This method is called when the application is about to terminate. Save data, if needed. 
        public override void WillTerminate (UIApplication application)
        {
        }

        public override void ReceivedLocalNotification (UIApplication application, UILocalNotification notification)
        {
            // Overwrite stuff  if we are "woken up"  from a LocalNotificaton out 
        }

        // Methods for IBackEndOwner

        public void StatusInd (NcResult status)
        {
            // FIXME.
        }

        public void StatusInd (int accountId, NcResult status)
        {
            {

                //Assert MCAccount != null;
                UILocalNotification badgeNotification;
                UILocalNotification notification = new UILocalNotification ();
                var countunread = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false);


                switch (status.SubKind) {
                case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:

                    notification.AlertAction = "Taco Mail";
                    notification.AlertBody = "You have new mail.";

                    notification.ApplicationIconBadgeNumber = countunread;
                    notification.SoundName = UILocalNotification.DefaultSoundName;
                    notification.FireDate = DateTime.Now;

                    UIApplication.SharedApplication.ScheduleLocalNotification (notification);

                    //badgeNotification = new UILocalNotification ();
                    //badgeNotification.FireDate = DateTime.Now;
                    //badgeNotification.ApplicationIconBadgeNumber = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false);
                    //UIApplication.SharedApplication.ScheduleLocalNotification (badgeNotification);
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
                case NcResult.SubKindEnum.Info_EmailMessageMarkedRead:
                    // need to find way to pop badge number without alert on app popping up
                    badgeNotification = new UILocalNotification ();
                    badgeNotification.AlertAction = "Taco Mail";
                    //badgeNotification.AlertBody = "Message Read"; // null body means don't show
                    badgeNotification.HasAction = false;  // no alert to show on screen
                    badgeNotification.FireDate = DateTime.Now;
                    var count2 = BackEnd.Instance.Db.Table<McEmailMessage> ().Count (x => x.IsRead == false);
                    badgeNotification.ApplicationIconBadgeNumber = count2;

                    UIApplication.SharedApplication.ScheduleLocalNotification (badgeNotification);
                    break;
                }
            }
        }

  
        public void StatusInd (int accountId, NcResult status, string[] tokens)

        {
            // FIXME.
        }

        public void CredReq(int accountId) {
            var Be = BackEnd.Instance;

            Log.Info (Log.LOG_UI, "Asking for Credentials");
            InvokeOnMainThread (delegate {
                var credView = new UIAlertView ();
                var account = Be.Db.Table<McAccount> ().Single (rec=>rec.Id == accountId);
                var tmpCred = Be.Db.Table<McCred> ().Single (rec => rec.Id == account.CredId);

                credView.Title = "Need to update Login Credentials";
                credView.AddButton ("Update");
                credView.AlertViewStyle = UIAlertViewStyle.LoginAndPasswordInput;
                credView.Show ();
          
                credView.Clicked += delegate(object sender, UIButtonEventArgs b) {
                    var parent = (UIAlertView)sender;
                    // FIXME - need  to display the login id they used in first login attempt
                    var tmplog = parent.GetTextField(0).Text; // login id
                    var tmppwd = parent.GetTextField(1).Text; // password
                    if ((tmplog != String.Empty) && (tmppwd != String.Empty)) {
                        tmpCred.Username = (string) tmplog;
                        tmpCred.Password = (string) tmppwd;
                        Be.Db.Update(tmpCred); //  update with new username/password

                        Be.CredResp(accountId);
                        credView.ResignFirstResponder();
                    } else {
                            var DoitYadummy = new UIAlertView();
                            DoitYadummy.Title = "You need to enter fields for Login ID and Password";
                            DoitYadummy.AddButton("Go Back");
                            DoitYadummy.AddButton("Exit - Do Not Care");
                            DoitYadummy.CancelButtonIndex = 1;
                            DoitYadummy.Show();
                            DoitYadummy.Clicked+= delegate(object silly, UIButtonEventArgs e) {

                            if (e.ButtonIndex == 0) { // I want to actually enter login data
                                CredReq(accountId);    // call to get credentials
                            };

                            DoitYadummy.ResignFirstResponder();
                           
                        };
                    };
                    credView.ResignFirstResponder(); // might want this moved
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
                            tmpServer.Fqdn = txt;
                            Be.Db.Update(tmpServer);
                            Be.ServerConfResp (accountId); 
                            credView.ResignFirstResponder();
                        };

                    };
                  
                    if (b.ButtonIndex == 1) {
                        var gonnaquit = new UIAlertView ();
                        gonnaquit.Title = "Are You Sure? \n No account information will be updated";

                        gonnaquit.AddButton ("Ok"); // continue exiting
                        gonnaquit.AddButton ("Go Back"); // enter info
                        gonnaquit.CancelButtonIndex = 1;
                        gonnaquit.Show ();
                        gonnaquit.Clicked += delegate(object sender, UIButtonEventArgs e) {
                            if (e.ButtonIndex== 1){
                                ServConfReq (accountId); // go again
                            }
                            gonnaquit.ResignFirstResponder();
                        };
                    };
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

