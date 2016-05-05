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

        TextView inboxMessageCountView;

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

            inboxMessageCountView = view.FindViewById<TextView> (Resource.Id.inbox_message_count);

            Update (NcApplication.Instance.Account);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            inboxView.Click += InboxView_Click;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            inboxView.Click -= InboxView_Click;
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
                EmailHelper.GetMessageCounts (account, out unreadCount, out likelyCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    inboxMessageCountView.Text = String.Format ("Go to Inbox ({0:N0} unread)", unreadCount);
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }

    }
}

