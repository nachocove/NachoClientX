using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.ActiveSync;
using NachoCore.Model;
using SQLite;

namespace NachoClient.iOS
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the 
	// User Interface of the application, as well as listening (and optionally responding) to 
	// application events from iOS.
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate, IAsDataSource, IAsOwner
	{
		// class-level declarations
		public override UIWindow Window {
			get;
			set;
		}
		public NcServer Server { get; set; }
		public NcProtocolState ProtocolState { get; set; }
		public NcAccount Account { get; set;}
		public NcCred Cred { get; set; }
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

			var db = new SQLiteConnection("foofoo");
			db.CreateTable<NcAccount>();

			//Account = new NcAccount ();
			//Account.Username = "jeffe@nachocove.com";
			db.Insert (new NcAccount () { Username = "jeffe@nachocove.com" });
			var query = db.Table<NcAccount>().Where(v => v.Username.StartsWith("j"));
			foreach (var acc in query) {
				Account = acc;
				Console.WriteLine (acc.Username);
			}
			Cred = new NcCred ();
			Cred.Username = "jeffe@nachocove.com";
			Cred.Password = "D0ggie789";
			Server = new NcServer ();
			Server.Fqdn = "nco9.com";
			Server.Port = 443;
			Server.Scheme = "https";
			ProtocolState = new NcProtocolState ();
			ProtocolState.AsProtocolVersion = "12.0";

			//var cmd = new AsOptions(this, this);
			var cmd = new AsProvisionCommand (this, this);
			cmd.Execute();
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
	}
}

