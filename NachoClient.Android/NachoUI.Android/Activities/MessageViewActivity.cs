//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
using Android.Support.Design.Widget;

using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{

    [Activity ()]
    public class MessageViewActivity : NcActivity
    {

        public const string EXTRA_MESSAGE_ID = "NachoClient.AndroidClient.MessageViewActivity.EXTRA_MESSAGE_ID";

        McEmailMessage Message;

        #region Intents

        public static Intent BuildIntent (Context context, int messageId)
        {
            var intent = new Intent (context, typeof (MessageViewActivity));
            intent.PutExtra (EXTRA_MESSAGE_ID, messageId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        FloatingActionButton FloatingActionButton;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            FloatingActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
            FloatingActionButton = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.MessageViewActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            FloatingActionButton.Click += FloatingActionButtonClicked;
        }

        void PopulateFromIntent ()
        {
            var bundle = Intent.Extras;
            var messageId = bundle.GetInt (EXTRA_MESSAGE_ID);
            Message = McEmailMessage.QueryById<McEmailMessage> (messageId);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is MessageViewFragment) {
                (fragment as MessageViewFragment).Message = Message;
            }
        }

        protected override void OnDestroy ()
        {
            FloatingActionButton.Click -= FloatingActionButtonClicked;
            base.OnDestroy ();
        }

        #endregion

        #region Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.message_view, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            case Resource.Id.forward:
                Forward ();
                return true;
            case Resource.Id.create_event:
                return true;
            case Resource.Id.move:
                ShowMoveOptions ();
                return true;
            case Resource.Id.archive:
                Archive ();
                return true;
            case Resource.Id.delete:
                Delete ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void FloatingActionButtonClicked (object sender, EventArgs e)
        {
            Reply ();
        }

        #endregion

        #region Private Helpers

        void Reply ()
        {
            var intent = MessageComposeActivity.RespondIntent (this, NachoCore.Utils.EmailHelper.Action.ReplyAll, Message);
            StartActivity (intent);
        }

        void Forward ()
        {
            var intent = MessageComposeActivity.RespondIntent (this, NachoCore.Utils.EmailHelper.Action.Forward, Message);
			StartActivity (intent);
        }

        void CreateEvent ()
        {
        }

        void ShowMoveOptions ()
        {
        }

        void Archive ()
        {
            if (Message.StillExists ()) {
                NcEmailArchiver.Archive (Message);
            }
            Finish ();
        }

        void Delete ()
        {
            if (Message.StillExists ()) {
                NcEmailArchiver.Delete (Message);
            }
            Finish ();
        }

        #endregion
    }

}
