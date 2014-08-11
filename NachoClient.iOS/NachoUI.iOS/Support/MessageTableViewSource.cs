//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class MessageTableViewSource : UITableViewSource, INachoMessageEditorParent, INachoFolderChooserParent
    {
        INachoEmailMessages messageThreads;
        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";
        protected HashSet<int> MultiSelect = null;
        protected bool allowMultiSelect;
        protected bool compactMode;
        public IMessageTableViewSourceDelegate owner;

        protected NcCapture ArchiveCaptureMessage;
        protected NcCapture RefreshCapture;
        private string ArchiveMessageCaptureName;
        private string RefreshCaptureName;


        // Short-term cache from GetHeight to GetCell
        private Dictionary<int, McEmailMessage> messageCache;

        public MessageTableViewSource ()
        {
            owner = null;
            allowMultiSelect = true;
            MultiSelect = new HashSet<int> ();
            ArchiveMessageCaptureName = "MessageTableViewSource.ArchiveMessage";
            NcCapture.AddKind (ArchiveMessageCaptureName);
            ArchiveCaptureMessage = NcCapture.Create (ArchiveMessageCaptureName);
            RefreshCaptureName = "MessageTableViewSource.Refresh";
            NcCapture.AddKind (RefreshCaptureName);
            RefreshCapture = NcCapture.Create (RefreshCaptureName);
            messageCache = new Dictionary<int, McEmailMessage> ();
        }

        public void SetCompactMode (bool compactMode)
        {
            this.compactMode = compactMode;
        }

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageThreads = messageThreads;
        }

        public string GetDisplayName ()
        {
            return messageThreads.DisplayName ();
        }

        public McEmailMessageThread GetFirstThread ()
        {
            if (null == this.messageThreads) {
                return null;
            }
            if (0 == this.messageThreads.Count ()) {
                return null;
            }
            return this.messageThreads.GetEmailThread (0);
        }

        public void RefreshEmailMessages ()
        {
            RefreshCapture.Start ();
            messageCache.Clear ();
            messageThreads.Refresh ();
            RefreshCapture.Stop ();
        }

        protected bool NoMessageThreads ()
        {
            return ((null == messageThreads) || (0 == messageThreads.Count ()));
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            if (NoMessageThreads ()) {
                return 1; // "No messages"
            } else {
                return messageThreads.Count ();
            }
        }

        const float COMPACT_ROW_HEIGHT = 69.0f;
        const float NORMAL_ROW_HEIGHT = 116.0f;
        const float DATED_ROW_HEIGHT = 141.0f;

        protected float HeightForMessage (McEmailMessage message)
        {
            if (compactMode) {
                return COMPACT_ROW_HEIGHT;
            }
            if (message.IsDeferred () || message.HasDueDate ()) {
                return DATED_ROW_HEIGHT;
            }
            return NORMAL_ROW_HEIGHT;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoMessageThreads ()) {
                return COMPACT_ROW_HEIGHT;
            }
            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            var message = messageThread.SingleMessageSpecialCase ();
            messageCache [indexPath.Row] = message;
            return HeightForMessage (message);
        }

        public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        {
            return NORMAL_ROW_HEIGHT;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoMessageThreads ()) {
                return;
            }
            if (MultiSelectActive ()) {
                return;
            }
            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            owner.MessageThreadSelected (messageThread);
        }

        protected const int USER_IMAGE_TAG = 99101;
        protected const int USER_LABEL_TAG = 99102;
        protected const int USER_CHILI_TAG = 99103;
        protected const int USER_CHECKMARK_TAG = 99104;
        protected const int FROM_TAG = 99105;
        protected const int SUBJECT_TAG = 99106;
        protected const int PREVIEW_TAG = 99107;
        protected const int REMINDER_ICON_TAG = 99108;
        protected const int REMINDER_TEXT_TAG = 99109;
        protected const int ATTACHMENT_TAG = 99110;
        protected const int RECEIVED_DATE_TAG = 99111;

        [MonoTouch.Foundation.Export ("MultiSelectTapSelector:")]
        public void MultiSelectTapSelector (UIGestureRecognizer sender)
        {
            var imageView = sender.View;
            var contentView = imageView.Superview;
            var threadIndex = contentView.Tag;

            if (MultiSelect.Contains (threadIndex)) {
                MultiSelect.Remove (threadIndex);
            } else {
                MultiSelect.Add (threadIndex);
            }
            // Skip the intermediate scroll view
            var cell = FindEnclosingTableViewCell (contentView);
            ConfigureMultiSelectCell (cell);

            // Did we just transition to or from multi-select?
            if (1 >= MultiSelect.Count) {
                var tableView = FindEnclosingTableView (cell);
                MultiSelectToggle (tableView);
            }
        }

        protected void ConfigureMultiSelectCell (UITableViewCell cell)
        {
            var threadIndex = cell.ContentView.Tag;
            var userCheckmarkView = cell.ContentView.ViewWithTag (USER_CHECKMARK_TAG) as UIImageView;

            if (false == allowMultiSelect) {
                userCheckmarkView.Hidden = true;
                return;
            }

            userCheckmarkView.Hidden = false;
            if (MultiSelect.Contains (threadIndex)) {
                userCheckmarkView.Image = UIImage.FromBundle ("inbox-multi-select-active");
            } else {
                userCheckmarkView.Image = UIImage.FromBundle ("inbox-multi-select-default");
            }
        }

        /// <summary>
        /// Disable swipes during multi-select
        /// </summary>
        protected void ConfigureMultiSelectSwipe (MCSwipeTableViewCell cell)
        {
            if (0 == MultiSelect.Count) {
                cell.ModeForState1 = MCSwipeTableViewCellMode.Switch;
                cell.ModeForState2 = MCSwipeTableViewCellMode.Switch;
                cell.ModeForState3 = MCSwipeTableViewCellMode.Switch;
                cell.ModeForState4 = MCSwipeTableViewCellMode.Switch;
            } else {
                cell.ModeForState1 = MCSwipeTableViewCellMode.None;
                cell.ModeForState2 = MCSwipeTableViewCellMode.None;
                cell.ModeForState3 = MCSwipeTableViewCellMode.None;
                cell.ModeForState4 = MCSwipeTableViewCellMode.None;
            }
        }

        /// <summary>
        /// Call when switching in to or out of multi select
        /// to reset cells and adjust nagivation bar buttons.
        /// </summary>
        protected void MultiSelectToggle (UITableView tableView)
        {
            if (!NoMessageThreads ()) {
                foreach (var cell in tableView.VisibleCells) {
                    ConfigureMultiSelectCell (cell);
                    ConfigureMultiSelectSwipe (cell as MCSwipeTableViewCell);
                }
            }
            if (null != owner) {
                owner.MultiSelectToggle (this, allowMultiSelect && (0 != MultiSelect.Count));
            }
        }

        protected bool MultiSelectActive ()
        {
            return (MultiSelect.Count > 0);
        }

        public void MultiSelectCancel (UITableView tableView)
        {
            MultiSelect.Clear ();
            MultiSelectToggle (tableView);
        }

        public void MultiSelectEnable (UITableView tableView, bool enabled)
        {
            if (allowMultiSelect == enabled) {
                return; // no change
            }
            allowMultiSelect = enabled;
            MultiSelectCancel (tableView);
        }

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UITableViewCell CellWithReuseIdentifier (UITableView tableView, string identifier)
        {
            if (identifier.Equals (UICellReuseIdentifier)) {
                var cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                cell.TextLabel.TextAlignment = UITextAlignment.Center;
                cell.TextLabel.TextColor = UIColor.FromRGB (0x0f, 0x42, 0x4c);
                cell.TextLabel.Font = A.Font_AvenirNextDemiBold17;
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                return cell;
            }

            if (identifier.Equals (EmailMessageReuseIdentifier)) {
                var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Default, identifier);
                if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                cell.DefaultColor = UIColor.White;

                var cellWidth = tableView.Frame.Width;

                // User image view
                var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
                userImageView.Layer.CornerRadius = 20;
                userImageView.Layer.MasksToBounds = true;
                userImageView.Tag = USER_IMAGE_TAG;
                cell.ContentView.AddSubview (userImageView);

                // Set up multi-select on user image
                var userImageTap = new UITapGestureRecognizer ();
                userImageTap.NumberOfTapsRequired = 1;
                userImageTap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("MultiSelectTapSelector:"));
                userImageTap.CancelsTouchesInView = false;
                userImageView.AddGestureRecognizer (userImageTap);
                userImageView.UserInteractionEnabled = true;

                // User userLabelView view, if no image
                var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
                userLabelView.Font = A.Font_AvenirNextRegular24;
                userLabelView.TextColor = UIColor.White;
                userLabelView.TextAlignment = UITextAlignment.Center;
                userLabelView.LineBreakMode = UILineBreakMode.Clip;
                userLabelView.Layer.CornerRadius = 20;
                userLabelView.Layer.MasksToBounds = true;
                userLabelView.Tag = USER_LABEL_TAG;
                cell.ContentView.AddSubview (userLabelView);

                // Set up multi-select on user label
                var userLabelTap = new UITapGestureRecognizer ();
                userLabelTap.NumberOfTapsRequired = 1;
                userLabelTap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("MultiSelectTapSelector:"));
                userLabelTap.CancelsTouchesInView = false;
                userLabelView.AddGestureRecognizer (userLabelTap);
                userLabelView.UserInteractionEnabled = true;


                // User chili view
                var chiliY = 72;
                var userChiliView = new UIImageView (new RectangleF (23, chiliY, 24, 24));
                userChiliView.Image = UIImage.FromBundle ("inbox-icn-chilli");
                userChiliView.Tag = USER_CHILI_TAG;
                cell.ContentView.AddSubview (userChiliView);

                // Multi-select checkmark overlay
                // Images are already cropped & transparent
                // TODO: Confirm 'y' of 38
                var userCheckmarkView = new UIImageView (new RectangleF (9, 45, 20, 20));
                userCheckmarkView.Tag = USER_CHECKMARK_TAG;
                cell.ContentView.AddSubview (userCheckmarkView);

                // Set up multi-select on checkmark
                var userCheckmarkTap = new UITapGestureRecognizer ();
                userCheckmarkTap.NumberOfTapsRequired = 1;
                userCheckmarkTap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("MultiSelectTapSelector:"));
                userCheckmarkTap.CancelsTouchesInView = false;
                userCheckmarkView.AddGestureRecognizer (userCheckmarkTap);
                userCheckmarkView.UserInteractionEnabled = true;

                // From label view
                // Font will vary bold or regular, depending on isRead.
                // Size fields will be recalculated after text is known.
                var fromLabelView = new UILabel (new RectangleF (65, 20, 150, 20));
                fromLabelView.Font = A.Font_AvenirNextDemiBold17;
                fromLabelView.TextColor = A.Color_0F424C;
                fromLabelView.Tag = FROM_TAG;
                cell.ContentView.AddSubview (fromLabelView);

                // Subject label view
                // Size fields will be recalculated after text is known.
                // TODO: Confirm 'y' of Subject
                var subjectLabelView = new UILabel (new RectangleF (65, 40, cellWidth - 15 - 65, 20));
                subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
                subjectLabelView.Font = A.Font_AvenirNextMedium14;
                subjectLabelView.TextColor = A.Color_0F424C;
                subjectLabelView.Tag = SUBJECT_TAG;
                cell.ContentView.AddSubview (subjectLabelView);

                // Summary label view
                // Size fields will be recalculated after text is known
                var previewLabelView = new UILabel (new RectangleF (65, 60, cellWidth - 15 - 65, 60));
                previewLabelView.ContentMode = UIViewContentMode.TopLeft;
                previewLabelView.Font = A.Font_AvenirNextRegular14;
                previewLabelView.TextColor = A.Color_999999;
                previewLabelView.Lines = 2;
                previewLabelView.Tag = PREVIEW_TAG;
                cell.ContentView.AddSubview (previewLabelView);

                // Reminder image view
                var reminderImageView = new UIImageView (new RectangleF (65, 119, 12, 12));
                reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
                reminderImageView.Tag = REMINDER_ICON_TAG;
                cell.ContentView.AddSubview (reminderImageView);

                // Reminder label view

                var reminderLabelView = new UILabel (new RectangleF (87, 115, 230, 20));
                reminderLabelView.Font = A.Font_AvenirNextRegular14;
                reminderLabelView.TextColor = A.Color_9B9B9B;
                reminderLabelView.Tag = REMINDER_TEXT_TAG;
                cell.ContentView.AddSubview (reminderLabelView);

                // Attachment image view
                // Attachment 'x' will be adjusted to be left of date received field
                var attachmentImageView = new UIImageView (new RectangleF (200, 18, 16, 16));
                attachmentImageView.Image = UIImage.FromBundle ("inbox-icn-attachment");
                attachmentImageView.Tag = ATTACHMENT_TAG;
                cell.ContentView.AddSubview (attachmentImageView);

                // Received label view
                var receivedLabelView = new UILabel (new RectangleF (220, 18, 100, 20));
                receivedLabelView.Font = A.Font_AvenirNextRegular14;
                receivedLabelView.TextColor = A.Color_9B9B9B;
                receivedLabelView.TextAlignment = UITextAlignment.Right;
                receivedLabelView.Tag = RECEIVED_DATE_TAG;
                cell.ContentView.AddSubview (receivedLabelView);

                return cell;
            }

            return null;
        }

        /// <summary>
        /// Populate cells with data, adjust sizes and visibility.
        /// </summary>
        protected void ConfigureCell (UITableViewCell cell, NSIndexPath indexPath)
        {
            if (cell.ReuseIdentifier.Equals (UICellReuseIdentifier)) {
                cell.TextLabel.Text = "No messages";
                return;
            }

            if (cell.ReuseIdentifier.Equals (EmailMessageReuseIdentifier)) {
                ConfigureMessageCell (cell, indexPath.Row);
                return;
            }
            NcAssert.CaseError ();
        }

        protected UITableView FindEnclosingTableView (UIView view)
        {
            while (null != view) {
                if (view is UITableView) {
                    return (view as UITableView);
                }
                view = view.Superview;
            }
            return null;
        }

        protected UITableViewCell FindEnclosingTableViewCell (UIView view)
        {
            while (null != view) {
                if (view is UITableViewCell) {
                    return (view as UITableViewCell);
                }
                view = view.Superview;
            }
            return null;
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureMessageCell (UITableViewCell cell, int messageThreadIndex)
        {
            // Save thread index
            cell.ContentView.Tag = messageThreadIndex;

            McEmailMessage message;
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);

            if (messageCache.TryGetValue (messageThreadIndex, out message)) {
                messageCache.Remove (messageThreadIndex);
            } else {
                message = messageThread.SingleMessageSpecialCase ();
            }

            var cellWidth = cell.Frame.Width;
            if (compactMode) {
                cellWidth -= 30;
            }

            // User image view
            var userImageView = cell.ContentView.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            var userLabelView = cell.ContentView.ViewWithTag (USER_LABEL_TAG) as UILabel;
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

            // User chili view
            var userChiliView = cell.ContentView.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            userChiliView.Hidden = (compactMode || (!message.isHot ()));

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Subject label view
            var subjectLabelView = cell.ContentView.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);

            // Preview label view
            var previewLabelView = cell.ContentView.ViewWithTag (PREVIEW_TAG) as UILabel;
            previewLabelView.Hidden = compactMode;
            if (!compactMode) {
                previewLabelView.Frame = new RectangleF (65, 60, cellWidth - 15 - 65, 60);
                var rawPreview = message.GetBodyPreviewOrEmpty ();
                var cookedPreview = System.Text.RegularExpressions.Regex.Replace (rawPreview, @"\s+", " ");
                previewLabelView.AttributedText = new NSAttributedString (cookedPreview);
                ;
            }

            // Reminder image view and label
            var reminderImageView = cell.ContentView.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = cell.ContentView.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if ((!compactMode) && (message.HasDueDate () || message.IsDeferred ())) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                if (message.IsDeferred ()) {
                    reminderLabelView.Text = String.Format ("Message hidden until {0}", message.FlagDeferUntil);
                } else if (message.IsOverdue ()) {
                    reminderLabelView.Text = String.Format ("Response was due {0}", message.FlagDueAsUtc ());
                } else {
                    reminderLabelView.Text = String.Format ("Response is due {0}", message.FlagDueAsUtc ());
                }
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            // Received label view
            var receivedLabelView = cell.ContentView.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.CompactDateString (message.DateReceived);
            receivedLabelView.SizeToFit ();
            var receivedLabelRect = receivedLabelView.Frame;
            receivedLabelRect.X = cellWidth - 15 - receivedLabelRect.Width;
            receivedLabelRect.Height = 20;
            receivedLabelView.Frame = receivedLabelRect;

            // Attachment image view
            var attachmentImageView = cell.ContentView.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            attachmentImageView.Hidden = !message.cachedHasAttachments;
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelRect.X - 10 - 16;
            attachmentImageView.Frame = attachmentImageRect;

            // From label view
            var fromLabelView = cell.ContentView.ViewWithTag (FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = attachmentImageRect.X - 65;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextRegular17 : A.Font_AvenirNextDemiBold17);

            ConfigureSwipes (cell as MCSwipeTableViewCell, messageThread);
            ConfigureMultiSelectSwipe (cell as MCSwipeTableViewCell);
        }

        /// <summary>
        /// Configures the swipes.
        /// </summary>
        void ConfigureSwipes (MCSwipeTableViewCell cell, McEmailMessageThread messageThread)
        {
            cell.FirstTrigger = 0.10f;
            cell.SecondTrigger = 0.70f;

            UIView checkView = null;
            UIColor greenColor = null;
            UIView crossView = null;
            UIColor redColor = null;
            UIView clockView = null;
            UIColor yellowColor = null;
            UIView listView = null;
            UIColor brownColor = null;

            try { 
                listView = ViewWithImageName ("list");
                brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ShowFileChooser (messageThread);
                    return;
                });
                clockView = ViewWithImageName ("clock");
                yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ShowPriorityChooser (messageThread);
                    return;
                });

                checkView = ViewWithImageName ("check");
                greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (checkView, greenColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ArchiveThisMessage (messageThread);
                });
                crossView = ViewWithImageName ("cross");
                redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    DeleteThisMessage (messageThread);
                });
            } finally {
                if (null != checkView) {
                    checkView.Dispose ();
                }
                if (null != greenColor) {
                    greenColor.Dispose ();
                }
                if (null != crossView) {
                    crossView.Dispose ();
                }
                if (null != redColor) {
                    redColor.Dispose ();
                }
                if (null != clockView) {
                    clockView.Dispose ();
                }
                if (null != yellowColor) {
                    yellowColor.Dispose ();
                }
                if (null != listView) {
                    listView.Dispose ();
                }
                if (null != brownColor) {
                    brownColor.Dispose ();
                }
            }
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            string cellIdentifier = (NoMessageThreads () ? UICellReuseIdentifier : EmailMessageReuseIdentifier);

            var cell = tableView.DequeueReusableCell (cellIdentifier);
            if (null == cell) {
                cell = CellWithReuseIdentifier (tableView, cellIdentifier);
            }
            ConfigureCell (cell, indexPath);
            return cell;

        }

        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
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
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Move (message, folder);
        }

        public void DeleteThisMessage (McEmailMessageThread messageThread)
        {
            Log.Debug (Log.LOG_UI, "DeleteThisMessage");
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (message);
        }

        public void ArchiveThisMessage (McEmailMessageThread messageThread)
        {
            ArchiveCaptureMessage.Start ();
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (message);
            ArchiveCaptureMessage.Stop ();
        }

        public List<McEmailMessage> GetSelectedMessages ()
        {
            var messageList = new List<McEmailMessage> ();

            foreach (var messageThreadIndex in MultiSelect) {
                var messageThread = messageThreads.GetEmailThread (messageThreadIndex);
                var message = messageThread.SingleMessageSpecialCase ();
                messageList.Add (message);
            }
            return messageList;
        }

        public void MultiSelectDelete (UITableView tableView)
        {
            var messageList = GetSelectedMessages ();
            foreach (var message in messageList) {
                NcEmailArchiver.Delete (message);
            }
            MultiSelect.Clear ();
            MultiSelectToggle (tableView);
        }

        public void MultiSelectMove (UITableView tableView, McFolder folder)
        {
            var messageList = GetSelectedMessages ();
            foreach (var message in messageList) {
                NcEmailArchiver.Move (message, folder);
            }
            MultiSelect.Clear ();
            MultiSelectToggle (tableView);
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
            owner.PerformSegueForDelegate ("NachoNowToMessagePriority", new SegueHolder (messageThread));
        }

        protected void ShowFileChooser (McEmailMessageThread messageThread)
        {
            owner.PerformSegueForDelegate ("NachoNowToMessageAction", new SegueHolder (messageThread));
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoClient.Util.HighPriority ();
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoClient.Util.RegularPriority ();
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                NachoClient.Util.RegularPriority ();
            }
        }

    }
}

