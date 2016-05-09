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
using SafariServices;

namespace NachoClient.iOS
{

    public partial class MessageViewController : NcUIViewController, INachoFolderChooserParent, IUIWebViewDelegate, MessageDownloadDelegate, IUIScrollViewDelegate, AttachmentsViewDelegate, ISFSafariViewControllerDelegate, ActionEditViewDelegate, IUIGestureRecognizerDelegate
    {
        
        private static ConcurrentStack<UIWebView> ReusableWebViews = new ConcurrentStack<UIWebView> ();

        #region Properties

        McEmailMessage _Message;
        McAction Action;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                Attachments = McAttachment.QueryByItem (_Message);
                if (Message.IsAction) {
                    Action = McAction.ActionForMessage (_Message);
                }
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
        BodyCalendarView CalendarView;
        UIWebView BodyView;
        MessageToolbar MessageToolbar;
        NcActivityIndicatorView ActivityIndicator;
        NcTimer ActivityShowTimer;
        MessageDownloader BodyDownloader;
        PressGestureRecognizer HeaderPressRecognizer;
        PressGestureRecognizer ActionPressRecognizer;
        UILabel _ErrorLabel;
        UILabel ErrorLabel {
            get {
                if (_ErrorLabel == null) {
                    _ErrorLabel = new UILabel ();
                    _ErrorLabel.Font = A.Font_AvenirNextRegular17;
                    _ErrorLabel.TextColor = A.Color_NachoRed;
                    _ErrorLabel.Lines = 0;
                    _ErrorLabel.UserInteractionEnabled = true;
                    _ErrorLabel.LineBreakMode = UILineBreakMode.WordWrap;
                    ErrorTapGestureRecognizer = new UITapGestureRecognizer (RetryDownload);
                    _ErrorLabel.AddGestureRecognizer (ErrorTapGestureRecognizer);
                }
                return _ErrorLabel;
            }
        }

        UILabel _PreviewLabel;
        UILabel PreviewLabel {
            get {
                if (_PreviewLabel == null) {
                    _PreviewLabel = new UILabel ();
                    _PreviewLabel.Font = A.Font_AvenirNextRegular17;
                    _PreviewLabel.TextColor = A.Color_NachoDarkText;
                    _PreviewLabel.Lines = 0;
                    _PreviewLabel.LineBreakMode = UILineBreakMode.WordWrap;
                }
                return _PreviewLabel;
            }
        }

        MessageActionHeaderView _ActionView;
        MessageActionHeaderView ActionView {
            get {
                if (_ActionView == null) {
                    _ActionView = new MessageActionHeaderView (new CGRect (0.0f, 0.0f, ScrollView.Bounds.Width, 44.0f));
                    _ActionView.Message = Message;
                    _ActionView.Action = Action;
                    ActionPressRecognizer = new PressGestureRecognizer (ActionPressed);
                    ActionPressRecognizer.Delegate = this;
                    ActionPressRecognizer.IsCanceledByPanning = true;
                    ActionPressRecognizer.DelaysStart = true;
                    _ActionView.AddGestureRecognizer (ActionPressRecognizer);
                }
                return _ActionView;
            }
        }

        UITapGestureRecognizer ErrorTapGestureRecognizer;

        UIBarButtonItem CreateEventButton;
        UIBarButtonItem ActionButton;
        UIBarButtonItem HotButton;

        // Information to be collected for telemetry
        protected DateTime appearTime;

        nfloat ActivityIndicatorSize = 40.0f;
        nfloat ToolbarHeight = 44.0f;

        #endregion

        #region Constructors

        public MessageViewController() : base  ()
        {
            CreateEventButton = new NcUIBarButtonItem (UIImage.FromBundle ("cal-add"), UIBarButtonItemStyle.Plain, CreateEventButtonClicked);
            CreateEventButton.AccessibilityLabel = "Create Event";
            ActionButton = new NcUIBarButtonItem (UIImage.FromBundle ("email-action-swipe"), UIBarButtonItemStyle.Plain, ActionButtonClicked);
            ActionButton.AccessibilityLabel = "Create Action";

            HotButton = new NcUIBarButtonItem (UIImage.FromBundle ("email-not-hot"), UIBarButtonItemStyle.Plain, ToggleHot);
            HotButton.AccessibilityLabel = "Hot";

            UpdateNavigationItem ();

            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "Message";

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
            HeaderPressRecognizer.IsCanceledByPanning = true;
            HeaderPressRecognizer.DelaysStart = true;
            HeaderView.AddGestureRecognizer (HeaderPressRecognizer);

            AttachmentsView = new AttachmentsView (new CGRect(0.0f, 0.0f, ScrollView.Bounds.Width, 100.0f));
            AttachmentsView.Delegate = this;
            AttachmentsView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            if (Message.MeetingRequest != null) {
                CalendarView = new BodyCalendarView (0.0f, ScrollView.Bounds.Width, Message, false, RemoveCalendarView, OpenUrl, new UIEdgeInsets (0.0f, 14.0f, 0.0f, 0.0f), UIColor.White.ColorDarkenedByAmount (0.15f));
            }

            if (!ReusableWebViews.TryPop (out BodyView)) {
                BodyView = new UIWebView (ScrollView.Bounds);
                BodyView.DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber | UIDataDetectorType.Address;
            }
            BodyView.Delegate = this;

            ScrollView.AddCompoundView (HeaderView);
            ScrollView.AddCompoundView (AttachmentsView);
            if (Action != null) {
                ScrollView.AddCompoundView (ActionView);
            }
            if (CalendarView != null) {
                ScrollView.AddCompoundView (CalendarView);
            }
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
            UpdateNavigationItem ();
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
            if (_ActionView != null && ActionView.Selected) {
                _ActionView.SetSelected (false, animated: true);
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
            if (ShouldCleanupDuringDidDisappear) {
                Cleanup ();
            }
            base.ViewDidDisappear (animated);
        }

        void Cleanup ()
        {
            // Stop any downloading
            HideActivityIndicator ();
            if (BodyDownloader != null) {
                BodyDownloader.Delegate = null;
                BodyDownloader = null;
            }

            // Recycle body view
            BodyView.Delegate = null;
            if (BodyView.IsLoading) {
                BodyView.StopLoading ();
            }
            BodyView.EvaluateJavascript ("document.body.innerHTML = ''");
            ScrollView.RemoveCompoundView (BodyView);
            ReusableWebViews.Push (BodyView);

            // clean up navbar
            CreateEventButton.Clicked -= CreateEventButtonClicked;
            HotButton.Clicked -= ToggleHot;
            ActionButton.Clicked -= ActionButtonClicked;

            // clean up header
            HeaderView.RemoveGestureRecognizer (HeaderPressRecognizer);
            HeaderPressRecognizer = null;

            // clean up attachments
            AttachmentsView.Delegate = null;
            AttachmentsView.Cleanup ();

            // clean up action view
            if (_ActionView != null) {
                _ActionView.RemoveGestureRecognizer (ActionPressRecognizer);
                ActionPressRecognizer.Delegate = null;
                ActionPressRecognizer = null;
                _ActionView.Cleanup ();
            }

            // clean up the calendar
            if (CalendarView != null) {
                CalendarView.Cleanup ();
            }

            // clean up toolbar
            MessageToolbar.OnClick = null;
            MessageToolbar.Cleanup ();

            // clean up error view
            if (_ErrorLabel != null) {
                _ErrorLabel.RemoveGestureRecognizer (ErrorTapGestureRecognizer);
                ErrorTapGestureRecognizer = null;
            }

            // clean up scroll view
            ScrollView.Delegate = null;
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
                HeaderView.SetSelected (true, animated: false);
                ShowHeaderDetails ();
            }else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Changed) {
                HeaderView.SetSelected (HeaderPressRecognizer.IsInsideView, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Failed) {
                HeaderView.SetSelected (false, animated: true);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                HeaderView.SetSelected (false, animated: false);
            }
        }

        void ActionPressed ()
        {
            if (ActionPressRecognizer.State == UIGestureRecognizerState.Began) {
                ActionView.SetSelected (true, animated: false);
            } else if (ActionPressRecognizer.State == UIGestureRecognizerState.Ended) {
                ActionView.SetSelected (true, animated: false);
                ShowAction ();
            }else if (ActionPressRecognizer.State == UIGestureRecognizerState.Changed) {
                ActionView.SetSelected (ActionPressRecognizer.IsInsideView, animated: false);
            } else if (ActionPressRecognizer.State == UIGestureRecognizerState.Failed) {
                ActionView.SetSelected (false, animated: true);
            } else if (ActionPressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                ActionView.SetSelected (false, animated: false);
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

        private void ActionButtonClicked (object sender, EventArgs e)
        {
            CreateAction ();
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

        void ToggleHot (object sender, EventArgs e)
        {
            Message.UserAction = NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (Message);
            HeaderView.Update ();
        }

        void RetryDownload ()
        {
            ScrollView.RemoveCompoundView (ErrorLabel);
            if (_PreviewLabel != null) {
                ScrollView.RemoveCompoundView (PreviewLabel);
            }
            BodyView.Hidden = false;
            LayoutScrollView ();
            StartBodyDownload ();
        }

        [Export ("gestureRecognizer:shouldReceiveTouch:")]
        public bool ShouldReceiveTouch (UIGestureRecognizer recognizer, UITouch touch)
        {
            var view = touch.View;
            while (view != _ActionView) {
                if (view == _ActionView.CheckboxView) {
                    return false;
                }
                view = view.Superview;
            }
            return true;
        }

        #endregion

        #region Body Download

        void StartBodyDownload ()
        {
            ActivityShowTimer = new NcTimer ("", ActivityShowTimerFired, null, TimeSpan.FromSeconds (2), TimeSpan.Zero);
            BodyDownloader = new MessageDownloader ();
            BodyDownloader.Delegate = this;
            BodyDownloader.Bundle = Bundle;
            BodyDownloader.Download (Message);
        }

        void ActivityShowTimerFired (object state)
        {
            BeginInvokeOnMainThread (StartActivityIndicator);
        }

        void StartActivityIndicator ()
        {
            ActivityShowTimer = null;
            // Download may have finished as the timer was firing and schedling this method
            if (BodyDownloader == null) {
                return;
            }
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
            BodyDownloader.Delegate = null;
            BodyDownloader = null;
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            HideActivityIndicator ();
            ShowDownloadErrorForResult (result);
            BodyDownloader.Delegate = null;
            BodyDownloader = null;
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

        [Export ("webView:shouldStartLoadWithRequest:navigationType:")]
        public bool ShouldStartLoad (UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType)
        {
            if (navigationType == UIWebViewNavigationType.LinkClicked) {
                var url = request.Url;
                var scheme = url.Scheme.ToLowerInvariant ();
                if (scheme.Equals ("x-apple-data-detectors")) {
                    return true;
                }
                OpenUrl (url);
                return false;
            }
            return true;
        }

        [Export ("safariViewControllerDidFinish:")]
        public virtual void DidFinish (SFSafariViewController controller)
        {
            controller.Delegate = null;
            NavigationController.PopViewController (animated: true);
            NavigationController.SetNavigationBarHidden (false, animated: true);
        }

        #endregion

        #region Private Helpers

        void OpenUrl (NSUrl url)
        {
            var scheme = url.Scheme.ToLowerInvariant ();
            if (scheme.Equals ("mailto")) {
                ComposeMessage (url);
            } else if (scheme.Equals ("http") || scheme.Equals ("https")) {
                var viewController = new SFSafariViewController (url);
                viewController.Delegate = this;
                NavigationController.PushViewController (viewController, animated: true);
                NavigationController.SetNavigationBarHidden (true, animated: true);
            } else {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }

        void RemoveCalendarView ()
        {
            ScrollView.RemoveCompoundView (CalendarView);
            LayoutScrollView ();
        }

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

        private void ComposeMessage (NSUrl url)
        {
            string body;
            var account = McAccount.EmailAccountForMessage (Message);
            var composeViewController = new MessageComposeViewController (account);
            composeViewController.Composer.Message = EmailHelper.MessageFromMailTo (account, url.AbsoluteString, out body);
            composeViewController.Composer.InitialText = body;
            composeViewController.Present ();
        }

        void ShowDownloadErrorForResult (NcResult result)
        {
            var canRetryDownload = result.Why != NcResult.WhyEnum.MissingOnServer;
            if (canRetryDownload) {
                ErrorLabel.Text = "Message download failed. Tap here to retry.";
            } else {
                ErrorLabel.Text = "Message download failed.";
            }
            ErrorTapGestureRecognizer.Enabled = canRetryDownload;
            nfloat padding = 14.0f;
            var width = ScrollView.Bounds.Width - padding * 2.0f;
            var size = ErrorLabel.SizeThatFits (new CGSize (width, 0.0f));
            ErrorLabel.Frame = new CGRect (padding, 0.0f, width, (nfloat)Math.Ceiling (size.Height + 2.0f * padding));
            ScrollView.AddCompoundView (ErrorLabel);
            BodyView.Hidden = true;
            if (!String.IsNullOrEmpty (Message.BodyPreview)) {
                PreviewLabel.Text = Message.BodyPreview;
                size = PreviewLabel.SizeThatFits (new CGSize (width, 0.0f));
                PreviewLabel.Frame = new CGRect (padding, 0.0f, width, (nfloat)Math.Ceiling (size.Height));
                ScrollView.AddCompoundView (PreviewLabel);
            }
            LayoutScrollView ();
        }

        void ShowAction ()
        {
            var viewController = new ActionEditViewController ();
            // re-query the action so the edit view alters a copy
            viewController.Action = McAction.QueryById<McAction> (Action.Id);
            viewController.Delegate = this;
            viewController.PresentOverViewController (this);
        }

        void CreateAction ()
        {
            var viewController = new ActionEditViewController ();
            viewController.Action = McAction.FromMessage (Message);
            viewController.Delegate = this;
            viewController.PresentOverViewController (this);
        }

        public void ActionEditViewDidSave (ActionEditViewController viewController)
        {
            bool creatingAction = Action == null;
            Action = McAction.ActionForMessage (_Message);
            if (creatingAction) {
                UpdateNavigationItem ();
            }
            if (_ActionView == null) {
                ScrollView.InsertCompoundViewBelow (ActionView, BodyView);
                LayoutScrollView ();
            }
            ActionView.Message = Message;
            ActionView.Action = Action;
        }

        public void ActionEditViewDidDismiss (ActionEditViewController viewController)
        {
            viewController.Delegate = null;
        }

        void UpdateNavigationItem ()
        {

            if (Action == null) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    CreateEventButton,
                    ActionButton,
                    HotButton
                };
            } else {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    CreateEventButton,
                    HotButton
                };
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
