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
                Log.Info(Log.LOG_UI, "empty Table");
            } else {
                // FIXME - this is wrong. Need to handle multiple accounts in future
                this.Account = Be.Db.Table<McAccount>().ElementAt(0);
            }
            Be.Start ();
            return true;
        }
        public override bool FinishedLaunching (UIApplication application, NSDictionary launcOptions)
        {
            // An instance of the EKEventStore class represents the iOS Calendar database.
            eventStore = new EKEventStore ( );
            // Set up webview to handle html with embedded custom types (curtesy of Exchange)
            NSUrlProtocol.RegisterClass (new MonoTouch.ObjCRuntime.Class (typeof (CidImageProtocol)));

            launchBe();
            var outbox = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault(x => "Outbox" == x.DisplayName && x.IsClientOwned == true);
            if (null == outbox) {
                outbox = McFolder.CreateClientOwned ();
                outbox.DisplayName = "Outbox"; // Don't ever search for this - remember localization will change it!
                outbox.ParentId = "0";
                outbox.ServerId = "OUTBOX"; // Search for this instead.
                outbox.Type = (uint)Xml.FolderHierarchy.TypeCode.UserCreatedMail;
                BackEnd.Instance.Db.Insert (outbox);
            }
            var galCache = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault(x => "GAL" == x.DisplayName && x.IsClientOwned == true);
            if (null == galCache) {
                galCache = McFolder.CreateClientOwned ();
                galCache.IsHidden = true;
                galCache.ParentId = "0";
                galCache.ServerId = "GAL";
                galCache.Type = (uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts;
                BackEnd.Instance.Db.Insert (galCache);
            }
            Console.WriteLine ("AppDelegate FinishedLaunching done.");

            return true;

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

        // Methods for IBackEndOwner

        public void StatusInd (NcResult status)
        {
            // FIXME.
        }

        public void StatusInd (McAccount account, NcResult status)
        {
            // FIXME.
        }

        public void StatusInd (McAccount account, NcResult status, string[] tokens)
        {
            // FIXME.
        }

        public void CredReq(McAccount account) {
            var Be = BackEnd.Instance;

            Console.WriteLine ("Asking for Credentials");
            InvokeOnMainThread (delegate {
                var credView = new UIAlertView ();
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

                        Be.CredResp(account);
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
                                CredReq(account);    // call to get credentials
                            };

                            DoitYadummy.ResignFirstResponder();
                           
                        };
                    };
                    credView.ResignFirstResponder(); // might want this moved
                };
            }); // end invokeonMain
        }

        public void ServConfReq (McAccount account)
        {
            // called if server name is wrong
            // cancel should call "exit program, enter new server name should be updated server
            var Be = BackEnd.Instance;

            Console.WriteLine ("Asking for Config Info");
            InvokeOnMainThread (delegate {  // lock on main thread
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
                            Console.WriteLine(" New Server Name = " + txt);
                            tmpServer.Fqdn = txt;
                            Be.Db.Update(tmpServer);
                            Be.ServerConfResp (account); 
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
                                ServConfReq (account); // go again
                            }
                            gonnaquit.ResignFirstResponder();
                        };
                    };
                };
            }); // end invoke MainThread
        }

        public void CertAskReq (McAccount account, X509Certificate2 certificate)
        {
            var Be = BackEnd.Instance;

            // UI FIXME - ask user and call CertAskResp async'ly.
            Be.CertAskResp (account, true);
        }
        public void SearchContactsResp (McAccount account, string prefix, string token)
        {
            // FIXME.
        }
    }
}

