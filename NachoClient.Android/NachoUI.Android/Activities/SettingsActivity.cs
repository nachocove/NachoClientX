using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "@string/settings_label", ParentActivity = typeof (MainTabsActivity), LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]
    public class SettingsActivity : NcActivity
    {

        private const int REQUEST_ADD_ACCOUNT = 1;

        #region Intents

        public static Intent BuildIntent (Context context)
        {
            var intent = new Intent (context, typeof (SettingsActivity));
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        private void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        private void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.SettingsActivity);
            FindSubviews ();
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

        #region Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.settings, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.action_add_account:
                ShowAddAccount ();
                break;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region Private Helpers

        void ShowAddAccount ()
        {
            var intent = AddAccountActivity.BuildIntent (this);
            StartActivityForResult (intent, REQUEST_ADD_ACCOUNT);
        }

        void HandleAddAccountResult (Result resultCode)
        {
            var fragment = FragmentManager.FindFragmentById (Resource.Id.fragment) as SettingsFragment;
            fragment.Refresh ();
        }

        #endregion

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
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

    }
}
