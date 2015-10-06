
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
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class GettingStartedFragment : Fragment
    {
        // Just shows "Welcome to NachoMail"
        public static GettingStartedFragment newInstance ()
        {
            var fragment = new GettingStartedFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.GettingStartedFragment, container, false);

            var submitButton = view.FindViewById<Button> (Resource.Id.submit);
            submitButton.Click += SubmitButton_Click;
            return view;
        }

        void SubmitButton_Click (object sender, EventArgs e)
        {
            var parent = (LaunchActivity)Activity;
            parent.GettingStartedFinished ();
        }
    }
}

