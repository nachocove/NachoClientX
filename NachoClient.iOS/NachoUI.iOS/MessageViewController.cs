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
        protected UIView view;
        protected AttachmentListView attachmentListView;
        protected UcAddressBlock toView;
        protected UcAddressBlock ccView;
        protected BodyView bodyView;
        protected UIBlockMenu blockMenu;
        protected UITapGestureRecognizer singleTapGesture;
        protected UITapGestureRecognizer.Token singleTapGestureHandlerToken;

        // UI elements for the navigation bar
        protected UIBarButtonItem deadlineButton;
        protected UIBarButtonItem blockMenuButton;
        protected UIActionSheet replyActionSheet;
        protected UIActionSheet deadlineActionSheet;

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
        protected RecursionCounter deferLayout;
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
            SPINNER_TAG = BodyView.TagType.SPINNER_TAG,
            USER_LABEL_TAG = 110,
            USER_CHILI_TAG = 111,
            MESSAGE_PART_TAG = BodyView.TagType.MESSAGE_PART_TAG,
            CALENDAR_PART_TAG = BodyCalendarView.CALENDAR_PART_TAG,
            ATTACHMENT_VIEW_TAG = 301,
            ATTACHMENT_NAME_TAG = 302,
            ATTACHMENT_STATUS_TAG = 303,
            DOWNLOAD_TAG = BodyView.TagType.DOWNLOAD_TAG,
            BLOCK_MENU_TAG = 1000,
        }

        public MessageViewController (IntPtr handle)
            : base (handle)
        {
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
        }

        protected override void CreateViewHierarchy ()
        {
            // Navigation controls

            Util.SetAutomaticImageForButton (replyButton, "toolbar-icn-reply");
            replyButton.TintColor = A.Color_NachoGreen;

            Util.SetAutomaticImageForButton (archiveButton, "email-archive-gray");
            archiveButton.TintColor = A.Color_NachoGreen;

            Util.SetAutomaticImageForButton (deleteButton, "email-delete-gray");
            deleteButton.TintColor = A.Color_NachoGreen;

            fixedSpaceButton.Width = 10;

            ToolbarItems = new UIBarButtonItem[] {
                replyButton,
                flexibleSpaceButton,
                archiveButton,
                fixedSpaceButton,
                deleteButton,
            };

            blockMenuButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (blockMenuButton, "gen-more");
            deadlineButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (deadlineButton, "email-calendartime");
            Util.SetAutomaticImageForButton (quickReplyButton, "contact-quickemail");

            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                blockMenuButton,
                deadlineButton,
                quickReplyButton,
            };

            quickReplyButton.Clicked += QuickReplyButtonClicked;
            blockMenuButton.Clicked += BlockMenuBottonClicked;
            replyButton.Clicked += ReplyButtonClicked;
            archiveButton.Clicked += ArchiveButtonClicked;
            deleteButton.Clicked += DeleteButtonClicked;
            deadlineButton.Clicked += DeadlineButtonClicked;

            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            // Main view

            view = new UIView (ViewHelper.InnerFrameWithInset (View.Frame, VIEW_INSET));
            scrollView.AddSubview (view);

            #if DEBUG_UI
            view.BackgroundColor = A.Color_NachoRed;
            scrollView.BackgroundColor = A.Color_NachoTeal;
            #endif

            scrollView.DidZoom += ScrollViewDidZoom;
            scrollView.MinimumZoomScale = 1.0f;
            scrollView.MaximumZoomScale = 1.0f;
            scrollView.Bounces = false;
            scrollView.Scrolled += ScrollViewScrolled;

            // A single tap on the header section toggles between the compact and expanded
            // views of the header.
            singleTapGesture = new UITapGestureRecognizer ();
            singleTapGesture.NumberOfTapsRequired = 1;
            singleTapGestureHandlerToken = singleTapGesture.AddTarget (HeaderSingleTapHandler);
            singleTapGesture.ShouldRecognizeSimultaneously = SingleTapGestureRecognizer;
            view.AddGestureRecognizer (singleTapGesture);

            // User image
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = (int)TagType.USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User label, to be used if no image is available
            var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = (int)TagType.USER_LABEL_TAG;
            view.AddSubview (userLabelView);

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
            view.AddSubview (fromLabelView);

            yOffset += 20;

            // Subject label
            var subjectLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.WordWrap;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = (int)TagType.SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            yOffset += 20;

            // Received label
            var receivedLabelView = new UILabel (new RectangleF (65, yOffset, 250, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Left;
            receivedLabelView.Tag = (int)TagType.RECEIVED_DATE_TAG;
            view.AddSubview (receivedLabelView);

            yOffset += 20;

            // "To" label
            float blockWidth = view.Frame.Width - TOVIEW_LEFT_MARGIN;
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
            view.AddSubview (toView);

            // "cc" label
            blockWidth = view.Frame.Width - CCVIEW_LEFT_MARGIN;
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
            view.AddSubview (ccView);

            // Reminder image
            var reminderImageView = new UIImageView (new RectangleF (65, yOffset + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = (int)TagType.REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label
            var reminderLabelView = new UILabel (new RectangleF (87, yOffset, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = (int)TagType.REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Chili image
            var chiliImageView = new UIImageView (new RectangleF (View.Frame.Width - 20 - 15, 14, 20, 20));
            chiliImageView.Image = UIImage.FromBundle ("icn-red-chili-small");
            chiliImageView.Tag = (int)TagType.USER_CHILI_TAG;
            view.AddSubview (chiliImageView);

            // Separator 1
            var separator1View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator1View.BackgroundColor = A.Color_NachoBorderGray;
            separator1View.Tag = (int)TagType.SEPARATOR1_TAG;
            view.AddSubview (separator1View);

            // Attachments
            attachmentListView = new AttachmentListView (new RectangleF (
                ATTACHMENTVIEW_INSET, yOffset + 1.0f,
                view.Frame.Width - ATTACHMENTVIEW_INSET, 50));
            attachmentListView.OnAttachmentSelected = AttachmentsOnSelected;
            attachmentListView.OnStateChanged = AttachmentsOnStateChange;
            attachmentListView.Tag = (int)TagType.ATTACHMENT_VIEW_TAG;
            view.AddSubview (attachmentListView);

            // Separater 2
            var separator2View = new UIView (new RectangleF (0, yOffset, 320, 1));
            separator2View.BackgroundColor = A.Color_NachoBorderGray;
            separator2View.Tag = (int)TagType.SEPARATOR2_TAG;
            view.AddSubview (separator2View);

            yOffset += 1;

            // Message body
            bodyView = new BodyView (new RectangleF (
                BodyView.BODYVIEW_INSET, yOffset,
                view.Frame.Width - 2 * BodyView.BODYVIEW_INSET, view.Frame.Height - BodyView.BODYVIEW_INSET),
                view);
            bodyView.VerticalScrollingEnabled = false;
            bodyView.HorizontalScrollingEnabled = false;
            bodyView.SpinnerCenteredOnParentFrame = true;
            bodyView.OnRenderStart = BodyViewOnRenderStart;
            bodyView.OnRenderComplete = BodyViewOnRenderComplete;
            view.AddSubview (bodyView);

            // Spinner
            var spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.Center = View.Center;
            spinner.HidesWhenStopped = true;
            spinner.Tag = (int)TagType.SPINNER_TAG;
            view.AddSubview (spinner);

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
            view.AddSubview (blockMenu);

            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        protected override void ConfigureAndLayout ()
        {
            var message = thread.SingleMessageSpecialCase ();
            attachments = McAttachment.QueryByItemId (message);

            var userImageView = view.ViewWithTag ((int)TagType.USER_IMAGE_TAG) as UIImageView;
            var userLabelView = view.ViewWithTag ((int)TagType.USER_LABEL_TAG) as UILabel;
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

            var cursor = new VerticalLayoutCursor (view);
            cursor.AddSpace (35); // for From and top inset

            var subjectLabelView = View.ViewWithTag ((int)TagType.SUBJECT_TAG) as UILabel;
            subjectLabelView.Lines = 0;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            if (string.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.TextColor = A.Color_9B9B9B;
            }
            cursor.LayoutView (subjectLabelView);

            var receivedLabelView = View.ViewWithTag ((int)TagType.RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.FullDateString (message.DateReceived);
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

            var separator1View = View.ViewWithTag ((int)TagType.SEPARATOR1_TAG);
            separator1View.Frame = new RectangleF (0, compactSeparatorYOffset, view.Frame.Width, 1);

            var chiliImageView = View.ViewWithTag ((int)TagType.USER_CHILI_TAG) as UIImageView;
            float chiliX;
            if (message.isHot ()) {
                chiliImageView.Hidden = false;
                chiliX = chiliImageView.Frame.X;
            } else {
                chiliImageView.Hidden = true;
                chiliX = View.Frame.Width;
            }

            ConfigureToolbar ();

            var fromLabelView = View.ViewWithTag ((int)TagType.FROM_TAG) as UILabel;
            ViewFramer.Create (fromLabelView).Width (chiliX - 10 - 16 - 65);
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

            deferLayout = new RecursionCounter (() => {
                LayoutView ();
            });
            deferLayout.Increment ();

            ConfigureAttachments ();

            if (bodyView.Configure (message)) {
                MarkAsRead ();
            }

            deferLayout.Decrement ();
        }

        protected override void Cleanup ()
        {
            // Remove all callbacks and handlers.
            singleTapGesture.RemoveTarget (singleTapGestureHandlerToken);
            singleTapGesture.ShouldRecognizeSimultaneously = null;
            view.RemoveGestureRecognizer (singleTapGesture);
            scrollView.Scrolled -= ScrollViewScrolled;
            quickReplyButton.Clicked -= QuickReplyButtonClicked;
            blockMenuButton.Clicked -= BlockMenuBottonClicked;
            replyButton.Clicked -= ReplyButtonClicked;
            archiveButton.Clicked -= ArchiveButtonClicked;
            deleteButton.Clicked -= DeleteButtonClicked;
            deadlineButton.Clicked -= DeadlineButtonClicked;
            scrollView.DidZoom -= ScrollViewDidZoom;
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;

            blockMenu.Cleanup ();

            archiveButton = null;
            deleteButton = null;
            fixedSpaceButton = null;
            flexibleSpaceButton = null;
            forwardButton = null;
            quickReplyButton = null;
            replyAllButton = null;
            replyButton = null;
            scrollView = null;
            blockMenu = null;

            view = null;
            attachmentListView = null;
            toView = null;
            ccView = null;
            bodyView = null;
            deadlineButton = null;
            blockMenuButton = null;
            replyActionSheet = null;
            deadlineActionSheet = null;
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

            ViewFramer.Create (attachmentListView).Y (separator1YOffset + 1.0f);

            // When setting the upper left corner, account for the X content offset
            // in "view", and the Y content offset in "bodyView".

            float bodyHeight = View.Frame.Height;
            bodyHeight -= 2 * BodyView.BODYVIEW_INSET;
            bodyHeight -= separator2View.Frame.Bottom;
            bodyHeight -= attachmentListView.Frame.Height;
            float bodyY = Math.Max (scrollView.ContentOffset.Y, separator2YOffset + 1);
            bodyView.Layout (VIEW_INSET, bodyY, view.Frame.Width - 2 * BodyView.BODYVIEW_INSET, bodyHeight);

            float viewWidth = scrollView.Frame.Width - 2 * VIEW_INSET;
            float viewHeight = separator2YOffset;
            viewHeight += bodyView.Frame.Height;
            viewHeight += 2 * VIEW_INSET;
            viewHeight = Math.Max (viewHeight, scrollView.Frame.Height);
            view.Frame = new RectangleF (
                VIEW_INSET + scrollView.ContentOffset.X, VIEW_INSET,
                viewWidth, viewHeight);
            scrollView.ContentSize = new SizeF (
                Math.Max (view.Frame.Width, bodyView.ContentSize.Width + 12.0f),
                separator2YOffset + bodyView.ContentSize.Height);

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
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
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
                        vc.SetQRType ((NcQuickResponse.QRTypeEnum)h.value2);
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

        protected void ShowReplyActionSheet ()
        {
            replyActionSheet = new UIActionSheet ();
            replyActionSheet.Add ("Reply");
            replyActionSheet.Add ("Reply All");
            replyActionSheet.Add ("Forward");
            replyActionSheet.Add ("Cancel");
            replyActionSheet.CancelButtonIndex = 3;
            replyActionSheet.Clicked += ReplyActionSheetClicked;
            replyActionSheet.ShowFromToolbar (NavigationController.Toolbar);
        }

        protected void ShowDeadlineActionSheet ()
        {
            deadlineActionSheet = new UIActionSheet ();
            deadlineActionSheet.Add ("Set Deadline");
            deadlineActionSheet.Add ("Create Meeting");
            deadlineActionSheet.Add ("Cancel");
            deadlineActionSheet.CancelButtonIndex = 2;
            deadlineActionSheet.Clicked += DeadlineActionSheetClicked;
            deadlineActionSheet.ShowFrom (deadlineButton, true);
        }

        protected void ConfigureToolbar ()
        {
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
            vc.SetOwner (null);
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
            vc.SetOwner (null, null);
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

            // Process vertical scrolling
            PointF bodyViewOffset = new PointF (scrollView.ContentOffset.X, scrollView.ContentOffset.Y);
            bodyViewOffset.Y -= separator2YOffset;
            ViewFramer framer = ViewFramer.Create (bodyView);
            if (0 < bodyViewOffset.Y) {
                framer.Y (1.0f + separator2YOffset + bodyViewOffset.Y);
            } else {
                framer.Y (1.0f + separator2YOffset);
            }
            bodyView.ScrollTo (bodyViewOffset);

            // Process horizontal scrolling
            framer = ViewFramer.Create (view);
            framer.X (scrollView.ContentOffset.X);
        }

        private void HeaderSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PointF touch = gesture.LocationInView (view);
                if (touch.Y <= separator1YOffset) {
                    expandedHeader = !expandedHeader;
                    LayoutView (true);
                }
            }
        }

        private void QuickReplyButtonClicked (object sender, EventArgs e)
        {
            PerformSegue ("MessageViewToCompose", new SegueHolder (
                MessageComposeViewController.REPLY_ACTION, NcQuickResponse.QRTypeEnum.Reply));
        }

        private void BlockMenuBottonClicked (object sender, EventArgs e)
        {
            UIBlockMenu blockMenu = (UIBlockMenu)View.ViewWithTag ((int)TagType.BLOCK_MENU_TAG);
            blockMenu.MenuTapped ();
        }

        private void ReplyButtonClicked (object sender, EventArgs e)
        {
            ShowReplyActionSheet ();
        }

        private void ArchiveButtonClicked (object sender, EventArgs e)
        {
            ArchiveThisMessage ();
            NavigationController.PopViewControllerAnimated (true);
        }

        private void DeleteButtonClicked (object sender, EventArgs e)
        {
            DeleteThisMessage ();
            NavigationController.PopViewControllerAnimated (true);
        }

        private void DeadlineButtonClicked (object sender, EventArgs e)
        {
            ShowDeadlineActionSheet ();
        }

        private void ScrollViewDidZoom (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "vertical scrollview did zoom");
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

        private void BodyViewOnRenderStart ()
        {
            deferLayout.Increment ();
        }

        private void BodyViewOnRenderComplete ()
        {
            deferLayout.Decrement ();
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_AttDownloadUpdate == statusEvent.Status.SubKind || NcResult.SubKindEnum.Error_AttDownloadFailed == statusEvent.Status.SubKind) {
                FetchAttachments ();
                ConfigureAttachments ();
                return;
            }
            if (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded == statusEvent.Status.SubKind) {
                var token = statusEvent.Tokens.FirstOrDefault ();
                Log.Info (Log.LOG_EMAIL, "EmailMessageBodyDownloadSucceeded {0}", token);
                if (bodyView.DownloadComplete (true, token)) {
                    ConfigureAndLayout ();
                    MarkAsRead ();
                }
            }
            if (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed == statusEvent.Status.SubKind) {
                var token = statusEvent.Tokens.FirstOrDefault ();
                Log.Info (Log.LOG_EMAIL, "EmailMessageBodyDownloadFailed {0}", token);
                if (bodyView.DownloadComplete (false, token)) {
                    ConfigureAndLayout ();
                }
            }
        }

        private void ReplyActionSheetClicked (object sender, UIButtonEventArgs b)
        {
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
            replyActionSheet.Clicked -= ReplyActionSheetClicked;
            replyActionSheet = null;
        }

        private void DeadlineActionSheetClicked (object sender, UIButtonEventArgs b)
        {
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
            deadlineActionSheet.Clicked -= DeadlineActionSheetClicked;
            deadlineActionSheet = null;
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
