
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
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class MessageListFragment : Fragment
    {
        Android.Widget.ListView listView;
        MessageListAdapter messageListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        INachoEmailMessages messages;

        Android.Widget.ImageView composeButton;

        public event EventHandler<McEmailMessageThread> onMessageClick;

        public static MessageListFragment newInstance (INachoEmailMessages messages)
        {
            var fragment = new MessageListFragment ();
            fragment.messages = messages;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                var nr = messages.StartSync ();
                rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
            };

            composeButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            composeButton.SetImageResource (Resource.Drawable.contact_newemail);
            composeButton.Visibility = Android.Views.ViewStates.Visible;
            composeButton.Click += ComposeButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.inbox_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_mail_active);

            messageListAdapter = new MessageListAdapter (messages);

            listView = view.FindViewById<Android.Widget.ListView> (Resource.Id.listView);
            listView.Adapter = messageListAdapter;

            listView.ItemClick += ListView_ItemClick;

            return view;
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onMessageClick) {
                onMessageClick (this, messageListAdapter [e.Position]);
            }
        }

        void ComposeButton_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (mSwipeRefreshLayout.Refreshing) {
                    mSwipeRefreshLayout.Refreshing = false;
                }
            });
        }

        NcTimer refreshTimer;

        void rearmRefreshTimer (int seconds)
        {
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
            refreshTimer = new NcTimer ("MessageListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (mSwipeRefreshLayout.Refreshing) {
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }
    }

    public class MessageListAdapter : Android.Widget.BaseAdapter<McEmailMessageThread>
    {
        INachoEmailMessages messages;

        public MessageListAdapter (INachoEmailMessages messages)
        {
            this.messages = messages;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override long GetItemId (int position)
        {
            return messages.GetEmailThread (position).FirstMessageId;
        }

        public override int Count {
            get {
                return messages.Count ();
            }
        }

        public override McEmailMessageThread this [int position] {  
            get { return messages.GetEmailThread (position); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
            }
            var thread = messages.GetEmailThread (position);
            var message = thread.FirstMessageSpecialCase ();
            Bind.BindMessageHeader (thread, message, view);
            return view;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshMessageIfVisible ();
                break;
            }
        }

        void RefreshMessageIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            if (messages.Refresh (out adds, out deletes)) {
                this.NotifyDataSetChanged ();
            }
        }


    }
}

