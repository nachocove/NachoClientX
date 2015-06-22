
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
    public class WelcomeFragment : Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.WelcomeFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Welcome fragment";

            var wv = view.FindViewById<TextView> (Resource.Id.welcome);
            wv.SetText (Resource.String.welcome);

            var button = view.FindViewById<Button> (Resource.Id.btnWelcome);
            button.Click += Button_Click;

            var skipButton = view.FindViewById<Button> (Resource.Id.btnSkip);
            skipButton.Click += SkipButton_Click;

            return view;
        }

        void SkipButton_Click (object sender, EventArgs e)
        {
            var parent = (LaunchActivity)Activity;
            parent.Skip ();
        }

        void Button_Click (object sender, EventArgs e)
        {
            var parent = (LaunchActivity)Activity;
            parent.WelcomeFinished ();
        }
    }
}

