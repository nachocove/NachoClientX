
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
    public interface GettingStartedDelegate
    {
        void GettingStartedFinished();
    }

    public class GettingStartedFragment : Fragment
    {
        // Just shows "Welcome to Nacho Mail"
        public static GettingStartedFragment newInstance ()
        {
            var fragment = new GettingStartedFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.GettingStartedFragment, container, false);
            var submitButton = view.FindViewById<Button> (Resource.Id.submit);
            submitButton.Click += SubmitButton_Click;
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();

            int welcomeId;
            var textView = View.FindViewById<TextView> (Resource.Id.welcome);
            if (NcMdmConfig.Instance.IsPopulated && null == McAccount.GetMDMAccount ()) {
                var fmt = GetString (Resource.String.mdmgettingstarted);
                textView.Text = string.Format (fmt,
                    !string.IsNullOrEmpty (NcMdmConfig.Instance.BrandingName) ? NcMdmConfig.Instance.BrandingName : "company");
            } else {
                var account = McAccount.GetAccountBeingConfigured ();
                welcomeId = (null == account ? Resource.String.gettingstarted : Resource.String.welcome_back);
                textView.SetText (welcomeId);
            }
        }

        void SubmitButton_Click (object sender, EventArgs e)
        {
            var parent = (GettingStartedDelegate)Activity;
            parent.GettingStartedFinished ();
        }
    }
}

