using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CoreGraphics;
using Foundation;
using UIKit;
using CoreAnimation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using MimeKit;
using DDay.iCal;
using DDay.iCal.Serialization;
using DDay.iCal.Serialization.iCalendar;

namespace NachoClient.iOS
{

    public partial class MessageViewController : NcUIViewController, INachoFolderChooserParent, IUIWebViewDelegate, MessageDownloadDelegate, IUIScrollViewDelegate, AttachmentsViewDelegate
    {
        
        private static ConcurrentStack<UIWebView> ReusableWebViews = new ConcurrentStack<UIWebView> ();

        #region Properties

        McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                Attachments = McAttachment.QueryByItem (_Message);
                if (_Message.BodyId != 0) {
                    Bundle = new NcEmailMessageBundle (Message);
                } else {
                    Bundle = null;
                }
            }
        }

        NcEmailMessageBundle Bundle;
        protected List<McAttachment> Attachments;

        CompoundScrollView ScrollView;
        MessageHeaderView HeaderView;
        AttachmentsView AttachmentsView;
        UIWebView BodyView;
        MessageToolbar MessageToolbar;
        NcActivityIndicatorView ActivityIndicator;
        NcTimer ActivityShowTimer;
        MessageDownloader BodyDownloader;
        PressGestureRecognizer HeaderPressRecognizer;

        UIBarButtonItem CreateEventButton;

        // Information to be collected for telemetry
        protected DateTime appearTime;

        nfloat ActivityIndicatorSize = 40.0f;
        nfloat ToolbarHeight = 44.0f;

        #endregion

        #region Constructors

        public MessageViewController() : base  ()
        {
            using (var image = UIImage.FromBundle ("cal-add")) {
                CreateEventButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, CreateEventButtonClicked);
                CreateEventButton.AccessibilityLabel = "Create Event";
            }

            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                CreateEventButton,
            };

            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";

        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();

            View.BackgroundColor = UIColor.White;

            MessageToolbar = new MessageToolbar (new CGRect (0, View.Frame.Height - ToolbarHeight, View.Frame.Width, ToolbarHeight));
            MessageToolbar.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
            MessageToolbar.OnClick = ToolbarItemSelected;

            ScrollView = new CompoundScrollView (new CGRect (0.0f, 0.0f, View.Bounds.Width, MessageToolbar.Frame.Y));
            ScrollView.Delegate = this;
            ScrollView.AlwaysBounceVertical = true;
            ScrollView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            HeaderView = new MessageHeaderView (new CGRect(0.0f, 0.0f, ScrollView.Bounds.Width, 100.0f));
            HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            HeaderPressRecognizer = new PressGestureRecognizer (HeaderPressed);
            HeaderView.AddGestureRecognizer (HeaderPressRecognizer);

            AttachmentsView = new AttachmentsView (new CGRect(0.0f, 0.0f, ScrollView.Bounds.Width, 100.0f));
            AttachmentsView.Delegate = this;
            AttachmentsView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            if (!ReusableWebViews.TryPop (out BodyView)) {
                BodyView = new UIWebView (ScrollView.Bounds);
                BodyView.DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber | UIDataDetectorType.Address;
            }
            BodyView.Delegate = this;

            ScrollView.AddCompoundView (HeaderView);
            ScrollView.AddCompoundView (AttachmentsView);
            ScrollView.AddCompoundView (BodyView);

            View.AddSubview (ScrollView);
            View.AddSubview (MessageToolbar);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            HeaderView.Message = Message;
            HeaderView.SizeToFit ();

            if (Attachments.Count > 0) {
                AttachmentsView.Hidden = false;
                AttachmentsView.Attachments = Attachments;
                AttachmentsView.SizeToFit ();
            } else {
                AttachmentsView.Hidden = true;
            }

            LayoutScrollView ();
            if (Bundle == null || Bundle.NeedsUpdate) {
                StartBodyDownload ();
            } else {
                DisplayMessageBody ();
                if (!Message.IsRead) {
                    EmailHelper.MarkAsRead (Message);
                }
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            if (null != Message) {
                if (!NcApplication.Instance.Account.ContainsAccount (Message.AccountId)) {
                    Log.Error (Log.LOG_UI, "MessageViewController mismatched accounts {0} {1}.", NcApplication.Instance.Account.Id, Message.AccountId);
                    if (null != NavigationController) {
                        NavigationController.PopViewController (false);
                    }
                }
                NcBrain.MessageReadStatusUpdated (Message, DateTime.UtcNow, 0.1);
            }

            if (HeaderView.Selected) {
                HeaderView.SetSelected (false, animated: true);
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            appearTime = DateTime.UtcNow;
        }

        public override void ViewWillDisappear (bool animated)
        {
            // Record information about the read email and then reset them.
            if (null != Message) {
                var now = DateTime.UtcNow;
                Telemetry.RecordFloatTimeSeries ("MessageViewController.Duration", appearTime, (now - appearTime).TotalMilliseconds);
                Telemetry.RecordIntTimeSeries ("McEmailMessage.Read.Id", appearTime, Message.Id);
                Telemetry.RecordFloatTimeSeries ("McEmailMessage.Read.Score", appearTime, Message.Score);
                var body = McBody.QueryById<McBody> (Message.BodyId);
                if (McBody.IsComplete (body)) {
                    Telemetry.RecordIntTimeSeries ("McEmailMessage.Read.BodyFileLength", appearTime, (int)body.FileSize);
                }
            }
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            if (IsMovingToParentViewController) {
                HideActivityIndicator ();
            }
            base.ViewDidDisappear (animated);
        }

        public override void ViewDidUnload ()
        {
            BodyView.Delegate = null;
            if (ReusableWebViews.Count < 3) {
                ReusableWebViews.Push (BodyView);
                if (BodyView.IsLoading) {
                    BodyView.StopLoading ();
                }
                BodyView.EvaluateJavascript ("document.body.innerHTML = ''");
            }
            MessageToolbar.OnClick = null;
            MessageToolbar.Cleanup ();
            base.ViewDidUnload ();
        }

        #endregion

        #region Layout

        private void UpdateScrollViewSize ()
        {
            ScrollView.DetermineContentSize ();
        }

        private void LayoutScrollView ()
        {
            UpdateScrollViewSize ();
            ScrollView.SetNeedsLayout ();
            ScrollView.LayoutIfNeeded ();
        }

        public void AttachmentsViewDidChangeSize (AttachmentsView view)
        {
            UIView.Animate(0.25f, () => {
                view.SizeToFit ();
                LayoutScrollView ();
            });
        }

        #endregion

        #region User Actions

        void HeaderPressed ()
        {
            if (HeaderPressRecognizer.State == UIGestureRecognizerState.Began) {
                HeaderView.SetSelected (true, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Ended) {
                ShowHeaderDetails ();
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Failed) {
                HeaderView.SetSelected (false, animated: true);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                HeaderView.SetSelected (false, animated: false);
            }
        }

        public void AttachmentsViewDidSelectAttachment (AttachmentsView view, McAttachment attachment)
        {
            PlatformHelpers.DisplayAttachment (this, attachment);
        }

        void ToolbarItemSelected (object sender, EventArgs e)
        {
            var toolbarEventArgs = (MessageToolbarEventArgs)e;
            switch (toolbarEventArgs.Action) {
            case MessageToolbar.ActionType.QUICK_REPLY:
                ComposeResponse (EmailHelper.Action.Reply, true);
                break;
            case MessageToolbar.ActionType.REPLY:
                onReplyButtonClicked (EmailHelper.Action.Reply);
                break;
            case MessageToolbar.ActionType.REPLY_ALL:
                onReplyButtonClicked (EmailHelper.Action.ReplyAll);
                break;
            case MessageToolbar.ActionType.FORWARD:
                onReplyButtonClicked (EmailHelper.Action.Forward);
                break;
            case MessageToolbar.ActionType.MOVE:
                ShowMove();
                break;
            case MessageToolbar.ActionType.ARCHIVE:
                onArchiveButtonClicked ();
                break;
            case MessageToolbar.ActionType.DELETE:
                onDeleteButtonClicked ();
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown toolbar action {0}",
                    (int)toolbarEventArgs.Action));
            }
        }

        private void CreateEventButtonClicked (object sender, EventArgs e)
        {
            CreateEvent ();
        }

        void ShowMove ()
        {
            var vc = new FoldersViewController ();
            vc.SetOwner (this, true, Message.AccountId, null);
            PresentViewController (vc, true, null);
        }

        private void onDeleteButtonClicked ()
        {
            DeleteThisMessage ();
            NavigationController.PopViewController (true);
        }

        private void onArchiveButtonClicked ()
        {
            ArchiveThisMessage ();
            NavigationController.PopViewController (true);
        }

        private void onReplyButtonClicked (EmailHelper.Action action)
        {
            ComposeResponse (action);
        }

        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            MoveThisMessage (folder);
            vc.SetOwner (null, false, 0, null);
            vc.DismissFolderChooser (false, new Action (delegate {
                NavigationController.PopViewController (true);
            }));
        }

        #endregion

        #region Body Download

        void StartBodyDownload ()
        {
            BodyDownloader = new MessageDownloader ();
            BodyDownloader.Delegate = this;
            BodyDownloader.Bundle = Bundle;
            BodyDownloader.Download (Message);
            ActivityShowTimer = new NcTimer ("", ActivityShowTimerFired, null, TimeSpan.FromSeconds (2), TimeSpan.Zero);
        }

        void ActivityShowTimerFired (object state)
        {
            BeginInvokeOnMainThread (StartActivityIndicator);
        }

        void StartActivityIndicator ()
        {
            ActivityShowTimer = null;
            if (ActivityIndicator == null) {
                ActivityIndicator = new NcActivityIndicatorView (new CGRect(0.0f, 0.0f, ActivityIndicatorSize, ActivityIndicatorSize));
                ActivityIndicator.Speed = 1.5f;
                ActivityIndicator.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            }
            nfloat y = HeaderView.Frame.Height + 2.0f * ActivityIndicatorSize;
            if (!AttachmentsView.Hidden) {
                y = AttachmentsView.Frame.Y + AttachmentsView.Frame.Height + 2.0f * ActivityIndicatorSize;
            }
            ActivityIndicator.Frame = new CGRect ((ScrollView.Bounds.Width - ActivityIndicatorSize) / 2.0f, y, ActivityIndicatorSize, ActivityIndicatorSize);
            ActivityIndicator.Alpha = 0.0f;
            ScrollView.AddSubview (ActivityIndicator);
            ActivityIndicator.StartAnimating ();
            nfloat duration = 0.5f;
            var animation = CABasicAnimation.FromKeyPath ("opacity");
            animation.From = new NSNumber (0.0f);
            animation.To = new NSNumber (1.0f);
            animation.Duration = duration;
            ActivityIndicator.Layer.AddAnimation (animation, "opacity");
            ActivityIndicator.Alpha = 1.0f;
        }

        void HideActivityIndicator ()
        {
            CancelActivityShowTimer ();
            CABasicAnimation animation;
            if (ActivityIndicator != null && ActivityIndicator.Superview != null) {
                animation = ActivityIndicator.Layer.AnimationForKey ("opacity") as CABasicAnimation;
                var duration = 0.15f;
                var opacity = 1.0f;
                if (animation != null) {
                    opacity = ActivityIndicator.Layer.PresentationLayer.Opacity;
                }
                duration = duration * opacity;
                ActivityIndicator.Layer.RemoveAnimation ("opacity");
                ActivityIndicator.StopAnimating ();

                animation = CABasicAnimation.FromKeyPath ("opacity");
                animation.From = new NSNumber (opacity);
                animation.To = new NSNumber (0.0f);
                animation.Duration = duration;
                animation.AnimationStopped += (object sender, CAAnimationStateEventArgs e) => {
                    ActivityIndicator.RemoveFromSuperview ();
                };
                ActivityIndicator.Layer.AddAnimation (animation, "opacity");
                ActivityIndicator.Alpha = 0.0f;
            }
        }

        void CancelActivityShowTimer ()
        {
            if (ActivityShowTimer != null) {
                ActivityShowTimer.Dispose ();
                ActivityShowTimer = null;
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            EmailHelper.MarkAsRead (Message);
            if (Bundle == null) {
                Bundle = downloader.Bundle;
            }
            HideActivityIndicator ();
            DisplayMessageBody ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            HideActivityIndicator ();
            var alert = UIAlertController.Create ("Download Failed", "Sorry, we couldn't download your message", UIAlertControllerStyle.Alert);
            alert.AddAction (UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
            PresentViewController (alert, true, null);
            // TODO: show preview and error message
        }

        void DisplayMessageBody ()
        {
            if (Bundle != null) {
                if (Bundle.FullHtmlUrl != null) {
                    Log.Info (Log.LOG_UI, "MessageViewController DisplayMessageBody() using uri");
                    NSUrlRequest request = new NSUrlRequest (Bundle.FullHtmlUrl);
                    BodyView.LoadRequest (request);
                } else {
                    Log.Info (Log.LOG_UI, "MessageViewController DisplayMessageBody() using html");
                    var html = Bundle.FullHtml;
                    var url = new NSUrl (Bundle.BaseUrl.AbsoluteUri);
                    if (html != null) {
                        BodyView.LoadHtmlString (new NSString (html), url);
                    } else {
                        Log.Error (Log.LOG_UI, "MessageViewController DisplayMessageBody() null html");
                        BodyView.LoadHtmlString (new NSString ("<html><body><div><br></div></body></html>"), url);
                    }
                }
            } else {
                Log.Error (Log.LOG_UI, "MessageViewController called without a valid bundle");
                var alert = UIAlertController.Create ("Could not load message", "Sorry, the message could not be loaded. Please try again", UIAlertControllerStyle.Alert);
                alert.AddAction (UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController (alert, true, null);
            }
        }

        #endregion

        #region Web View Delegate

        [Export ("webViewDidFinishLoad:")]
        public void LoadingFinished (UIWebView webView)
        {
            Log.Info (Log.LOG_UI, "MessageViewController LoadingFinished()");
            int width = (int)ScrollView.Bounds.Width;
            var viewportString = String.Format ("width={0},minimum-scale=0.2,maximum-scale=5.0,user-scalable=yes", width);
            BodyView.EvaluateJavascript (String.Format ("Viewer.Enable(); Viewer.defaultViewer.setViewportContent({0})", viewportString.JavascriptEscapedString ()));
            UpdateScrollViewSize ();
        }

        [Export ("scrollViewDidScroll:")]
        public void DidScroll (UIScrollView scrollView)
        {
            HeaderView.Frame = new CGRect (scrollView.ContentOffset.X, HeaderView.Frame.Y, HeaderView.Frame.Width, HeaderView.Frame.Height);
            if (!AttachmentsView.Hidden) {
                AttachmentsView.Frame = new CGRect (scrollView.ContentOffset.X, AttachmentsView.Frame.Y, AttachmentsView.Frame.Width, AttachmentsView.Frame.Height);
            }
        }

        // TODO: handle links (shouldLoadUrl?)

        #endregion

        #region Private Helpers

        void ShowHeaderDetails ()
        {
            var viewController = new MessageHeaderDetailViewController ();
            viewController.Message = Message;
            NavigationController.PushViewController (viewController, true);
        }

        protected void CreateEvent ()
        {
            var c = CalendarHelper.CreateMeeting (Message);
            EditEvent (c);
        }

        void EditEvent (McCalendar calendarEvent)
        {
            var vc = new EditEventViewController ();
            vc.SetCalendarItem (calendarEvent);
            var navigationController = new UINavigationController (vc);
            Util.ConfigureNavBar (false, navigationController);
            PresentViewController (navigationController, true, null);
        }

        protected void DeleteThisMessage ()
        {
            if (Message.StillExists ()) {
                NcEmailArchiver.Delete (Message);
            }
        }

        protected void ArchiveThisMessage ()
        {
            if (Message.StillExists ()) {
                NcEmailArchiver.Archive (Message);
            }
        }

        protected void MoveThisMessage (McFolder folder)
        {
            NcEmailArchiver.Move (Message, folder);
        }

        private void ComposeResponse (EmailHelper.Action action, bool startWithQuickResponse = false)
        {
            if (Message.StillExists ()){
                var account = McAccount.EmailAccountForMessage (Message);
                var thread = new McEmailMessageThread ();
                thread.FirstMessageId = Message.Id;
                thread.MessageCount = 1;
                var composeViewController = new MessageComposeViewController (account);
                composeViewController.Composer.Kind = action;
                composeViewController.Composer.RelatedThread = thread;
                composeViewController.StartWithQuickResponse = startWithQuickResponse;
                composeViewController.Present ();
            }
        }

        #endregion

        #region View Controller Overrides

        public override bool HidesBottomBarWhenPushed {
            get {
                return true;
            }
        }

        #endregion

        #region Folder Selector Delegate

        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
        }

        #endregion

    }
}
