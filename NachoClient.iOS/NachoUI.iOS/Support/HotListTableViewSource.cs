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
            if (NoMessageThreads ()) {
                return 1; // "No messages"
            } else {
                return messageThreads.Count ();
            }
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
            ConfigureCell (tableView, cell, indexPath);
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
            var previewLabelView = BodyView.FixedSizeBodyView (new RectangleF (12, 75, viewWidth - 15 - 12, view.Frame.Height - 128));
            previewLabelView.Tag = PREVIEW_TAG;
            view.AddSubview (previewLabelView);

            var toolbar = new MessageToolbar (new RectangleF (0, frame.Height - 44, frame.Width, 44));
            toolbar.Tag = TOOLBAR_TAG;
            view.AddSubview (toolbar);

            var moreImageView = new UIImageView (new RectangleF (view.Frame.Width - 23, frame.Height - 44 - 3 - 16, 16, 16));
            moreImageView.Image = UIImage.FromBundle ("gen-readmore");
            moreImageView.Tag = USER_MORE_TAG;
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
            cell.TextLabel.ContentMode = UIViewContentMode.Center;
        }

        protected void ConfigureAsNoMessages (UITableViewCell cell)
        {
            foreach (var v in cell.ContentView.Subviews) {
                v.Hidden = true;
            }
            cell.TextLabel.Hidden = false;
            cell.TextLabel.Text = "No Hot Messages";
            cell.TextLabel.ContentMode = UIViewContentMode.Center;
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            if (NoMessageThreads ()) {
                ConfigureAsNoMessages (cell);
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

            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;

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

            var userImage = Util.ImageOfSender (message.AccountId, Pretty.EmailString (message.From));

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

            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG) as MessageHeaderView;
            messageHeaderView.ConfigureView (message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureView (message);
            };

            float previewViewAdjustment = 0;

            // Reminder image view and label
            var reminderImageView = view.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
                previewViewAdjustment = 24;
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            // Size of preview, depends on reminder view
            var previewViewHeight = view.Frame.Height - 80 - previewViewAdjustment;
            previewViewHeight -= 44; // toolbar
            previewViewHeight -= 16; // more button

            var previewView = view.ViewWithTag (PREVIEW_TAG) as BodyView;
            previewView.Configure (message);
            previewView.Resize (new RectangleF (12, 0 + previewViewAdjustment, previewView.Frame.Width, previewViewHeight));
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
            NcEmailArchiver.Move (message, folder);
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
            startingPoint = scrollView.ContentOffset;
            NachoCore.Utils.NcAbate.HighPriority ("MessageTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DecelerationEnded");

//            SquareUpCard (scrollView);
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
//                SquareUpCard (scrollView);
                NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DraggingEnded");
            }
        }

        public override void WillEndDragging (UIScrollView scrollView, PointF velocity, ref PointF targetContentOffset)
        {
            var tableView = (UITableView)scrollView;
            var pathForTargetTopCell = tableView.IndexPathForRowAtPoint (new PointF (tableView.Frame.X / 2, targetContentOffset.Y));

            if (startingPoint.Y > targetContentOffset.Y) {
                targetContentOffset.Y = tableView.RectForRowAtIndexPath (pathForTargetTopCell).Location.Y - 10;
                return;
            }

            var next = NSIndexPath.FromRowSection (pathForTargetTopCell.Row + 1, pathForTargetTopCell.Section);
            targetContentOffset.Y = tableView.RectForRowAtIndexPath (next).Location.Y - 10;

        }

        protected void SquareUpCard (UIScrollView scrollView)
        {
            float y = scrollView.ContentOffset.Y;

            if (startingPoint.Y < scrollView.ContentOffset.Y) {
                y += (scrollView.Frame.Height * 0.66f);
            } else {
                y += (scrollView.Frame.Height * 0.33f);
            }

            // Fix us up
            var tableView = (UITableView)scrollView;
            var pathForTargetMiddleCell = tableView.IndexPathForRowAtPoint (new PointF (tableView.Frame.X / 2, y));
            tableView.ScrollToRow (pathForTargetMiddleCell, UITableViewScrollPosition.Middle, true);
        }
    }
}

