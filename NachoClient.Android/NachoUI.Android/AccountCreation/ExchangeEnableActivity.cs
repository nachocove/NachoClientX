//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using Android.Content;
using Android.Support.V7.Widget;

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    [Android.App.Activity()]
    public class ExchangeEnableActivity : NcActivity, ExchangeEnableFragment.Listener
    {

        public const string EXTRA_SERVICE = "NachoClient.AndroidClient.ExchangeEnableActivity.EXTRA_SERVICE";
        McAccount.AccountServiceEnum Service;

        #region Intents

        public static Intent BuildIntent (Context context, McAccount.AccountServiceEnum service)
        {
            var intent = new Intent (context, typeof (ExchangeEnableActivity));
            intent.PutExtra (EXTRA_SERVICE, (int)service);
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

        protected override void OnCreate (Android.OS.Bundle savedInstanceState)
        {
            Service = (McAccount.AccountServiceEnum)Intent.GetIntExtra (EXTRA_SERVICE, 0);
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.ExchangeEnableActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Android.App.Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is ExchangeEnableFragment){
                (fragment as ExchangeEnableFragment).Service = Service;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

        #region Options Menu

        public override bool OnOptionsItemSelected (Android.Views.IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region Fragment Listener

        public void OnExchangeEnabled ()
        {
            FinishWithData ();
        }

        #endregion

        #region Private Helpers

        void FinishWithData ()
        {
            var data = new Intent ();
            data.PutExtra (EXTRA_SERVICE, (int)Service);
            SetResult (Android.App.Result.Ok, data);
            Finish ();
        }

        #endregion
    }
}
