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
    public class ChatViewActivityData
    {
        public McChat chat;
    }

    [Activity (Label = "ChatViewActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class ChatViewActivity :  NcActivityWithData<ChatViewActivityData>, IChatViewFragmentOwner
    {
        private const string EXTRA_CHAT = "com.nachocove.nachomail.EXTRA_CHAT";
        private const string CHATVIEW_FRAGMENT_TAG = "ChatViewFragment";

        private McChat chat;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            var dataFromIntent = RetainedData;
            if (null == dataFromIntent) {
                dataFromIntent = new ChatViewActivityData ();
                dataFromIntent.chat = IntentHelper.RetrieveValue<McChat> (Intent.GetStringExtra (EXTRA_CHAT));
                RetainedData = dataFromIntent;
            }
            this.chat = dataFromIntent.chat;

            SetContentView (Resource.Layout.ChatViewActivity);
        }

        public static Intent ShowChatIntent (Context context, McChat chat)
        {
            var intent = new Intent (context, typeof(ChatViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_CHAT, IntentHelper.StoreValue (chat));
            return intent;
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is ChatViewFragment) {
                ((ChatViewFragment)f).OnBackPressed ();
            }
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        void IChatViewFragmentOwner.DoneWithMessage ()
        {
            Finish ();
        }

        McChat IChatViewFragmentOwner.ChatToView {
            get {
                return this.chat;
            }
        }
    }
}
