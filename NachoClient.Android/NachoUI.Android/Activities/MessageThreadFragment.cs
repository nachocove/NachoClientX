
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
    public class MessageThreadFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        MessageListAdapter messageListAdapter;

        McEmailMessageThread thread;

        public event EventHandler<McEmailMessage> onMessageClick;

        public static MessageThreadFragment newInstance (McEmailMessageThread thread)
        {
            var fragment = new MessageThreadFragment ();
            fragment.thread = thread;
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

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.inbox_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_mail_active);

            // FIXME -- not just inbox
            var inbox = McFolder.GetDefaultInboxFolder (NcApplication.Instance.Account.Id);
            var messages = new NachoThreadedEmailMessages (inbox, thread.GetThreadId ());
            messageListAdapter = new MessageListAdapter (messages);

            messageListAdapter.onMessageClick += MessageListAdapter_onMessageClick;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (messageListAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

        void MessageListAdapter_onMessageClick (object sender, McEmailMessageThread thread)
        {
            Console.WriteLine ("MessageListAdapter_onMessageClick: {0}", thread);
            if (null != onMessageClick) {
                var message = thread.SingleMessageSpecialCase ();
                onMessageClick (this, message);
            }
        }

    }
}