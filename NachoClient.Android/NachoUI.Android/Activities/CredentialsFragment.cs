
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

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class CredentialsFragment : Fragment
    {
        public McAccount.AccountServiceEnum service;

        public static CredentialsFragment newInstance ()
        {
            var fragment = new CredentialsFragment ();
            return fragment;
        }


        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.CredentialsFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Credentials fragment";

            var button = view.FindViewById<Button> (Resource.Id.submit);
            button.Click += Button_Click;
            return view;
        }

        void Button_Click (object sender, EventArgs e)
        {
            var emailAddress = View.FindViewById<EditText> (Resource.Id.email).Text.Trim ();
            var password = View.FindViewById<EditText> (Resource.Id.password).Text;

            if (String.IsNullOrEmpty (emailAddress) || string.IsNullOrEmpty (password)) {
                return;
            }

            var parent = (LaunchActivity)Activity;
            parent.CredentialsFinished (service, emailAddress, password); 
        }
    }
}

