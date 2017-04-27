//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

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

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    [Activity (Label="@string/folder_picker_title")]
    public class FoldersActivity : NcActivity
    {

        public const string EXTRA_ACCOUNT_ID = "NachoClient.NachoAndroid.FoldersActivity.EXTRA_ACCOUNT_ID";
        public const string EXTRA_FOLDER_ID = "NachoClient.NachoAndroid.FoldersActivity.EXTRA_FOLDER_ID";

        McAccount Account;

        #region Intents

        public static Intent BuildIntent (Context context, int accountId)
        {
            var intent = new Intent (context, typeof (FoldersActivity));
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.FoldersActivity);
            FindSubviews ();
            Toolbar.Subtitle = String.IsNullOrEmpty (Account.DisplayName) ? Account.EmailAddr : Account.DisplayName;
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Android.Support.V4.App.Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is FolderListFragment) {
                var folderFragment = (fragment as FolderListFragment);
                folderFragment.Account = Account;
                folderFragment.IsPicker = true;
                folderFragment.PickFolder += FolderPicked;
            }
        }

        protected override void OnDestroy ()
        {
            foreach (var fragment in SupportFragmentManager.Fragments) {
                if (fragment is FolderListFragment) {
                    (fragment as FolderListFragment).PickFolder -= FolderPicked;
                }
            }
            ClearSubviews ();
            base.OnDestroy ();
        }

        void PopulateFromIntent ()
        {
            var accountId = Intent.Extras.GetInt (EXTRA_ACCOUNT_ID);
            Account = McAccount.QueryById<McAccount> (accountId);
        }

        #endregion

        #region Menu

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        void FolderPicked (object sender, NachoCore.Model.McFolder folder)
        {
            var intent = new Intent ();
            intent.PutExtra (EXTRA_FOLDER_ID, folder.Id);
            SetResult (Result.Ok, intent);
            Finish ();
        }
    }
}
