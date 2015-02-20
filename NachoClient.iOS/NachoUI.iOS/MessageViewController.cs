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
    public partial class MessageViewController : NcUIViewControllerNoLeaks,
        INachoMessageEditorParent, INachoFolderChooserParent, INachoCalendarItemEditorParent, 
        INcDatePickerDelegate, IUcAddressBlockDelegate, INachoDateControllerParent
    {
        // Model data
        public McEmailMessageThread thread;
        protected List<McAttachment> attachments;

        // UI elements for the main view
        protected UIView headerView;
        protected AttachmentListView attachmentListView;
        protected UcAddressBlock toView;
        protected UcAddressBlock ccView;
        protected BodyView bodyView;
        protected UIBlockMenu blockMenu;
        protected MessageToolbar messageToolbar;
        protected UITapGestureRecognizer singleTapGesture;
        protected UITapGestureRecognizer.Token singleTapGestureHandlerToken;
        protected UITapGestureRecognizer doubleTapGesture;
        protected UITapGestureRecognizer.Token doubleTapGestureHandlerToken;

        // UI elements for the navigation bar
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem deferButton;
        protected UIBarButtonItem blockMenuButton;

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

        public enum TagType
        {
            USER_IMAGE_TAG = 101,
            FROM_TAG = 102,
            SUBJECT_TAG = 103,
            REMINDER_TEXT_TAG = 104,
            REMINDER_ICON_TAG = 105,
            ATTACHMENT_ICON_TAG = 106,
            RECEIVED_DATE_TAG = 107,
            SEPARATOR1_TAG = 108,
            SEPARATOR2_TAG = 112,
            USER_LABEL_TAG = 110,
            USER_CHILI_TAG = 111,
            BLANK_VIEW_TAG = 113,
            ATTACHMENT_VIEW_TAG = 301,
            ATTACHMENT_NAME_TAG = 302,
            ATTACHMENT_STATUS_TAG = 303,
            BLOCK_MENU_TAG = 1000,
        }

        public MessageViewController (IntPtr handle)
            : base (handle)
        {
        }

        protected override void CreateViewHierarchy ()
        {
            ViewFramer.Create (scrollView).Height (View.Frame.Height - 44 - 64);

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

            messageToolbar = new MessageToolbar (new CGRect (0, scrollView.Frame.Bottom, View.Frame.Width, 44));
            messageToolbar.OnClick = (object sender, EventArgs e) => {
                var toolbarEventArgs = (MessageToolbarEventArgs)e;
                switch (toolbarEventArgs.Action) {
                case MessageToolbar.ActionType.REPLY:
                    onReplyButtonClicked (MessageComposeViewController.REPLY_ACTION);
                    break;
                case MessageToolbar.ActionType.REPLY_ALL:
                    onReplyButtonClicked (MessageComposeViewController.REPLY_ALL_ACTION);
                    break;
                case MessageToolbar.ActionType.FORWARD:
                    onReplyButtonClicked (MessageComposeViewController.FORWARD_ACTION);
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

            blockMenuButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (blockMenuButton, "gen-more");
            deferButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (deferButton, "email-defer");
            moveButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (moveButton, "folder-move");

            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                blockMenuButton,
                deferButton,
                moveButton,
            };

            moveButton.Clicked += MoveButtonClicked;
            blockMenuButton.Clicked += BlockMenuButtonClicked;
            deferButton.Clicked += DeferButtonClicked;

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

            // Reminder image
            var reminderImageView = new UIImageView (new CGRect (65, yOffset + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = (int)TagType.REMINDER_ICON_TAG;
            headerView.AddSubview (reminderImageView);

            // Reminder label
            var reminderLabelView = new UILabel (new CGRect (87, yOffset, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = (int)TagType.REMINDER_TEXT_TAG;
            headerView.AddSubview (reminderLabelView);

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
            bodyView = BodyView.VariableHeightBodyView (new CGPoint (VIEW_INSET, yOffset), scrollView.Frame.Width - 2 * VIEW_INSET, scrollView.Frame.Size, LayoutView, onLinkSelected);
            scrollView.AddSubview (bodyView);


            blockMenu = new UIBlockMenu (this, new List<UIBlockMenu.Block> () {
                new UIBlockMenu.Block ("contact-quickemail", "Quick Reply", () => {
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.REPLY_ACTION, NcQuickResponse.QRTypeEnum.Reply));
                }),
                new UIBlockMenu.Block ("email-calendartime", "Create Deadline", () => {
                    PerformSegue ("SegueToDatePicker", new SegueHolder (null));
                }),
                new UIBlockMenu.Block ("now-addcalevent", "Create Event", () => {
                    var message = thread.SingleMessageSpecialCase ();
                    if (null != message) {
                        var c = CalendarHelper.CreateMeeting (message);
                        PerformSegue ("SegueToEditEvent", new SegueHolder (c));
                    }
                })
            }, View.Frame.Width);
            blockMenu.Tag = (int)TagType.BLOCK_MENU_TAG;
            View.AddSubview (blockMenu);
        }

        protected override void ConfigureAndLayout ()
        {
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

            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            attachments = McAttachment.QueryByItemId (message);

            var userImageView = headerView.ViewWithTag ((int)TagType.USER_IMAGE_TAG) as UIImageView;
            var userLabelView = headerView.ViewWithTag ((int)TagType.USER_LABEL_TAG) as UILabel;
            var userImage = Util.ImageOfSender (message.AccountId, Pretty.EmailString (message.From));
            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
                userLabelView.Hidden = true;
            } else {
                userLabelView.Hidden = false;
                if (string.IsNullOrEmpty (message.cachedFromLetters) || (2 > message.cachedFromColor)) {
                    Util.CacheUserMessageFields (message);
                }
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
                userImageView.Hidden = true;
            }

            attachmentListView.Hidden = !HasAttachments;

            var cursor = new VerticalLayoutCursor (headerView);
            cursor.AddSpace (35); // for From and top inset

            var subjectLabelView = View.ViewWithTag ((int)TagType.SUBJECT_TAG) as UILabel;
            subjectLabelView.Lines = 0;
            if (string.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.TextColor = A.Color_9B9B9B;
                subjectLabelView.Text = Pretty.NoSubjectString ();
            } else {
                subjectLabelView.TextColor = A.Color_0F424C;
                subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            }
            cursor.LayoutView (subjectLabelView);

            var receivedLabelView = View.ViewWithTag ((int)TagType.RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.FullDateTimeString (message.DateReceived);
            cursor.LayoutView (receivedLabelView);

            // Reminder image view and label
            nfloat yOffset = receivedLabelView.Frame.Bottom;
            var reminderImageView = View.ViewWithTag ((int)TagType.REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = View.ViewWithTag ((int)TagType.REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
                ViewFramer.Create (reminderImageView).Y (yOffset + 4);
                ViewFramer.Create (reminderLabelView).Y (yOffset);
                yOffset += 20;
                cursor.AddSpace (20);
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

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
            moveButton.Clicked -= MoveButtonClicked;
            blockMenuButton.Clicked -= BlockMenuButtonClicked;
            deferButton.Clicked -= DeferButtonClicked;
            messageToolbar.OnClick = null;

            blockMenu.Cleanup ();
            messageToolbar.Cleanup ();
            attachmentListView.Cleanup ();

            moveButton = null;
            scrollView = null;
            blockMenu = null;
            messageToolbar = null;

            headerView = null;
            attachmentListView = null;
            toView = null;
            ccView = null;
            bodyView = null;
            deferButton = null;
            blockMenuButton = null;
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
            scrollView.ContentSize = new CGSize (NMath.Max (headerView.Frame.Width, bodyView.Frame.Width) + 2 * VIEW_INSET, bodyView.Frame.Bottom);

            // MarkAsRead() will change the message from unread to read only if the body has been
            // completely downloaded, so it is safe to call it unconditionally.  We put the call
            // here, rather than in ConfigureAndLayout(), to handle the case where the body is
            // downloaded long after the message view has been opened.
            MarkAsRead ();

            #if (DEBUG_UI)
            ViewHelper.DumpViews<TagType> (scrollView);
            #endif
        }

        protected void LayoutBody ()
        {
            // Force the BodyView to redo its layout.
            ScrollViewScrolled (null, null);
        }

        public override void ViewWillLayoutSubviews ()
        {
            base.ViewWillLayoutSubviews ();
            if (null != TabBarController) {
                ViewFramer.Create (View).AdjustHeight (TabBarController.TabBar.Frame.Height);
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessageViewToMessagePriority") {
                var vc = (INachoDateController)segue.DestinationViewController;
                vc.Setup (this, thread, DateControllerType.Defer);
                return;
            }
            if (segue.Identifier == "MessageViewToFolders") {
                var vc = (INachoFolderChooser)segue.DestinationViewController;
                vc.SetOwner (this, true, thread);
                return;
            }
            if (segue.Identifier == "MessageViewToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;

                if (null != h.value) {
                    vc.SetAction (thread, (string)h.value);
                    vc.SetOwner (this);  
                    if (null != h.value2) {
                        vc.SetQRType ((NcQuickResponse.QRTypeEnum)h.value2);
                    }
                }
                return;
            }
            if (segue.Identifier == "SegueToMailTo") {
                var dc = (MessageComposeViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var url = (string) holder.value;
                dc.SetMailToUrl (url);
                return;
            }
            if (segue.Identifier == "MessageViewToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                var c = h.value as McCalendar;
                vc.SetOwner (this);
                vc.SetCalendarItem (c);
                return;
            }
            if (segue.Identifier == "SegueToDatePicker") {
                var vc = (DatePickerViewController)segue.DestinationViewController;
                vc.owner = this;
                return;
            }
            if (segue.Identifier == "SegueToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var e = holder.value as McCalendar;
                vc.SetCalendarItem (e);
                vc.SetOwner (this);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
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

        protected void MarkAsRead ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if ((null != message) && !message.IsRead) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                if (McBody.IsComplete (body)) {
                    BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id);
                }
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

        // Interface implemntations

        public void DismissDatePicker (DatePickerViewController vc, DateTime chosenDateTime)
        {
            NcMessageDeferral.SetDueDate (thread, chosenDateTime);
            vc.owner = null;
            vc.DismissViewController (false, null);
            ConfigureAndLayout ();
        }

        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new Action (delegate {
                NavigationController.PopViewController (true);
            }));
        }

        public void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DeferThread (thread, request, selectedDate);
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.Setup (null, null, DateControllerType.None);
            vc.DismissDateController (false, new Action (delegate {
                NavigationController.PopViewController (true);
            }));
        }

        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var message = thread.SingleMessageSpecialCase ();
            var task = CalendarHelper.CreateTask (message);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new Action (delegate {
                PerformSegue ("", new SegueHolder (task));
            }));
        }

        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null != message) {
                var cal = CalendarHelper.CreateMeeting (message);
                vc.DismissMessageEditor (false, new Action (delegate {
                    PerformSegue ("MessageViewToEditEvent", new SegueHolder (cal));
                }));
            }
        }

        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            MoveThisMessage (folder);
            vc.SetOwner (null, false, null);
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

        private void MoveButtonClicked (object sender, EventArgs e)
        {
            PerformSegue ("MessageViewToFolders", new SegueHolder (null));
        }

        private void BlockMenuButtonClicked (object sender, EventArgs e)
        {
            UIBlockMenu blockMenu = (UIBlockMenu)View.ViewWithTag ((int)TagType.BLOCK_MENU_TAG);
            blockMenu.MenuTapped ();
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

        private void onReplyButtonClicked (string action)
        {
            PerformSegue ("MessageViewToCompose", new SegueHolder (action));
        }

        private void DeferButtonClicked (object sender, EventArgs e)
        {
            PerformSegue ("MessageViewToMessagePriority", new SegueHolder (null));
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

        public void onLinkSelected(NSUrl url)
        {
            if(EmailHelper.IsMailToURL(url.AbsoluteString)) {
                PerformSegue ("SegueToMailTo", new SegueHolder (url.AbsoluteString));
            } else {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }

    }
}
