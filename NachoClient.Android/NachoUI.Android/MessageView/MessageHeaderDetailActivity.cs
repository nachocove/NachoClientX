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
    [Activity ()]
    public class MessageHeaderDetailActivity : NcActivity
    {

        public const string EXTRA_MESSAGE_ID = "NachoClient.NachoAndroid.MessageHeaderDetailActivity.EXTRA_MESSAGE_ID";

        McEmailMessage Message;

        #region Intents

        public static Intent BuildIntent (Context context, int messageId)
        {
            var intent = new Intent (context, typeof (MessageHeaderDetailActivity));
            intent.PutExtra (EXTRA_MESSAGE_ID, messageId);
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
            SetContentView (Resource.Layout.MessageHeaderDetailActivity);
            FindSubviews ();
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is MessageHeaderDetailFragment) {
                (fragment as MessageHeaderDetailFragment).Message = Message;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        void PopulateFromIntent ()
        {
            var messageId = Intent.Extras.GetInt (EXTRA_MESSAGE_ID);
            Message = McEmailMessage.QueryById<McEmailMessage> (messageId);
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
    }
}
