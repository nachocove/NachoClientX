//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using UIKit;
using Foundation;
using CoreGraphics;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class MessageTableViewSource : UITableViewSource, IMessageTableViewSource, INachoMessageEditorParent, INachoFolderChooserParent
    {
        string messageWhenEmpty;
        INachoEmailMessages messageThreads;
        protected const string NoMessagesReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";
        protected const string DraftsMessageReuseIdentifier = "DraftsMessage";
        protected HashSet<nint> MultiSelect = null;
        protected bool multiSelectAllowed;
        protected bool multiSelectActive;
        protected bool swipingActive;
        public IMessageTableViewSourceDelegate owner;

        protected NcCapture ArchiveCaptureMessage;
        protected NcCapture RefreshCapture;
        private string ArchiveMessageCaptureName;
        private string RefreshCaptureName;

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


        public INachoEmailMessages GetNachoEmailMessages ()
        {
            return messageThreads;
        }

        public UITableViewSource GetTableViewSource ()
        {
            return this;
        }

        int[] first = new int[3];
        List<McEmailMessage>[] cache = new List<McEmailMessage>[3];
        const int CACHEBLOCKSIZE = 32;

        void ClearCache ()
        {
            for (var i = 0; i < first.Length; i++) {
                first [i] = -1;
            }
        }

        McEmailMessage GetCachedMessage (int i)
        {
            var block = i / CACHEBLOCKSIZE;
            var cacheIndex = block % 3;

            if (block != first [cacheIndex]) {
                MaybeReadBlock (block);
            } else {
                MaybeReadBlock (block - 1);
                MaybeReadBlock (block + 1);
            }

            var index = i % CACHEBLOCKSIZE;
            return cache [cacheIndex] [index];
        }

        void MaybeReadBlock (int block)
        {
            if (0 > block) {
                return;
            }
            var cacheIndex = block % 3;
            if (block == first [cacheIndex]) {
                return;
            }
            var start = block * CACHEBLOCKSIZE;
            var finish = (messageThreads.Count () < (start + CACHEBLOCKSIZE)) ? messageThreads.Count () : start + CACHEBLOCKSIZE;
            var indexList = new List<int> ();
            for (var i = start; i < finish; i++) {
                indexList.Add (messageThreads.GetEmailThread (i).FirstMessageSpecialCaseIndex ());
            }
            cache [cacheIndex] = new List<McEmailMessage> ();
            var resultList = McEmailMessage.QueryForSet (indexList);
            // Reorder the list, add in nulls for missing entries
            foreach (var i in indexList) {
                var result = resultList.Find (x => x.Id == i);
                cache [cacheIndex].Add (result);
            }
            first [cacheIndex] = block;
        }

        public MessageTableViewSource (IMessageTableViewSourceDelegate owner)
        {
            this.owner = owner;
            multiSelectAllowed = true;
            MultiSelect = new HashSet<nint> ();
            ArchiveMessageCaptureName = "MessageTableViewSource.ArchiveMessage";
            NcCapture.AddKind (ArchiveMessageCaptureName);
            ArchiveCaptureMessage = NcCapture.Create (ArchiveMessageCaptureName);
            RefreshCaptureName = "MessageTableViewSource.Refresh";
            NcCapture.AddKind (RefreshCaptureName);
            RefreshCapture = NcCapture.Create (RefreshCaptureName);
        }

        public void SetEmailMessages (INachoEmailMessages messageThreads, string messageWhenEmpty)
        {
            ClearCache ();
            this.messageThreads = messageThreads;
            this.messageWhenEmpty = messageWhenEmpty;
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return messageThreads.GetAdapterForThread (threadId);
        }

        public bool RefreshEmailMessages (out List<int> adds, out List<int> deletes)
        {
            RefreshCapture.Start ();
            ClearCache ();
            var didRefresh = messageThreads.Refresh (out adds, out deletes);
            RefreshCapture.Stop ();
            return didRefresh;
        }

        public bool NoMessageThreads ()
        {
            return ((null == messageThreads) || (0 == messageThreads.Count ()));
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override nint RowsInSection (UITableView tableview, nint section)
        {
            if (NoMessageThreads ()) {
                return 1; // "No messages"
            } else {
                return messageThreads.Count ();
            }
        }

        protected nfloat HeightForMessage (McEmailMessage message)
        {
            if (null == message) {
                return MessageTableViewConstants.NORMAL_ROW_HEIGHT;
            }
            if (message.IsDeferred () || message.HasDueDate ()) {
                return MessageTableViewConstants.DATED_ROW_HEIGHT;
            }
            return MessageTableViewConstants.NORMAL_ROW_HEIGHT;
        }

        //        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        //        {
        //            if (NoMessageThreads ()) {
        //                return NORMAL_ROW_HEIGHT;
        //            }
        //
        //            McEmailMessage message;
        //            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
        //
        //            if (null == messageThread) {
        //                return NORMAL_ROW_HEIGHT;
        //            }
        //
        //            message = GetCachedMessage (indexPath.Row);
        //
        //            return HeightForMessage (message);
        //        }
        //
        //        public override nfloat EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        //        {
        //            return NORMAL_ROW_HEIGHT;
        //        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.CellAt (indexPath);
            if (null != cell) {
                cell.SetSelected (false, true);
            }

            if (NoMessageThreads ()) {
                return;
            }

            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            if (null == messageThread) {
                return;
            }

            if (MultiSelectActive ()) {
                var threadIndex = indexPath.Row;
                if (MultiSelect.Contains (threadIndex)) {
                    MultiSelect.Remove (threadIndex);
                } else {
                    MultiSelect.Add (threadIndex);
                }
                ConfigureMultiSelectCell (cell);
                owner.MultiSelectChange (this, MultiSelect.Count);
            } else if (messageThreads.HasOutboxSemantics () || messageThreads.HasDraftsSemantics ()) {
                // TODO open draft in compose view
            } else {
                owner.MessageThreadSelected (messageThread);
                DumpInfo (messageThread);
            }
        }

        protected const int SWIPE_TAG = 99100;
        protected const int USER_IMAGE_TAG = 99101;
        protected const int USER_LABEL_TAG = 99102;
        protected const int MULTISELECT_IMAGE_TAG = 99103;
        protected const int PREVIEW_TAG = 99104;
        protected const int REMINDER_ICON_TAG = 99105;
        protected const int REMINDER_TEXT_TAG = 99106;
        protected const int MESSAGE_HEADER_TAG = 99107;
        protected const int UNREAD_IMAGE_TAG = 99108;

        [Foundation.Export ("ImageViewTapSelector:")]
        public void ImageViewTapSelector (UIGestureRecognizer sender)
        {

        }

        protected void ConfigureMultiSelectCell (UITableViewCell cell)
        {
            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            if (null != view) {
                view.EnableSwipe (!MultiSelectActive ());
            }

            if (MultiSelectActive ()) {
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
            } else {
                cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            }

            var threadIndex = cell.ContentView.Tag;
            var userCheckmarkView = (UIImageView)cell.ContentView.ViewWithTag (MULTISELECT_IMAGE_TAG);

            if (!multiSelectAllowed || !multiSelectActive) {
                userCheckmarkView.Hidden = true;
                return;
            }
            userCheckmarkView.Hidden = false;
            var iconName = MultiSelect.Contains (threadIndex) ? "gen-checkbox-checked" : "gen-checkbox";
            using (var image = UIImage.FromBundle (iconName)) {
                userCheckmarkView.Image = image;
            }
        }

        /// <summary>
        /// Call when switching in to or out of multi select
        /// to reset cells and adjust nagivation bar buttons.
        /// </summary>
        public void MultiSelectToggle (UITableView tableView)
        {
            if (!NoMessageThreads ()) {
                foreach (var cell in tableView.VisibleCells) {
                    ConfigureMultiSelectCell (cell);
                }
            }
            if (null != owner) {
                owner.MultiSelectToggle (this, multiSelectAllowed && multiSelectActive);
                owner.MultiSelectChange (this, MultiSelect.Count);
            }
        }

        public bool MultiSelectActive ()
        {
            return multiSelectActive;
        }

        public void MultiSelectEnable (UITableView tableView)
        {
            MultiSelect.Clear ();
            multiSelectActive = true;
            MultiSelectToggle (tableView);
        }

        public void MultiSelectCancel (UITableView tableView)
        {
            MultiSelect.Clear ();
            multiSelectActive = false;
            MultiSelectToggle (tableView);
        }

        public void ToggleSwiping (UITableView tableView, SwipeActionView activeView, bool active)
        {
            swipingActive = active;
            tableView.ScrollEnabled = !active;

            if (!NoMessageThreads ()) {
                foreach (var cell in tableView.VisibleCells) {
                    var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
                    if (view != activeView) {
                        cell.UserInteractionEnabled = !active;
                    } else {
                        cell.UserInteractionEnabled = true;
                        if (swipingActive) {
                            cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                        } else {
                            cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
                        }
                    }
                }
            }

        }

        // Don't select row when swiper is active
        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            return (swipingActive ? null : indexPath);
        }

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UITableViewCell CellWithReuseIdentifier (UITableView tableView, string identifier)
        {
            if (identifier.Equals (NoMessagesReuseIdentifier)) {
                var cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                cell.TextLabel.TextAlignment = UITextAlignment.Center;
                cell.TextLabel.TextColor = UIColor.FromRGB (0x0f, 0x42, 0x4c);
                cell.TextLabel.Font = A.Font_AvenirNextDemiBold17;
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                return cell;
            }

            if (identifier.Equals (EmailMessageReuseIdentifier) || identifier.Equals(DraftsMessageReuseIdentifier)) {
                var cell = tableView.DequeueReusableCell (identifier);
                if (null == cell) {
                    cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                }
                if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
                cell.ContentView.BackgroundColor = UIColor.White;

                var cellWidth = tableView.Frame.Width;

                var frame = new CGRect (0, 0, tableView.Frame.Width, MessageTableViewConstants.NORMAL_ROW_HEIGHT);
                var view = new SwipeActionView (frame);
                view.Tag = SWIPE_TAG;

                if (messageThreads.HasOutboxSemantics () || messageThreads.HasDraftsSemantics ()) {
                    view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
                } else {
                    view.SetAction (ARCHIVE_BUTTON, SwipeSide.RIGHT);
                    view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
                    view.SetAction (SAVE_BUTTON, SwipeSide.LEFT);
                    view.SetAction (DEFER_BUTTON, SwipeSide.LEFT);
                }

                cell.ContentView.AddSubview (view);

                // Create subview for a larger touch target for multi-select
                var imageViews = new UIView (new CGRect (0, 0, 60, 70));
                view.AddSubview (imageViews);

                // User image view
                var userImageView = new UIImageView (new CGRect (15, 20, 40, 40));
                userImageView.Layer.CornerRadius = 20;
                userImageView.Layer.MasksToBounds = true;
                userImageView.Tag = USER_IMAGE_TAG;
                imageViews.AddSubview (userImageView);

                // User userLabelView view, if no image
                var userLabelView = new UILabel (new CGRect (15, 20, 40, 40));
                userLabelView.Font = A.Font_AvenirNextRegular24;
                userLabelView.TextColor = UIColor.White;
                userLabelView.TextAlignment = UITextAlignment.Center;
                userLabelView.LineBreakMode = UILineBreakMode.Clip;
                userLabelView.Layer.CornerRadius = 20;
                userLabelView.Layer.MasksToBounds = true;
                userLabelView.Tag = USER_LABEL_TAG;
                imageViews.AddSubview (userLabelView);
                userLabelView.BackgroundColor = UIColor.Yellow;

                // Unread message dot
                var unreadMessageView = new UIImageView (new Rectangle (4, 35, 9, 9));
                using (var image = UIImage.FromBundle ("SlideNav-Btn")) {
                    unreadMessageView.Image = image;
                }
                unreadMessageView.BackgroundColor = UIColor.White;
                unreadMessageView.Tag = UNREAD_IMAGE_TAG;
                imageViews.AddSubview (unreadMessageView);

                // Set up multi-select on checkmark
                var imagesViewTap = new UITapGestureRecognizer ();
                imagesViewTap.NumberOfTapsRequired = 1;
                imagesViewTap.AddTarget (this, new ObjCRuntime.Selector ("ImageViewTapSelector:"));
                imagesViewTap.CancelsTouchesInView = true; // prevents the row from being selected
                imageViews.AddGestureRecognizer (imagesViewTap);

                //Multi select icon
                var multiSelectImageView = new UIImageView ();
                multiSelectImageView.Tag = MULTISELECT_IMAGE_TAG;
                multiSelectImageView.BackgroundColor = UIColor.White;
                multiSelectImageView.Frame = new CGRect (15 + 20 - 8, 82, 16, 16); // Centered
                // multiSelectImageView.Frame = new CGRect (15 + 40 - 16, 80, 16, 16); // Right align with image
                // multiSelectImageView.Frame = new CGRect (15 + 20, 82, 16, 16); // Left align with image center
                multiSelectImageView.Hidden = true;
                view.AddSubview (multiSelectImageView);

                MessageHeaderView messageHeaderView;
                if(identifier.Equals (EmailMessageReuseIdentifier)) {
                    messageHeaderView = new MessageHeaderView (new CGRect (65, 0, cellWidth - 65, 75));
                } else {
                    messageHeaderView = new MessageHeaderView (new CGRect (15, 0, cellWidth - 15, 75));
                }
                messageHeaderView.CreateView ();
                messageHeaderView.Tag = MESSAGE_HEADER_TAG;
                messageHeaderView.SetAllBackgroundColors (UIColor.White);
                view.AddSubview (messageHeaderView);

                // Preview label view
                var previewLabelView = new UILabel (new CGRect (65, 80, cellWidth - 15 - 65, 60));
                previewLabelView.ContentMode = UIViewContentMode.TopLeft;
                previewLabelView.Font = A.Font_AvenirNextRegular14;
                previewLabelView.TextColor = A.Color_NachoDarkText;
                previewLabelView.BackgroundColor = UIColor.White;
                previewLabelView.Lines = 2;
                previewLabelView.Tag = PREVIEW_TAG;
                view.AddSubview (previewLabelView);

                // Reminder image view
                var reminderImageView = new UIImageView (new CGRect (65, 129, 12, 12));
                using (var image = UIImage.FromBundle ("inbox-icn-deadline")) {
                    reminderImageView.Image = image;
                }

                reminderImageView.BackgroundColor = UIColor.White;
                reminderImageView.Tag = REMINDER_ICON_TAG;
                view.AddSubview (reminderImageView);

                // Reminder label view
                var reminderLabelView = new UILabel (new CGRect (87, 125, 230, 20));
                reminderLabelView.Font = A.Font_AvenirNextRegular14;
                reminderLabelView.TextColor = A.Color_9B9B9B;
                reminderLabelView.BackgroundColor = UIColor.White;
                reminderLabelView.Tag = REMINDER_TEXT_TAG;
                view.AddSubview (reminderLabelView);

                return cell;
            }

            return null;
        }

        /// <summary>
        /// Populate cells with data, adjust sizes and visibility.
        /// </summary>
        protected void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            if (cell.ReuseIdentifier.ToString ().Equals (NoMessagesReuseIdentifier)) {
                cell.TextLabel.Text = messageWhenEmpty;
                return;
            }

            if (cell.ReuseIdentifier.ToString ().Equals (EmailMessageReuseIdentifier)) {
                ConfigureMessageCell (tableView, cell, indexPath.Row);
                return;
            }

            if (cell.ReuseIdentifier.ToString ().Equals (DraftsMessageReuseIdentifier)) {
                ConfigureDraftMessageCell (tableView, cell, indexPath.Row);
                return;
            }

            NcAssert.CaseError ();
        }

        protected void ConfigureAsUnavailable (UITableViewCell cell)
        {
            foreach (var view in cell.ContentView.Subviews) {
                view.Hidden = true;
            }
            cell.TextLabel.Hidden = false;
            cell.TextLabel.Text = "Information temporarily unavailable";
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureMessageCell (UITableView tableView, UITableViewCell cell, int messageThreadIndex)
        {
            // Save thread index
            cell.ContentView.Tag = messageThreadIndex;

            McEmailMessage message;
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);

            if (null == messageThread) {
                ConfigureAsUnavailable (cell);
                return;
            }

            message = GetCachedMessage (messageThreadIndex);

            if (null == message) {
                ConfigureAsUnavailable (cell);
                return;
            }

            cell.TextLabel.Text = "";
            cell.ContentView.Hidden = false;

            var cellWidth = tableView.Frame.Width;

            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            view.Frame = new CGRect (0, 0, cellWidth, HeightForMessage (message));
            view.Hidden = false;

            view.OnClick = (int tag) => {
                switch (tag) {
                case SAVE_TAG:
                    ShowFileChooser (messageThread);
                    break;
                case DEFER_TAG:
                    ShowPriorityChooser (messageThread);
                    break;
                case ARCHIVE_TAG:
                    ArchiveThisMessage (messageThread);
                    break;
                case DELETE_TAG:
                    DeleteThisMessage (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };
            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    ToggleSwiping (tableView, activeView, true);
                    cell.Layer.CornerRadius = 0;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    ToggleSwiping (tableView, activeView, false);
                    cell.Layer.CornerRadius = 15;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    ToggleSwiping (tableView, activeView, true);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            // User image view
            var userImageView = cell.ContentView.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            var userLabelView = cell.ContentView.ViewWithTag (USER_LABEL_TAG) as UILabel;
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
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
            }

            var unreadMessageView = (UIImageView)cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = message.IsRead;

            var messageHeaderView = (MessageHeaderView) cell.ContentView.ViewWithTag (MESSAGE_HEADER_TAG);
            messageHeaderView.ConfigureMessageView (messageThread, message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureMessageView (messageThread, message);
            };

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Preview label view
            var previewLabelView = cell.ContentView.ViewWithTag (PREVIEW_TAG) as UILabel;
            previewLabelView.Hidden = false;
            var rawPreview = message.GetBodyPreviewOrEmpty ();
            var cookedPreview = System.Text.RegularExpressions.Regex.Replace (rawPreview, @"\s+", " ");
            using (var text = new NSAttributedString (cookedPreview)) {
                previewLabelView.AttributedText = text;
            }
            previewLabelView.Frame = new CGRect (65, 80, cellWidth - 15 - 65, 60);
            previewLabelView.SizeToFit ();

            // Reminder image view and label
            var reminderImageView = cell.ContentView.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = cell.ContentView.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }
        }

        protected void ConfigureDraftMessageCell (UITableView tableView, UITableViewCell cell, int messageThreadIndex)
        {
            // Save thread index
            cell.ContentView.Tag = messageThreadIndex;

            McEmailMessage message;
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);

            if (null == messageThread) {
                ConfigureAsUnavailable (cell);
                return;
            }

            message = GetCachedMessage (messageThreadIndex);

            if (null == message) {
                ConfigureAsUnavailable (cell);
                return;
            }

            cell.TextLabel.Text = "";
            cell.ContentView.Hidden = false;

            var cellWidth = tableView.Frame.Width;

            var view = cell.ContentView.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            view.Frame = new CGRect (0, 0, cellWidth, HeightForMessage (message));
            view.Hidden = false;

            view.OnClick = (int tag) => {
                switch (tag) {
                case DELETE_TAG:
                    DeleteThisMessage (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };
            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    ToggleSwiping (tableView, activeView, true);
                    cell.Layer.CornerRadius = 0;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    ToggleSwiping (tableView, activeView, false);
                    cell.Layer.CornerRadius = 15;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    ToggleSwiping (tableView, activeView, true);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            // User image view
            var userImageView = cell.ContentView.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            var userLabelView = cell.ContentView.ViewWithTag (USER_LABEL_TAG) as UILabel;
            userImageView.Hidden = true;
            userLabelView.Hidden = true;
           
            var unreadMessageView = (UIImageView)cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = true;

            var messageHeaderView = (MessageHeaderView) cell.ContentView.ViewWithTag (MESSAGE_HEADER_TAG);
            messageHeaderView.ConfigureDraftView (messageThread, message);

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Preview label view
            var previewLabelView = cell.ContentView.ViewWithTag (PREVIEW_TAG) as UILabel;
            previewLabelView.Hidden = false;
            var rawPreview = message.GetBodyPreviewOrEmpty ();
            var cookedPreview = System.Text.RegularExpressions.Regex.Replace (rawPreview, @"\s+", " ");
            using (var text = new NSAttributedString (cookedPreview)) {
                previewLabelView.AttributedText = text;
            }
            previewLabelView.Frame = new CGRect (65, 80, cellWidth - 15 - 65, 60);
            previewLabelView.SizeToFit ();
        }


        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            string cellIdentifier = EmailMessageReuseIdentifier;

            if (NoMessageThreads ()) {
                cellIdentifier = NoMessagesReuseIdentifier;
            } else if (messageThreads.HasDraftsSemantics () || messageThreads.HasOutboxSemantics ()) {
                cellIdentifier = DraftsMessageReuseIdentifier;
            }

            var cell = tableView.DequeueReusableCell (cellIdentifier);
            if (null == cell) {
                cell = CellWithReuseIdentifier (tableView, cellIdentifier);
            }

            if (NoMessageThreads ()) {
                cell.BackgroundColor = A.Color_NachoBackgroundGray;
                cell.ContentView.BackgroundColor = A.Color_NachoBackgroundGray;
                cell.UserInteractionEnabled = false;
            } else {
                cell.Layer.CornerRadius = 15;
                cell.Layer.MasksToBounds = true;
                cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            }

            ConfigureCell (tableView, cell, indexPath);
            return cell;
        }

        public void ReconfigureVisibleCells (UITableView tableView)
        {
            if (null == tableView) {
                return;
            }
            ClearCache ();
            var paths = tableView.IndexPathsForVisibleRows;
            if (null != paths) {
                foreach (var path in paths) {
                    var cell = tableView.CellAt (path);
                    if (null != cell) {
                        ConfigureCell (tableView, cell, path);
                    }
                }
            }
        }

        public void MoveToFolder (UITableView tableView, McFolder folder, object cookie)
        {
            if (MultiSelectActive ()) {
                MultiSelectMove (tableView, folder);
            } else {
                var h = cookie as SegueHolder;
                var messageThread = (McEmailMessageThread)h.value;
                MoveThisMessage (messageThread, folder);
            }
        }

        public void MoveThisMessage (McEmailMessageThread messageThread, McFolder folder)
        {
            NcAssert.NotNull (messageThread);
            NcEmailArchiver.Move (messageThread, folder);
        }

        public void DeleteThisMessage (McEmailMessageThread messageThread)
        {
            if (messageThreads.HasOutboxSemantics ()) {
                EmailHelper.DeleteEmailThreadFromOutbox (messageThread);
                return;
            }
            NcAssert.NotNull (messageThread);
            Log.Debug (Log.LOG_UI, "DeleteThisMessage");
            NcEmailArchiver.Delete (messageThread);
        }

        public void ArchiveThisMessage (McEmailMessageThread messageThread)
        {
            NcAssert.NotNull (messageThread);
            ArchiveCaptureMessage.Start ();
            NcEmailArchiver.Archive (messageThread);
            ArchiveCaptureMessage.Stop ();
        }

        public List<McEmailMessage> GetSelectedMessages ()
        {
            var messageList = new List<McEmailMessage> ();

            foreach (var messageThreadIndex in MultiSelect) {
                var messageThread = messageThreads.GetEmailThread ((int)messageThreadIndex);
                foreach (var message in messageThread) {
                    messageList.Add (message);
                }
            }
            return messageList;
        }

        public void MultiSelectDelete (UITableView tableView)
        {
            var messageList = GetSelectedMessages ();
            foreach (var message in messageList) {
                NcEmailArchiver.Delete (message);
            }
            MultiSelectCancel (tableView);
        }

        public void MultiSelectMove (UITableView tableView, McFolder folder)
        {
            var messageList = GetSelectedMessages ();
            foreach (var message in messageList) {
                NcEmailArchiver.Move (message, folder);
            }
            MultiSelectCancel (tableView);
        }

        public void MultiSelectArchive (UITableView tableView)
        {
            var messageList = GetSelectedMessages ();
            foreach (var message in messageList) {
                NcEmailArchiver.Archive (message);
            }
            MultiSelectCancel (tableView);
        }

        /// <summary>
        /// INachoMessageEditor delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.DismissMessageEditor (true, null);
        }

        /// <summary>
        /// INachoMessageEditor delegate
        /// </summary>
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "MessageTableViewSource: CreateTaskForEmailMessage");
        }

        /// <summary>
        /// INachoMessageEditor delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "MessageTableViewSource: CreateMeetingEmailForMessage");
        }

        /// <summary>
        /// INachoFolderChooserParent delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            NcAssert.True (cookie is SegueHolder);
            var h = cookie as SegueHolder;

            if (MultiSelectActive ()) {
                var t = h.value as UITableView;
                MultiSelectMove (t, folder);
            } else {
                var messageThread = h.value as McEmailMessageThread;
                MoveThisMessage (messageThread, folder);
            }
        }

        /// <summary>
        /// INachoFolderChooserParent delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
        }

        protected void ShowPriorityChooser (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToMessagePriority", new SegueHolder (messageThread));
        }

        protected void ShowFileChooser (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("MessageListToFolders", new SegueHolder (messageThread));
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.HighPriority ("MessageTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                NachoCore.Utils.NcAbate.RegularPriority ("MessageTableViewSource DraggingEnded");
            }
        }

        protected void DumpInfo (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            foreach (var message in messageThread) {
                if (null != message) {
                    Log.Debug (Log.LOG_UI, "message Id={0} bodyId={1} Score={2}", message.Id, message.BodyId, message.Score);
                }
            }
        }
    }
}

