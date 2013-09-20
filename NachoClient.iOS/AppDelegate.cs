using System;
using System.IO;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.ActiveSync;
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
		public BackEnd Be { get; set; }

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

			Be = new BackEnd (this);
			var account = new NcAccount () { EmailAddr = "jeffe@nachocove.com" };
			Be.Db.InsertOrReplace (BackEnd.Actors.Ui, account);
			var cred = new NcCred () { Username = "jeffe@nachocove.com", Password = "D0ggie789" };
			Be.Db.InsertOrReplace (BackEnd.Actors.Ui, cred);
			account.CredId = cred.Id;
			var server = new NcServer () { Fqdn = "nco9.com", Port = 443, Scheme = "https"};
			Be.Db.InsertOrReplace (BackEnd.Actors.Ui, server);
			account.ServerId = server.Id;
			var protocolStateBlocks = Be.Db.Table<NcProtocolState> ();
			NcProtocolState protocolState = null;
			if (0 != protocolStateBlocks.Count()) {
				protocolState = protocolStateBlocks.First ();
			} else  {
				protocolState = new NcProtocolState () { AsProtocolVersion = "12.0", AsPolicyKey = "0" };
			}
			Be.Db.InsertOrReplace (BackEnd.Actors.Ui, protocolState);
			account.ProtocolStateId = protocolState.Id;
			Be.Db.Update (BackEnd.Actors.Ui, account);
			Be.Start (account);
			return true;
		}
		public void CredRequest(NcAccount account) {
		}
		public void ServConfRequest (NcAccount account) {
			Be.Db.Update (BackEnd.Actors.Ui, account);
		}
		public void HardFailureIndication (NcAccount account) {
		}
		public void SoftFailureIndication (NcAccount account) {
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

