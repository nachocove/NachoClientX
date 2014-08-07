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
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, IMessageTableViewSourceDelegate
    {
        MessageTableViewSource messageSource;
        // iOS Bug Workaround
        // The cancel button on the search bar breaks
        // if the searchbar is hidden by a scrolled tableview.
        PointF savedContentOffset;
        //        private static Object StaticLockObj = new Object ();
        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected HashSet<int> MultiSelect = null;

        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads);
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            MultiSelect = new HashSet<int> ();
            messageSource = new MessageTableViewSource ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            ReloadCaptureName = "MessageListViewController.Reload";
            NcCapture.AddKind (ReloadCaptureName);
            ReloadCapture = NcCapture.Create (ReloadCaptureName);

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

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
                messageSource.MultiSelectCancel (TableView);
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                if (null != messageSource) {
                    messageSource.MultiSelectDelete (TableView);
                }
            };
            saveButton.Clicked += (object sender, EventArgs e) => {
                var h = new SegueHolder (TableView);
                PerformSegue ("NachoNowToMessageAction", h);
            };

            // Initially let's hide the search controller
            TableView.SetContentOffset (new PointF (0.0f, 44.0f), false);
            TableView.SeparatorColor = A.Color_NachoSeparator;

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

            messageSource.owner = this;
            TableView.Source = messageSource;

            MultiSelectToggle (messageSource, false);
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
            UIView.Animate (0.2, new NSAction (
                delegate {
                    if (enabled) {
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { deleteButton, saveButton };
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { cancelButton };
                    } else {
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton, searchButton };
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
                    }
                })
            );
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
            NachoClient.Util.HighPriority ();
            messageSource.RefreshEmailMessages ();
            ReloadCapture.Start ();
            TableView.ReloadData ();
            ReloadCapture.Stop ();
            NachoClient.Util.RegularPriority ();


//            // Refresh in background    
//            System.Threading.ThreadPool.QueueUserWorkItem (delegate {
//                lock (StaticLockObj) {
//                    var idList = new int[messageThreads.Count ()];
//                    for (var i = 0; i < messageThreads.Count (); i++) {
//                        var m = messageThreads.GetEmailThread (i);
//                        idList [i] = m.GetEmailMessageIndex (0);
//                    }
//                    messageThreads.Refresh ();
//                    InvokeOnMainThread (() => {
//                        var row = GetFirstVisibleRow ();
//                        NSIndexPath p = null;
//                        if ((-1 != row) && (0 < idList.Count ())) {
//                            var targetId = idList [row];
//                            for (int i = 0; i < messageThreads.Count (); i++) {
//                                var m = messageThreads.GetEmailThread (i);
//                                if (m.GetEmailMessageIndex (0) == targetId) {
//                                    p = NSIndexPath.FromItemSection (i, 0);
//                                    break;
//                                }
//                            }
//                        }
//                        TableView.ReloadData ();
//                        if (null != p) {
//                            TableView.ScrollToRow (p, UITableViewScrollPosition.Top, false);
//                        }
//                        if (endRefreshing) {
//                            RefreshControl.EndRefreshing ();
//                        }
//                    });
//                }
//            });
            if (endRefreshing) {
                RefreshControl.EndRefreshing ();
            }
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
            // In case we exit during scrolling
            NachoClient.Util.RegularPriority ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback");
                ReloadDataMaintainingPosition (false);
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }
            if (segue.Identifier == "NachoNowToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                if (null == h) {
                    // Composing a message
                    vc.Action = null;
                    vc.ActionThread = null;
                } else {
                    vc.Action = (string)h.value;
                    vc.ActionThread = (McEmailMessageThread)h.value2;
                }
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "MessageListToNachoNow") {
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var vc = (MessageViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;                
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, h);
                return;
            }
            if (segue.Identifier == "NachoNowToCalendarItem") {
                CalendarItemViewController vc = (CalendarItemViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var c = holder.value as McCalendar;
                if (null == c) {
                    vc.SetCalendarItem (null, CalendarItemEditorAction.create);
                } else {
                    vc.SetCalendarItem (c, CalendarItemEditorAction.view);
                }
                vc.SetOwner (this);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        ///  IMessageTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        ///  IMessageTableViewSourceDelegate
        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
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
            if (null != messageSource) {
                messageSource.FolderSelected (vc, folder, cookie);
            }
            vc.DismissFolderChooser (true, null);
        }

    }
}
 
