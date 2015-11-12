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
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
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
}
