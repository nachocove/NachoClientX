
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
        Android.Widget.ListView listView;
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

            listView.ItemClick += ListView_ItemClick;

            listView = view.FindViewById<Android.Widget.ListView> (Resource.Id.listView);
            listView.Adapter = messageListAdapter;

            return view;
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            Console.WriteLine ("MessageListAdapter_onMessageClick: {0}", thread);
            if (null != onMessageClick) {
                var thread = McEmailMessage.QueryById<McEmailMessage> (e.Position);
                onMessageClick (this, thread);
            }
        }

    }
}