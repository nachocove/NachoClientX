
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
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class GettingStartedFragment : Fragment
    {
        int welcomeId;

        // Just shows "Welcome to NachoMail"
        public static GettingStartedFragment newInstance (McAccount account)
        {
            var fragment = new GettingStartedFragment ();
            fragment.welcomeId = (null == account ? Resource.String.gettingstarted : Resource.String.welcome_back);
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.GettingStartedFragment, container, false);

            var textView = view.FindViewById<TextView> (Resource.Id.welcome);
            textView.SetText (welcomeId);

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

