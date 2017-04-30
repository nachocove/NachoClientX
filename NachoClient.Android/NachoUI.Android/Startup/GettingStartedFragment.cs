
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
        
        private const int REQUEST_ADD_ACCOUNT = 1;

        #region Subviews

        Button ContinueButton;
        TextView TextView;

        void FindSubviews (View view)
        {
            ContinueButton = view.FindViewById (Resource.Id.submit) as Button;
            TextView = view.FindViewById (Resource.Id.welcome) as TextView;
            ContinueButton.Click += ContinueButtonClicked;
        }

        void DestroySubviews ()
        {
            ContinueButton.Click -= ContinueButtonClicked;
            ContinueButton = null;
            TextView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.GettingStartedFragment, container, false);
            FindSubviews (view);
            return view;
        }

        public override void OnDestroyView ()
        {
            DestroySubviews ();
            base.OnDestroyView ();
        }

        public override void OnResume ()
        {
            base.OnResume ();
            UpdateTextView ();
        }

        #endregion

        #region User Actions

        void ContinueButtonClicked (object sender, EventArgs e)
        {
            ShowAddAccount ();
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_ADD_ACCOUNT:
                HandleAddAccountResult (resultCode);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region Private Helpers

        void UpdateTextView ()
        {
            if (NcMdmConfig.Instance.IsPopulated && null == McAccount.GetMDMAccount ()) {
                var messageFormat = GetString (Resource.String.welcome_mdm_format);
                var name = String.IsNullOrEmpty (NcMdmConfig.Instance.BrandingName) ? GetString (Resource.String.welcome_company) : NcMdmConfig.Instance.BrandingName;
                TextView.Text = String.Format (messageFormat, name);
            } else {
                var account = McAccount.GetAccountBeingConfigured ();
                var welcomeResource = (account == null ? Resource.String.welcome_get_started : Resource.String.welcome_continue);
                TextView.SetText (welcomeResource);
            }
        }

        void ShowAddAccount ()
        {
            var intent = AddAccountActivity.BuildIntent (Activity);
            StartActivityForResult (intent, REQUEST_ADD_ACCOUNT);
        }

        void HandleAddAccountResult (Result resultCode)
        {
            if (resultCode == Result.Ok) {
                Activity.SetResult (Result.Ok);
                Activity.Finish ();
            }
        }

        #endregion
    }
}

