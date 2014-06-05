// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;

//using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using MCSwipeTableViewCellBinding;
using SWRevealViewControllerBinding;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUITableViewDelegate, IUISearchDisplayDelegate, IUISearchBarDelegate, IUIScrollViewDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent
    {
        INachoEmailMessages messageThreads;
        // iOS Bug Workaround
        // The cancel button on the search bar breaks
        // if the searchbar is hidden by a scrolled tableview.
        PointF savedContentOffset;
        private static Object StaticLockObj = new Object ();
        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected HashSet<int> MultiSelect = null;

        public void SetEmailMessages (INachoEmailMessages l)
        {
            messageThreads = l;
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {


            MultiSelect = new HashSet<int> ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            MultiSelectToggle ();

            using (var nachoImage = UIImage.FromBundle ("navbar-icn-inbox-active")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            using (var revealImage = UIImage.FromBundle ("navbar-icn-menu")) {
                revealButton.Image = revealImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            using (var composeImage = UIImage.FromBundle ("navbar-icn-newEmail")) {
                composeButton.Image = composeImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }

            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageListToNachoNow", this);
            };
            cancelButton.Clicked += (object sender, EventArgs e) => {
                MultiSelectCancel ();
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                MultiSelectDelete ();
            };
            saveButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToMessageAction", this);
            };

            // Initially let's hide the search controller
            TableView.SetContentOffset (new PointF (0.0f, 44.0f), false);

            // Search button brings up the search controller
            searchButton.Clicked += (object sender, EventArgs e) => {
                if (SearchDisplayController.Active) {
                    return;
                }
                // Cleans up the UI
                if (RefreshControl.Refreshing) {
                    RefreshControl.EndRefreshing ();
                }
                // Save the tableview location, then scroll
                // searchbar into view.  This searchbar is
                // not used; it works around an iOS bug.
                savedContentOffset = TableView.ContentOffset;
                TableView.SetContentOffset (new PointF (0.0f, 0.0f), false);
                if (44.0f >= savedContentOffset.Y) {
                    SearchDisplayController.SetActive (true, true);
                } else {
                    SearchDisplayController.SetActive (true, false);
                }
            };

            // Search cancel handler needed as workaround for 'inactive button' bug
            SearchDisplayController.SearchBar.CancelButtonClicked += (object sender, EventArgs e) => {
                // Disable search & reset the tableview
                if (44.0f >= savedContentOffset.Y) {
                    SearchDisplayController.SetActive (false, true);
                } else {
                    SearchDisplayController.SetActive (false, false);
                }
                TableView.SetContentOffset (savedContentOffset, false);
            };

            // Refreshing
            RefreshControl.ValueChanged += delegate {
                // iOS 7 BUGS
                // Setting Title in ViewDidLoad hides the SearchBar
                // Title is misaligned the first time a refresh controller is displayed
                // RefreshControl.AttributedTitle = new NSAttributedString ("Refreshing");
                // TODO: Sleeping is a placeholder until we implement the refresh code.
                ReloadDataMaintainingPosition (true);
            };

            UIView backgroundView = new UIView (new RectangleF (0, 0, 320, 480));
            backgroundView.BackgroundColor = new UIColor (227f / 255f, 227f / 255f, 227f / 255f, 1.0f);
            TableView.BackgroundView = backgroundView;

            // iOS 7 BUG Workaround
            // iOS 7 puts the  background view over the refresh view, hiding it.
            RefreshControl.Layer.ZPosition = TableView.BackgroundView.Layer.ZPosition + 1;

            //this.setNeedsStatusBarAppearanceUpdate;
        }

        public int GetFirstVisibleRow ()
        {       
            var paths = TableView.IndexPathsForVisibleRows; // Must be on UI thread
            if (null == paths) {
                return -1;
            }
            var path = paths.FirstOrDefault ();
            if (null == path) {
                return -1;
            }
            return path.Row;
        }

        public void ReloadDataMaintainingPosition (bool endRefreshing)
        {            
            // Refresh in background    
            System.Threading.ThreadPool.QueueUserWorkItem (delegate {
                lock (StaticLockObj) {
                    var idList = new int[messageThreads.Count ()];
                    for (var i = 0; i < messageThreads.Count (); i++) {
                        var m = messageThreads.GetEmailThread (i);
                        idList [i] = m.GetEmailMessageIndex (0);
                    }
                    messageThreads.Refresh ();
                    InvokeOnMainThread (() => {
                        var row = GetFirstVisibleRow ();
                        NSIndexPath p = null;
                        if ((-1 != row) && (0 < idList.Count ())) {
                            var targetId = idList [row];
                            for (int i = 0; i < messageThreads.Count (); i++) {
                                var m = messageThreads.GetEmailThread (i);
                                if (m.GetEmailMessageIndex (0) == targetId) {
                                    p = NSIndexPath.FromItemSection (i, 0);
                                    break;
                                }
                            }
                        }
                        TableView.ReloadData ();
                        if (null != p) {
                            TableView.ScrollToRow (p, UITableViewScrollPosition.Top, false);
                        }
                        if (endRefreshing) {
                            RefreshControl.EndRefreshing ();
                        }
                    });
                }
            });
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            ReloadDataMaintainingPosition (false);

//            for (int i = 0; i < messageThreads.Count (); i++) {
//                Console.WriteLine ("Thread {0}", i); 
//                var messageThread = messageThreads.GetEmailThread (i);
//                foreach (var msg in messageThread) {
//                    Console.WriteLine ("    SBJ: {0}", msg.Subject);
//                    Console.WriteLine ("    MID: {0}", msg.MessageID);
//                    Console.WriteLine ("    RPL: {0}", msg.InReplyTo);
//                    Console.WriteLine ("    REF: {0}", msg.References);
//                    Console.WriteLine ("    CID: {0}", msg.ConversationId);
//                }
//            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                ReloadDataMaintainingPosition (false);
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }
                
            if (segue.Identifier == "MessageListToMessageView") {
                var vc = (MessageViewController)segue.DestinationViewController;
                vc.thread = messageThreads.GetEmailThread (TableView.IndexPathForSelectedRow.Row);
            }
            if (segue.Identifier == "MessageToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                var messageThreadIndex = (int)h.value;
                vc.thread = messageThreads.GetEmailThread (messageThreadIndex);
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, h);
            }
            if (segue.Identifier == "MessageListToCalendarItemEdit") {
                var vc = (CalendarItemViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                var c = h.value as McCalendar;
                vc.SetOwner (this);
                vc.SetCalendarItem (c, CalendarItemEditorAction.edit);
            }
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                this.DismissViewController (true, null);
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
                PerformSegue ("MessageListToCalendarItemEdit", new SegueHolder (c));
            }));
        }

        /// <summary>
        /// INachoCalendarItemEditorParent Delegate
        /// </summary>
        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.SetOwner (null, null);
            vc.DismissFolderChooser (false, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            if (MultiSelectActive ()) {
                MultiSelectMove (folder);
            } else {
                var h = cookie as SegueHolder;
                var messageThreadIndex = (int)h.value;
                MoveThisMessage (messageThreadIndex, folder);
            }
            vc.DismissFolderChooser (true, null);
        }

        protected bool NoMessageThreads ()
        {
            return ((null == messageThreads) || (0 == messageThreads.Count ()));
        }

        public override int NumberOfSections (UITableView tableView)
        {
            if (tableView == SearchDisplayController.SearchResultsTableView) {
                return 1;
            } else {
                return 1;
            }
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            if (NoMessageThreads ()) {
                return 1; // "No messages"
            } else {
                return messageThreads.Count ();
            }
        }

        protected float HeightForMessage (McEmailMessage message)
        {
            if (message.IsDeferred () || message.HasDueDate ()) {
                return 141.0f;
            } else {
                return 116.0f;
            }
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoMessageThreads ()) {
                return 44.0f;
            }
            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            var message = messageThread.SingleMessageSpecialCase ();
            return HeightForMessage (message);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (!MultiSelectActive ()) {
                PerformSegue ("MessageListToMessageView", indexPath);
            }
        }

        protected const int USER_IMAGE_TAG = 101;
        protected const int USER_CHILI_TAG = 102;
        protected const int USER_CHECKMARK_TAG = 103;
        protected const int FROM_TAG = 104;
        protected const int SUBJECT_TAG = 105;
        protected const int SUMMARY_TAG = 106;
        protected const int REMINDER_ICON_TAG = 107;
        protected const int REMINDER_TEXT_TAG = 108;
        protected const int ATTACHMENT_TAG = 109;
        protected const int RECEIVED_DATE_TAG = 110;

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
            var cell = contentView.Superview.Superview as UITableViewCell;
            ConfigureMultiSelectCell (cell);

            // Did we just transition to or from multi-select?
            if (1 >= MultiSelect.Count) {
                MultiSelectToggle ();
            }
        }

        protected void ConfigureMultiSelectCell (UITableViewCell cell)
        {
            var threadIndex = cell.ContentView.Tag;
            var userCheckmarkView = cell.ContentView.ViewWithTag (USER_CHECKMARK_TAG) as UIImageView;

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
        protected void MultiSelectToggle ()
        {
            if (!NoMessageThreads ()) {
                foreach (var cell in TableView.VisibleCells) {
                    ConfigureMultiSelectCell (cell);
                    ConfigureMultiSelectSwipe (cell as MCSwipeTableViewCell);
                }
            }
            UIView.Animate (0.2, new NSAction (
                delegate {
                    if (0 == MultiSelect.Count) {
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton, searchButton };
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
                    } else {
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { deleteButton, saveButton };
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { cancelButton };
                    }
                })
            );
        }

        protected bool MultiSelectActive ()
        {
            return (MultiSelect.Count > 0);
        }

        protected void MultiSelectCancel ()
        {
            MultiSelect.Clear ();
            MultiSelectToggle ();
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

                var cellWidth = View.Frame.Width;

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
                userImageView.AddGestureRecognizer (userImageTap);
                userImageView.UserInteractionEnabled = true;

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
                var summaryLabelView = new UILabel (new RectangleF (65, 60, cellWidth - 15 - 65, 60));
                summaryLabelView.Font = A.Font_AvenirNextRegular14;
                summaryLabelView.TextColor = A.Color_999999;
                summaryLabelView.Lines = 2;
                summaryLabelView.Tag = SUMMARY_TAG;
                cell.ContentView.AddSubview (summaryLabelView);

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
            NachoAssert.CaseError ();
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureMessageCell (UITableViewCell cell, int messageThreadIndex)
        {
            // Save thread index
            cell.ContentView.Tag = messageThreadIndex;

            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();

            var cellWidth = View.Frame.Width;

            // User image view
            // TODO: user images
            var userImageView = cell.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            userImageView.Image = Util.LettersWithColor ("BP", UIColor.LightGray, A.Font_AvenirNextUltraLight24);

            // User chili view
            var userChiliView = cell.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            userChiliView.Hidden = !message.isHot();

            // User checkmark view
            ConfigureMultiSelectCell (cell);

            // Subject label view
            var subjectLabelView = cell.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);

            // Summary label view
            var summaryLabelView = cell.ViewWithTag (SUMMARY_TAG) as UILabel;
            if (null == message.Summary) {
                message.Summarize ();
            }
            NachoAssert.True (null != message.Summary);
            summaryLabelView.Frame = new RectangleF (65, 60, cellWidth - 15 - 65, 60);
            summaryLabelView.Text = message.Summary;
            summaryLabelView.SizeToFit ();

            // Reminder image view and label
            var reminderImageView = cell.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = cell.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                if (message.IsOverdue ()) {
                    reminderLabelView.Text = String.Format ("Response was due {0}", message.FlagDueAsUtc ());
                } else {
                    reminderLabelView.Text = String.Format ("Response is due {0}", message.FlagDueAsUtc ());
                }
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            // Received label view
            var receivedLabelView = cell.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.CompactDateString (message.DateReceived);
            receivedLabelView.SizeToFit ();
            var receivedLabelRect = receivedLabelView.Frame;
            receivedLabelRect.X = cellWidth - 15 - receivedLabelRect.Width;
            receivedLabelRect.Height = 20;
            receivedLabelView.Frame = receivedLabelRect;

            // Attachment image view
            var attachmentImageView = cell.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            attachmentImageView.Hidden = false;
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelRect.X - 10 - 16;
            attachmentImageView.Frame = attachmentImageRect;

            // From label view
            var fromLabelView = cell.ViewWithTag (FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = attachmentImageRect.X - 65;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

            ConfigureSwipes (cell as MCSwipeTableViewCell, messageThreadIndex);
            ConfigureMultiSelectSwipe (cell as MCSwipeTableViewCell);
        }

        /// <summary>
        /// Configures the swipes.
        /// </summary>
        void ConfigureSwipes (MCSwipeTableViewCell cell, int messageThreadIndex)
        {
            cell.FirstTrigger = 0.20f;
            cell.SecondTrigger = 0.50f;

            UIView checkView = null;
            UIColor greenColor = null;
            UIView crossView = null;
            UIColor redColor = null;
            UIView clockView = null;
            UIColor yellowColor = null;
            UIView listView = null;
            UIColor brownColor = null;

            try { 
                checkView = ViewWithImageName ("check");
                greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (checkView, greenColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    Console.WriteLine ("Did swipe Checkmark cell");
                    ArchiveThisMessage (messageThreadIndex);
                });
                crossView = ViewWithImageName ("cross");
                redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    DeleteThisMessage (messageThreadIndex);
                });
                clockView = ViewWithImageName ("clock");
                yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    PerformSegue ("MessageToMessagePriority", new SegueHolder (messageThreadIndex));
                });
                listView = ViewWithImageName ("list");
                brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    PerformSegue ("MessageToMessageAction", new SegueHolder (messageThreadIndex));
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

        public void MoveThisMessage (int messageThreadIndex, McFolder folder)
        {
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Move (message, folder);
        }

        public void DeleteThisMessage (int messageThreadIndex)
        {
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (message);
        }

        public void ArchiveThisMessage (int messageThreadIndex)
        {
            var messageThread = messageThreads.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (message);
        }

        protected void MultiSelectDelete ()
        {
            foreach (var messageThreadIndex in MultiSelect) {
                DeleteThisMessage (messageThreadIndex);
            }
            MultiSelect.Clear ();
            MultiSelectToggle ();
        }

        protected void MultiSelectMove (McFolder folder)
        {
            foreach (var messageThreadIndex in MultiSelect) {
                MoveThisMessage (messageThreadIndex, folder);
            }
            MultiSelect.Clear ();
            MultiSelectToggle ();
        }
    }
}
 
