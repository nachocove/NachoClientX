// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using iCarouselBinding;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MCSwipeTableViewCellBinding;

namespace NachoClient.iOS
{
    public partial class NachoNowViewController : UIViewController, INachoMessageControllerDelegate
    {
        public NachoNowViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.BackgroundColor = UIColor.LightGray;

//            tableView.WeakDataSource = new NachoNowDataSource (this);
            tableView.Source = new NachoNowDataSource (this);

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Toolbar buttons
            emailNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToMessageList", new SegueHolder (NcEmailManager.Inbox ()));
            };
            calendarNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToCalendar", new SegueHolder (null));
            };
            tasksNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToMessageList", new SegueHolder (new NachoDeferredEmailMessages ()));
            };
            contactsNowButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToContacts", new SegueHolder (null));
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            this.NavigationController.ToolbarHidden = false;

            UpdateHotList ();
            tableView.ReloadData ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }
        //        NachoNowToCalendar(null)
        //        NachoNowToCalendarItem (index path)
        //        NachoNowToContacts (null)
        //        NachoNowToMessageAction (index path)
        //        NachoNowToMessageList (inbox folder)
        //        NachoNowToMessageList(deferred folder)
        //        NachoNowToMessagePriority  (index path)
        //        NachoNowToMessageView (index path)
        //
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "NachoNowToCalendar") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToCalendarItem") {
                var indexPath = (NSIndexPath)sender;
                McCalendar calendarItem = (McCalendar)hotList [indexPath.Row];
                CalendarItemViewController destinationController = (CalendarItemViewController)segue.DestinationViewController;
                destinationController.calendarItem = calendarItem;
                destinationController.Title = Pretty.SubjectString (calendarItem.Subject);
                return;
            }
            if (segue.Identifier == "NachoNowToContacts") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageList") {
                var holder = (SegueHolder)sender;
                var messageList = (INachoEmailMessages)holder.value;
                var messageListViewController = (MessageListViewController)segue.DestinationViewController;
                messageListViewController.SetEmailMessages (messageList);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var indexPath = (NSIndexPath)sender;
                var vc = (MessageViewController)segue.DestinationViewController;
                vc.thread = (List<McEmailMessage>)hotList [indexPath.Row];
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var indexPath = (NSIndexPath)sender;
                vc.thread = messageThreads.GetEmailThread (indexPath.Row);
                vc.SetOwner (this);
                return;
            }

            Log.Info ("Unhandled segue identifer %s", segue.Identifier);
            NachoAssert.CaseError ();
        }

        public void DismissMessageViewController (INachoMessageController vc)
        {
            vc.SetOwner (null);
            vc.DismissViewController (false, null);
        }

        INachoEmailMessages messageThreads;
        INachoEmailMessages taskThreads;
        INachoCalendar calendar;
        List<object> hotList;

        protected void UpdateHotList ()
        {
            messageThreads = NcEmailManager.Inbox ();
            taskThreads = new NachoDeferredEmailMessages ();
            calendar = NcCalendarManager.Instance;
            hotList = new List<object> ();

            for (int i = 0; (i < messageThreads.Count ()) && (i < 3); i++) {
                hotList.Add (messageThreads.GetEmailThread (i));
            }
            for (int i = 0; (i < taskThreads.Count ()) && (i < 3); i++) {
                hotList.Add (taskThreads.GetEmailThread (i));
            }

            int day = calendar.IndexOfDate (DateTime.UtcNow.Date);
            for (int i = 0; i < calendar.NumberOfItemsForDay (day); i++) {
                hotList.Add (calendar.GetCalendarItem (day, i));
            }
        }

        public class NachoNowDataSource : UITableViewSource
        {
            NachoNowViewController owner;

            /// <summary>
            /// Initializes a new instance of the
            /// <see cref="NachoClient.iOS.NachoNowViewController+NachoNowDataSource"/> class.
            /// </summary>
            /// <param name="owner">Owner.</param>
            public NachoNowDataSource (NachoNowViewController owner)
            {
                this.owner = owner;
            }

            public override int NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override int RowsInSection (UITableView tableview, int section)
            {
                return owner.hotList.Count;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                object item = owner.hotList [indexPath.Row];

                var messageThread = item as List<McEmailMessage>;
                if (null != messageThread) {
                    var cell = NachoSwipeTableViewCell.GetCell (tableView, messageThread);
                    ConfigureCellActions (cell, indexPath);
                    return cell;
                }

                var calendarItem = item as McCalendar;
                if (null != calendarItem) {
                    var cell = GetCalendarCell (tableView, calendarItem);
                    return cell;
                }

                NachoAssert.CaseError ();
                return null;
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return 78.0f;
            }

            public UITableViewCell GetCalendarCell (UITableView tableView, McCalendar c)
            {
                const string CellIdentifier = "CalendarToCalendarItem";

                UITableViewCell cell = tableView.DequeueReusableCell (CellIdentifier);
                // Should always get a prototype cell
                NachoCore.NachoAssert.True (null != cell);

                UILabel startLabel = (UILabel)cell.ViewWithTag (1);
                UILabel durationLabel = (UILabel)cell.ViewWithTag (2);
                UIImageView calendarImage = (UIImageView)cell.ViewWithTag (3);
                UILabel titleLabel = (UILabel)cell.ViewWithTag (4);

                if (c.AllDayEvent) {
                    startLabel.Text = "ALL DAY";
                    durationLabel.Text = "";
                } else {
                    startLabel.Text = Pretty.ShortTimeString (c.StartTime);
                    durationLabel.Text = Pretty.CompactDuration (c);
                }
                calendarImage.Image = NachoClient.Util.DotWithColor (UIColor.Green);
                var titleLabelFrame = titleLabel.Frame;
                titleLabelFrame.Width = cell.Frame.Width - titleLabel.Frame.Left;
                titleLabel.Frame = titleLabelFrame;
                titleLabel.Text = c.Subject;
                titleLabel.SizeToFit ();

                return cell;
            }

            void ConfigureCellActions (NachoSwipeTableViewCell cell, NSIndexPath indexPath)
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
                    });
                    crossView = ViewWithImageName ("cross");
                    redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        DeleteThisMessage (indexPath);
                    });
                    clockView = ViewWithImageName ("clock");
                    yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        owner.PerformSegue ("NachoNowToMessagePriority", indexPath);
                    });
                    listView = ViewWithImageName ("list");
                    brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        owner.PerformSegue ("NachoNowToMessageAction", indexPath);
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

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                object item = owner.hotList [indexPath.Row];

                var messageThread = item as List<McEmailMessage>;
                if (null != messageThread) {
                    owner.PerformSegue ("NachoNowToMessageView", indexPath);
                    return;
                }

                var calendarItem = item as McCalendar;
                if (null != calendarItem) {
                    owner.PerformSegue ("NachoNowToCalendarItem", indexPath);
                    return;
                }

                NachoAssert.CaseError ();
            }

            UIView ViewWithImageName (string imageName)
            {
                var image = UIImage.FromBundle (imageName);
                var imageView = new UIImageView (image);
                imageView.ContentMode = UIViewContentMode.Center;
                return imageView;
            }

            public void DeleteThisMessage (NSIndexPath indexPath)
            {
                var t = owner.messageThreads.GetEmailThread (indexPath.Row);
                var m = t.First ();
                BackEnd.Instance.DeleteEmailCmd (m.AccountId, m.Id);
            }
        }
    }
}
