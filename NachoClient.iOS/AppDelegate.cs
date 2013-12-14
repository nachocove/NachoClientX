using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
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

        private NachoDemo Demo { get; set; }
        public BackEnd Be { get; set;}
        public NcAccount Account { get; set; }

        private bool launchBe(){
            // Register to receive DB update indications.
            NcEventable.DbEvent += (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, NcEventable target, EventArgs e) => {
                if (BackEnd.DbActors.Ui != dbActor) {
                    Console.WriteLine("DB Event {1} on {0}", target.ToString(), dbEvent.ToString());
                }
            };
            // There is one back-end object covering all protocols and accounts. It does not go in the DB.
            // It manages everything while the app is running.
            Be = new BackEnd (this);
            if (0 == Be.Db.Table<NcAccount> ().Count ()) {
                Log.Info(Log.LOG_UI, "empty Table");
            } else {
                // FIXME - this is wrong. Need to handle multiple accounts in future
                this.Account = Be.Db.Table<NcAccount>().ElementAt(0);
            }
            Be.Start ();
            return true;
        }
        public override bool FinishedLaunching (UIApplication application, NSDictionary launcOptions)
        {
            launchBe();

            Console.WriteLine ("AppDelegate FinishedLaunching done.");

            return true;

//            if (0 == Be.Db.Table<NcAccount> ().Count ()) {
//                // we will enter the "login schema"
//                // FIXME - need to address ipad/iphone in future release
//
//                UIStoryboard storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
//                var rootControllerView = (UIViewController)storyboard.InstantiateViewController ("Login_Storyboard");
//                this.Window = new UIWindow (UIScreen.MainScreen.Bounds);
//                this.Window.RootViewController = rootControllerView;
//                this.Window.MakeKeyAndVisible();
//
//                return true;
//            } else {
//                UIStoryboard storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
//                var rootControllerView = (UIViewController)storyboard.InstantiateViewController ("LaunchAccount_Storyboard");
//                this.Window = new UIWindow (UIScreen.MainScreen.Bounds);
//                this.Window.RootViewController = rootControllerView;
//                this.Window.MakeKeyAndVisible();
//
//                return true;
//            }
//            // Override point for customization after application launch.
//            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad) {
//                var splitViewController = (UISplitViewController)Window.RootViewController;
//
//                // Get the UINavigationControllers containing our master & detail view controllers
//                var masterNavigationController = (UINavigationController)splitViewController.ViewControllers [0];
//                var detailNavigationController = (UINavigationController)splitViewController.ViewControllers [1];
//
//                var masterViewController = (RootViewController)masterNavigationController.TopViewController;
//                var detailViewController = (DetailViewController)detailNavigationController.TopViewController;
//
//                masterViewController.DetailViewController = detailViewController;
//
//                // Set the DetailViewController as the UISplitViewController Delegate.
//                splitViewController.WeakDelegate = detailViewController;
//            }
//
//            // FOR DEBUGGING BE ONLY. Demo = new NachoDemo ();
//            // We launch the DB, or grab a handle on the instance
//            // launchBe ();
//            //Demo = new NachoDemo ();
//            return true;
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

        public void CredReq(NcAccount account) {
            Console.WriteLine ("Asking for Credentials");
            Be.CredResp (account);
        }
        public void ServConfReq (NcAccount account) {
            Console.WriteLine ("Asking for Config Info");
            Be.ServerConfResp (account);

        }
        public void HardFailInd (NcAccount account) {
        }
        public void SoftFailInd (NcAccount account) {
        }
        public bool RetryPermissionReq (NcAccount account, uint delaySeconds) {
            return true;
        }
        public void ServerOOSpaceInd (NcAccount account) {
        }
        public void CertAskReq (NcAccount account, X509Certificate2 certificate) {
            // UI FIXME - ask user and call CertAskResp async'ly.
            Be.CertAskResp (account, true);
        }
    }
}

