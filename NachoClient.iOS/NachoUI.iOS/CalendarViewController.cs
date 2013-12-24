// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class CalendarViewController : UITableViewController
    {
        public bool UseDeviceCalendar;
        UIAlertView alert;
        INachoCalendar calendar;
        /// <summary>
        ///  Must match the id in the prototype cell.
        /// </summary>
        static readonly NSString CellSegueID = new NSString ("CalendarToCalendarItem");

        AppDelegate appDelegate { get; set; }

        public CalendarViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // We must request permission to access the user's calendar
            // This will prompt the user on platforms that ask, or it will validate
            // manifest permissions on platforms that declare their required permissions.
            if (UseDeviceCalendar) {
                appDelegate.EventStore.RequestAccess (EKEntityType.Event, 
                    (bool granted, NSError e) => {
                        InvokeOnMainThread (() => {
                            if (granted) {
                                calendar = new DeviceCalendar ();
                                TableView.ReloadData ();
                            } else {
                                alert = new UIAlertView ("Permissions denied", "You have denied this app access to your calendars", null, "Close");
                                alert.Show ();
                            }
                        });
                    });
            } else {
                calendar = new NachoCalendar ();
                TableView.ReloadData ();
            }
        }

        /// <summary>
        /// Prepares for segue.
        /// </summary>
        /// <param name="segue">Segue in charge</param>
        /// <param name="sender">Typically the cell that was clicked.</param>
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            // The "+" button segues with CalendarToNewCalendarItem
            // Cells segue with CellSegueID, CalendarToCalendarItem
            if (segue.Identifier.Equals (CellSegueID)) {
                UITableViewCell cell = (UITableViewCell)sender;
                NSIndexPath indexPath = TableView.IndexPathForCell (cell);
                NcCalendar i = calendar.GetCalendarItem (indexPath.Row);
                CalendarItemViewController destinationController = (CalendarItemViewController)segue.DestinationViewController;
                destinationController.calendarItem = i;
                destinationController.Title = i.Subject;
            }
        }

        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return ((null == calendar) ? 0 : calendar.Count ());
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = TableView.DequeueReusableCell (CellSegueID);
            // Should always get a prototype cell
            System.Diagnostics.Trace.Assert (null != cell);

            NcCalendar c = calendar.GetCalendarItem (indexPath.Row);

            cell.TextLabel.Text = c.Subject;
            cell.DetailTextLabel.Text = c.StartTime.ToString ();

            return cell;
        }
    }
}
