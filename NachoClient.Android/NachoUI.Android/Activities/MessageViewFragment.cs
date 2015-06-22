
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

namespace NachoClient.AndroidClient
{
    public class MessageViewFragment : Fragment
    {
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
            var view = inflater.Inflate (Resource.Layout.MessageViewFragment, container, false);

            var saveButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            saveButton.SetImageResource (Resource.Drawable.folder_move);
            saveButton.Visibility = Android.Views.ViewStates.Visible;
            saveButton.Click += SaveButton_Click;

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

        void SaveButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("SaveButton_Click");
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
        }
    }
}

