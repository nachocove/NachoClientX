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
    [Activity (Label = "MessageComposeActivity")]            
    public class MessageComposeActivity : AppCompatActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.MessageComposeActivity);

            var composeFragment = new ComposeFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, composeFragment).AddToBackStack("Now").Commit ();
           
        }
    }
}

