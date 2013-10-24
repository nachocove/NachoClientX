using System;
using System.IO;
using System.Runtime.InteropServices;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using SQLite;

namespace NachoClient.iOS
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the 
	// User Interface of the application, as well as listening (and optionally responding) to 
	// application events from iOS.
	[Register ("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate, IBackEndDelegate
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
                Console.WriteLine ("empty Table");
            }
            Be.Start ();
            return true;
        }
		public override bool FinishedLaunching (UIApplication application, NSDictionary launcOptions)
		{
			// Override point for customization after application launch.
			if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad) {
				var splitViewController = (UISplitViewController)Window.RootViewController;

				// Get the UINavigationControllers containing our master & detail view controllers
				var masterNavigationController = (UINavigationController)splitViewController.ViewControllers [0];
				var detailNavigationController = (UINavigationController)splitViewController.ViewControllers [1];

				var masterViewController = (RootViewController)masterNavigationController.TopViewController;
				var detailViewController = (DetailViewController)detailNavigationController.TopViewController;

				masterViewController.DetailViewController = detailViewController;

				// Set the DetailViewController as the UISplitViewController Delegate.
				splitViewController.WeakDelegate = detailViewController;
			}

			// FOR DEBUGGING BE ONLY. Demo = new NachoDemo ();
            // We launch the DB, or grab a handle on the instance
            launchBe ();
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

        //Methods for IBackendDelegate
        // Methods for IBackEndDelegate:
        public void CredReq(NcAccount account) {
        }
        public void ServConfReq (NcAccount account) {
            // Will change - needed for current autodiscover flow.
            Be.Db.Update (BackEnd.DbActors.Ui, account);
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

	}
}

