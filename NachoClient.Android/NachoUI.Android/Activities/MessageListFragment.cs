
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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class MessageListFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        MessageListAdapter messageListAdapter;

        INachoEmailMessages messages;

        Android.Widget.ImageView composeButton;

        public event EventHandler<int> onMessageClick;

        public MessageListFragment(INachoEmailMessages messages)
        {
            this.messages = messages;
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

            composeButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            composeButton.SetImageResource (Resource.Drawable.contact_newemail);
            composeButton.Visibility = Android.Views.ViewStates.Visible;
            composeButton.Click += ComposeButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.inbox_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_mail_active);

            messageListAdapter = new MessageListAdapter (messages);

            messageListAdapter.onMessageClick += MessageListAdapter_onMessageClick;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (messageListAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

        void ComposeButton_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
        }

        void MessageListAdapter_onMessageClick (object sender, int e)
        {
            Console.WriteLine ("MessageListAdapter_onMessageClick: {0}", e);
            if (null != onMessageClick) {
                onMessageClick (this, e);
            }
        }

    }

    public class MessageListAdapter : RecyclerView.Adapter
    {
        public event EventHandler<int> onMessageClick;

        INachoEmailMessages messages;

        public MessageListAdapter(INachoEmailMessages messages)
        {
            this.messages = messages;
        }

        class MessageHolder : RecyclerView.ViewHolder
        {
            Action<int> listener;

            public MessageHolder (View view, Action<int> listener) : base (view)
            {
                this.listener = listener;
                view.Click += View_Click;
            }

            void View_Click (object sender, EventArgs e)
            {
                listener (base.Position);
            }
        }

        public override int ItemCount {
            get {
                return messages.Count();
            }
        }

        public void OnClick (int position)
        {
            if (null != onMessageClick) {
                onMessageClick (this, position);
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
            return new MessageHolder (view, OnClick);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var thread = messages.GetEmailThread (position);
            var message = thread.FirstMessageSpecialCase ();
            Bind.BindMessageHeader (thread, message, holder.ItemView);

            var previewView = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
            var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
            previewView.Text = cookedPreview;
        }


    }
}

