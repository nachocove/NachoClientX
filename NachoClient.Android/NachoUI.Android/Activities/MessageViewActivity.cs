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
    [Activity (Label = "MessageViewActivity")]
    public class MessageViewActivity : NcActivity, IMessageViewFragmentOwner
    {
        private const string EXTRA_THREAD = "com.nachocove.nachomail.EXTRA_THREAD";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        private McEmailMessageThread thread;
        private McEmailMessage message;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            this.thread = IntentHelper.RetrieveValue<McEmailMessageThread> (Intent.GetStringExtra (EXTRA_THREAD));
            this.message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));

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
