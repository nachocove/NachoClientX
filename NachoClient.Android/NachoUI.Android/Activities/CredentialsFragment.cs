
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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class CredentialsFragment : Fragment
    {
        McAccount.AccountServiceEnum service;

        public static CredentialsFragment newInstance (McAccount.AccountServiceEnum service)
        {
            var fragment = new CredentialsFragment ();
            fragment.service = service;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.CredentialsFragment, container, false);

            var button = view.FindViewById<Button> (Resource.Id.submit);
            button.Click += Button_Click;

            var imageview = view.FindViewById<RoundedImageView> (Resource.Id.service_image);
            var labelview = view.FindViewById<TextView> (Resource.Id.service_prompt);

            imageview.SetImageResource (Util.GetAccountServiceImageId(service));

            var serviceFormat = GetString (Resource.String.get_credentials);
            labelview.Text = String.Format(serviceFormat, NcServiceHelper.AccountServiceName (service));

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

