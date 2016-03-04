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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ChatListActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class ChatListActivity : NcTabBarActivity
    {
        private const string CHAT_LIST_FRAGMENT_TAG = "ChatListFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.ChatListActivity);

            ChatListFragment fragment = null;
            if (null != bundle) {
                fragment = FragmentManager.FindFragmentByTag<ChatListFragment> (CHAT_LIST_FRAGMENT_TAG);
            }
            if (null == fragment) {
                fragment = ChatListFragment.newInstance ();
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, fragment, CHAT_LIST_FRAGMENT_TAG).Commit ();
            }
            fragment.onChatClick += ChatListFragment_onChatClick;
        }

        void ChatListFragment_onChatClick (object sender, McChat chat)
        {
            // StartActivity (ChatViewActivity.ShowChatIntent (this, chat));
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is ChatListFragment) {
                ((ChatListFragment)f).OnBackPressed ();
            }
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
