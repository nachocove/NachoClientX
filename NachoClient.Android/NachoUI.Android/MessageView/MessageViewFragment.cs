
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
using Android.Support.V4.View;
using Android.Support.V7.Widget;
using Android.Webkit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;
using Android.Content.PM;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{

    public class MessageViewFragment : Fragment, MessageDownloadDelegate
    {

        McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                if (_Message.cachedHasAttachments) {
                    Attachments = McAttachment.QueryByItem (_Message);
                } else {
                    Attachments = new List<McAttachment> ();
                }
                if (Message.BodyId != 0) {
                    Bundle = new NcEmailMessageBundle (_Message);
                } else {
                    Bundle = null;
                }
            }
        }
        List<McAttachment> Attachments;
        NcEmailMessageBundle Bundle;
        MessageDownloader BodyDownloader;

        #region Subviews

        MessageHeaderView HeaderView;
        AttachmentsView AttachmentsView;
        WebView BodyView;
        TextView ErrorLabel;

        void FindSubviews (View view)
        {
            HeaderView = view.FindViewById (Resource.Id.message_header) as MessageHeaderView;
            BodyView = view.FindViewById (Resource.Id.webview) as WebView;
            ErrorLabel = view.FindViewById (Resource.Id.error_label) as TextView;
            AttachmentsView = view.FindViewById (Resource.Id.attachments_view) as AttachmentsView;
            HeaderView.Click += HeaderViewClicked;
            ErrorLabel.Click += ErrorLabelClicked;
            AttachmentsView.SelectAttachment += AttachmentSelected;
        }

        void ClearSubviews ()
        {
            HeaderView.Click -= HeaderViewClicked;
            ErrorLabel.Click -= ErrorLabelClicked;
            AttachmentsView.SelectAttachment -= AttachmentSelected;
            AttachmentsView.Cleanup ();
            HeaderView = null;
            BodyView = null;
            ErrorLabel = null;
            AttachmentsView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageViewFragment, container, false);
            FindSubviews (view);

            Update ();

            if (Bundle == null || Bundle.NeedsUpdate) {
                StartBodyDownload ();
            } else {
                DisplayMessageBody ();
                if (!Message.IsRead) {
                    EmailHelper.MarkAsRead (Message);
                }
            }

            return view;
        }

        public override void OnDestroyView ()
        {
            if (BodyDownloader != null) {
                BodyDownloader.Delegate = null;
                BodyDownloader = null;
            }
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Message Loading

        void StartBodyDownload ()
        {
            BodyDownloader = new MessageDownloader ();
            BodyDownloader.Delegate = this;
            BodyDownloader.Bundle = Bundle;
            BodyDownloader.Download (Message);
        }

        void RetryDownload ()
        {
            ErrorLabel.Visibility = ViewStates.Gone;
            BodyView.Visibility = ViewStates.Visible;
            StartBodyDownload ();
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            EmailHelper.MarkAsRead (Message);
            if (Bundle == null) {
                Bundle = downloader.Bundle;
            }
            DisplayMessageBody ();
            BodyDownloader.Delegate = null;
            BodyDownloader = null;
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            ShowDownloadErrorForResult (result);
            BodyDownloader.Delegate = null;
            BodyDownloader = null;
        }

        #endregion

        #region View updates

        void DisplayMessageBody ()
        {
            if (Bundle != null) {
                if (Bundle.FullHtmlUrl != null) {
                    Log.Info (Log.LOG_UI, "MessageViewFragment DisplayMessageBody() using uri");
                    BodyView.LoadUrl (Bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    Log.Info (Log.LOG_UI, "MessageViewFragment DisplayMessageBody() using html");
                    var html = Bundle.FullHtml;
                    if (html == null) {
                        Log.Error (Log.LOG_UI, "MessageViewFragment DisplayMessageBody() null html");
                        html = "<html><body><div><br></div></body></html>";
                    }
                    BodyView.LoadDataWithBaseURL (Bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf8", null);
                }
            } else {
                Log.Error (Log.LOG_UI, "MessageViewFragment DisplayMessageBody() called without a valid bundle");
                // TODO: show alert
            }
        }

        void Update ()
        {
            HeaderView.SetMessage (Message);
            AttachmentsView.Attachments = Attachments;
        }

        void ShowDownloadErrorForResult (NcResult result)
        {
            var canRetryDownload = result.Why != NcResult.WhyEnum.MissingOnServer;
            if (canRetryDownload) {
                ErrorLabel.Text = "Message download failed. Tap here to retry.";
            } else {
                ErrorLabel.Text = "Message download failed.";
            }
            ErrorLabel.Clickable = canRetryDownload;
            ErrorLabel.Visibility = ViewStates.Visible;
            BodyView.Visibility = ViewStates.Gone;
            // TODO: show preview?
        }

        #endregion

        #region User Actions

        void ErrorLabelClicked (object sender, EventArgs e)
        {
            RetryDownload ();
        }

        void HeaderViewClicked (object sender, EventArgs e)
        {
            ShowHeaderDetails ();
        }

        void AttachmentSelected (object sender, NachoCore.Model.McAttachment e)
        {
            AttachmentHelper.OpenAttachment (Activity, e);
        }

        #endregion

        #region Private Helpers

        void ShowHeaderDetails ()
        {
            var intent = MessageHeaderDetailActivity.BuildIntent (Activity, Message.Id);
            StartActivity (intent);
        }

        #endregion
    }

}
