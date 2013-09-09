using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using SQLite;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.Android.Tablet
{
	[Activity (Label = "NachoClient.Android.Tablet", MainLauncher = true)]
	public class MainActivity : Activity, IAsDataSource
	{
		int count = 1;

		public NcServer Server { get; set; }
		public NcProtocolState ProtocolState { get; set; }
		public NcAccount Account { get; set;}
		public NcCred Cred { get; set; }

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.myButton);
			
			button.Click += delegate {
				button.Text = string.Format ("{0} clicks!", count++);
			};

			//var db = new SQLiteConnection("foofoo");
			//db.CreateTable<NcAccount>();

			//Account = new NcAccount ();
			//Account.Username = "jeffe@nachocove.com";
			//db.Insert (new NcAccount () { Username = "jeffe@nachocove.com" });
			//var query = db.Table<NcAccount>().Where(v => v.Username.StartsWith("j"));
			//foreach (var acc in query) {
			//	Account = acc;
			//}
			Account = new NcAccount () { Username = "jeffe@nachocove.com" };
			Cred = new NcCred ();
			Cred.Username = "jeffe@nachocove.com";
			Cred.Password = "D0ggie789";
			Server = new NcServer ();
			Server.Fqdn = "m.google.com";
			Server.Port = 443;
			Server.Scheme = "https";
			ProtocolState = new NcProtocolState ();
			ProtocolState.AsProtocolVersion = "12.0";

			var sm = new StateMachine () {
				TransTable = new[] {
					new Node {State = (uint)St.Start, On = new [] {
							new Trans {Event=(uint)Ev.Launch, Act=DoNop, State=(uint)St.Start},
							new Trans {Event=(uint)Ev.Success, Act=DoNop, State=(uint)St.Start},
							new Trans {Event=(uint)Ev.Failure, Act=DoNop, State=(uint)St.Start}}}}
			};
			var cmd = new AsProvisionCommand (null);
			cmd.Execute(sm);
		}

		public void DoNop (){
		}
	}
}


