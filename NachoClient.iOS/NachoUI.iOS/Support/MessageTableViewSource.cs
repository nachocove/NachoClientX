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
using NachoCore.Brain;
using System.Linq;

namespace NachoClient.iOS
{
    public class MessageTableViewSource : UITableViewSource, IMessageTableViewSource, INachoFolderChooserParent
    {
        bool scrolling;
        string messageWhenEmpty;
        INachoEmailMessages messageThreads;
        protected const string NoMessagesReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";
        protected const string DraftsMessageReuseIdentifier = "DraftsMessage";
        protected HashSet<nint> MultiSelect = null;
        protected Dictionary<int, int> MultiSelectAccounts = null;
        protected bool multiSelectAllowed;
        protected bool multiSelectActive;
        public IMessageTableViewSourceDelegate owner;

        protected NcCapture ArchiveCaptureMessage;
        protected NcCapture RefreshCapture;
        private string ArchiveMessageCaptureName;
        private string RefreshCaptureName;

        private UIView headerWrapper;
        private UILabel headerText;

        IDisposable abatementRequest = null;

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
        private static SwipeActionDescriptor SOLO_DELETE_BUTTON =
            new SwipeActionDescriptor (DELETE_TAG, 0.50f, UIImage.FromBundle (A.File_NachoSwipeEmailDelete),
                "Delete", A.Color_NachoSwipeEmailDelete);


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
            // Get portraits
            var fromAddressIdList = new List<int> ();
            foreach (var message in cache[cacheIndex]) {
                if (null != message) {
                    if ((0 != message.FromEmailAddressId) && !fromAddressIdList.Contains (message.FromEmailAddressId)) {
                        fromAddressIdList.Add (message.FromEmailAddressId);
                    }
                }
            }
            // Assign matching portrait ids to email messages
            var portraitIndexList = McContact.QueryForPortraits (fromAddressIdList);
            foreach (var portraitIndex in portraitIndexList) {
                foreach (var message in cache[cacheIndex]) {
                    if (null != message) {
                        if (portraitIndex.EmailAddress == message.FromEmailAddressId) {
                            message.cachedPortraitId = portraitIndex.PortraitId;
                        }
                    }
                }
            }
        }

        protected bool MaybeUpdateMessageInCache (int id)
        {
            foreach (var c in cache) {
                if (null == c) {
                    continue;
                }
                for (int i = 0; i < c.Count; i++) {
                    var m = c [i];
                    if (null != m) {
                        if (m.Id == id) {
                            c [i] = McEmailMessage.QueryById<McEmailMessage> (id);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public MessageTableViewSource (IMessageTableViewSourceDelegate owner)
        {
            this.owner = owner;
            multiSelectAllowed = true;
            MultiSelect = new HashSet<nint> ();
            MultiSelectAccounts = new Dictionary<int, int> ();
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

        public bool RefreshEmailMessages (out List<int> adds, out List<int> deletes)
        {
            RefreshCapture.Start ();
            ClearCache ();
            var didRefresh = messageThreads.Refresh (out adds, out deletes);
            RefreshCapture.Stop ();
            if (null != headerText && messageThreads.HasFilterSemantics ()) {
                headerText.Text = Folder_Helpers.FilterString (messageThreads.FilterSetting);
            }
            return didRefresh;
        }

        public void BackgroundRefreshEmailMessages (NachoMessagesRefreshCompletionDelegate completionAction)
        {
            if (!messageThreads.HasBackgroundRefresh ()) {
                List<int> adds;
                List<int> deletes;
                bool changed = RefreshEmailMessages (out adds, out deletes);
                if (null != completionAction) {
                    completionAction (changed, adds, deletes);
                }
                return;
            }
            ClearCache ();
            messageThreads.BackgroundRefresh ((changed, adds, deletes) => {
                if (null != headerText && messageThreads.HasFilterSemantics ()) {
                    headerText.Text = Folder_Helpers.FilterString (messageThreads.FilterSetting);
                }
                if (null != completionAction) {
                    completionAction (changed, adds, deletes);
                }
            });
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

        public override nfloat EstimatedHeightForHeader (UITableView tableView, nint section)
        {
            return messageThreads.HasFilterSemantics () ? 24 : 0;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            return EstimatedHeightForHeader (tableView, section);
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (!messageThreads.HasFilterSemantics ()) {
                return null;
            }

            if (null == headerWrapper) {
                headerWrapper = new UIView (new CGRect (0, 0, tableView.Frame.Width, 24));
                headerWrapper.BackgroundColor = A.Color_NachoBackgroundGray;

                var headerIcon = new UIImageView (new CGRect (30, 0, 24, 24));
                headerIcon.Image = UIImage.FromBundle ("gen-read-list");
                headerWrapper.AddSubview (headerIcon);

                headerText = new UILabel (new CGRect (65, 0, tableView.Frame.Width - 65, 24));
                headerWrapper.AddSubview (headerText);
                headerText.BackgroundColor = A.Color_NachoBackgroundGray;
                headerText.AccessibilityLabel = "MessageListFilterSetting";
                headerText.Font = A.Font_AvenirNextDemiBold14;
            }

            headerText.Text = Folder_Helpers.FilterString (messageThreads.FilterSetting);

            return headerWrapper;
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
                    UpdateMultiSelectAccounts (messageThread, -1);
                } else {
                    MultiSelect.Add (threadIndex);
                    UpdateMultiSelectAccounts (messageThread, 1);
                }
                ConfigureMultiSelectCell (cell);
                owner.MultiSelectChange (this, MultiSelect.Count, 1 < MultiSelectAccounts.Count);
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
        protected const int MESSAGE_ERROR_TAG = 99109;

        [Foundation.Export ("ImageViewTapSelector:")]
        public void ImageViewTapSelector (UIGestureRecognizer sender)
        {
            

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
                    var message = GetCachedMessage (indexPath.Row);
                    EmailHelper.ToggleRead (message);
                    tableView.ReloadRows (new NSIndexPath[]{ indexPath }, UITableViewRowAnimation.None);
                }
            }
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
                owner.MultiSelectChange (this, MultiSelect.Count, 1 < MultiSelectAccounts.Count);
            }
        }

        public bool MultiSelectActive ()
        {
            return multiSelectActive;
        }

        public void MultiSelectEnable (UITableView tableView)
        {
            MultiSelect.Clear ();
            MultiSelectAccounts.Clear ();
            multiSelectActive = true;
            MultiSelectToggle (tableView);
        }

        public void MultiSelectCancel (UITableView tableView)
        {
            MultiSelect.Clear ();
            MultiSelectAccounts.Clear ();
            multiSelectActive = false;
            MultiSelectToggle (tableView);
        }

        void UpdateMultiSelectAccounts (McEmailMessageThread messageThread, int delta)
        {
            var message = messageThread.FirstMessage ();
            if (null == message) {
                return;
            }
            int value;
            if (MultiSelectAccounts.TryGetValue (message.AccountId, out value)) {
                value += delta;
                if (0 == value) {
                    MultiSelectAccounts.Remove (message.AccountId);
                } else {
                    MultiSelectAccounts [message.AccountId] = value;
                }
            } else {
                NcAssert.True (1 == delta);
                MultiSelectAccounts.Add (message.AccountId, delta);
            }
        }

        public int MultiSelectAccount (UITableView tableView)
        {
            NcAssert.True (1 == MultiSelectAccounts.Count);
            return MultiSelectAccounts.Keys.First<int> ();
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

            if (identifier.Equals (EmailMessageReuseIdentifier) || identifier.Equals (DraftsMessageReuseIdentifier)) {
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
                    view.SetAction (SOLO_DELETE_BUTTON, SwipeSide.RIGHT);
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
                view.AddSubview (userImageView);

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
                var unreadMessageView = new UnreadMessageIndicator (new Rectangle (15, 60, 40, 27));
                unreadMessageView.BackgroundColor = UIColor.White;
                unreadMessageView.Tag = UNREAD_IMAGE_TAG;
                unreadMessageView.UserInteractionEnabled = true;
                view.AddSubview (unreadMessageView);
                var unreadTap = new UITapGestureRecognizer ();
                unreadTap.CancelsTouchesInView = true;
                unreadTap.AddTarget (this, new ObjCRuntime.Selector ("UnreadViewTapped:"));
                unreadMessageView.AddGestureRecognizer (unreadTap);

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
                if (identifier.Equals (EmailMessageReuseIdentifier)) {
                    messageHeaderView = new MessageHeaderView (new CGRect (65, 0, cellWidth - 65, 75));
                } else {
                    messageHeaderView = new MessageHeaderView (new CGRect (45, 0, cellWidth - 45, 75));
                    using (var image = UIImage.FromBundle ("Slide1-5")) {
                        var errorImageView = new UIImageView (image);
                        errorImageView.Frame = new CGRect (15, 0, 24, 24);
                        errorImageView.Image = image;
                        errorImageView.Tag = MESSAGE_ERROR_TAG;
                        view.AddSubview (errorImageView);
                    }
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

            NcTask.Run (() => {
                NcBrain.MessageNotificationStatusUpdated (message, DateTime.UtcNow, 60);
            }, "MessageNotificationStatusUpdated");

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
                    tableView.ScrollEnabled = false;
                    cell.Layer.CornerRadius = 0;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    cell.Layer.CornerRadius = 15;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            // User image view
            var userImageView = (UIImageView)cell.ContentView.ViewWithTag (USER_IMAGE_TAG);
            var userLabelView = (UILabel)cell.ContentView.ViewWithTag (USER_LABEL_TAG);
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

            var unreadMessageView = (UnreadMessageIndicator)cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = false;
            unreadMessageView.State = message.IsRead ? UnreadMessageIndicator.MessageState.Read : UnreadMessageIndicator.MessageState.Unread;
            unreadMessageView.Color = Util.ColorForAccount (message.AccountId);

            var messageHeaderView = (MessageHeaderView)cell.ContentView.ViewWithTag (MESSAGE_HEADER_TAG);
            messageHeaderView.ConfigureMessageView (messageThread, message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                // Set the value for redraw; status ind will show up soon for permanent action
                message.UserAction = NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureMessageView (messageThread, message);
            };

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Preview label view
            var previewLabelView = (UILabel)cell.ContentView.ViewWithTag (PREVIEW_TAG);
            previewLabelView.Hidden = false;
            var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
            using (var text = new NSAttributedString (cookedPreview)) {
                previewLabelView.AttributedText = text;
            }
            previewLabelView.Frame = new CGRect (65, 80, cellWidth - 15 - 65, 60);
            previewLabelView.SizeToFit ();

            // Reminder image view and label
            var reminderImageView = (UIImageView)cell.ContentView.ViewWithTag (REMINDER_ICON_TAG);
            var reminderLabelView = (UILabel)cell.ContentView.ViewWithTag (REMINDER_TEXT_TAG);
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }
            // Since there is a decent chance that the user will open this message, ask the backend to fetch it
            // download its body.
            if (0 == message.BodyId) {
                NcTask.Run (() => {
                    BackEnd.Instance.SendEmailBodyFetchHint (message.AccountId, message.Id);
                }, "MessageTableViewSource.SendEmailBodyFetchHint");
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
            view.BackgroundColor = UIColor.White;
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
                    tableView.ScrollEnabled = false;
                    cell.Layer.CornerRadius = 0;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    cell.Layer.CornerRadius = 15;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            // User image view
            var userImageView = (UIImageView)cell.ContentView.ViewWithTag (USER_IMAGE_TAG);
            var userLabelView = (UILabel)cell.ContentView.ViewWithTag (USER_LABEL_TAG);
            userImageView.Hidden = true;
            userLabelView.Hidden = true;
           
            var unreadMessageView = cell.ContentView.ViewWithTag (UNREAD_IMAGE_TAG);
            unreadMessageView.Hidden = true;

            var messageHeaderView = (MessageHeaderView)cell.ContentView.ViewWithTag (MESSAGE_HEADER_TAG);
            messageHeaderView.ConfigureDraftView (messageThread, message);

            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            var errorImageView = (UIImageView)cell.ContentView.ViewWithTag (MESSAGE_ERROR_TAG);
            errorImageView.Hidden = (null == pending) || (NcResult.KindEnum.Error != pending.ResultKind);
            ViewFramer.Create (errorImageView).CenterY (0, HeightForMessage (message));

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Preview label view
            var previewLabelView = (UILabel)cell.ContentView.ViewWithTag (PREVIEW_TAG);
            previewLabelView.Hidden = false;
            var rawPreview = message.BodyPreview ?? "";
            var cookedPreview = System.Text.RegularExpressions.Regex.Replace (rawPreview, @"\s+", " ");
            using (var text = new NSAttributedString (cookedPreview)) {
                previewLabelView.AttributedText = text;
            }
            previewLabelView.Frame = new CGRect (45, 80, cellWidth - 15 - 45, 60);
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
            if (null != headerText && null != messageThreads && messageThreads.HasFilterSemantics ()) {
                headerText.Text = Folder_Helpers.FilterString (messageThreads.FilterSetting);
            }
        }

        public void EmailMessageChanged (UITableView tableView, int id)
        {
            if (MaybeUpdateMessageInCache (id)) {
                if (!scrolling) {
                    ReconfigureVisibleCells (tableView);
                }
            }
        }

        public void MoveToFolder (UITableView tableView, McFolder folder, object cookie)
        {
            if (MultiSelectActive ()) {
                MultiSelectMove (tableView, folder);
            } else {
                var messageThread = (McEmailMessageThread)cookie;
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
            if (messageThreads.HasDraftsSemantics ()) {
                EmailHelper.DeleteEmailThreadFromDrafts (messageThread);
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
            NcEmailArchiver.Delete (messageList);
            MultiSelectCancel (tableView);
        }

        public void MultiSelectMove (UITableView tableView, McFolder folder)
        {
            NcAssert.True (1 == MultiSelectAccounts.Count);
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Move (messageList, folder);
            MultiSelectCancel (tableView);
        }

        public void MultiSelectArchive (UITableView tableView)
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Archive (messageList);
            MultiSelectCancel (tableView);
        }

        /// <summary>
        /// INachoFolderChooserParent delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            if (MultiSelectActive ()) {
                var t = cookie as UITableView;
                MultiSelectMove (t, folder);
            } else {
                var messageThread = cookie as McEmailMessageThread;
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
            owner.DeferThread (messageThread);
        }

        protected void ShowFileChooser (McEmailMessageThread messageThread)
        {
            if (null == messageThread) {
                return;
            }
            owner.MoveThread (messageThread);
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            scrolling = true;
            if (null == abatementRequest) {
                abatementRequest = NcAbate.UITimedAbatement (TimeSpan.FromSeconds (10));
            }
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            scrolling = false;
            if (null != abatementRequest) {
                abatementRequest.Dispose ();
                abatementRequest = null;
            }
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            scrolling = false;
            if (!willDecelerate && null != abatementRequest) {
                abatementRequest.Dispose ();
                abatementRequest = null;
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

