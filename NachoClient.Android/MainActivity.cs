using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using NachoCore;
using NachoPlatform;

namespace NachoClient.Android.Tablet
{
	[Activity (Label = "NachoClient.Android", MainLauncher = true)]
	public class MainActivity : Activity
	{
		int count = 1;

		private NachoDemo Demo { get; set; }

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

			NachoPlatform.Assets.AndroidAssetManager = Assets;
			Demo = new NachoDemo ();
		}
	}
}


