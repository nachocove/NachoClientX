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
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreGraphics;
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
        protected McAccount account;
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

        // UI elements for the navigation bar
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem deferButton;
        protected UIBarButtonItem blockMenuButton;

        // UI related constants (or pseudo constants)
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected const int TOVIEW_LEFT_MARGIN = 20;
        protected const int CCVIEW_LEFT_MARGIN = 20;
        #if DEBUG_UI
        const int VIEW_INSET = 4;
        const int ATTACHMENTVIEW_INSET = 10;
#else
        const int VIEW_INSET = 2;
        const int ATTACHMENTVIEW_INSET = 15;
        #endif

        // UI helper objects
        protected bool expandedHeader = false;
        protected float expandedSeparatorYOffset;
        protected float compactSeparatorYOffset;

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
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
        }

        protected override void CreateViewHierarchy ()
        {
            ViewFramer.Create (scrollView).Height (scrollView.Frame.Height - 44);

            // Toolbar controls

            messageToolbar = new MessageToolbar (new RectangleF (0, scrollView.Frame.Bottom, View.Frame.Width, 44));
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

            headerView = new UIView (new RectangleF (VIEW_INSET, 0, View.Frame.Width - 2 * VIEW_INSET, View.Frame.Height));
            scrollView.AddSubview (headerView);

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
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = (int)TagType.USER_IMAGE_TAG;
            headerView.AddSubview (userImageView);

            // User label, to be used if no image is available
            var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
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
            float yOffset = 15;

            // "From" label. Font will be bold or regular, depending on isRead.
            // Sizes will be recalculated after the text is known.
            var fromLabelView = new UILabel (new RectangleF (65, yOffset, 150, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = (int)TagType.FROM_TAG;
            fromLabelView.UserInteractionEnabled = true;
            headerView.AddSubview (fromLabelView);

            yOffset += 20;

            // Subject label
            var subjectLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.WordWrap;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = (int)TagType.SUBJECT_TAG;
            headerView.AddSubview (subjectLabelView);

            yOffset += 20;

            // Received label
            var receivedLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Left;
            receivedLabelView.Tag = (int)TagType.RECEIVED_DATE_TAG;
            headerView.AddSubview (receivedLabelView);

            yOffset += 20;

            // "To" label
            float blockWidth = headerView.Frame.Width - TOVIEW_LEFT_MARGIN;
            toView = new UcAddressBlock (this, "To:", blockWidth);
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
            ccView = new UcAddressBlock (this, "Cc:", blockWidth);
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
            var reminderImageView = new UIImageView (new RectangleF (65, yOffset + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = (int)TagType.REMINDER_ICON_TAG;
            headerView.AddSubview (reminderImageView);

            // Reminder label
            var reminderLabelView = new UILabel (new RectangleF (87, yOffset, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = (int)TagType.REMINDER_TEXT_TAG;
            headerView.AddSubview (reminderLabelView);

            // Chili image
            var chiliImageView = new UIImageView (new RectangleF (View.Frame.Width - 20 - 15, 14, 20, 20));
            chiliImageView.Image = UIImage.FromBundle ("icn-red-chili-small");
            chiliImageView.Tag = (int)TagType.USER_CHILI_TAG;
            headerView.AddSubview (chiliImageView);

            // A blank view below separator2, which covers up the To and CC fields
            // when the headers are colapsed.  (The To and CC fields are often
            // covered by the attachments view or the message body. But not always.)
            var blankView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 0));
            blankView.BackgroundColor = UIColor.White;
            blankView.Tag = (int)TagType.BLANK_VIEW_TAG;
            headerView.AddSubview (blankView);

            // Separator 1
            var separator1View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator1View.BackgroundColor = A.Color_NachoBorderGray;
            separator1View.Tag = (int)TagType.SEPARATOR1_TAG;
            headerView.AddSubview (separator1View);

            // Attachments
            attachmentListView = new AttachmentListView (new RectangleF (
                ATTACHMENTVIEW_INSET, yOffset + 1.0f,
                headerView.Frame.Width - ATTACHMENTVIEW_INSET, 50));
            attachmentListView.OnAttachmentSelected = AttachmentsOnSelected;
            attachmentListView.OnStateChanged = AttachmentsOnStateChange;
            attachmentListView.Tag = (int)TagType.ATTACHMENT_VIEW_TAG;
            headerView.AddSubview (attachmentListView);

            // Separater 2
            var separator2View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator2View.BackgroundColor = A.Color_NachoBorderGray;
            separator2View.Tag = (int)TagType.SEPARATOR2_TAG;
            headerView.AddSubview (separator2View);

            yOffset += 1;

            // Message body, which is added to the scroll view, not the header view.
            bodyView = BodyView.VariableHeightBodyView (new PointF (VIEW_INSET, yOffset), scrollView.Frame.Width - 2 * VIEW_INSET, scrollView.Frame.Size, LayoutView);
            scrollView.AddSubview (bodyView);

            blockMenu = new UIBlockMenu (this, new List<UIBlockMenu.Block> () {
                new UIBlockMenu.Block ("contact-quickemail", "Quick Reply", () => {
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.REPLY_ACTION, NcQuickResponse.QRTypeEnum.Reply));
                }),
                new UIBlockMenu.Block ("email-calendartime", "Create Deadline", () => {
                    PerformSegue ("SegueToDatePicker", new SegueHolder (null));
                }),
                new UIBlockMenu.Block ("now-addcalevent", "Create Event", () => {
                    var c = CalendarHelper.CreateMeeting (thread.SingleMessageSpecialCase ());
                    PerformSegue ("SegueToEditEvent", new SegueHolder (c));
                })
            }, View.Frame.Width);
            blockMenu.Tag = (int)TagType.BLOCK_MENU_TAG;
            View.AddSubview (blockMenu);

            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        protected override void ConfigureAndLayout ()
        {
            var message = thread.SingleMessageSpecialCase ();
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
                if (string.IsNullOrEmpty (message.cachedFromLetters) || 2 <= message.cachedFromColor) {
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
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            if (string.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.TextColor = A.Color_9B9B9B;
            }
            cursor.LayoutView (subjectLabelView);

            var receivedLabelView = View.ViewWithTag ((int)TagType.RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.FullDateTimeString (message.DateReceived);
            cursor.LayoutView (receivedLabelView);

            // Reminder image view and label
            float yOffset = receivedLabelView.Frame.Bottom;
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
            separator1View.Frame = new RectangleF (0, compactSeparatorYOffset, headerView.Frame.Width, 1);

            var chiliImageView = View.ViewWithTag ((int)TagType.USER_CHILI_TAG) as UIImageView;
            float chiliX;
            if (message.isHot ()) {
                chiliImageView.Hidden = false;
                chiliX = chiliImageView.Frame.X;
            } else {
                chiliImageView.Hidden = true;
                chiliX = View.Frame.Width;
            }
                
            var fromLabelView = View.ViewWithTag ((int)TagType.FROM_TAG) as UILabel;
            ViewFramer.Create (fromLabelView).Width (chiliX - 10 - 16 - 65);
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

            ConfigureAttachments ();

            bodyView.Configure (message);

            LayoutView ();
        }

        protected override void Cleanup ()
        {
            // Remove all callbacks and handlers.
            singleTapGesture.RemoveTarget (singleTapGestureHandlerToken);
            singleTapGesture.ShouldRecognizeSimultaneously = null;
            headerView.RemoveGestureRecognizer (singleTapGesture);
            scrollView.Scrolled -= ScrollViewScrolled;
            moveButton.Clicked -= MoveButtonClicked;
            blockMenuButton.Clicked -= BlockMenuButtonClicked;
            deferButton.Clicked -= DeferButtonClicked;
            messageToolbar.OnClick = null;
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;

            blockMenu.Cleanup ();
            messageToolbar.Cleanup ();

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
            float duration = animated ? 0.3f : 0.0f;
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

            ViewFramer.Create (bodyView).Y (separator2YOffset + 1);
            scrollView.ContentSize = new SizeF (Math.Max (headerView.Frame.Width, bodyView.Frame.Width) + 2 * VIEW_INSET, bodyView.Frame.Bottom);

            // MarkAsRead() will change the message from unread to read only if the body has been
            // completely downloaded, so it is safe to call it unconditionally.  We put the call
            // here, rather than in ConfigureAndLayout(), to handle the case where the body is
            // downloaded long after the message view has been opened.
            MarkAsRead ();

            #if (DEBUG_UI)
            ViewHelper.DumpViews<TagType> (scrollView);
            #endif
        }

        public override void ViewWillLayoutSubviews ()
        {
            base.ViewWillLayoutSubviews ();
            if (null != TabBarController) {
                ViewFramer.Create (View).AdjustHeight (TabBarController.TabBar.Frame.Height);
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
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

        protected void FetchAttachments ()
        {
            attachments = McAttachment.QueryByItemId (thread.SingleMessageSpecialCase ());
        }

        protected float separator1YOffset {
            get {
                return expandedHeader ? expandedSeparatorYOffset : compactSeparatorYOffset;
            }
        }

        protected float separator2YOffset {
            get {
                float yOffset = separator1YOffset;
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
            if (!message.IsRead) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                if (McBody.IsComplete (body)) {
                    BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id);
                }
            }
        }

        protected void DeleteThisMessage ()
        {
            NcEmailArchiver.Delete (thread.SingleMessageSpecialCase ());
        }

        protected void ArchiveThisMessage ()
        {
            NcEmailArchiver.Archive (thread.SingleMessageSpecialCase ());
        }

        protected void MoveThisMessage (McFolder folder)
        {
            NcEmailArchiver.Move (thread.SingleMessageSpecialCase (), folder);
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
            vc.DismissMessageEditor (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        public void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DeferThread (thread, request, selectedDate);
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.Setup (null, null, DateControllerType.None);
            vc.DimissDateController (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var message = thread.SingleMessageSpecialCase ();
            var task = CalendarHelper.CreateTask (message);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("", new SegueHolder (task));
            }));
        }

        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var message = thread.SingleMessageSpecialCase ();
            var cal = CalendarHelper.CreateMeeting (message);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("MessageViewToEditEvent", new SegueHolder (cal));
            }));
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
            vc.DismissFolderChooser (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
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
            bodyView.ScrollingAdjustment (new PointF (
                scrollView.ContentOffset.X - bodyView.Frame.X, scrollView.ContentOffset.Y - bodyView.Frame.Y));
        }

        private void HeaderSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PointF touch = gesture.LocationInView (headerView);
                if (touch.Y <= separator1YOffset) {
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
            NavigationController.PopViewControllerAnimated (true);
        }

        private void onArchiveButtonClicked ()
        {
            ArchiveThisMessage ();
            NavigationController.PopViewControllerAnimated (true);
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
            } else {
                PlatformHelpers.DownloadAttachment (attachment);
            }
        }

        private void AttachmentsOnStateChange (bool isExpanded)
        {
            LayoutView (true);
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_AttDownloadUpdate == statusEvent.Status.SubKind || NcResult.SubKindEnum.Error_AttDownloadFailed == statusEvent.Status.SubKind) {
                FetchAttachments ();
                ConfigureAttachments ();
                return;
            }
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

        public void AddressBlockAddContactClicked (UcAddressBlock view, string prefix)
        {
        }
    }
}
