// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
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
    public partial class MessageListViewController : UITableViewController, IUITableViewDelegate, IUISearchDisplayDelegate, IUISearchBarDelegate, IUIScrollViewDelegate
    {
        INachoEmailMessages messageThreads;
        // iOS Bug Workaround
        // The cancel button on the search bar breaks
        // if the searchbar is hidden by a scrolled tableview.
        PointF savedContentOffset;

        public void SetEmailMessages (INachoEmailMessages l)
        {
            messageThreads = l;
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Multiple buttons on the right side
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton, searchButton };

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
                System.Threading.ThreadPool.QueueUserWorkItem (delegate {
                    System.Threading.Thread.Sleep (5000);
                    InvokeOnMainThread (() => {
                        RefreshControl.EndRefreshing ();
                    });
                });
            };

            UIView backgroundView = new UIView (new RectangleF (0, 0, 320, 480));
            backgroundView.BackgroundColor = new UIColor (227f / 255f, 227f / 255f, 227f / 255f, 1.0f);
            TableView.BackgroundView = backgroundView;

            // iOS 7 BUG Workaround
            // iOS 7 puts the  background view over the refresh view, hiding it.
            RefreshControl.Layer.ZPosition = TableView.BackgroundView.Layer.ZPosition + 1;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            messageThreads.Refresh ();
            TableView.ReloadData ();

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

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessagesToRead") {
                var vc = (ReadMessageViewController)segue.DestinationViewController;
                vc.messages = messageThreads;
                vc.ThreadIndex = TableView.IndexPathForSelectedRow.Row;
            }
            if (segue.Identifier == "MessageToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.owner = this;
            }
        }

        public void DismissMessagePriorityViewController (MessagePriorityViewController vc)
        {
            vc.owner = null;
            vc.DismissViewController (false, new NSAction (delegate {
                this.DismissViewController (true, null);
            }));
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
            if (tableview == SearchDisplayController.SearchResultsTableView) {
                return 0;
            }
            if (null == messageThreads) {
                return 0;
            } else {
                return messageThreads.Count ();
            }
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            PerformSegue ("MessagesToRead", indexPath);
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            const string CellIdentifier = "Cell";

            NachoSwipeTableViewCell cell = (NachoSwipeTableViewCell)tableView.DequeueReusableCell (CellIdentifier);

            if (null == cell) {
                cell = new NachoSwipeTableViewCell (UITableViewCellStyle.Subtitle, CellIdentifier);

                if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
            }
            ConfigureCell (cell, indexPath);

            var messageThread = messageThreads.GetEmailThread (indexPath.Row);
            var message = messageThread.First ();
            var sender = message.From;
            var subject = message.Subject;
            if (null == message.Summary) {
                UpdateDbWithSummary (message);
            }
            NachoAssert.True (null != message.Summary);
            var summary = message.Summary;
            var date = message.DateReceived;
            var icon = (message.IsRead ? NachoMessageIcon.None : NachoMessageIcon.Read);
            if (DateTime.UtcNow < message.DeferUntil) {
                icon = NachoMessageIcon.Clock;
            }
            var count = (messageThread.Count > 1 ? messageThread.Count : 0);

            cell.Update (sender, summary, subject, date, icon, count);

            return cell;
        }

        void UpdateDbWithSummary (McEmailMessage message)
        {
            var body = message.GetBody (BackEnd.Instance.Db);
            var summary = MimeHelpers.CreateSummary (body);
            message.Summary = summary;
            BackEnd.Instance.Db.Update (message);
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 78.0f;
        }

        void ConfigureCell (NachoSwipeTableViewCell cell, NSIndexPath indexPath)
        {

            // Setting the default inactive state color to the tableView background color
            cell.DefaultColor = TableView.BackgroundView.BackgroundColor;

//            cell.Delegate = this;

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
                });
                crossView = ViewWithImageName ("cross");
                redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    Console.WriteLine ("Did swipe Cross cell");
                });
                clockView = ViewWithImageName ("clock");
                yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    Console.WriteLine ("Did swipe Clock cell");
                    PerformSegue ("MessageToMessagePriority", indexPath);
                });
                listView = ViewWithImageName ("list");
                brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    Console.WriteLine ("Did swipe List cell");
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

        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
        }
    }
}
 