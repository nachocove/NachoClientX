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

namespace NachoClient.AndroidClient
{
    public class HotMessageFragment : Android.App.Fragment
    {
        public event EventHandler<int> onMessageClick;

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

            return view;
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

