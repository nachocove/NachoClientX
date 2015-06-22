using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Support.V4.App;
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
    public class HotMessageFragment : Android.App.Fragment
    {
        public event EventHandler<int> onMessageClick;

        McEmailMessageThread thread;
        INachoEmailMessages threads;

        public HotMessageFragment (McEmailMessageThread thread, INachoEmailMessages threads) : base ()
        {
            this.thread = thread;
            this.threads = threads;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);

            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.HotMessageFragment, container, false);

            view.Click += View_Click;

            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click += ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click += ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click += ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click += ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click += DeleteButton_Click;

            var chiliButton = view.FindViewById (Resource.Id.chili);
            chiliButton.Click += ChiliButton_Click;

            BindValues (view);

            return view;
        }
            
        void BindValues (View view)
        {
            var message = thread.FirstMessageSpecialCase ();

            Bind.BindMessageHeader (thread, message, view);

            var bodyView = view.FindViewById<Android.Widget.TextView> (Resource.Id.body);
            bodyView.Visibility = ViewStates.Visible;

            if (null == message) {
                bodyView.SetText (Resource.String.message_not_available);
                return;
            }
               
            var body = McBody.QueryById<McBody> (message.BodyId);

            if (!McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                // FIXME download body
                return;
            }

            var text = MimeHelpers.ExtractTextPart (message);
            if (null == text) {
                bodyView.Text = "No text available.";
            } else {
                bodyView.Text = text;
            }
            bodyView.Visibility = ViewStates.Visible;
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ChiliButton_Click");
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ArchiveButton_Click");
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("DeleteButton_Click");
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ForwardButton_Click");
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ReplyButton_Click");
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ReplyAllButton_Click");
        }

        void View_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("View_Click");
            if (null != onMessageClick) {
                // FIXME: position
                onMessageClick (this, 42);
            }
        }
    }
}

