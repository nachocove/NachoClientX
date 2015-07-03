using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "FolderListActivity")]            
    public class FolderListActivity : Activity
    {
        int count;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Create your application here
            SetContentView (Resource.Layout.FolderListActivity);

            // Get our button from the layout resource,
            // and attach an event to it
            var button = FindViewById<Android.Widget.Button> (Resource.Id.myButton);

            button.Click += delegate {
                button.Text = string.Format ("{0} folder list  clicks!", count++);
            };
        }
    }
}

