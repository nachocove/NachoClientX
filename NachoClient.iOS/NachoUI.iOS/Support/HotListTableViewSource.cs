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
using NachoCore.Brain;

namespace NachoClient.iOS
{

    public class HotListTableViewSource : UITableViewSource, INachoFolderChooserParent, IBodyViewOwner
    {
        INachoEmailMessages messageThreads;
        protected const string EmailMessageReuseIdentifier = "EmailMessage";
        protected const string AccountReuseIdentifier = "Account";

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

        protected const int UNREAD_MESSAGES_VIEW = 99115;

        public UIEdgeInsets CellCardInset = new UIEdgeInsets (5.0f, 7.0f, 5.0f, 8.0f);
        public UIEdgeInsets PreviewInset = new UIEdgeInsets (5.0f, 12.0f, 5.0f, 12.0f);
        public nfloat CardPeekDistance = 10.0f;
        protected CGPoint? expectedScrollEndOffset;
        protected int cardIndexAtScrollStart;

        public HotListTableViewSource (IMessageTableViewSourceDelegate owner, INachoEmailMessages messageThreads)
        {
            this.owner = owner;
            this.messageThreads = messageThreads;
        }

        public void SetMessageThreads(INachoEmailMessages messageThreads)
        {
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
            return messageThreads.Count () + 1;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            if (indexPath.Row < messageThreads.Count ()) {
                cell = tableView.DequeueReusableCell (EmailMessageReuseIdentifier);
                if (null == cell) {
                    cell = CreateMessageCell (tableView);
                }
                ConfigureMessageCell (tableView, cell, indexPath);
            } else {
                cell = tableView.DequeueReusableCell (AccountReuseIdentifier);
                if (null == cell) {
                    cell = CreateAccountCell (tableView);
                }
                ConfigureAccountCell (tableView, cell, indexPath);
            }
            return cell;
        }

        protected UITableViewCell CreateCardCell (UITableView tableView, string reuseIdentifier)
        {
            var cell = new UITableViewCell (UITableViewCellStyle.Default, reuseIdentifier);
            if (cell.Frame.Height < 300) {
                // The table view will change this height anyway, but we need to ensure that subsequent
                // calculations don't end up generating a negative height for a web view, which
                // will crash if given a negative height.  So just pick a number big enough and rely on 
                // autoresizing masks to set everything right when the cell is actually laid out at the correct height.
                cell.Frame = new CGRect (cell.Frame.X, cell.Frame.Y, cell.Frame.Width, 300.0f);
                cell.LayoutIfNeeded ();
            }
            if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;
            cell.ContentView.BackgroundColor = A.Color_NachoBackgroundGray;

            var cardFrame = new CGRect (
                CellCardInset.Left,
                CellCardInset.Top,
                cell.ContentView.Frame.Width - CellCardInset.Left - CellCardInset.Right,
                cell.ContentView.Frame.Height - CellCardInset.Top - CellCardInset.Bottom
            );
            var cardView = new UIView (cardFrame);
            cardView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            cardView.BackgroundColor = UIColor.White;
            cardView.Tag = CARD_VIEW_TAG;
            cardView.Layer.CornerRadius = 6.0f;
            cardView.ClipsToBounds = true;
            cardView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            cardView.Layer.BorderWidth = .5f;
            cell.ContentView.AddSubview (cardView);
            return cell;
        }

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UITableViewCell CreateMessageCell (UITableView tableView)
        {
            var cell = CreateCardCell (tableView, EmailMessageReuseIdentifier);
            var cardView = cell.ViewWithTag (CARD_VIEW_TAG);

            var frame = new CGRect (0, 0, cardView.Frame.Width, cardView.Frame.Height);
            var view = new SwipeActionView (frame);
            view.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            view.Tag = SWIPE_TAG;
            view.BackgroundColor = UIColor.White;

            cardView.AddSubview (view);

            view.SetAction (ARCHIVE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (SAVE_BUTTON, SwipeSide.LEFT);
            view.SetAction (DEFER_BUTTON, SwipeSide.LEFT);

            view.ContentMode = UIViewContentMode.Center;

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
            var unreadMessageView = new UIImageView (new CGRect (15, 60, 40, 27));
            unreadMessageView.ContentMode = UIViewContentMode.Center;
            using (var image = UIImage.FromBundle ("SlideNav-Btn")) {
                unreadMessageView.Image = image;
            }
            unreadMessageView.BackgroundColor = UIColor.White;
            unreadMessageView.Tag = UNREAD_IMAGE_TAG;
            unreadMessageView.UserInteractionEnabled = true;
            view.AddSubview (unreadMessageView);
            var unreadTap = new UITapGestureRecognizer ();
            unreadTap.CancelsTouchesInView = true;
            unreadTap.AddTarget (this, new ObjCRuntime.Selector ("UnreadViewTapped:"));
            unreadMessageView.AddGestureRecognizer (unreadTap);

            var messageHeaderView = new MessageHeaderView (new CGRect (65, 0, frame.Width - 65, 75));
            messageHeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            messageHeaderView.CreateView ();
            messageHeaderView.Tag = MESSAGE_HEADER_TAG;
            messageHeaderView.SetAllBackgroundColors (UIColor.White);
            view.AddSubview (messageHeaderView);
            // Reminder image view
            var reminderImageView = new UIImageView (new CGRect (65, 75 + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            reminderImageView.Hidden = true;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new CGRect (87, 75, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            reminderLabelView.Hidden = true;
            view.AddSubview (reminderLabelView);

            var toolbar = new MessageToolbar (new CGRect (0, frame.Height - 44, frame.Width, 44));
            toolbar.Tag = TOOLBAR_TAG;
            view.AddSubview (toolbar);

            // Preview label view
            var previewFrame = PreviewFrame (cell);
            var previewLabelView = new ScrollableBodyView (previewFrame, this);
            previewLabelView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            previewLabelView.Tag = PREVIEW_TAG;
            view.InsertSubviewBelow (previewLabelView, toolbar);

            var moreImageView = new UIImageView (new CGRect (view.Frame.Width - 40, frame.Height - 44 - 32, 18, 10));
            moreImageView.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin;
            moreImageView.ContentMode = UIViewContentMode.Center;
            moreImageView.Image = UIImage.FromBundle ("gen-readmore");
            moreImageView.Tag = USER_MORE_TAG;
            moreImageView.BackgroundColor = A.Color_NachoLightGrayBackground;
            moreImageView.Layer.CornerRadius = 2;
            moreImageView.Layer.MasksToBounds = true;
            view.AddSubview (moreImageView);

            return cell;
        }

        protected UITableViewCell CreateAccountCell (UITableView tableView)
        {
            var cell = CreateCardCell (tableView, AccountReuseIdentifier);
            var cardView = cell.ViewWithTag (CARD_VIEW_TAG);
            var frame = new CGRect (0, 0, cardView.Frame.Width, cardView.Frame.Height);

            var cellFont = (UIScreen.MainScreen.Bounds.Width > 390 ? A.Font_AvenirNextMedium17 : A.Font_AvenirNextMedium14);

            var noMessagesView = new UIView (frame);
            noMessagesView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;

            var minButtonsFrame = ((40 * 3) + (2 * A.Card_Vertical_Indent));
            var messageFrameHeight = noMessagesView.Frame.Height - minButtonsFrame;

            var helpView = new HotHelpMessageView (new CGRect (0.0, 0.0, noMessagesView.Frame.Width, messageFrameHeight));
            helpView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            NSMutableAttributedString lastCardMessage = null;
            var stringAttributes = new UIStringAttributes {
                ForegroundColor = A.Color_NachoGreen,
                BackgroundColor = UIColor.White,
                Font = cellFont
            };
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
            helpView.hotListLabel.AttributedText = lastCardMessage;

            noMessagesView.AddSubview (helpView);

            var cellHeight = (noMessagesView.Frame.Height - messageFrameHeight) / 3;
            var unreadMessageViewFrame = new CGRect (A.Card_Horizontal_Indent, messageFrameHeight, frame.Width - A.Card_Horizontal_Indent, cellHeight);
            var unreadMessagesView = new UnreadMessagesView (unreadMessageViewFrame, InboxClicked, DeadlinesClicked, DeferredClicked);
            unreadMessagesView.Tag = UNREAD_MESSAGES_VIEW;
            unreadMessagesView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;

            unreadMessagesView.SetFont (cellFont);

            noMessagesView.AddSubview (unreadMessagesView);

            cardView.AddSubview (noMessagesView);

            return cell;
        }

        private void ConfigureAccountCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            var unreadMessagesView = (UnreadMessagesView)cell.ViewWithTag (UNREAD_MESSAGES_VIEW);
            unreadMessagesView.Update (NcApplication.Instance.Account);
        }

        private CGRect PreviewFrame (UITableViewCell cell)
        {
            var view = cell.ContentView.ViewWithTag (SWIPE_TAG);
            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG);
            var frame = view.Frame;
            var toolbar = view.ViewWithTag (TOOLBAR_TAG);
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG);
            nfloat top = reminderLabelView.Hidden ? messageHeaderView.Frame.Bottom : reminderLabelView.Frame.Bottom;
            return new CGRect (
                PreviewInset.Left,
                top + PreviewInset.Top,
                frame.Width - PreviewInset.Left - PreviewInset.Right,
                toolbar.Frame.Top - top - PreviewInset.Top - PreviewInset.Bottom
            );
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
        protected void ConfigureMessageCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {

            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            view.OnSwipe = null;
            view.OnClick = null;
            view.ShouldSwipe = ShouldSwipeCell;

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
            // Notify brain that the message is being shown
            NcBrain.MessageNotificationStatusUpdated (message, DateTime.UtcNow, 60.0);

            cell.TextLabel.Text = "";
            foreach (var v in cell.ContentView.Subviews) {
                v.Hidden = false;
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
                    onReplyButtonClicked (messageThread, EmailHelper.Action.Reply);
                    break;
                case MessageToolbar.ActionType.REPLY_ALL:
                    onReplyButtonClicked (messageThread, EmailHelper.Action.ReplyAll);
                    break;
                case MessageToolbar.ActionType.FORWARD:
                    onReplyButtonClicked (messageThread, EmailHelper.Action.Forward);
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
            unreadMessageView.Hidden = false;
            if (message.IsRead) {
                using (var image = UIImage.FromBundle ("MessageRead")) {
                    unreadMessageView.Image = image;
                }
            } else {
                using (var image = UIImage.FromBundle ("SlideNav-Btn")) {
                    unreadMessageView.Image = image;
                }
            }

            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG) as MessageHeaderView;
            messageHeaderView.ConfigureMessageView (messageThread, message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureMessageView (messageThread, message);
            };

            // Reminder image view and label
            var reminderImageView = view.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            var previewView = (ScrollableBodyView)view.ViewWithTag (PREVIEW_TAG);
            previewView.Frame = PreviewFrame (cell);
            previewView.SetItem (message);
        }

        public bool ShouldSwipeCell ()
        {
            return !scrolling;
        }

        [Foundation.Export ("UnreadViewTapped:")]
        public void UnreadViewTapped (UIGestureRecognizer sender)
        {
            var view = sender.View;
            while (view != null && !(view is UITableViewCell)) {
                view = view.Superview;
            }
            if (view != null) {
                var cell = view as UITableViewCell;
                while (view != null && !(view is UITableView)) {
                    view = view.Superview;
                }
                if (view != null) {
                    var tableView = view as UITableView;
                    var indexPath = tableView.IndexPathForCell (cell);
                    var messageThread = messageThreads.GetEmailThread (indexPath.Row);
                    var message = messageThread.FirstMessageSpecialCase ();
                    EmailHelper.ToggleRead (message);
                    tableView.ReloadRows (new NSIndexPath[]{ indexPath }, UITableViewRowAnimation.None);
                }
            }
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            Log.Info (Log.LOG_UI, "HotList WillSelectRow {0} message count: {1}", indexPath, messageThreads.Count());
            if (indexPath.Row < messageThreads.Count ()) {
                Log.Info (Log.LOG_UI, "Hot WillSelectRow returning {0}", indexPath);
                return indexPath;
            }
            Log.Info (Log.LOG_UI, "HotList WillSelectRow returning null", indexPath);
            return null;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            Log.Info (Log.LOG_UI, "HotList RowSelected {0}", indexPath);
            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            if (null == messageThread) {
                Log.Info (Log.LOG_UI, "HotList RowSelected got null messageThread");
                return;
            }
            if (messageThread.HasMultipleMessages ()) {
                Log.Info (Log.LOG_UI, "HotList RowSelected segue to message thread view");
                owner.PerformSegueForDelegate ("SegueToMessageThreadView", new SegueHolder (messageThread));
            } else {
                Log.Info (Log.LOG_UI, "HotList RowSelected segue to message thread view");
                owner.PerformSegueForDelegate ("NachoNowToMessageView", new SegueHolder (messageThread));
            }
        }

        private void InboxClicked (object sender)
        {
            var nachoTabBar = Util.GetActiveTabBar ();
            nachoTabBar.SwitchToInbox ();
        }

        private void DeferredClicked (object sender)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeferredEmailMessages (NcApplication.Instance.Account.Id)));
        }

        private void DeadlinesClicked (object sender)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageList", new SegueHolder (new NachoDeadlineEmailMessages (NcApplication.Instance.Account.Id)));
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

        void onReplyButtonClicked (McEmailMessageThread messageThread, EmailHelper.Action action)
        {
            if (null == messageThread) {
                return;
            }
            owner.RespondToMessageThread (messageThread, action);
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

        public override void DraggingStarted (UIScrollView scrollView)
        {
            cardIndexAtScrollStart = CardIndexNearestOffset ((UITableView)scrollView, scrollView.ContentOffset);
            scrolling = true;
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            scrolling = false;
            EnsureScrollEndIsAsExpected (scrollView);
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                scrolling = false;
                EnsureScrollEndIsAsExpected (scrollView);
            }
        }

        public override void WillEndDragging (UIScrollView scrollView, CGPoint velocity, ref CGPoint targetContentOffset)
        {
            var tableView = (UITableView)scrollView;
            var index = cardIndexAtScrollStart;
            var startCardOffset = OffsetForCardIndex (tableView, index);
            if (velocity.Y > 0) {
                index += 1;
            } else if (velocity.Y < 0) {
                index -= 1;
            } else if (scrollView.ContentOffset.Y > startCardOffset.Y + 25.0) {
                index += 1;
            } else if (scrollView.ContentOffset.Y < startCardOffset.Y - 25.0) {
                index -= 1;
            }
            var offset = OffsetForCardIndex (tableView, index);
            targetContentOffset.Y = offset.Y;
            expectedScrollEndOffset = offset;
        }

        private void EnsureScrollEndIsAsExpected (UIScrollView scrollView)
        {
            if (expectedScrollEndOffset.HasValue) {
                if (expectedScrollEndOffset.Value.Y != scrollView.ContentOffset.Y) {
                    Log.Warn (Log.LOG_UI, "HotList EnsureScrollEndIsAsExpected correcting offset from {0} to {1}", scrollView.ContentOffset, expectedScrollEndOffset.Value);
                    scrollView.SetContentOffset (expectedScrollEndOffset.Value, false);
                }
                expectedScrollEndOffset = null;
            } else {
                Log.Error (Log.LOG_UI, "HotList EnsureScrollEndIsAsExpected called without expectedScrollEndOffset");
            }
        }

        private int CardIndexNearestOffset (UITableView tableView, CGPoint offset)
        {
            if (offset.Y < -tableView.ContentInset.Top) {
                return 0;
            }
            var index = (int)((offset.Y + tableView.ContentInset.Top + tableView.RowHeight / 2.0f) / tableView.RowHeight);
            var maxIndex = (int)RowsInSection(tableView, 0) - 1;
            if (index > maxIndex){
                return maxIndex;
            }
            return index;
        }

        public int CurrentCardIndex (UITableView tableView)
        {
            return CardIndexNearestOffset (tableView, tableView.ContentOffset);
        }

        private CGPoint OffsetForCardIndex (UITableView tableView, int index)
        {
            var maxIndex = (int)RowsInSection(tableView, 0) - 1;
            if (index < 0) {
                index = 0;
            } else if (index > maxIndex) {
                index = maxIndex;
            }
            CGPoint offset = new CGPoint(0.0, (nfloat)index * tableView.RowHeight - tableView.ContentInset.Top);
            return offset;
        }

        public void ScrollTableViewToCardIndex (UITableView tableView, int index, bool animated)
        {
            var offset = OffsetForCardIndex (tableView, index);
            tableView.SetContentOffset (offset, animated);
        }

        #region IBodyViewOwner implementation

        // Items in the hot list can't react to size changes or be dismissed, so those implementations are empty.
        // We do need to react to URL that are tapped.

        void IBodyViewOwner.SizeChanged ()
        {
        }

        void IBodyViewOwner.LinkSelected (NSUrl url)
        {
            if (EmailHelper.IsMailToURL (url.AbsoluteString)) {
                string body;
                // would be best to use the account from the message that was selected, but we don't have that info here
                var account = NcApplication.Instance.DefaultEmailAccount;
                var composeViewController = new MessageComposeViewController (account);
                composeViewController.Composer.Message = EmailHelper.MessageFromMailTo (account, url.AbsoluteString, out body);
                composeViewController.Composer.InitialText = body;
                composeViewController.Present ();
            } else {
                UIApplication.SharedApplication.OpenUrl (url);
            }
        }

        void IBodyViewOwner.DismissView ()
        {
        }

        #endregion
    }

}

