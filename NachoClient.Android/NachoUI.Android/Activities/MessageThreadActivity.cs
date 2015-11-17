﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class MessageThreadActivityData
    {
        public INachoEmailMessages ThreadMessages;
    }

    [Activity (Label = "MessageThreadActivity")]
    public class MessageThreadActivity : NcActivityWithData<INachoEmailMessages>, MessageListDelegate
    {
        private const string EXTRA_THREAD = "com.nachocove.nachomail.EXTRA_THREAD";

        private const string MESSAGE_LIST_FRAGMENT_TAG = "MessageList";

        public static Intent ShowThreadIntent (Context context, INachoEmailMessages threadMessages)
        {
            var intent = new Intent (context, typeof(MessageThreadActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_THREAD, IntentHelper.StoreValue (threadMessages));
            return intent;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.MessageThreadActivity);

            var threadMessages = RetainedData;
            if (null == threadMessages) {
                threadMessages = IntentHelper.RetrieveValue<INachoEmailMessages> (Intent.GetStringExtra (EXTRA_THREAD));
                RetainedData = threadMessages;
            }

            List<int> adds;
            List<int> deletes;
            threadMessages.Refresh (out adds, out deletes);

            MessageListFragment fragment = null;
            if (null != bundle) {
                fragment = FragmentManager.FindFragmentByTag<MessageListFragment> (MESSAGE_LIST_FRAGMENT_TAG);
            }
            if (null == fragment) {
                fragment = new MessageListFragment ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, fragment, MESSAGE_LIST_FRAGMENT_TAG).Commit ();
            }
            fragment.Initialize (threadMessages, MessageListFragment_onMessageClick);
        }

        protected override void OnResume ()
        {
            base.OnResume ();
        }

        void MessageListFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            var message = thread.FirstMessageSpecialCase ();
            var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
            StartActivity (intent);
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        void MessageListDelegate.ListIsEmpty ()
        {
            Finish ();
        }

        bool MessageListDelegate.ShowHotEvent ()
        {
            return false;
        }

        void MessageListDelegate.SetActiveImage (View view)
        {
            view.FindViewById<View> (Resource.Id.account).Visibility = ViewStates.Gone;

            var title = view.FindViewById<TextView> (Resource.Id.title);
            title.SetText (Resource.String.thread);
            title.Visibility = ViewStates.Visible;
        }

    }
}