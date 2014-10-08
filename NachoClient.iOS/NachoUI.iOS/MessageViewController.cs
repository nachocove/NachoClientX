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
using MonoTouch.Dialog;

namespace NachoClient.iOS
{
    public partial class MessageViewController : NcUIViewController, INachoMessageEditorParent,
        INachoFolderChooserParent, INachoCalendarItemEditorParent, INcDatePickerDelegate, IUcAddressBlockDelegate, INachoDateControllerParent
    {
        const int TOVIEW_LEFT_MARGIN = 20;
        const int CCVIEW_LEFT_MARGIN = 20;

        public McEmailMessageThread thread;
        // A container view inside scrollView. All header, message part, attachment views go
        // inside this view. Vertical scroll view scrolls this view to move all subviews in
        // unison
        protected UIView view;
        // A container view inside horizontalScrollView. All message part views go inside this
        // view. Horizontal scroll view scrolls and zoom this view to move and scale all subviews
        // in unison. Header and attachment subviews are left unzoomed.
        protected AttachmentListView attachmentListView;
        protected List<McAttachment> attachments;
        protected UcAddressBlock toView;
        protected UcAddressBlock ccView;
        protected BodyView bodyView;

        protected UIBarButtonItem chiliButton;
        protected UIBarButtonItem deadlineButton;
        protected McAccount account;

        protected RecursionCounter deferLayout;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected int LINE_OFFSET = 30;
        protected int CELL_HEIGHT = 44;

        protected bool expandedHeader = false;
        protected bool firstConfig = true;
        protected float expandedSeparatorYOffset;
        protected float compactSeparatorYOffset;

        protected UIBarButtonItem blockMenuButton;

        protected const int BLOCK_MENU_TAG = 1000;

        protected float separator1YOffset {
            get {
                return (expandedHeader ? expandedSeparatorYOffset : compactSeparatorYOffset);
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

        protected bool HasAttachments {
            get {
                return (0 < attachments.Count);
            }
        }

        public MessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewWillLayoutSubviews ()
        {
            base.ViewWillLayoutSubviews ();
            if (null != TabBarController) {
                ViewFramer.Create (View).AdjustHeight (TabBarController.TabBar.Frame.Height);
            }
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            chiliButton = new UIBarButtonItem ("Hot", UIBarButtonItemStyle.Plain, null);

            blockMenuButton = new UIBarButtonItem ();
            deadlineButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (blockMenuButton, "gen-more");
            Util.SetOriginalImageForButton (quickReplyButton, "contact-quickemail");
            Util.SetOriginalImageForButton (deadlineButton, "email-calendartime");
            var spacer = new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace) { Width = 5 };

            // Multiple buttons spaced evently
            ToolbarItems = new UIBarButtonItem[] {
                replyButton,
                flexibleSpaceButton,
                chiliButton,
                flexibleSpaceButton,
                archiveButton,
                spacer,
                saveButton,
                spacer,
                deleteButton,
            };

            Util.SetOriginalImageForButton (replyButton, "toolbar-icn-reply");
            Util.SetOriginalImageForButton (archiveButton, "email-archive-gray");
            Util.SetOriginalImageForButton (saveButton, "email-fileinfolder-gray");
            Util.SetOriginalImageForButton (deleteButton, "email-delete-gray");

            // Multiple buttons on the right side
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                blockMenuButton,
                deadlineButton,
                quickReplyButton,
            };
            quickReplyButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.REPLY_ACTION, NcQuickResponse.QRTypeEnum.Reply));
            };

            blockMenuButton.Clicked += (object sender, EventArgs e) => {
                UIBlockMenu blockMenu = (UIBlockMenu)View.ViewWithTag(BLOCK_MENU_TAG);
                blockMenu.MenuTapped ();
            };

            saveButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToFolders", this);
            };
            replyButton.Clicked += (object sender, EventArgs e) => {
                ReplyActionSheet ();
            };
            archiveButton.Clicked += (object sender, EventArgs e) => {
                ArchiveThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                DeleteThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };
            chiliButton.Clicked += (object sender, EventArgs e) => {
                var message = thread.SingleMessageSpecialCase ();
                message.ToggleHotOrNot ();
                ConfigureToolbar ();
            };
            deadlineButton.Clicked += (object sender, EventArgs e) => {
                DeadlineActionSheet ();
            };

            FetchAttachments ();
            CreateView ();

            MarkAsRead ();

            //Remove thin black line from bottom of navigation controller
            UINavigationBar b = NavigationController.NavigationBar;
            b.SetBackgroundImage(new UIImage (),UIBarMetrics.Default);
            b.ShadowImage = new UIImage ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }
            ConfigureView ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if ((NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) || (NcResult.SubKindEnum.Error_AttDownloadFailed == s.Status.SubKind)) {
                FetchAttachments ();
                ConfigureAttachments ();
                return;
            }
            if (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded == s.Status.SubKind) {
                Log.Info (Log.LOG_EMAIL, "EmailMessageBodyDownloadSucceeded");
                bodyView.DownloadComplete (true);
                ConfigureView ();
                MarkAsRead();
            }
            if (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed == s.Status.SubKind) {
                Log.Info (Log.LOG_EMAIL, "EmailMessageBodyDownloadFailed");
                bodyView.DownloadComplete (false);
                ConfigureView ();
            }
        }

        protected void FetchAttachments ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if (null == message) {
                attachments = new List<McAttachment> ();
                return;
            }
            attachments = McAttachment.QueryByItemId (message);
        }

        protected void ReplyActionSheet ()
        {
            var actionSheet = new UIActionSheet ();
            actionSheet.Add ("Reply");
            actionSheet.Add ("Reply All");
            actionSheet.Add ("Forward");
            actionSheet.Add ("Cancel");

            actionSheet.CancelButtonIndex = 3;

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.REPLY_ACTION));
                    break;
                case 1:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.REPLY_ALL_ACTION));
                    break;
                case 2:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.FORWARD_ACTION));
                    break;
                case 3:
                    break; // Cancel
                }
            };
            actionSheet.ShowFromToolbar (NavigationController.Toolbar);
        }

        protected void DeadlineActionSheet ()
        {
            var actionSheet = new UIActionSheet ();
            actionSheet.Add ("Set Deadline");
            actionSheet.Add ("Create Meeting");
            actionSheet.Add ("Cancel");

            actionSheet.CancelButtonIndex = 2;

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    PerformSegue ("SegueToDatePicker", new SegueHolder (null));
                    break;
                case 1:
                    var c = CalendarHelper.CreateMeeting (thread.SingleMessageSpecialCase ());
                    PerformSegue ("SegueToEditEvent", new SegueHolder (c));
                    break;
                case 2:
                    break; // Cancel
                }
            };
            actionSheet.ShowFrom (deadlineButton, true);
        }

        public void DismissDatePicker (DatePickerViewController vc, DateTime chosenDateTime)
        {
            NcMessageDeferral.SetDueDate (thread, chosenDateTime);
            vc.owner = null;
            vc.DismissViewController (false, null);
            ConfigureView ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessageViewToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                vc.thread = thread;
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "MessageViewToFolders") {
                var vc = (FoldersViewController)segue.DestinationViewController;
                vc.SetModal (true);
                vc.SetOwner (this, thread);
                return;
            }
            if (segue.Identifier == "MessageViewToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;

                if (null != h.value) {
                    vc.SetAction (thread, (string)h.value);
                    vc.SetOwner (this);  
                    if (null != h.value2) {
                        vc.SetQRType((NcQuickResponse.QRTypeEnum)h.value2);
                    }
                }

                return;
            }
            if (segue.Identifier == "MessageViewToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                var e = h.value as McEvent;
                vc.SetOwner (this);
                vc.SetCalendarItem (e, CalendarItemEditorAction.create);
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
                var e = holder.value as McEvent;
                vc.SetCalendarItem (e, CalendarItemEditorAction.create);
                vc.SetOwner (this);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        public void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            if (MessageDeferralType.Custom != request) {
                NcMessageDeferral.DeferThread (thread, request);
            } else {
                NcMessageDeferral.DeferThread (thread, request, selectedDate);
            }
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.SetOwner (null);
            vc.DimissDateController (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var t = CalendarHelper.CreateTask (m);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("", new SegueHolder (t));
            }));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var c = CalendarHelper.CreateMeeting (m);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("MessageViewToEditEvent", new SegueHolder (c));
            }));
        }

        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        /// <summary>
        /// INachoFolderChooser delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            MoveThisMessage (folder);
            vc.SetOwner (null, null);
            vc.DismissFolderChooser (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        /// <summary>
        /// INachoFolderChooser delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
        }

        void MarkAsRead ()
        {
            var message = thread.SingleMessageSpecialCase ();
            if (!message.IsDownloaded ()) {
                return;
            }
            if (false == message.IsRead) {
                BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id);
            }
        }

        public void DeleteThisMessage ()
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (m);
        }

        public void ArchiveThisMessage ()
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (m);
        }

        public void MoveThisMessage (McFolder folder)
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Move (m, folder);
        }

        public enum TagType {
            USER_IMAGE_TAG = 101,
            FROM_TAG = 102,
            SUBJECT_TAG = 103,
            REMINDER_TEXT_TAG = 104,
            REMINDER_ICON_TAG = 105,
            ATTACHMENT_ICON_TAG = 106,
            RECEIVED_DATE_TAG = 107,
            SEPARATOR1_TAG = 108,
            SEPARATOR2_TAG = 112,
            SPINNER_TAG = BodyView.TagType.SPINNER_TAG,
            USER_LABEL_TAG = 110,
            USER_CHILI_TAG = 111,
            MESSAGE_PART_TAG = BodyView.TagType.MESSAGE_PART_TAG,
            CALENDAR_PART_TAG = BodyCalendarView.CALENDAR_PART_TAG,
            ATTACHMENT_VIEW_TAG = 301,
            ATTACHMENT_NAME_TAG = 302,
            ATTACHMENT_STATUS_TAG = 303,
            DOWNLOAD_TAG = BodyView.TagType.DOWNLOAD_TAG,
        }

        #if (DEBUG_UI)
        const int VIEW_INSET = 4;
        const int ATTACHMENTVIEW_INSET = 10;
        #else
        const int VIEW_INSET = 2;
        const int ATTACHMENTVIEW_INSET = 15;
        #endif

        protected void CreateView ()
        {
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
            view = new UIView (ViewHelper.InnerFrameWithInset (View.Frame, VIEW_INSET));
            scrollView.AddSubview (view);

            #if (DEBUG_UI)
            view.BackgroundColor = A.Color_NachoRed;
            scrollView.BackgroundColor = A.Color_NachoTeal;
            #endif

            float yOffset = 0;

            scrollView.DidZoom += (object sender, EventArgs e) => {
                Log.Info (Log.LOG_UI, "vertical scrollview did zoom");
            };
            scrollView.MinimumZoomScale = 1.0f;
            scrollView.MaximumZoomScale = 1.0f;
            scrollView.Bounces = false;

            // A single tap on the header section (everything above the horizontal rule separator)
            // toggles between the compact and expanded view of the header.
            var singletap = new UITapGestureRecognizer ();
            singletap.NumberOfTapsRequired = 1;
            singletap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("SingleTapSelector:"));
            singletap.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            view.AddGestureRecognizer (singletap);

            // User image view
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = (int)TagType.USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = (int)TagType.USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            yOffset = 15;

            // From label view
            // Font will vary bold or regular, depending on isRead.
            // Size fields will be recalculated after text is known.
            var fromLabelView = new UILabel (new RectangleF (65, 15, 150, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = (int)TagType.FROM_TAG;
            fromLabelView.UserInteractionEnabled = true;
            view.AddSubview (fromLabelView);

            yOffset += 20;

            // Subject label view
            // Size fields will be recalculated after text is known.
            var subjectLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.WordWrap;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = (int)TagType.SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            yOffset += 20;

            // Received label view
            var receivedLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Left;
            receivedLabelView.Tag = (int)TagType.RECEIVED_DATE_TAG;
            view.AddSubview (receivedLabelView);

            yOffset += 20;

            // To label view
            float aBlockWidth = view.Frame.Width - TOVIEW_LEFT_MARGIN;
            toView = new UcAddressBlock (this, "To:", aBlockWidth);
            toView.SetCompact (false, -1);
            toView.SetEditable (false);
            toView.SetLineHeight (20);
            toView.SetAddressIndentation (45);
            ViewFramer.Create (toView)
                .X (TOVIEW_LEFT_MARGIN)
                .Y (yOffset)
                .Width (aBlockWidth)
                .Height (0);
            view.AddSubview (toView);

            // CC label view
            aBlockWidth = view.Frame.Width - CCVIEW_LEFT_MARGIN;
            ccView = new UcAddressBlock (this, "Cc:", aBlockWidth);
            ccView.SetCompact (false, -1);
            ccView.SetEditable (false);
            ccView.SetLineHeight (20);
            ccView.SetAddressIndentation (45);
            ViewFramer.Create (ccView)
                .X (CCVIEW_LEFT_MARGIN)
                .Y (yOffset)
                .Width (aBlockWidth)
                .Height (0);
            view.AddSubview (ccView);

            // Reminder image view
            var reminderImageView = new UIImageView (new RectangleF (65, yOffset + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = (int)TagType.REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new RectangleF (87, yOffset, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = (int)TagType.REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Chili image view
            var chiliImageView = new UIImageView (new RectangleF (View.Frame.Width - 20 - 15, 14, 20, 20));
            chiliImageView.Image = UIImage.FromBundle("icn-red-chili-small");
            chiliImageView.Tag = (int)TagType.USER_CHILI_TAG;
            view.AddSubview (chiliImageView);

            // Separator 1
            var separator1View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator1View.BackgroundColor = A.Color_NachoBorderGray;
            separator1View.Tag = (int)TagType.SEPARATOR1_TAG;
            view.AddSubview (separator1View);

            // Attachments
            attachmentListView =
                new AttachmentListView (new RectangleF (ATTACHMENTVIEW_INSET, yOffset + 1.0f,
                    view.Frame.Width - ATTACHMENTVIEW_INSET, 30.0f));
            attachmentListView.OnAttachmentSelected = onAttachmentSelected;
            attachmentListView.OnStateChanged = (bool IsExpanded) => {
                LayoutView ();
            };
            attachmentListView.Tag = (int)TagType.ATTACHMENT_VIEW_TAG;
            if (HasAttachments) {
                attachmentListView.Hidden = false;
                yOffset += attachmentListView.Frame.Height;
            } else {
                attachmentListView.Hidden = true;
            }
            view.AddSubview (attachmentListView);

            // Separator 2
            var separator2View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator2View.BackgroundColor = A.Color_NachoBorderGray;
            separator2View.Tag = (int)TagType.SEPARATOR2_TAG;
            view.AddSubview (separator2View);

            yOffset += 1;

            // Horizontal scroll bar - All message parts go inside here.
            bodyView = new BodyView (new RectangleF (
                BodyView.BODYVIEW_INSET,
                yOffset,
                view.Frame.Width - 2 * BodyView.BODYVIEW_INSET,
                view.Frame.Height - BodyView.BODYVIEW_INSET),
                view);
            bodyView.VerticalScrollingEnabled = false;
            bodyView.SpinnerCenteredOnParentFrame = true;
            bodyView.OnRenderStart = () => {
                deferLayout.Increment ();
            };
            bodyView.OnRenderComplete = () => {
                deferLayout.Decrement ();
            };
            view.AddSubview (bodyView);

            // Spinner
            var spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.Center = View.Center;
            spinner.HidesWhenStopped = true;
            spinner.Tag = (int)TagType.SPINNER_TAG;
            view.AddSubview (spinner);
        }

        protected void ConfigureView ()
        {
            var message = thread.SingleMessageSpecialCase ();
            attachments = McAttachment.QueryByItemId (message);

            // User image view
            var userImageView = view.ViewWithTag ((int)TagType.USER_IMAGE_TAG) as UIImageView;
            var userLabelView = view.ViewWithTag ((int)TagType.USER_LABEL_TAG) as UILabel;
            userImageView.Hidden = true;
            userLabelView.Hidden = true;

            var userImage = Util.ImageOfSender (message.AccountId, Pretty.EmailString (message.From));

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                if (String.IsNullOrEmpty (message.cachedFromLetters) || (2 <= message.cachedFromColor)) {
                    Util.CacheUserMessageFields (message);
                }
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
            }

            VerticalLayoutCursor cursor = new VerticalLayoutCursor (view);
            cursor.AddSpace (35); // for From and top inset

            // Subject label view
            var subjectLabelView = View.ViewWithTag ((int)TagType.SUBJECT_TAG) as UILabel;
            subjectLabelView.Lines = 0;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            if (String.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.TextColor = A.Color_9B9B9B;
            }
            cursor.LayoutView (subjectLabelView);

            // Received label view
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
                AdjustY (reminderImageView, yOffset + 4);
                AdjustY (reminderLabelView, yOffset);
                yOffset += 20;
                cursor.AddSpace (20);
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            compactSeparatorYOffset = cursor.TotalHeight;

            if (firstConfig) {
                toView.Clear ();
                foreach (var address in NcEmailAddress.ParseToAddressListString (message.To)) {
                    toView.Append (address);
                }
                ccView.Clear ();
                foreach (var address in NcEmailAddress.ParseCcAddressListString (message.Cc)) {
                    ccView.Append (address);
                }
                toView.ConfigureView ();
                ccView.ConfigureView ();
                cursor.LayoutView (toView);
                cursor.LayoutView (ccView);

                expandedSeparatorYOffset = cursor.TotalHeight;

                var separatorView = View.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
                separatorView.Frame = new RectangleF (0, compactSeparatorYOffset, View.Frame.Width, 1);
                firstConfig = false;
            }

            // Chili image view
            var chiliImageView = View.ViewWithTag ((int)TagType.USER_CHILI_TAG) as UIImageView;
            float X;
            if (message.isHot ()) {
                chiliImageView.Hidden = false;
                X = chiliImageView.Frame.X;
            } else {
                chiliImageView.Hidden = true;
                X = View.Frame.Width;
            }

            // From label view
            var fromLabelView = View.ViewWithTag ((int)TagType.FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = X - 10 - 16 - 65;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

            deferLayout = new RecursionCounter (() => {
                LayoutView ();
            });
            deferLayout.Increment (); // count = 1

            RenderBody (message);
            ConfigureAttachments ();

            ConfigureToolbar ();

            deferLayout.Decrement ();

            UIBlockMenu blockMenu = new UIBlockMenu (this, new List<UIBlockMenu.Block> () {
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

            blockMenu.Tag = BLOCK_MENU_TAG;
            View.AddSubview (blockMenu);
        }

        protected void AdjustY (UIView view, float yOffset)
        {
            var rect = view.Frame;
            rect.Y = yOffset;
            view.Frame = rect;
        }

        protected void ConfigureToolbar ()
        {
            var message = thread.SingleMessageSpecialCase ();

            string icon;
            switch (message.UserAction) {
            case 0:
                icon = (message.isHot () ? "icn-nothot-gray" : "icn-hot-gray");
                break;
            case 1:
                icon = "icn-nothot-gray";
                break;
            case -1:
                icon = "icn-hot-gray";
                break;
            default:
                icon = "shutup";
                NcAssert.CaseError ();
                break;
            }
            Util.SetOriginalImageForButton (chiliButton, icon);
        }

        protected void RenderBody (McEmailMessage message)
        {
            bodyView.Configure (message);

        }

        protected void ConfigureAttachments ()
        {
            attachmentListView.Reset ();
            for (int i = 0; i < attachments.Count; i++) {
                if (0 < i) {
                    attachmentListView.LastAttachmentView ().ShowSeparator ();
                }
                attachmentListView.AddAttachment (attachments [i]);
            }
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
            var separator1View = view.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
            ViewFramer.Create (separator1View).Y (separator1YOffset);
            var separator2View = view.ViewWithTag ((int)TagType.SEPARATOR2_TAG);
            ViewFramer.Create (separator2View).Y (separator2YOffset);

            LayoutScrollViews ();

            #if (DEBUG_UI)
            ViewHelper.DumpViews<TagType> (scrollView);
            #endif
        }


        [MonoTouch.Foundation.Export ("SingleTapSelector:")]
        public void OnSingleTap (UIGestureRecognizer sender)
        {
            // Make sure the touch is in the header area
            PointF touch = sender.LocationInView (view);
            if (touch.Y > separator1YOffset) {
                return;
            }

            // Toggle header display mode and redraw
            expandedHeader = !expandedHeader;

            LayoutView (true);
        }

        protected void onAttachmentSelected (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            } else {
                PlatformHelpers.DownloadAttachment (attachment);
            }
        }

        protected void LayoutAttachmentListView ()
        {
            ViewFramer.Create (attachmentListView).Y (separator1YOffset + 1.0f);
            attachmentListView.Hidden = !HasAttachments;
        }

        protected void LayoutVerticalScrollView ()
        {
            // Just need to set view (inside scrollView) with a small inset
            float width = scrollView.Frame.Width - 2 * VIEW_INSET;
            float height;

            height = separator1YOffset;
            height += bodyView.Frame.Height * bodyView.ZoomScale;
            height += 2 * VIEW_INSET;
            height = Math.Max (height, scrollView.Frame.Height);
            view.Frame = new RectangleF (VIEW_INSET, VIEW_INSET, width, height);

            scrollView.ContentSize = new SizeF(
                view.Frame.Width + 2 * VIEW_INSET,
                view.Frame.Height + 2 * VIEW_INSET
            );
        }

        protected void LayoutBodyView ()
        {
            // Assume messageView properly configured already so its size is known.
            // Also, view is properly laid out so its (finalized) frame is known.

            // Horizontal scroll view width is a function of the screen size alone.
            // (Width of messageView does not matter since it is allowed to scroll
            // horizontally.)

            // By default, the body view should take up all remaining
            // space on the screen after all header subviews and attachment list
            // view are laid out. This default height is also the smallest value
            // However, if messageView height is larger than this value, we must
            // increase the height to prevent vertical scroll bar from showing up.
            float height = View.Frame.Height;
            height -= 2 * BodyView.BODYVIEW_INSET;
            var separator = view.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
            height -= separator.Frame.Bottom;
            if (null != attachmentListView) {
                height -= attachmentListView.Frame.Height;
            }

            bodyView.Layout (VIEW_INSET, separator2YOffset + 1,
                view.Frame.Width - 2 * BodyView.BODYVIEW_INSET, height);
        }

        protected void LayoutScrollViews ()
        {
            LayoutAttachmentListView ();    // layout attachmentListView & view
            LayoutBodyView ();
            LayoutVerticalScrollView ();    // layout scrollView
        }

        // IUcAddressBlockDelegate
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

        public void AddressBlockAddContactClicked(UcAddressBlock view, string prefix)
        {
        }
    }
}
