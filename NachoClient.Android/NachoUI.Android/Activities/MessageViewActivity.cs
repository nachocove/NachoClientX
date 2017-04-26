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
            SetContentView (Resource.Layout.MessageViewActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
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

    /*
    public class MessageViewActivityData
    {
        public McEmailMessageThread Thread;
        public McEmailMessage Message;
    }

    [Activity (Label = "MessageViewActivity")]
    public class MessageViewActivity : NcActivityWithData<MessageViewActivityData>, IMessageViewFragmentOwner
    {
        private const string EXTRA_THREAD = "com.nachocove.nachomail.EXTRA_THREAD";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        private McEmailMessageThread thread;
        private McEmailMessage message;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            var dataFromIntent = RetainedData;
            if (null == dataFromIntent) {
                dataFromIntent = new MessageViewActivityData ();
                dataFromIntent.Thread = IntentHelper.RetrieveValue<McEmailMessageThread> (Intent.GetStringExtra (EXTRA_THREAD));
                dataFromIntent.Message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));
                RetainedData = dataFromIntent;
            }
            this.thread = dataFromIntent.Thread;
            this.message = dataFromIntent.Message;

            SetContentView (Resource.Layout.MessageViewActivity);
        }

        public static Intent ShowMessageIntent (Context context, McEmailMessageThread thread, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(MessageViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_THREAD, IntentHelper.StoreValue (thread));
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            return intent;
        }

        void IMessageViewFragmentOwner.DoneWithMessage()
        {
            Finish();
        }

        McEmailMessage IMessageViewFragmentOwner.MessageToView {
            get {
                return this.message;
            }
        }

        McEmailMessageThread IMessageViewFragmentOwner.ThreadToView {
            get {
                return this.thread;
            }
        }
    }
    */
}
