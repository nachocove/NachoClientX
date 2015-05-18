//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using UIKit;
using Foundation;
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

        public UIView footer;

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

        protected static readonly int DEADLINES_BUTTON_TAG = 99118;
        protected static readonly int DEFERRED_BUTTON_TAG = 99119;
        protected static readonly int INBOX_BUTTON_TAG = 99120;

        protected static readonly nint DEADLINES_ACCESSORY_TAG = 99121;
        protected static readonly nint DEFERRED_ACCESSORY_TAG = 99122;
        protected static readonly nint INBOX_ACCESSORY_TAG = 99123;

        protected static readonly int DEADLINES_ICON_TAG = 99124;
        protected static readonly int DEFERRED_ICON_TAG = 99125;
        protected static readonly int INBOX_ICON_TAG = 99126;

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
        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// The number of rows in the specified section.
        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return messageThreads.Count ();
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
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
            if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;
            cell.ContentView.BackgroundColor = A.Color_NachoBackgroundGray;

            var cellWidth = tableView.Frame.Width;

            var cardFrame = new CGRect (7, 10, tableView.Frame.Width - 15.0f, tableView.Frame.Height - 30);
            var cardView = new UIView (cardFrame);
            cardView.BackgroundColor = A.Color_NachoBackgroundGray;
            cardView.Tag = CARD_VIEW_TAG;
            cell.ContentView.AddSubview (cardView);

            var frame = new CGRect (0, 0, tableView.Frame.Width - 15.0f, tableView.Frame.Height - 40);
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
            var userImageView = new UIImageView (new CGRect (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new CGRect (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            // Unread message dot
            var unreadMessageView = new UIImageView (new CGRect (5, 30, 9, 9));
            using (var image = UIImage.FromBundle ("SlideNav-Btn")) {
                unreadMessageView.Image = image;
            }
            unreadMessageView.BackgroundColor = UIColor.White;
            unreadMessageView.Tag = UNREAD_IMAGE_TAG;
            view.AddSubview (unreadMessageView);

            var messageHeaderView = new MessageHeaderView (new CGRect (65, 0, viewWidth - 65, 75));
            messageHeaderView.CreateView ();
            messageHeaderView.Tag = MESSAGE_HEADER_TAG;
            messageHeaderView.SetAllBackgroundColors (UIColor.White);
            view.AddSubview (messageHeaderView);
            // Reminder image view
            var reminderImageView = new UIImageView (new CGRect (65, 75 + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new CGRect (87, 75, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Preview label view
            // Size fields will be recalculated after text is known
            var previewLabelView = new ScrollableBodyView (new CGRect (12, 75, viewWidth - 15 - 12, view.Frame.Height - 128), onLinkSelected);
            previewLabelView.Tag = PREVIEW_TAG;
            view.AddSubview (previewLabelView);

            var toolbar = new MessageToolbar (new CGRect (0, frame.Height - 44, frame.Width, 44));
            toolbar.Tag = TOOLBAR_TAG;
            view.AddSubview (toolbar);

            var moreImageView = new UIImageView (new CGRect (view.Frame.Width - 40, frame.Height - 44 - 32, 18, 10));
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

            var message = messageThread.FirstMessageSpecialCase ();
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

            var userImage = Util.MessageToPortraitImage (message);

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
            }

            var unreadMessageView = (UIImageView)cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = message.IsRead;

            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG) as MessageHeaderView;
            messageHeaderView.ConfigureMessageView (messageThread, message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureMessageView (messageThread, message);
            };

            nfloat previewViewTop;

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
            nfloat previewViewBottom = view.Frame.Bottom - 44 - 5;

            var previewView = (ScrollableBodyView)view.ViewWithTag (PREVIEW_TAG);
            // X and Width remain the same, but Y and Height might change.
            previewView.ConfigureAndResize (message, isRefresh,
                new CGRect (previewView.Frame.X, previewViewTop, previewView.Frame.Width, previewViewBottom - previewViewTop));
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            return 0;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            return new UIView (new CGRect (0, 0, 0, 0));
        }

        public override nfloat GetHeightForFooter (UITableView tableView, nint section)
        {
            return (tableView.Frame.Height - 30);
        }

        private nfloat GetFooterCardHeight (UITableView tableView)
        {
            return tableView.Frame.Height - 40;
        }

        public override UIView GetViewForFooter (UITableView tableView, nint section)
        {
            var noMessagesView = new UIView ();
            noMessagesView.Frame = new CGRect (7.5f, 10, tableView.Frame.Width - 15.0f, GetFooterCardHeight (tableView));
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
            var rightIndent = (2 * A.Card_Horizontal_Indent) + (cellHeight / 2);

            var buttonsFrame = ((cellHeight * 3) + (2 * A.Card_Vertical_Indent));
            var messageFrameHeight = noMessagesView.Frame.Height - buttonsFrame;

            var isFourS = false;
            var isSixOrGreater = false;
            var isSixPlusOrGreater = false;

            if (260 > noMessagesView.Frame.Height) {
                isFourS = true;
            } else {
                if (360 <= noMessagesView.Frame.Width) {
                    isSixOrGreater = true;
                }
                if (390 < noMessagesView.Frame.Width) {
                    isSixPlusOrGreater = true;
                }
            }


            // Nacho Mail icon
            var nachoMailIcon = new UIImageView ();
            nachoMailIcon.Frame = (isSixOrGreater ? new CGRect (cardWidth / 2 - 32.5f, A.Card_Vertical_Indent, 65, 65) : new CGRect (cardWidth / 2 - 22.5f, A.Card_Horizontal_Indent, 45, 45));
            nachoMailIcon.Image = UIImage.FromBundle ("Bootscreen-1");
            nachoMailIcon.Hidden = isFourS;
            noMessagesView.AddSubview (nachoMailIcon);

            // Chips left
            var chipsLeftIcon = new UIImageView ();
            chipsLeftIcon.Frame = (isSixOrGreater ? new CGRect (0, messageFrameHeight - 45, 115, 45) : new CGRect (0, messageFrameHeight - 33, 85, 33));
            chipsLeftIcon.Image = UIImage.FromBundle ("gen-nacholeft");
            chipsLeftIcon.Hidden = isFourS;
            noMessagesView.AddSubview (chipsLeftIcon);

            // Chips right
            var chipsRightIcon = new UIImageView ();
            chipsRightIcon.Frame = (isSixOrGreater ? new CGRect (cardWidth - 115, messageFrameHeight - 45, 115, 45) : new CGRect (cardWidth - 85, messageFrameHeight - 33, 85, 33));
            chipsRightIcon.Image = UIImage.FromBundle ("gen-nachoright");
            chipsRightIcon.Hidden = isFourS;
            noMessagesView.AddSubview (chipsRightIcon);

            // Empty Hot list label
            var stringAttributes = new UIStringAttributes {
                ForegroundColor = A.Color_NachoGreen,
                BackgroundColor = UIColor.White,
                Font = (isSixPlusOrGreater ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextDemiBold14)
            };

            NSMutableAttributedString lastCardMessage = null;

            if (NoMessageThreads ()) {
                lastCardMessage = new NSMutableAttributedString ("You do not have any Hot messages. \n \nYou can add Hot messages by tapping on the  ", stringAttributes);
            } else {
                lastCardMessage = new NSMutableAttributedString ("You can add Hot messages by tapping on the  ", stringAttributes);
            }
            var lastCardMessagePartTwo = new NSAttributedString ("  icon in your mail.", stringAttributes);

            var inlineIcon = new NachoInlineImageTextAttachment ();
            inlineIcon.Image = UIImage.FromBundle ("email-not-hot");

            var stringWithImage = NSAttributedString.CreateFrom (inlineIcon);
            lastCardMessage.Append (stringWithImage);
            lastCardMessage.Append (lastCardMessagePartTwo);

            var messageWidth = (isSixPlusOrGreater ? noMessagesView.Frame.Width - 4 * A.Card_Horizontal_Indent : 320 - 4 * A.Card_Horizontal_Indent);

            var hotListLabel = new UILabel (new CGRect (0, 0, messageWidth, 50));
            hotListLabel.TextAlignment = UITextAlignment.Center;
            hotListLabel.BackgroundColor = UIColor.White;
            hotListLabel.Lines = 0;
            hotListLabel.LineBreakMode = UILineBreakMode.WordWrap;            
            hotListLabel.AttributedText = lastCardMessage;
            hotListLabel.SizeToFit ();
            var hotListLabelYOffset = (isFourS ? messageFrameHeight / 2 : ((chipsLeftIcon.Frame.Top - nachoMailIcon.Frame.Bottom) / 2) + nachoMailIcon.Frame.Bottom + 5);
            hotListLabel.Center = new CGPoint (noMessagesView.Frame.Width / 2, hotListLabelYOffset); 
            hotListLabel.Hidden = true;
            hotListLabel.Tag = HOT_LIST_LABEL;

            noMessagesView.AddSubview (hotListLabel);

            var iconFrame = new CGRect (A.Card_Horizontal_Indent, 0, cellHeight / 2, cellHeight);

            Util.AddHorizontalLine (0, messageFrameHeight, noMessagesView.Frame.Width, A.Color_NachoBorderGray, noMessagesView);
            Util.AddHorizontalLine (rightIndent, messageFrameHeight + (buttonsFrame / 3), noMessagesView.Frame.Width - rightIndent, A.Color_NachoBorderGray, noMessagesView);
            Util.AddHorizontalLine (rightIndent, messageFrameHeight + (buttonsFrame / 3) * 2, noMessagesView.Frame.Width - rightIndent, A.Color_NachoBorderGray, noMessagesView);

            // Inbox 
            var inboxButton = UIButton.FromType (UIButtonType.RoundedRect);
            inboxButton.Frame = new CGRect (0, messageFrameHeight + A.Card_Vertical_Indent / 2, cardWidth, cellHeight);
            inboxButton.AccessibilityLabel = "Inbox";
            inboxButton.BackgroundColor = UIColor.White;
            inboxButton.Tag = INBOX_BUTTON_TAG;
            inboxButton.TouchUpInside += InboxClicked;

            var inboxIcon = new UIImageView (iconFrame);
            inboxIcon.ContentMode = UIViewContentMode.Center;
            inboxIcon.BackgroundColor = UIColor.White;
            inboxIcon.Image = UIImage.FromBundle ("gen-inbox");
            inboxIcon.Tag = INBOX_ICON_TAG;

            var inboxLabel = new UILabel ();
            inboxLabel.Font = (isSixPlusOrGreater ? A.Font_AvenirNextMedium17 : A.Font_AvenirNextMedium14);
            inboxLabel.TextColor = A.Color_NachoGreen;
            inboxLabel.Lines = 0;
            inboxLabel.LineBreakMode = UILineBreakMode.WordWrap;
            inboxLabel.Tag = INBOX_LABEL;

            var inboxAccessory = Util.AddArrowAccessory (inboxButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            inboxAccessory.Tag = INBOX_ACCESSORY_TAG;

            inboxButton.AddSubview (inboxIcon);
            inboxButton.AddSubview (inboxAccessory);
            inboxButton.AddSubview (inboxLabel);
            noMessagesView.AddSubview (inboxButton);

            // Deadlines
            var deadlinesButton = UIButton.FromType (UIButtonType.RoundedRect);
            deadlinesButton.AccessibilityLabel = "Deadlines";
            deadlinesButton.Frame = new CGRect (0, inboxButton.Frame.Bottom + A.Card_Vertical_Indent / 2, cardWidth, cellHeight);
            deadlinesButton.BackgroundColor = UIColor.White;
            deadlinesButton.Tag = DEADLINES_BUTTON_TAG;
            deadlinesButton.TouchUpInside += DeadlinesClicked;

            var deadlinesIcon = new UIImageView (iconFrame);
            deadlinesIcon.ContentMode = UIViewContentMode.Center;
            deadlinesIcon.BackgroundColor = UIColor.White;
            deadlinesIcon.Image = UIImage.FromBundle ("gen-deadline");
            deadlinesIcon.Tag = DEADLINES_ICON_TAG;

            var deadlinesLabel = new UILabel ();
            deadlinesLabel.Font = (isSixPlusOrGreater ? A.Font_AvenirNextMedium17 : A.Font_AvenirNextMedium14);
            deadlinesLabel.TextColor = A.Color_NachoGreen;
            deadlinesLabel.Lines = 0;
            deadlinesLabel.LineBreakMode = UILineBreakMode.WordWrap;
            deadlinesLabel.Tag = DEADLINES_LABEL;

            var deadlinesAccessory = Util.AddArrowAccessory (deadlinesButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            deadlinesAccessory.Tag = DEADLINES_ACCESSORY_TAG;

            deadlinesButton.AddSubview (deadlinesIcon);
            deadlinesButton.AddSubview (deadlinesAccessory);
            deadlinesButton.AddSubview (deadlinesLabel);
            noMessagesView.AddSubview (deadlinesButton);

            // Deferred
            var deferredButton = UIButton.FromType (UIButtonType.RoundedRect);
            deferredButton.AccessibilityLabel = "Deferred";
            deferredButton.Frame = new CGRect (0, deadlinesButton.Frame.Bottom + A.Card_Vertical_Indent / 2, cardWidth, cellHeight);
            deferredButton.BackgroundColor = UIColor.White;
            deferredButton.Tag = DEFERRED_BUTTON_TAG;
            deferredButton.TouchUpInside += DeferredClicked;

            var deferredIcon = new UIImageView (iconFrame);
            deferredIcon.ContentMode = UIViewContentMode.Center;
            deferredIcon.BackgroundColor = UIColor.White;
            deferredIcon.Image = UIImage.FromBundle ("gen-deferred-msgs");
            deferredIcon.Tag = DEFERRED_ICON_TAG;

            var deferredLabel = new UILabel ();
            deferredLabel.Font = (isSixPlusOrGreater ? A.Font_AvenirNextMedium17 : A.Font_AvenirNextMedium14);
            deferredLabel.TextColor = A.Color_NachoGreen;
            deferredLabel.Lines = 0;
            deferredLabel.LineBreakMode = UILineBreakMode.WordWrap;
            deferredLabel.Tag = DEFERRED_LABEL;

            var deferredAccessory = Util.AddArrowAccessory (deferredButton.Frame.Width - 18 - 12, (cellHeight / 2) - 6, 12);
            deferredAccessory.Tag = DEFERRED_ACCESSORY_TAG;

            deferredButton.AddSubview (deferredIcon);
            deferredButton.AddSubview (deferredAccessory);
            deferredButton.AddSubview (deferredLabel);
            noMessagesView.AddSubview (deferredButton);

            footer = new UIView ();
            footer.BackgroundColor = A.Color_NachoBackgroundGray;
            footer.AddSubview (noMessagesView);

            ConfigureFooter (tableView);

            return footer;
        }

        public void ConfigureFooter (UITableView tableView)
        {
            if (null == footer) {
                return;
            }
            footer.ViewWithTag (HOT_LIST_LABEL).Hidden = false;

            var cellHeight = (tableView.Frame.Height - 40) / 8;
            var cardWidth = tableView.Frame.Width - 15.0f;
            var rightIndent = (2 * A.Card_Horizontal_Indent) + (cellHeight / 2);

            // Inbox label
            var inboxButton = (UIButton)footer.ViewWithTag (INBOX_BUTTON_TAG);
            var inboxLabel = (UILabel)footer.ViewWithTag (INBOX_LABEL);
            inboxLabel.Frame = new CGRect (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);

            var inboxFolder = NcEmailManager.InboxFolder (NcApplication.Instance.Account.Id);
            var unreadInboxMessagesCount = 0;
            if (null != inboxFolder) {
                unreadInboxMessagesCount = McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id);
            }

            inboxLabel.Text = "Go to Inbox (" + unreadInboxMessagesCount + " unread)";
            inboxButton.Enabled = true;
            footer.ViewWithTag (INBOX_ACCESSORY_TAG).Hidden = false;
            inboxLabel.Hidden = false;
            inboxLabel.SizeToFit ();
            inboxLabel.Center = new CGPoint (inboxLabel.Center.X, inboxButton.Frame.Height / 2);

            // Deadline label
            var deadlinesButton = (UIButton)footer.ViewWithTag (DEADLINES_BUTTON_TAG);
            var deadlineMessages = McEmailMessage.QueryDueDateMessageItems (inboxFolder.AccountId);
            var deadlinesLabel = (UILabel)footer.ViewWithTag (DEADLINES_LABEL);
            deadlinesLabel.Frame = new CGRect (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);

            deadlinesLabel.Text = "Go to Deadlines (" + deadlineMessages.Count + ")";
            deadlinesButton.Enabled = true;
            deadlinesLabel.Hidden = false;
            footer.ViewWithTag (DEADLINES_ACCESSORY_TAG).Hidden = false;
            deadlinesLabel.SizeToFit ();
            deadlinesLabel.Center = new CGPoint (deadlinesLabel.Center.X, deadlinesButton.Frame.Height / 2);

            // Deferred label
            var deferredButton = (UIButton)footer.ViewWithTag (DEFERRED_BUTTON_TAG);
            var deferredMessages = new NachoDeferredEmailMessages (inboxFolder.AccountId);
            var deferredLabel = (UILabel)footer.ViewWithTag (DEFERRED_LABEL);
            deferredLabel.Frame = new CGRect (rightIndent, 0, cardWidth - 2 * A.Card_Horizontal_Indent - rightIndent, cellHeight);

            deferredLabel.Text = "Go to Deferred Messages (" + deferredMessages.Count () + ")";
            deferredButton.Enabled = true;
            footer.ViewWithTag (DEFERRED_ACCESSORY_TAG).Hidden = false;

            deferredLabel.Hidden = false;
            deferredLabel.SizeToFit ();
            deferredLabel.Center = new CGPoint (deferredLabel.Center.X, deferredButton.Frame.Height / 2);
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

        private int VisibleRow (UITableView tableView)
        {
            var visibleRows = tableView.IndexPathsForVisibleRows;
            if (1 == visibleRows.Length) {
                // Only one entry in the hot view.
                return visibleRows [0].Row;
            }
            if (2 == visibleRows.Length) {
                if (2 == messageThreads.Count ()) {
                    // There isn't an easy way to tell which row is fully visible and which is only partially visible.
                    // Pretend that no row is visible, so the caller will use the indexPath passed into RowSelected.
                    return -1;
                }
                if (0 == visibleRows[0].Row) {
                    // At the beginning.
                    return 0;
                } else {
                    // At the end of the list.
                    return visibleRows [1].Row;
                }
            }
            if (3 == visibleRows.Length) {
                // The normal case.  We are somewhere in the middle of the list.
                return visibleRows [1].Row;
            }
            return -1;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            // There appears to be a bug in Xamarin or iOS where the indexPath that is passed into RowSelected
            // is sometimes incorrect.  I have not found the pattern for when the bug happens.  So we ignore
            // the row that is passed in and figure out the message that is currently visible.  (This only works
            // because the Hot view shows only one message at a time.  If the Hot view changes, this code will
            // have to change.)
            int selectedRow = VisibleRow (tableView);
            if (0 > selectedRow) {
                selectedRow = indexPath.Row;
            }
            if (selectedRow != indexPath.Row) {
                Log.Warn (Log.LOG_UI, "HotListTableViewSource.RowSelected was passed a row index, {0}, that is not the currently visible cell, {1}", indexPath.Row, selectedRow);
            }
            if (NoMessageThreads ()) {
                return;
            }
            var messageThread = messageThreads.GetEmailThread (selectedRow);
            if (null == messageThread) {
                return;
            }
            if (messageThread.HasMultipleMessages ()) {
                owner.PerformSegueForDelegate ("SegueToMessageThreadView", new SegueHolder (messageThread));
            } else {
                owner.PerformSegueForDelegate ("NachoNowToMessageView", new SegueHolder (messageThread));
            }
        }

        private void InboxClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (NcEmailManager.Inbox (NcApplication.Instance.Account.Id)));
        }

        private void DeferredClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeferredEmailMessages (NcApplication.Instance.Account.Id)));
        }

        private void DeadlinesClicked (object sender, EventArgs e)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeadlineEmailMessages (NcApplication.Instance.Account.Id)));
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
            NcEmailArchiver.Move (messageThread, folder);
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
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (messageThread);
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
            NcEmailArchiver.Archive (messageThread);
        }

        void onDeleteButtonClicked (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            NcEmailArchiver.Delete (messageThread);
        }

        protected CGPoint startingPoint;

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

        public override void WillEndDragging (UIScrollView scrollView, CGPoint velocity, ref CGPoint targetContentOffset)
        {
            if (NoMessageThreads ()) {
                return;
            }

            var tableView = (UITableView)scrollView;
            var totalContentY = tableView.ContentSize.Height;

            var pathForTargetTopCell = tableView.IndexPathForRowAtPoint (new CGPoint (tableView.Frame.X / 2, targetContentOffset.Y + 10));

            if (null == pathForTargetTopCell) {
                return;
            }

            // pull down, go to previous cell
            if (startingPoint.Y >= targetContentOffset.Y) {
                targetContentOffset.Y = tableView.RectForRowAtIndexPath (pathForTargetTopCell).Location.Y - 10;
                return;
            }

            var nextRow = pathForTargetTopCell.Row + 1;

            // push up, go to table footer if past the end
            if (nextRow >= RowsInSection (tableView, pathForTargetTopCell.Section)) {
                targetContentOffset.Y = tableView.RectForFooterInSection (0).Location.Y - 10;
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

