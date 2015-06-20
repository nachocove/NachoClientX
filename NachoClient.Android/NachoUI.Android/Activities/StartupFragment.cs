
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
    public class StartupFragment : Fragment
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
            var view = inflater.Inflate (Resource.Layout.StartupFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Startup fragment";

            var demoButton = view.FindViewById<Button> (Resource.Id.btnDemo);
            demoButton.Click += DemoButton_Click;

            var skipButton = view.FindViewById<Button> (Resource.Id.btnSkip);
            skipButton.Click += SkipButton_Click;

            return view;
        }

        void SkipButton_Click (object sender, EventArgs e)
        {
            var parent = (MainActivity)Activity;
            parent.Skip ();
        }

        void DemoButton_Click (object sender, EventArgs e)
        {
            var parent = (MainActivity)Activity;
            parent.StartupFinished ();
        }
    }
}

