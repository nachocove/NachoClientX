// Uncomment this #define if you need to get more debugging.
// It adds additional inset and different background colors for different
// views.
//#define DEBUG_UI
// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CoreGraphics;
using Foundation;
using UIKit;
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
    public partial class MessageViewController : NcUIViewControllerNoLeaks, INachoMessageViewer,
        INachoFolderChooserParent, 
        IUcAddressBlockDelegate, IBodyViewOwner
    {
        // Model data
        public McEmailMessageThread thread;
        protected List<McAttachment> attachments;

        UIScrollView scrollView;

        // UI elements for the main view
        protected UIView headerView;
        protected AttachmentListView attachmentListView;
        protected UcAddressBlock toView;
        protected UcAddressBlock ccView;
        protected BodyView bodyView;
        protected MessageToolbar messageToolbar;
        protected UITapGestureRecognizer singleTapGesture;
        protected UITapGestureRecognizer.Token singleTapGestureHandlerToken;
        protected UITapGestureRecognizer doubleTapGesture;
        protected UITapGestureRecognizer.Token doubleTapGestureHandlerToken;

        // UI elements for the navigation bar
        protected UIBarButtonItem createEventButton;

        // UI related constants (or pseudo constants)
        protected static nfloat SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected static nfloat TOVIEW_LEFT_MARGIN = 20;
        protected static nfloat CCVIEW_LEFT_MARGIN = 20;
        protected static nfloat CHILI_ICON_WIDTH = 20;

        #if DEBUG_UI
        const int VIEW_INSET = 4;
        const int ATTACHMENTVIEW_INSET = 10;
        nfloat HEADER_TOP_MARGIN = 0;
        #else
        const int VIEW_INSET = 0;
        const int ATTACHMENTVIEW_INSET = 15;
        nfloat HEADER_TOP_MARGIN = 0;
        #endif

        // UI helper objects
        protected bool expandedHeader = false;
        protected nfloat expandedSeparatorYOffset;
        protected nfloat compactSeparatorYOffset;

        // Information to be collected for telemetry
        protected DateTime appearTime;

        public enum TagType
        {
            USER_IMAGE_TAG = 101,
            FROM_TAG = 102,
            SUBJECT_TAG = 103,
            ATTACHMENT_ICON_TAG = 106,
            RECEIVED_DATE_TAG = 107,
            SEPARATOR1_TAG = 108,
            SEPARATOR2_TAG = 112,
            USER_LABEL_TAG = 110,
            USER_CHILI_TAG = 111,
            BLANK_VIEW_TAG = 113,
            ATTACHMENT_VIEW_TAG = 301,
            ATTACHMENT_NAME_TAG = 302,
            ATTACHMENT_STATUS_TAG = 303
        }

        private bool isAppearing;

        public MessageViewController() : base  ()
        {
        }

        public MessageViewController (IntPtr handle)
            : base (handle)
        {
        }

        public void SetSingleMessageThread (McEmailMessageThread thread)
        {
            NcAssert.True (1 == thread.Count);
            this.thread = thread;
        }

        public override void ViewDidLoad ()
        {
            scrollView = new UIScrollView (View.Bounds);
            scrollView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            View.AddSubview (scrollView);
            base.ViewDidLoad ();
            ConfigureAndLayout ();
        }

        public override void ViewWillAppear (bool animated)
        {
            isAppearing = true;
            base.ViewWillAppear (animated);
            isAppearing = false;

            // When the app is re-started from a notification on a
            // different account, the tab bar and nacho now should
            // close all views & start in nacho now. But perhaps it
            // is possible for this view to become visible just as
            // it is about to be popped?  Catch & avoid that case.
            var message = thread.FirstMessageSpecialCase ();
            if (null != message) {
                if (!NcApplication.Instance.Account.ContainsAccount (message.AccountId)) {
                    Log.Error (Log.LOG_UI, "MessageViewController mismatched accounts {0} {1}.", NcApplication.Instance.Account.Id, message.AccountId);
                    if (null != NavigationController) {
                        NavigationController.PopViewController (false);
                    }
                }
                NcTask.Run (() => {
                    NcBrain.MessageReadStatusUpdated (message, DateTime.UtcNow, 0.1);
                }, "MessageViewController.MessageReadStatusUpdated");
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
            if (null != thread) {
                var now = DateTime.UtcNow;
                var message = thread.FirstMessageSpecialCase ();
                // The message may have been deleted while the view was open.
                if (null != message) {
                    Telemetry.RecordFloatTimeSeries ("MessageViewController.Duration", appearTime, (now - appearTime).TotalMilliseconds);
                    Telemetry.RecordIntTimeSeries ("McEmailMessage.Read.Id", appearTime, message.Id);
                    Telemetry.RecordFloatTimeSeries ("McEmailMessage.Read.Score", appearTime, message.Score);
                    var body = McBody.QueryById<McBody> (message.BodyId);
                    if (McBody.IsComplete (body)) {
                        Telemetry.RecordIntTimeSeries ("McEmailMessage.Read.BodyFileLength", appearTime, (int)body.FileSize);
                    }
                }
            }
            base.ViewWillDisappear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            scrollView.Frame = View.Frame;
            ViewFramer.Create (scrollView).AdjustHeight (-64);

            // Turn on zooming
            scrollView.MinimumZoomScale = 0.2f;
            scrollView.MaximumZoomScale = 5.0f;
            scrollView.ZoomingEnded += ScrollViewZoomingEnded;
            scrollView.ViewForZoomingInScrollView = ViewForZooming;
            doubleTapGesture = new UITapGestureRecognizer ();
            doubleTapGesture.NumberOfTapsRequired = 2;
            doubleTapGesture.NumberOfTouchesRequired = 1;
            doubleTapGesture.ShouldRecognizeSimultaneously = ((UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => {
                return true;
            });
            doubleTapGestureHandlerToken = doubleTapGesture.AddTarget (ZoomingDoubleTapGesture);
            scrollView.AddGestureRecognizer (doubleTapGesture);

            // Toolbar controls

            messageToolbar = new MessageToolbar (new CGRect (0, View.Frame.Height - 44, View.Frame.Width, 44));
            messageToolbar.OnClick = (object sender, EventArgs e) => {
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
            };
            View.AddSubview (messageToolbar);

            // Navigation controls

            createEventButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (createEventButton, "cal-add");
            createEventButton.AccessibilityLabel = "Create Event";

            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                createEventButton,
            };
                
            createEventButton.Clicked += CreateEventButtonClicked;

            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            // Main view

            HEADER_TOP_MARGIN = A.Card_Vertical_Indent;

            headerView = new UIView (new CGRect (VIEW_INSET, HEADER_TOP_MARGIN, View.Frame.Width - 2 * VIEW_INSET, View.Frame.Height));
            scrollView.AddSubview (headerView);

            headerView.BackgroundColor = UIColor.White;
            headerView.Layer.CornerRadius = A.Card_Edge_To_Edge_Corner_Radius;
            headerView.Layer.MasksToBounds = true;
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;

            #if DEBUG_UI
            headerView.BackgroundColor = A.Color_NachoRed;
            scrollView.BackgroundColor = A.Color_NachoTeal;
            #endif

            scrollView.Bounces = false;
            scrollView.Scrolled += ScrollViewScrolled;

            // A single tap on the header section toggles between the compact and expanded
            // views of the header.
            singleTapGesture = new UITapGestureRecognizer ();
            singleTapGesture.NumberOfTapsRequired = 1;
            singleTapGestureHandlerToken = singleTapGesture.AddTarget (HeaderSingleTapHandler);
            singleTapGesture.ShouldRecognizeSimultaneously = SingleTapGestureRecognizer;
            headerView.AddGestureRecognizer (singleTapGesture);

            // User image
            var userImageView = new UIImageView (new CGRect (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = (int)TagType.USER_IMAGE_TAG;
            headerView.AddSubview (userImageView);

            // User label, to be used if no image is available
            var userLabelView = new UILabel (new CGRect (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = (int)TagType.USER_LABEL_TAG;
            headerView.AddSubview (userLabelView);

            // Initial Y offsets for various elements. These will be adjusted when the
            // final layout is done.
            nfloat yOffset = 15;

            // "From" label. Font will be bold or regular, depending on isRead.
            // Sizes will be recalculated after the text is known.
            var fromLabelView = new UILabel (new CGRect (65, yOffset, headerView.Frame.Width - CHILI_ICON_WIDTH - 65 - 10, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = (int)TagType.FROM_TAG;
            headerView.AddSubview (fromLabelView);

            yOffset += 20;

            // Subject label
            var subjectLabelView = new UILabel (new CGRect (65, yOffset, 250, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.WordWrap;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.Tag = (int)TagType.SUBJECT_TAG;
            headerView.AddSubview (subjectLabelView);

            yOffset += 20;

            // Received label
            var receivedLabelView = new UILabel (new CGRect (65, yOffset, 250, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Left;
            receivedLabelView.Tag = (int)TagType.RECEIVED_DATE_TAG;
            headerView.AddSubview (receivedLabelView);

            yOffset += 20;

            // "To" label
            nfloat blockWidth = headerView.Frame.Width - TOVIEW_LEFT_MARGIN;
            toView = new UcAddressBlock (this, "To:", null, blockWidth);
            toView.SetCompact (false, -1);
            toView.SetEditable (false);
            toView.SetLineHeight (20);
            toView.SetAddressIndentation (45);
            ViewFramer.Create (toView)
                .X (TOVIEW_LEFT_MARGIN)
                .Y (yOffset)
                .Width (blockWidth)
                .Height (0);
            headerView.AddSubview (toView);

            // "cc" label
            blockWidth = headerView.Frame.Width - CCVIEW_LEFT_MARGIN;
            ccView = new UcAddressBlock (this, "Cc:", null, blockWidth);
            ccView.SetCompact (false, -1);
            ccView.SetEditable (false);
            ccView.SetLineHeight (20);
            ccView.SetAddressIndentation (45);
            ViewFramer.Create (ccView)
                .X (CCVIEW_LEFT_MARGIN)
                .Y (yOffset)
                .Width (blockWidth)
                .Height (0);
            headerView.AddSubview (ccView);

            // Chili image
            var chiliImageView = new UIImageView (new CGRect (fromLabelView.Frame.Right, 14, 20, 20));
            chiliImageView.Tag = (int)TagType.USER_CHILI_TAG;
            headerView.AddSubview (chiliImageView);

            // A blank view below separator2, which covers up the To and CC fields
            // when the headers are colapsed.  (The To and CC fields are often
            // covered by the attachments view or the message body. But not always.)
            var blankView = new UIView (new CGRect (0, yOffset, View.Frame.Width, 0));
            blankView.BackgroundColor = UIColor.White;
            blankView.Tag = (int)TagType.BLANK_VIEW_TAG;
            headerView.AddSubview (blankView);

            // Separator 1
            var separator1View = new UIView (new CGRect (0, yOffset, View.Frame.Width, 1));
            separator1View.BackgroundColor = A.Color_NachoBorderGray;
            separator1View.Tag = (int)TagType.SEPARATOR1_TAG;
            headerView.AddSubview (separator1View);

            // Attachments
            attachmentListView = new AttachmentListView (new CGRect (
                ATTACHMENTVIEW_INSET, yOffset + 1.0f,
                headerView.Frame.Width - ATTACHMENTVIEW_INSET, 50));
            attachmentListView.SetHeader ("Attachments", A.Font_AvenirNextRegular17, A.Color_NachoTextGray, null, A.Font_AvenirNextDemiBold14, UIColor.White, A.Color_909090, 10f);
            attachmentListView.OnAttachmentSelected = AttachmentsOnSelected;
            attachmentListView.OnAttachmentError = AttachmentOnError;
            attachmentListView.OnStateChanged = AttachmentsOnStateChange;
            attachmentListView.Tag = (int)TagType.ATTACHMENT_VIEW_TAG;
            headerView.AddSubview (attachmentListView);

            // Separater 2
            var separator2View = new UIView (new CGRect (0, yOffset, View.Frame.Width, 1));
            separator2View.BackgroundColor = A.Color_NachoBorderGray;
            separator2View.Tag = (int)TagType.SEPARATOR2_TAG;
            headerView.AddSubview (separator2View);

            yOffset += 1;

            // Message body, which is added to the scroll view, not the header view.
            bodyView = BodyView.VariableHeightBodyView (new CGPoint (VIEW_INSET, yOffset), scrollView.Frame.Width - 2 * VIEW_INSET, scrollView.Frame.Size, this);
            scrollView.AddSubview (bodyView);
        }

        protected override void ConfigureAndLayout ()
        {
            using (NcAbate.UIAbatement ()) {
                if (isAppearing) {
                    // NcUIViewController will call ConfigureAndLayout on ViewWillAppear
                    // But in order to load the web view earlier, this controller calls ConfigureAndLayout in ViewDidLoad
                    // Therefore, we want to skip a duplicate call from base.ViewWillAppear
                    return;
                }
                // It appears that sometimes we get here during a pop to root view controller,
                // and nothing is setup as it would be during an intentional segue.  Can't figure
                // out the root cause, but adding these couple checks should prevent crashes.
                if (this.NavigationController == null) {
                    Log.Error (Log.LOG_UI, "MessageViewController ConfigureAndLayout null NavigationController");
                    return;
                }
                if (thread == null) {
                    Log.Error (Log.LOG_UI, "MessageViewController ConfigureAndLayout null thread");
                    return;
                }
                if (this.NavigationController.RespondsToSelector (new ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                    this.NavigationController.InteractivePopGestureRecognizer.Enabled = true;
                    this.NavigationController.InteractivePopGestureRecognizer.Delegate = null;
                }

                var message = thread.SingleMessageSpecialCase ();

                if (null == message) {
                    // TODO: Unavailable message
                    NavigationController.PopViewController (true);
                    return;
                }
                
                attachments = McAttachment.QueryByItem (message);
                attachmentListView.Hidden = !HasAttachments;

                // User image view
                var userImageView = headerView.ViewWithTag ((int)TagType.USER_IMAGE_TAG) as UIImageView;
                var userLabelView = headerView.ViewWithTag ((int)TagType.USER_LABEL_TAG) as UILabel;
                userImageView.Hidden = true;
                userLabelView.Hidden = true;

                var userImage = Util.PortraitToImage (message.cachedPortraitId);

                if (null != userImage) {
                    userImageView.Hidden = false;
                    userImageView.Image = userImage;
                } else {
                    userLabelView.Hidden = false;
                    userLabelView.Text = message.cachedFromLetters;
                    userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
                }
                
                var cursor = new VerticalLayoutCursor (headerView);
                cursor.AddSpace (35); // for From and top inset

                var subjectLabelView = View.ViewWithTag ((int)TagType.SUBJECT_TAG) as UILabel;
                subjectLabelView.Lines = 0;
                string subject = EmailHelper.CreateSubjectWithIntent (message.Subject, message.Intent, message.IntentDateType, message.IntentDate);
                if (string.IsNullOrEmpty (subject)) {
                    subjectLabelView.TextColor = A.Color_9B9B9B;
                    subjectLabelView.Text = Pretty.NoSubjectString ();
                } else {
                    subjectLabelView.TextColor = A.Color_0F424C;
                    subjectLabelView.Text = subject;
                }
                cursor.LayoutView (subjectLabelView);

                var receivedLabelView = View.ViewWithTag ((int)TagType.RECEIVED_DATE_TAG) as UILabel;
                receivedLabelView.Text = Pretty.MediumFullDateTime (message.DateReceived);
                cursor.LayoutView (receivedLabelView);

                // Reminder image view and label
                nfloat yOffset = receivedLabelView.Frame.Bottom;

                compactSeparatorYOffset = cursor.TotalHeight;

                toView.Clear ();
                foreach (var address in NcEmailAddress.ParseToAddressListString(message.To)) {
                    toView.Append (address);
                }
                toView.ConfigureView ();
                cursor.LayoutView (toView);

                ccView.Clear ();
                foreach (var address in NcEmailAddress.ParseCcAddressListString(message.Cc)) {
                    ccView.Append (address);
                }
                ccView.ConfigureView ();
                cursor.LayoutView (ccView);

                expandedSeparatorYOffset = cursor.TotalHeight;

                var blankView = View.ViewWithTag ((int)TagType.BLANK_VIEW_TAG);
                ViewFramer.Create (blankView).Y (separator1YOffset).Height (expandedSeparatorYOffset - compactSeparatorYOffset);

                var separator1View = View.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
                separator1View.Frame = new CGRect (0, compactSeparatorYOffset, headerView.Frame.Width, 1);
           
                // Chili image view
                ConfigureChili (message);

                var fromLabelView = View.ViewWithTag ((int)TagType.FROM_TAG) as UILabel;
                fromLabelView.Text = Pretty.SenderString (message.From);
                fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

                ConfigureAttachments ();

                bodyView.Configure (message, false);

                LayoutView ();
            }
        }

        protected void ConfigureChili (McEmailMessage message)
        {
            var chiliImageView = View.ViewWithTag ((int)TagType.USER_CHILI_TAG) as UIImageView;
            var chiliImageIcon = (message.isHot () ? "email-hot" : "email-not-hot");
            using (var image = UIImage.FromBundle (chiliImageIcon)) {
                chiliImageView.Image = image;
            }
            chiliImageView.Hidden = false;
        }

        protected override void Cleanup ()
        {
            // Clean up gesture recognizers.
            singleTapGesture.RemoveTarget (singleTapGestureHandlerToken);
            singleTapGesture.ShouldRecognizeSimultaneously = null;
            headerView.RemoveGestureRecognizer (singleTapGesture);
            doubleTapGesture.RemoveTarget (doubleTapGestureHandlerToken);
            doubleTapGesture.ShouldRecognizeSimultaneously = null;
            scrollView.RemoveGestureRecognizer (doubleTapGesture);

            // Remove all callbacks and handlegs.
            scrollView.Scrolled -= ScrollViewScrolled;
            scrollView.ZoomingEnded -= ScrollViewZoomingEnded;
            scrollView.ViewForZoomingInScrollView = null;
            createEventButton.Clicked -= CreateEventButtonClicked;
            messageToolbar.OnClick = null;

            messageToolbar.Cleanup ();
            attachmentListView.Cleanup ();

            scrollView = null;
            messageToolbar = null;

            headerView = null;
            attachmentListView = null;
            toView = null;
            ccView = null;
            bodyView = null;
            createEventButton = null;
        }

        protected void LayoutView (bool animated)
        {
            nfloat duration = animated ? 0.3f : 0.0f;
            UIView.Animate (duration, 0, UIViewAnimationOptions.CurveLinear, () => {
                LayoutView ();
            }, () => {
            });
        }

        protected void LayoutView ()
        {
            // Header view is the background when loading a message.
            // Size the header to be at least as large as the screen.

            var headerSize = NMath.Max (separator2YOffset, View.Frame.Height);
            ViewFramer.Create (headerView).Height (headerSize);

            var separator1View = headerView.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
            ViewFramer.Create (separator1View).Y (separator1YOffset);
            var separator2View = headerView.ViewWithTag ((int)TagType.SEPARATOR2_TAG);
            ViewFramer.Create (separator2View).Y (separator2YOffset);
            var blankView = headerView.ViewWithTag ((int)TagType.BLANK_VIEW_TAG);
            ViewFramer.Create (blankView).Y (separator1YOffset);

            ViewFramer.Create (attachmentListView).Y (separator1YOffset + 1.0f);

            var bodyViewLocation = separator2YOffset + HEADER_TOP_MARGIN;

            bool bodyNeedsLayout = bodyView.Frame.Y > bodyViewLocation + 1;
            ViewFramer.Create (bodyView).Y (bodyViewLocation + 1);
            if (bodyNeedsLayout) {
                // The body view was moved up on the screen, making more of it visible.
                // Make sure that newly visible part is showing the right contents.
                LayoutBody ();
            }

            messageToolbar.Frame = new CGRect (0, View.Frame.Height - 44, View.Frame.Width, 44);

            ViewFramer.Create (scrollView).Height (View.Frame.Height - 44);
            scrollView.ContentSize = new CGSize (NMath.Max (headerView.Frame.Width, bodyView.Frame.Width) + 2 * VIEW_INSET, bodyView.Frame.Bottom);

            // MarkAsRead() will change the message from unread to read only if the body has been
            // completely downloaded, so it is safe to call it unconditionally.  We put the call
            // here, rather than in ConfigureAndLayout(), to handle the case where the body is
            // downloaded long after the message view has been opened.
            EmailHelper.MarkAsRead (thread);

            #if (DEBUG_UI)
            ViewHelper.DumpViews<TagType> (scrollView);
            #endif
        }

        protected void LayoutBody ()
        {
            // Force the BodyView to redo its layout.
            ScrollViewScrolled (null, null);
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return true;
            }
        }

        protected bool HasAttachments {
            get {
                return 0 < attachments.Count;
            }
        }

        protected nfloat separator1YOffset {
            get {
                return expandedHeader ? expandedSeparatorYOffset : compactSeparatorYOffset;
            }
        }

        protected nfloat separator2YOffset {
            get {
                nfloat yOffset = separator1YOffset;
                if (HasAttachments) {
                    yOffset += attachmentListView.Frame.Height;
                    yOffset += 1.0f; // for separator 1
                }
                return yOffset;
            }
        }

        protected void ConfigureAttachments ()
        {
            attachmentListView.Reset ();
            bool firstAttachment = true;
            foreach (var attachment in attachments) {
                if (!firstAttachment) {
                    attachmentListView.LastAttachmentView ().ShowSeparator ();
                }
                firstAttachment = false;
                attachmentListView.AddAttachment (attachment);
            }
        }

        protected void CreateEvent ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null != message) {
                var c = CalendarHelper.CreateMeeting (message);
                EditEvent (c);
            }
        }

        protected void DeleteThisMessage ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null != message) {
                NcEmailArchiver.Delete (message);
            }
        }

        protected void ArchiveThisMessage ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null != message) {
                NcEmailArchiver.Archive (message);
            }
        }

        protected void MoveThisMessage (McFolder folder)
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null != message) {
                NcEmailArchiver.Move (message, folder);
            }
        }

        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            MoveThisMessage (folder);
            vc.SetOwner (null, false, 0, null);
            vc.DismissFolderChooser (false, new Action (delegate {
                NavigationController.PopViewController (true);
            }));
        }

        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
        }

        // Event handlers

        private void ScrollViewScrolled (object sender, EventArgs e)
        {
            // When scrolling horizontally, keep the header on screen.
            ViewFramer.Create (headerView).X (scrollView.ContentOffset.X + VIEW_INSET);

            // Let the body view do its magic to keep the right stuff visible.
            // Adjust the offsets from scrollView's coordinates to bodyView's coordinates.
            bodyView.ScrollingAdjustment (new CGPoint (
                scrollView.ContentOffset.X - bodyView.Frame.X, scrollView.ContentOffset.Y - bodyView.Frame.Y));
        }

        private void HeaderSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                CGPoint touch = gesture.LocationInView (headerView);
                // In the chili zone?
                if ((touch.X > View.Frame.Width - 50) && (touch.Y < 50)) {
                    var message = thread.SingleMessageSpecialCase ();
                    if (null != message) {
                        NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                        ConfigureChili (message);
                    }
                } else if (touch.Y <= separator1YOffset) {
                    expandedHeader = !expandedHeader;
                    LayoutView (true);
                }
            }
        }

        void EditEvent (McCalendar calendarEvent)
        {
            var vc = new EditEventViewController ();
            vc.SetCalendarItem (calendarEvent);
            var navigationController = new UINavigationController (vc);
            Util.ConfigureNavBar (false, navigationController);
            PresentViewController (navigationController, true, null);
        }

        void ShowMove ()
        {
            var vc = new FoldersViewController ();
            var message = thread.FirstMessage ();
            if (null != message) {
                vc.SetOwner (this, true, message.AccountId, thread);
            }
            PresentViewController (vc, true, null);
        }

        private void CreateEventButtonClicked (object sender, EventArgs e)
        {
            CreateEvent ();
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

        private bool SingleTapGestureRecognizer (UIGestureRecognizer a, UIGestureRecognizer b)
        {
            return true;
        }

        private void AttachmentsOnSelected (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            }
        }

        private void AttachmentOnError (McAttachment attachment, NcResult result)
        {
            string message;
            if (!ErrorHelper.ExtractErrorString (result, out message)) {
                message = "Download failed.";
            }
            NcAlertView.ShowMessage (this, "Attachment error", message);
        }

        private void AttachmentsOnStateChange (bool isExpanded)
        {
            LayoutView (true);
        }

        private void ScrollViewZoomingEnded (object sender, EventArgs e)
        {
            // The body view needs to redo its layout to account for the new
            // apparent screen size.
            LayoutBody ();
            // iOS messes up the scroll view's ContentSize when zooming.
            scrollView.ContentSize = new CGSize (NMath.Max (headerView.Frame.Width, bodyView.Frame.Width) + 2 * VIEW_INSET, bodyView.Frame.Bottom);
        }

        private UIView ViewForZooming (UIScrollView sv)
        {
            // The body view zooms.  The message header doesn't.
            return bodyView;
        }

        private void ZoomingDoubleTapGesture (NSObject sender)
        {
            var recognizer = (UIGestureRecognizer)sender;
            // Cycle between 2x, 1x, and small enough to fit the width of the screen.
            // If 1x is small enough to fit on the screen, then only cycle between
            // 2x and 1x.
            nfloat currentZoom = scrollView.ZoomScale;
            nfloat zoomScaleToFit = (scrollView.Frame.Width - 2 * VIEW_INSET) / (scrollView.ContentSize.Width / currentZoom);
            // If the zoom scale necessary to fit on the screen is bigger than 1x
            // or close to 1x, ignore it.
            if (0.95f < zoomScaleToFit) {
                zoomScaleToFit = 1.0f;
            }
            if (zoomScaleToFit < scrollView.MinimumZoomScale) {
                zoomScaleToFit = scrollView.MinimumZoomScale;
            }
            nfloat targetZoomScale;
            if (1.0f > zoomScaleToFit && Math.Sqrt (zoomScaleToFit) < currentZoom && currentZoom < Math.Sqrt (2)) {
                targetZoomScale = zoomScaleToFit;
            } else if (currentZoom < Math.Sqrt (2)) {
                targetZoomScale = 2.0f;
            } else {
                targetZoomScale = 1.0f;
            }
            // Attempt to center the resulting view on the location where the user tapped.
            var touchPoint = recognizer.LocationInView (bodyView);
            var zoomToRect = new CGRect (
                                 touchPoint.X - ((scrollView.Frame.Width / targetZoomScale) / 2.0f),
                                 touchPoint.Y - ((scrollView.Frame.Height / targetZoomScale) / 2.0f),
                                 scrollView.Frame.Width / targetZoomScale,
                                 scrollView.Frame.Height / targetZoomScale);
            scrollView.ZoomToRect (zoomToRect, true);
            LayoutBody ();
        }
            
        // IUcAddressBlockDelegate implementation

        public void AddressBlockNeedsLayout (UcAddressBlock view)
        {
            view.Layout ();
        }

        public void AddressBlockWillBecomeActive (UcAddressBlock view)
        {
        }

        public void AddressBlockWillBecomeInactive (UcAddressBlock view)
        {
        }

        public void AddressBlockAutoCompleteContactClicked (UcAddressBlock view, string prefix)
        {
        }

        public void AddressBlockSearchContactClicked (UcAddressBlock view, string prefix)
        {
        }

        public void AddressBlockRemovedAddress (UcAddressBlock view, NcEmailAddress address)
        {
        }

        private void ComposeResponse (EmailHelper.Action action, bool startWithQuickResponse = false)
        {
            var message = thread.FirstMessageSpecialCase ();
            // The message may have been deleted while the view was open.
            if (null != message) {
                var account = McAccount.EmailAccountForMessage (message);
                var composeViewController = new MessageComposeViewController (account);
                composeViewController.Composer.Kind = action;
                composeViewController.Composer.RelatedThread = thread;
                composeViewController.StartWithQuickResponse = startWithQuickResponse;
                composeViewController.Present ();
            }
        }

        #region IBodyViewOwner implementation

        void IBodyViewOwner.SizeChanged ()
        {
            // BodyView calls its layout delegate after the download of the body has finished.
            // Downloading the body can result in a status change for attachments.  So we refresh
            // the attachments in addition to laying out the view.
            attachmentListView.Refresh ();
            LayoutView ();
        }

        void IBodyViewOwner.LinkSelected (NSUrl url)
        {
            if (EmailHelper.IsMailToURL (url.AbsoluteString)) {
                string body;
                var message = thread.FirstMessageSpecialCase ();
                // The message may have been deleted while the view was open.
                if (null == message) {
                    var account = McAccount.EmailAccountForMessage (message);
                    var composeViewController = new MessageComposeViewController (account);
                    composeViewController.Composer.Message = EmailHelper.MessageFromMailTo (account, url.AbsoluteString, out body);
                    composeViewController.Composer.InitialText = body;
                    composeViewController.Present ();
                }
            } else {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }

        void IBodyViewOwner.DismissView ()
        {
            NavigationController.PopViewController (true);
        }

        #endregion
    }
}
