
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public class ComposeFragment : Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.ComposeFragment, container, false);

            var sendButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            sendButton.SetImageResource (Resource.Drawable.icn_send);
            sendButton.Visibility = Android.Views.ViewStates.Visible;
            sendButton.Click += SendButton_Click;

            return view;
        }

        void SendButton_Click (object sender, EventArgs e)
        {
            this.Activity.Finish ();
        }
    }
}

