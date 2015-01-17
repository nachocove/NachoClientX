//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class HotListTableViewSource : UITableViewSource, INachoMessageEditorParent, INachoFolderChooserParent
    {
        INachoEmailMessages messageThreads;
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected IMessageTableViewSourceDelegate owner;

        bool scrolling;
        // to control whether swiping is allowed or not

        private const int ARCHIVE_TAG = 1;
        private const int SAVE_TAG = 2;
        private const int DELETE_TAG = 3;
        private const int DEFER_TAG = 4;

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor ARCHIVE_BUTTON =
            new SwipeActionDescriptor (ARCHIVE_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeEmailArchive),
                "Archive", A.Color_NachoSwipeEmailArchive);
        private static SwipeActionDescriptor SAVE_BUTTON =
            new SwipeActionDescriptor (SAVE_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeEmailMove),
                "Move", A.Color_NachoSwipeEmailMove);
        private static SwipeActionDescriptor DELETE_BUTTON =
            new SwipeActionDescriptor (DELETE_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeEmailDelete),
                "Delete", A.Color_NachoSwipeEmailDelete);
        private static SwipeActionDescriptor DEFER_BUTTON =
            new SwipeActionDescriptor (DEFER_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeEmailDefer),
                "Defer", A.Color_NachoSwipeEmailDefer);

        protected const int SWIPE_TAG = 99100;
        protected const int USER_IMAGE_TAG = 99101;
        protected const int USER_LABEL_TAG = 99102;
        protected const int PREVIEW_TAG = 99104;
        protected const int REMINDER_ICON_TAG = 99105;
        protected const int REMINDER_TEXT_TAG = 99106;
        protected const int MESSAGE_HEADER_TAG = 99107;
        protected const int TOOLBAR_TAG = 99109;
        protected const int USER_MORE_TAG = 99110;
        protected const int UNREAD_IMAGE_TAG = 99111;
        protected const int CARD_VIEW_TAG = 99112;

        protected const int NO_MESSAGES_VIEW = 99113;
        protected const int HOT_LIST_LABEL = 99114;
        protected const int DEADLINES_LABEL = 99115;
        protected const int DEFERRED_LABEL = 99116;
        protected const int INBOX_LABEL = 99117;

        protected const int DEADLINES_BUTTON_TAG = 99118;
        protected const int DEFERRED_BUTTON_TAG = 99119;
        protected const int INBOX_BUTTON_TAG = 99120;

        protected const int DEADLINES_ACCESSORY_TAG = 99121;
        protected const int DEFERRED_ACCESSORY_TAG = 99122;
        protected const int INBOX_ACCESSORY_TAG = 99123;

        protected const int DEADLINES_ICON_TAG = 99124;
        protected const int DEFERRED_ICON_TAG = 99125;
        protected const int INBOX_ICON_TAG = 99126;

        public HotListTableViewSource (IMessageTableViewSourceDelegate owner, INachoEmailMessages messageThreads)
        {
            this.owner = owner;
            this.messageThreads = messageThreads;
        }

        protected bool NoMessageThreads ()
        {
            return ((null == messageThreads) || (0 == messageThreads.Count ()));
        }

        /// Tableview delegate
        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// The number of rows in the specified section.
        public override int RowsInSection (UITableView tableview, int section)
        {
            return messageThreads.Count ();
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return (tableView.Frame.Height - 30);
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (EmailMessageReuseIdentifier);
            if (null == cell) {
                cell = CreateCell (tableView);
            }
            ConfigureCell (tableView, cell, indexPath, false);
            return cell;
        }

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UITableViewCell CreateCell (UITableView tableView)
        {
            var cell = new UITableViewCell (UITableViewCellStyle.Default, EmailMessageReuseIdentifier);
            if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;
            cell.ContentView.BackgroundColor = A.Color_NachoBackgroundGray;

            var cellWidth = tableView.Frame.Width;

            var cardFrame = new RectangleF (7, 10, tableView.Frame.Width - 15.0f, tableView.Frame.Height - 30);
            var cardView = new UIView (cardFrame);
            cardView.BackgroundColor = A.Color_NachoBackgroundGray;
            cardView.Tag = CARD_VIEW_TAG;
            cell.ContentView.AddSubview (cardView);

            var frame = new RectangleF (0, 0, tableView.Frame.Width - 15.0f, tableView.Frame.Height - 40);
            var view = new SwipeActionView (frame);
            view.Tag = SWIPE_TAG;
            view.BackgroundColor = UIColor.White;

            cardView.AddSubview (view);

            view.SetAction (ARCHIVE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (SAVE_BUTTON, SwipeSide.LEFT);
            view.SetAction (DEFER_BUTTON, SwipeSide.LEFT);

            view.BackgroundColor = UIColor.White;
            view.AutoresizingMask = UIViewAutoresizing.None;
            view.ContentMode = UIViewContentMode.Center;
            view.Layer.CornerRadius = 6;
            view.Layer.MasksToBounds = true;
            view.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            view.Layer.BorderWidth = .5f;

            var viewWidth = view.Frame.Width;

            // User image view
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            // Unread message dot
            var unreadMessageView = new UIImageView (new Rectangle (5, 30, 9, 9));
            using (var image = UIImage.FromBundle ("SlideNav-Btn")) {
                unreadMessageView.Image = image;
            }
            unreadMessageView.BackgroundColor = UIColor.White;
            unreadMessageView.Tag = UNREAD_IMAGE_TAG;
            view.AddSubview (unreadMessageView);

            var messageHeaderView = new MessageHeaderView (new RectangleF (65, 0, viewWidth - 65, 75));
            messageHeaderView.CreateView ();
            messageHeaderView.Tag = MESSAGE_HEADER_TAG;
            messageHeaderView.SetAllBackgroundColors (UIColor.White);
            view.AddSubview (messageHeaderView);
            // Reminder image view
            var reminderImageView = new UIImageView (new RectangleF (65, 75 + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new RectangleF (87, 75, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Preview label view
            // Size fields will be recalculated after text is known
            var previewLabelView = new ScrollableBodyView (new RectangleF (12, 75, viewWidth - 15 - 12, view.Frame.Height - 128), onLinkSelected);
            previewLabelView.Tag = PREVIEW_TAG;
            view.AddSubview (previewLabelView);

            var toolbar = new MessageToolbar (new RectangleF (0, frame.Height - 44, frame.Width, 44));
            toolbar.Tag = TOOLBAR_TAG;
            view.AddSubview (toolbar);

            var moreImageView = new UIImageView (new RectangleF (view.Frame.Width - 40, frame.Height - 44 - 32, 18, 10));
            moreImageView.ContentMode = UIViewContentMode.Center;
            moreImageView.Image = UIImage.FromBundle ("gen-readmore");
            moreImageView.Tag = USER_MORE_TAG;
            moreImageView.BackgroundColor = A.Color_NachoLightGrayBackground;
            moreImageView.Layer.CornerRadius = 2;
            moreImageView.Layer.MasksToBounds = true;
            view.AddSubview (moreImageView);

            return cell;
        }

        protected void ConfigureAsUnavailable (UITableViewCell cell)
        {
            foreach (var v in cell.ContentView.Subviews) {
                v.Hidden = true;
            }
            cell.TextLabel.Hidden = false;
            cell.TextLabel.Text = "Information temporarily unavailable";
            cell.TextLabel.TextAlignment = UITextAlignment.Center;
            cell.TextLabel.Font = A.Font_AvenirNextDemiBold14;
            cell.TextLabel.TextColor = A.Color_NachoGreen;
            cell.TextLabel.ContentMode = UIViewContentMode.Center;
        }

        protected void ConfigureFooter (UIView footer)
        {

            footer.ViewWithTag (HOT_LIST_LABEL).Hidden = !NoMessageThreads ();

            // Inbox label
            var inboxHitBox = (UIButton)footer.ViewWithTag (INBOX_BUTTON_TAG);
            var inboxLabel = (UILabel)footer.ViewWithTag (INBOX_LABEL);
            var inboxFolder = NcEmailManager.InboxFolder ();
            var unreadInboxMessagesCount = McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id);

            if (0 == unreadInboxMessagesCount) {
                inboxLabel.Text = "You do not have any unread messages in your Inbox.";
                inboxHitBox.Enabled = false;
                footer.ViewWithTag (INBOX_ACCESSORY_TAG).Hidden = true;
            } else if (1 == unreadInboxMessagesCount) {
                inboxLabel.Text = "You have " + unreadInboxMessagesCount + " unread message in your Inbox.";
                inboxHitBox.Enabled = true;
                footer.ViewWithTag (INBOX_ACCESSORY_TAG).Hidden = false;
            } else {
                inboxLabel.Text = "You have " + unreadInboxMessagesCount + " unread messages in your Inbox.";
                inboxHitBox.Enabled = true;
                footer.ViewWithTag (INBOX_ACCESSORY_TAG).Hidden = false;
            }
            inboxLabel.Hidden = false;
            inboxLabel.SizeToFit ();
            inboxLabel.Center = new PointF (inboxLabel.Center.X, inboxHitBox.Frame.Height / 2);

            // Deadline label
            var deadlinesHitBox = (UIButton)footer.ViewWithTag (DEADLINES_BUTTON_TAG);
            var deadlineMessages = McEmailMessage.QueryDueDateMessageItemsAllAccounts ();
            var deadlinesLabel = (UILabel)footer.ViewWithTag (DEADLINES_LABEL);

            if (0 == deadlineMessages.Count) {
                deadlinesLabel.Text = "You do not have any messages with deadlines.";
                deadlinesHitBox.Enabled = false;
                footer.ViewWithTag (DEADLINES_ACCESSORY_TAG).Hidden = true;
            } else if (1 == deadlineMessages.Count) {
                deadlinesLabel.Text = "You have 1 message with a deadline.";
                deadlinesHitBox.Enabled = true;
                footer.ViewWithTag (DEADLINES_ACCESSORY_TAG).Hidden = false;
            } else {
                deadlinesLabel.Text = "You have " + deadlineMessages.Count + " messages with deadlines.";
                deadlinesHitBox.Enabled = true;
                footer.ViewWithTag (DEADLINES_ACCESSORY_TAG).Hidden = false;
            }
            deadlinesLabel.Hidden = false;
            deadlinesLabel.SizeToFit ();
            deadlinesLabel.Center = new PointF (deadlinesLabel.Center.X, inboxHitBox.Frame.Height / 2);

            // Deferred label
            var deferredHitBox = (UIButton)footer.ViewWithTag (DEFERRED_BUTTON_TAG);
            var deferredMessages = new NachoDeferredEmailMessages ();
            var deferredLabel = (UILabel)footer.ViewWithTag (DEFERRED_LABEL);
            if (0 == deferredMessages.Count ()) {
                deferredLabel.Text = "You do not have any deferred messages.";
                deferredHitBox.Enabled = false;
                footer.ViewWithTag (DEFERRED_ACCESSORY_TAG).Hidden = true;
            } else if (1 == deferredMessages.Count ()) {
                deferredLabel.Text = "You have 1 deferred message.";
                deferredHitBox.Enabled = true;
                footer.ViewWithTag (DEFERRED_ACCESSORY_TAG).Hidden = false;
            } else {
                deferredLabel.Text = "You have " + deferredMessages.Count () + " deferred messages.";
                deferredHitBox.Enabled = true;
                footer.ViewWithTag (DEFERRED_ACCESSORY_TAG).Hidden = false;
            }
            deferredLabel.Hidden = false;
            deferredLabel.SizeToFit ();
            deferredLabel.Center = new PointF (deferredLabel.Center.X, inboxHitBox.Frame.Height / 2);
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath, bool isRefresh)
        {
            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            view.DisableSwipe ();
            view.OnSwipe = null;
            view.OnClick = null;

            if (NoMessageThreads ()) {
                return;
            }

            var messageThreadIndex = indexPath.Row;
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);

            if (null == messageThread) {
                ConfigureAsUnavailable (cell);
                return;
            }

            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                ConfigureAsUnavailable (cell);
                return;
            }

            cell.TextLabel.Text = "";
            foreach (var v in cell.ContentView.Subviews) {
                v.Hidden = false;
            }

            var cellWidth = tableView.Frame.Width;

            if (!scrolling) {
                view.EnableSwipe (true);
            }

            view.OnClick = (int tag) => {
                switch (tag) {
                case SAVE_TAG:
                    onSaveButtonClicked (messageThread);
                    break;
                case DEFER_TAG:
                    onDeferButtonClicked (messageThread);
                    break;
                case ARCHIVE_TAG:
                    onArchiveButtonClicked (messageThread);
                    break;
                case DELETE_TAG:
                    onDeleteButtonClicked (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };

            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", state));
                }
            };

            var toolbar = (MessageToolbar)cell.ContentView.ViewWithTag (TOOLBAR_TAG);
            toolbar.OnClick = (object sender, EventArgs e) => {
                var toolbarEventArgs = e as MessageToolbarEventArgs;
                switch (toolbarEventArgs.Action) {
                case MessageToolbar.ActionType.REPLY:
                    onReplyButtonClicked (messageThread, MessageComposeViewController.REPLY_ACTION);
                    break;
                case MessageToolbar.ActionType.REPLY_ALL:
                    onReplyButtonClicked (messageThread, MessageComposeViewController.REPLY_ALL_ACTION);
                    break;
                case MessageToolbar.ActionType.FORWARD:
                    onReplyButtonClicked (messageThread, MessageComposeViewController.FORWARD_ACTION);
                    break;
                case MessageToolbar.ActionType.ARCHIVE:
                    onArchiveButtonClicked (messageThread);
                    break;
                case MessageToolbar.ActionType.DELETE:
                    onDeleteButtonClicked (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown toolbar action {0}", toolbarEventArgs.Action));
                }
            };

            // User image view
            var userImageView = (UIImageView)view.ViewWithTag (USER_IMAGE_TAG);
            var userLabelView = (UILabel)view.ViewWithTag (USER_LABEL_TAG);
            userImageView.Hidden = true;
            userLabelView.Hidden = true;

            var userImage = Util.PortraitOfSender (message);
            if (null == userImage) {
                userImage = Util.ImageOfSender (message.AccountId, Pretty.EmailString (message.From));
            }

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                if (String.IsNullOrEmpty (message.cachedFromLetters) || (2 > message.cachedFromColor)) {
                    Util.CacheUserMessageFields (message);
                }
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
            }

            var unreadMessageView = (UIImageView)cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = message.IsRead;

            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG) as MessageHeaderView;
            messageHeaderView.ConfigureView (message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureView (message);
            };

            float previewViewTop;

            // Reminder image view and label
            var reminderImageView = view.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
                previewViewTop = reminderLabelView.Frame.Bottom;
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
                previewViewTop = messageHeaderView.Frame.Bottom;
            }

            // Five points of padding between the header and the body.
            previewViewTop += 5;

            // The "more" image overlays the preview view
            float previewViewBottom = view.Frame.Bottom - 44 - 5;

            var previewView = (ScrollableBodyView)view.ViewWithTag (PREVIEW_TAG);
            // X and Width remain the same, but Y and Height might change.
            previewView.ConfigureAndResize (message, isRefresh,
                new RectangleF (previewView.Frame.X, previewViewTop, previewView.Frame.Width, previewViewBottom - previewViewTop));
        }

        public override float GetHeightForHeader (UITableView tableView, int section)
        {
            return 0;
        }

        public override UIView GetViewForHeader (UITableView tableView, int section)
        {
            return new UIView (new RectangleF (0, 0, 0, 0));
        }

        public override float GetHeightForFooter (UITableView tableView, int section)
        {
            if (NoMessageThreads ()) {
                return (tableView.Frame.Height - 30);
            }
            return (((tableView.Frame.Height - 40) / 8) * 3) + (4 * A.Card_Vertical_Indent) + 10;
        }

        private float GetFooterCardHeight (UITableView tableView)
        {
            if (NoMessageThreads ()) {
                return tableView.Frame.Height - 40;
            }
            return (((tableView.Frame.Height - 40) / 8) * 3) + (4 * A.Card_Vertical_Indent);
        }

        public override UIView GetViewForFooter (UITableView tableView, int section)
        {
            var noMessagesView = new UIView ();
            noMessagesView.Frame = new RectangleF (7.5f, 10, tableView.Frame.Width - 15.0f, GetFooterCardHeight (tableView));
            noMessagesView.BackgroundColor = UIColor.White;
            noMessagesView.AutoresizingMask = UIViewAutoresizing.None;
            noMessagesView.ContentMode = UIViewContentMode.Center;
            noMessagesView.Layer.CornerRadius = A.Card_Corner_Radius;
            noMessagesView.Layer.MasksToBounds = true;
            noMessagesView.Layer.BorderColor = A.Card_Border_Color;
            noMessagesView.Layer.BorderWidth = A.Card_Border_Width;
            noMessagesView.Tag = NO_MESSAGES_VIEW;

            var cellHeight = (tableView.Frame.Height - 40) / 8;
            var cardWidth = noMessagesView.Frame.Width;

            var messagePadding = (NoMessageThreads () ? noMessagesView.Frame.Height - ((cellHeight * 3) + (4 * A.Card_Vertical_Indent)) : 0);

            // Empty Hot list label
            var stringAttributes = new UIStringAttributes {
                ForegroundColor = A.Color_NachoGreen,
                BackgroundColor = UIColor.White,
                Font = A.Font_AvenirNextDemiBold14
            };

            var noHotMessagesString = new NSMutableAttributedString ("You do not have any Hot messages. \n \nStart adding Hot messages by tapping on the  ", stringAttributes);
            var noHotMessagesStringPartTwo = new NSAttributedString ("  icon in your mail.", stringAttributes);

            var inlineIcon = new NachoInlineImageTextAttachment ();
            inlineIcon.Image = UIImage.FromBundle ("email-not-hot");

            var stringWithAttachment = new NSAttributedString ();
            var stringWithImage = stringWithAttachment.FromTextAttachment (inlineIcon);

            noHotMessagesString.Append (stringWithImage);
            noHotMessagesString.Append (noHotMessagesStringPartTwo);

            var messageWidth = 320 - 4 * A.Card_Horizontal_Indent;

            var hotListLabel = new UILabel (new RectangleF (0, 0, messageWidth, 50));
            hotListLabel.TextAlignment = UITextAlignment.Center;
            hotListLabel.Font = A.Font_AvenirNextDemiBold14;
            hotListLabel.TextColor = A.Color_NachoGreen;
            hotListLabel.BackgroundColor = UIColor.White;
            hotListLabel.Lines = 0;
            hotListLabel.LineBreakMode = UILineBreakMode.WordWrap;            
            hotListLabel.AttributedText = noHotMessagesString;
            hotListLabel.SizeToFit ();
            hotListLabel.Center = new PointF (noMessagesView.Frame.Width / 2, (messagePadding / 2) + (A.Card_Vertical_Indent / 2)); 
            hotListLabel.Hidden = true;
            hotListLabel.Tag = HOT_LIST_LABEL;

            noMessagesView.AddSubview (hotListLabel);

            var iconFrame = new RectangleF (A.Card_Horizontal_Indent, 0, cellHeight / 2, cellHeight);
            var rightIndent = (2 * A.Card_Horizontal_Indent) + (cellHeight / 2);

            // Inbox 
            var inboxButton = UIButton.FromType (UIButtonType.RoundedRect);
            inboxButton.Frame = new RectangleF (0, messagePadding + A.Card_Vertical_Indent, cardWidth, cellHeight);
            inboxButton.BackgroundColor = UIColor.White;
            inboxButton.Tag = INBOX_BUTTON_TAG;
            inboxButton.TouchUpInside += InboxClicked;

            var inboxIcon = new UIImageView (new RectangleF (A.Card_Horizontal_Indent + 2, 0, cellHeight / 2, cellHeight));
            inboxIcon.ContentMode = UIViewContentMode.Center;
            inboxIcon.BackgroundColor = UIColor.White;
            inboxIcon.Image = UIImage.FromBundle ("gen-unread-msgs");
            inboxIcon.Tag = INBOX_ICON_TAG;

            var inboxLabel = new UILabel ();
            inboxLabel.Font = A.Font_AvenirNextDemiBold14;
            inboxLabel.TextColor = A.Color_NachoGreen;
            inboxLabel.Lines = 0;
            inboxLabel.LineBreakMode = UILineBreakMode.WordWrap;
            inboxLabel.Frame = new RectangleF (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);
            inboxLabel.Tag = INBOX_LABEL;

            var inboxAccessory = Util.AddArrowAccessoryView (inboxButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            inboxAccessory.Tag = INBOX_ACCESSORY_TAG;

            inboxButton.AddSubview (inboxIcon);
            inboxButton.AddSubview (inboxAccessory);
            inboxButton.AddSubview (inboxLabel);
            noMessagesView.AddSubview (inboxButton);

            // Deadlines
            var deadlinesButton = UIButton.FromType (UIButtonType.RoundedRect);
            deadlinesButton.Frame = new RectangleF (0, inboxButton.Frame.Bottom + A.Card_Vertical_Indent, cardWidth, cellHeight);
            deadlinesButton.BackgroundColor = UIColor.White;
            deadlinesButton.Tag = DEADLINES_BUTTON_TAG;
            deadlinesButton.TouchUpInside += DeadlinesClicked;

            var deadlinesIcon = new UIImageView (iconFrame);
            deadlinesIcon.ContentMode = UIViewContentMode.Center;
            deadlinesIcon.BackgroundColor = UIColor.White;
            deadlinesIcon.Image = UIImage.FromBundle ("gen-deadline");
            deadlinesIcon.Tag = DEADLINES_ICON_TAG;

            var deadlinesLabel = new UILabel ();
            deadlinesLabel.Font = A.Font_AvenirNextDemiBold14;
            deadlinesLabel.TextColor = A.Color_NachoGreen;
            deadlinesLabel.Lines = 0;
            deadlinesLabel.LineBreakMode = UILineBreakMode.WordWrap;
            deadlinesLabel.Frame = new RectangleF (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);
            deadlinesLabel.Tag = DEADLINES_LABEL;

            var deadlinesAccessory = Util.AddArrowAccessoryView (deadlinesButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            deadlinesAccessory.Tag = DEADLINES_ACCESSORY_TAG;

            deadlinesButton.AddSubview (deadlinesIcon);
            deadlinesButton.AddSubview (deadlinesAccessory);
            deadlinesButton.AddSubview (deadlinesLabel);
            noMessagesView.AddSubview (deadlinesButton);

            // Deferred
            var deferredButton = UIButton.FromType (UIButtonType.RoundedRect);
            deferredButton.Frame = new RectangleF (0, deadlinesButton.Frame.Bottom + A.Card_Vertical_Indent, cardWidth, cellHeight);
            deferredButton.BackgroundColor = UIColor.White;
            deferredButton.Tag = DEFERRED_BUTTON_TAG;
            deferredButton.TouchUpInside += DeferredClicked;

            var deferredIcon = new UIImageView (iconFrame);
            deferredIcon.ContentMode = UIViewContentMode.Center;
            deferredIcon.BackgroundColor = UIColor.White;
            deferredIcon.Image = UIImage.FromBundle ("gen-deferred-msgs");
            deferredIcon.Tag = DEFERRED_ICON_TAG;

            var deferredLabel = new UILabel ();
            deferredLabel.Font = A.Font_AvenirNextDemiBold14;
            deferredLabel.TextColor = A.Color_NachoGreen;
            deferredLabel.Lines = 0;
            deferredLabel.LineBreakMode = UILineBreakMode.WordWrap;
            deferredLabel.Frame = new RectangleF (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);
            deferredLabel.Tag = DEFERRED_LABEL;

            var deferredAccessory = Util.AddArrowAccessoryView (deferredButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            deferredAccessory.Tag = DEFERRED_ACCESSORY_TAG;

            deferredButton.AddSubview (deferredIcon);
            deferredButton.AddSubview (deferredAccessory);
            deferredButton.AddSubview (deferredLabel);
            noMessagesView.AddSubview (deferredButton);

            var footerView = new UIView ();
            footerView.AddSubview (noMessagesView);

            ConfigureFooter (footerView);

            return footerView;
        }

        /// <summary>
        /// Reconfigures the visible cells.
        /// Enables, disables scrolling too.
        /// </summary>
        public void ReconfigureVisibleCells (UITableView tableView)
        {
            if (null == tableView) {
                return;
            }
            var paths = tableView.IndexPathsForVisibleRows;
            if (null != paths) {
                foreach (var path in paths) {
                    var cell = tableView.CellAt (path);
                    if (null != cell) {
                        ConfigureCell (tableView, cell, path, true);
                    }
                }
            }
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoMessageThreads ()) {
                return;
            }
            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToMessageView", new SegueHolder (messageThread));
        }

        private void InboxClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (NcEmailManager.Inbox ()));
        }

        private void DeferredClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeferredEmailMessages ()));
        }

        private void DeadlinesClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeadlineEmailMessages ()));
        }

        /// INachoMessageEditorParent delegate
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.DismissMessageEditor (true, null);
        }

        /// INachoMessageEditorParent delegate
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "MessageTableViewSource: CreateTaskForEmailMessage");
        }

        /// INachoMessageEditorParent delegate
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "MessageTableViewSource: CreateMeetingEmailForMessage");
        }

        /// INachoFolderChooserParent delegate
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            NcAssert.True (cookie is SegueHolder);
            var h = cookie as SegueHolder;

            var messageThread = (McEmailMessageThread)h.value;
            NcAssert.NotNull (messageThread);
            var message = messageThread.SingleMessageSpecialCase ();
            if (null != message) {
                NcEmailArchiver.Move (message, folder);
            }
        }

        /// INachoFolderChooserParent delegate
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
        }

        void onReplyButtonClicked (McEmailMessageThread messageThread, string action)
        {
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToCompose", new SegueHolder (action, messageThread));
        }

        void onChiliButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                return;
            }
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
        }

        void onDeferButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToMessagePriority", new SegueHolder (messageThread));
        }

        void onSaveButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToFolders", new SegueHolder (messageThread));
        }

        void onArchiveButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                return;
            }
            NcEmailArchiver.Archive (message);
        }

        void onDeleteButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                return;
            }
            NcEmailArchiver.Delete (message);
        }

        protected PointF startingPoint;

        public override void DraggingStarted (UIScrollView scrollView)
        {
            scrolling = true;
            var tableView = (UITableView)scrollView;
            ReconfigureVisibleCells (tableView);

            startingPoint = scrollView.ContentOffset;
            NachoCore.Utils.NcAbate.HighPriority ("MessageTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            scrolling = false;
            var tableView = (UITableView)scrollView;
            ReconfigureVisibleCells (tableView);

            NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                scrolling = false;
                var tableView = (UITableView)scrollView;
                ReconfigureVisibleCells (tableView);
                NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DraggingEnded");
            }
        }

        public override void WillEndDragging (UIScrollView scrollView, PointF velocity, ref PointF targetContentOffset)
        {
            if (NoMessageThreads ()) {
                return;
            }

            var tableView = (UITableView)scrollView;
            var pathForTargetTopCell = tableView.IndexPathForRowAtPoint (new PointF (tableView.Frame.X / 2, targetContentOffset.Y + 10));

            if (null == pathForTargetTopCell) {
                return;
            }

            if (startingPoint.Y >= targetContentOffset.Y) {
                targetContentOffset.Y = tableView.RectForRowAtIndexPath (pathForTargetTopCell).Location.Y - 10;
                return;
            }

            var nextRow = pathForTargetTopCell.Row + 1;
            if (nextRow >= RowsInSection (tableView, pathForTargetTopCell.Section)) {
                return;
            }

            var next = NSIndexPath.FromRowSection (nextRow, pathForTargetTopCell.Section);
            if (null == next) {
                return;
            }
            targetContentOffset.Y = tableView.RectForRowAtIndexPath (next).Location.Y - 10;
        }

        public void onLinkSelected (NSUrl url)
        {
            if (EmailHelper.IsMailToURL (url.AbsoluteString)) {
                owner.PerformSegueForDelegate ("SegueToMailTo", new SegueHolder (url.AbsoluteString));
            } else {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }
            
    }
       
}

