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
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class HotSummaryFragment : Fragment
    {

        View inboxView;
        View deferredView;
        View deadlinesView;

        TextView inboxMessageCountView;
        TextView deferredMessageCountView;
        TextView deadlinesMessageCountView;

        // Display first message of a thread in a cardview
        public static HotSummaryFragment newInstance ()
        {
            var fragment = new HotSummaryFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.HotSummaryFragment, container, false);

            inboxView = view.FindViewById<View> (Resource.Id.go_to_inbox);
            deferredView = view.FindViewById<View> (Resource.Id.go_to_deferred);
            deadlinesView = view.FindViewById<View> (Resource.Id.go_to_deadlines);

            inboxMessageCountView = view.FindViewById<TextView> (Resource.Id.inbox_message_count);
            deferredMessageCountView = view.FindViewById<TextView> (Resource.Id.deferred_message_count);
            deadlinesMessageCountView = view.FindViewById<TextView> (Resource.Id.deadlines_message_count);

            Update (NcApplication.Instance.Account);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            inboxView.Click += InboxView_Click;
            deferredView.Click += DeferredView_Click;
            deadlinesView.Click += DeadlinesView_Click;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            inboxView.Click -= InboxView_Click;
            deferredView.Click -= DeferredView_Click;
            deadlinesView.Click -= DeadlinesView_Click;
        }

        void DeadlinesView_Click (object sender, EventArgs e)
        {
        }

        void DeferredView_Click (object sender, EventArgs e)
        {
        }

        void InboxView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (Activity, typeof(InboxActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        public void Update (McAccount account)
        {
            NcTask.Run (() => {
                int unreadCount;
                int likelyCount;
                int deferredCount;
                int deadlineCount;
                EmailHelper.GetMessageCounts (account, out unreadCount, out deferredCount, out deadlineCount, out likelyCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    inboxMessageCountView.Text = String.Format ("Go to Inbox ({0:N0} unread)", unreadCount);
                    deferredMessageCountView.Text = String.Format ("Go to Deferred Messages ({0:N0})", deferredCount);
                    deadlinesMessageCountView.Text = String.Format ("Go to Deadlines ({0:N0})", deadlineCount);
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }

    }
}

