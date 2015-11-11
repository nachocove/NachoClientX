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

namespace NachoClient.AndroidClient
{

    public class MessageComposeAttachmentsView : LinearLayout
    {

        public MessageComposeHeaderView HeaderView;
        TextView AttachmentsLabel;
        ImageButton AddButton;
        LinearLayout AttachmentListView;
        List<McAttachment> Attachments;
        bool IsListViewHidden;

        public MessageComposeAttachmentsView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public MessageComposeAttachmentsView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public MessageComposeAttachmentsView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            Attachments = new List<McAttachment> ();

            var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.MessageComposeAttachmentsView, this);

            AttachmentsLabel = view.FindViewById<TextView> (Resource.Id.attachments_label);
            AttachmentsLabel.Text = "Attachments";
            AddButton = view.FindViewById<ImageButton> (Resource.Id.add_attachment);
            AttachmentListView = view.FindViewById<LinearLayout> (Resource.Id.attachment_list_view);

            AttachmentsLabel.Click += ClickLabel;
            AddButton.Click += ClickAddButton;
        }

        void ClickAddButton (object sender, EventArgs e)
        {
            ShowBrowser ();
        }

        void ClickLabel (object sender, EventArgs e)
        {
            if (Attachments.Count == 0) {
                ShowBrowser ();
            } else {
                ToggleListView ();
            }
        }

        void AttachmentClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
        }

        void ShowBrowser ()
        {
            if (HeaderView != null && HeaderView.Delegate != null){
                HeaderView.Delegate.MessageComposeHeaderViewDidSelectAddAttachment (HeaderView);
            }
        }

        void ToggleListView ()
        {
            IsListViewHidden = !IsListViewHidden;
            AttachmentListView.Visibility = IsListViewHidden ? ViewStates.Gone : ViewStates.Visible;
        }

        public void SetAttachments (List<McAttachment> attachments)
        {
            Attachments = attachments;
            if (attachments.Count > 0) {
                AttachmentsLabel.Text = String.Format ("Attachments ({0})", attachments.Count);
            } else {
                AttachmentsLabel.Text = "Attachments";
            }
            RedrawAttachments ();
        }

        public void AddAttachment (McAttachment attachment)
        {
            Attachments.Add (attachment);
            AddViewForAttachment (attachment);
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            Attachments.Remove (attachment);
            RedrawAttachments ();
        }

        void RedrawAttachments ()
        {
            AttachmentListView.RemoveAllViews ();
            foreach (var attachment in Attachments) {
                AddViewForAttachment (attachment);
            }
        }

        void AddViewForAttachment (McAttachment attachment)
        {
            var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.AttachmentListViewCell, null);
            var deleteButton = view.FindViewById<ImageButton> (Resource.Id.attachment_remove);
            deleteButton.Visibility = ViewStates.Visible;
            Bind.BindAttachmentView (attachment, view);
            AttachmentListView.AddView (view);
            view.Click += (object sender, EventArgs e) => {
                if (HeaderView != null && HeaderView.Delegate != null){
                    HeaderView.Delegate.MessageComposeHeaderViewDidSelectAttachment (HeaderView, attachment);
                }
            };
            deleteButton.Click += (object sender, EventArgs e) => {
                RemoveAttachment (attachment);
                if (HeaderView != null && HeaderView.Delegate != null){
                    HeaderView.Delegate.MessageComposeHeaderViewDidRemoveAttachment (HeaderView, attachment);
                }
            };
        }

    }
}

