//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class AttachmentsView : LinearLayout, MessageAttachmentsAdapter.Listener, AttachmentDownloaderDelegate
    {
        int NumberOfItemsToCollapse = 3;
        bool IsExpanded;
        MessageAttachmentsAdapter AttachmentsAdapter;
        public List<McAttachment> Attachments {
            get {
                return AttachmentsAdapter.Attachments;
            }
            set {
                AttachmentsAdapter.Attachments = value;
                IsExpanded = false;
                if (value.Count < NumberOfItemsToCollapse) {
                    HeaderView.Visibility = ViewStates.Gone;
                    AttachmentsListView.Visibility = ViewStates.Visible;
                    IsExpanded = true;
                } else {
                    HeaderView.Visibility = ViewStates.Visible;
                    AttachmentsListView.Visibility = ViewStates.Gone;
                }
                UpdateHeader ();
            }
        }
        Dictionary<int, AttachmentDownloader> DownloadersByAttachmentId;

        public event EventHandler<McAttachment> SelectAttachment;

        #region Subviews

        View HeaderView;
        RecyclerView AttachmentsListView;
        TextView HeaderLabel;
        ImageView HeaderExpansionIndicator;

        void FindSubviews ()
        {
            HeaderView = FindViewById (Resource.Id.header_view);
            AttachmentsListView = FindViewById (Resource.Id.attachments_list) as RecyclerView;
            AttachmentsListView.SetLayoutManager (new LinearLayoutManager (Context));
            HeaderLabel = FindViewById (Resource.Id.header_label) as TextView;
            HeaderExpansionIndicator = FindViewById (Resource.Id.expansion_indicator) as ImageView;
        }

        #endregion

        #region Creating an attachments view

        public AttachmentsView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public AttachmentsView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public AttachmentsView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            DownloadersByAttachmentId = new Dictionary<int, AttachmentDownloader> ();
            LayoutInflater.From (Context).Inflate (Resource.Layout.AttachmentsView, this);
            FindSubviews ();
            AttachmentsAdapter = new MessageAttachmentsAdapter (this);
            AttachmentsListView.SetAdapter (AttachmentsAdapter);
            HeaderView.Click += HeaderViewClicked;
        }

        public void Cleanup ()
        {
            HeaderView.Click -= HeaderViewClicked;
            foreach (var pair in DownloadersByAttachmentId) {
                pair.Value.Delegate = null;
            }
            DownloadersByAttachmentId.Clear ();
        }

        #endregion

        #region User Actions

        public void OnAttachmentSelected (McAttachment attachment)
        {
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                SelectAttachment.Invoke (this, attachment);
            } else {
                if (!DownloadersByAttachmentId.ContainsKey (attachment.Id)) {
                    var downloader = new AttachmentDownloader ();
                    DownloadersByAttachmentId.Add (attachment.Id, downloader);
                    downloader.Delegate = this;
                    downloader.Download (attachment);
                }
                ReplaceAttachment (McAttachment.QueryById<McAttachment> (attachment.Id));
            }
        }

        void HeaderViewClicked (object sender, EventArgs e)
        {
            IsExpanded = !IsExpanded;
            if (IsExpanded) {
                AttachmentsListView.Visibility = ViewStates.Visible;
            } else {
                AttachmentsListView.Visibility = ViewStates.Gone;
            }
            UpdateHeader ();
        }

        #endregion

        void UpdateHeader ()
        {
            if (IsExpanded) {
                HeaderLabel.SetText (Resource.String.attachments_header_hide);
                HeaderExpansionIndicator.SetImageResource (Resource.Drawable.gen_readmore_active);
            } else {
                var format = Context.GetString (Resource.String.attachments_header_show_format);
                HeaderLabel.Text = String.Format (format, Attachments.Count);
                HeaderExpansionIndicator.SetImageResource (Resource.Drawable.gen_readmore);
            }
        }

        public void AttachmentDownloadDidFinish (AttachmentDownloader downloader)
        {
            DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
            ReplaceAttachment (downloader.Attachment);
        }

        public void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result)
        {
            DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
            ReplaceAttachment (downloader.Attachment);
        }

        void ReplaceAttachment (McAttachment attachment)
        {
            for (int i = 0; i < Attachments.Count; ++i) {
                if (Attachments [i].Id == attachment.Id) {
                    Attachments.RemoveAt (i);
                    Attachments.Insert (i, attachment);
                    AttachmentsAdapter.NotifyItemChanged (i);
                    break;
                }
            }
        }
    }

    class MessageAttachmentsAdapter : RecyclerView.Adapter
    {

        List<McAttachment> _Attachments;
        public List<McAttachment> Attachments {
            get {
                return _Attachments;
            }
            set {
                _Attachments = value;
                NotifyDataSetChanged ();
            }
        }

        public interface Listener
        {
            void OnAttachmentSelected (McAttachment attachment);
        }

        WeakReference<Listener> WeakListener;

        public MessageAttachmentsAdapter (Listener listener)
        {
            WeakListener = new WeakReference<Listener> (listener);
            Attachments = new List<McAttachment> ();
        }

        public override int ItemCount {
            get {
                return Attachments.Count;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            return MessageAttachmentViewHolder.Create (parent);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var attachmentHolder = (holder as MessageAttachmentViewHolder);
            attachmentHolder.SetAttachment (Attachments [position]);
            attachmentHolder.SetClickHandler ((sender, e) => {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnAttachmentSelected (Attachments [position]);
                }
            });
        }

    }

    class MessageAttachmentViewHolder : RecyclerView.ViewHolder
    {

        ImageView IconView;
        TextView NameLabel;
        TextView DetailLabel;
        View DownloadFrame;
        ImageView DownloadIndicator;
        ImageView ErrorIndicator;
        ProgressBar DownloadProgress;
        EventHandler ClickHandler;

        public static MessageAttachmentViewHolder Create (ViewGroup parent)
        {
            var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttachmentListItem, parent, false);
            return new MessageAttachmentViewHolder (view);
        }

        public MessageAttachmentViewHolder (View view) : base (view)
        {
            IconView = view.FindViewById (Resource.Id.icon) as ImageView;
            NameLabel = view.FindViewById (Resource.Id.attachment_name) as TextView;
            DetailLabel = view.FindViewById (Resource.Id.attachment_detail) as TextView;
            DownloadFrame = view.FindViewById (Resource.Id.attachment_download_frame);
            ErrorIndicator = DownloadFrame.FindViewById (Resource.Id.error_indicator) as ImageView;
            DownloadIndicator = DownloadFrame.FindViewById (Resource.Id.download_indicator) as ImageView;
            DownloadProgress = DownloadFrame.FindViewById (Resource.Id.download_progress) as ProgressBar;
        }

        public void SetAttachment (McAttachment attachment)
        {
            var name = Path.GetFileNameWithoutExtension (attachment.DisplayName);
            if (String.IsNullOrEmpty (name)) {
                name = "(no name)";
            }
            IconView.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
            NameLabel.Text = name;
            DetailLabel.Text = Pretty.GetAttachmentDetail (attachment);
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Error) {
                DownloadFrame.Visibility = ViewStates.Visible;
                ErrorIndicator.Visibility = ViewStates.Visible;
                DownloadIndicator.Visibility = ViewStates.Gone;
                DownloadProgress.Visibility = ViewStates.Gone;
            } else if (attachment.FilePresence != McAbstrFileDesc.FilePresenceEnum.Complete) {
                DownloadFrame.Visibility = ViewStates.Visible;
                ErrorIndicator.Visibility = ViewStates.Gone;
                if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Partial) {
                    var pending = McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id);
                    if (pending != null && pending.State != McPending.StateEnum.Failed) {
                        DownloadIndicator.Visibility = ViewStates.Gone;
                        DownloadProgress.Visibility = ViewStates.Visible;
                    } else {
                        DownloadIndicator.Visibility = ViewStates.Visible;
                        DownloadProgress.Visibility = ViewStates.Gone;
                    }
                } else {
                    DownloadIndicator.Visibility = ViewStates.Visible;
                    DownloadProgress.Visibility = ViewStates.Gone;
                }
            } else {
                DownloadFrame.Visibility = ViewStates.Gone;
            }
        }

        public void SetClickHandler (EventHandler handler)
        {
            if (ClickHandler != null) {
                ItemView.Click -= ClickHandler;
            }
            ClickHandler = handler;
            if (ClickHandler != null) {
                ItemView.Click += ClickHandler;
            }
        }
    }
}
