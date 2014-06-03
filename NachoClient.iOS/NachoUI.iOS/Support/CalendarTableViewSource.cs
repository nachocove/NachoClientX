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
    public class CalendarTableViewSource : UITableViewSource
    {
        INachoCalendar calendar;
        public ICalendarTableViewSourceDelegate owner;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string CalendarEventReuseIdentifier = "CalendarEvent";

        public CalendarTableViewSource ()
        {
            owner = null;
        }

        public void SetCalendar (INachoCalendar calendar)
        {
            this.calendar = calendar;
        }

        protected bool NoCalendarEvents ()
        {
            return ((null == calendar) || (0 == calendar.NumberOfDays ()));
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            if (null == calendar) {
                return 1;
            } else {
                return calendar.NumberOfDays ();
            }
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            if (null == calendar) {
                return 1;
            } else {
                return calendar.NumberOfItemsForDay (section);
            }
        }

        protected float HeightForCalendarEvent (McCalendar c)
        {
            return 87.0f;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoCalendarEvents ()) {
                return 44.0f;
            }
            McCalendar c = calendar.GetCalendarItem (indexPath.Section, indexPath.Row);
            return HeightForCalendarEvent (c);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            McCalendar c = calendar.GetCalendarItem (indexPath.Section, indexPath.Row);
            owner.PerformSegueForDelegate ("NachoNowToCalendarItem", new SegueHolder (c));
        }

        protected const int SUBJECT_TAG = 101;
        protected const int DURATION_TAG = 102;
        protected const int LOCATION_ICON_TAG = 103;
        protected const int LOCATION_TEXT_TAG = 104;

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

            if (identifier.Equals (CalendarEventReuseIdentifier)) {
                var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Default, identifier);
                if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                cell.DefaultColor = UIColor.White;

                var cellWidth = tableView.Frame.Width;

                // Subject label view
                var subjectLabelView = new UILabel (new RectangleF (65, 21, 150, 20));
                subjectLabelView.Font = A.Font_AvenirNextDemiBold17;
                subjectLabelView.TextColor = A.Color_114645;
                subjectLabelView.Tag = SUBJECT_TAG;
                cell.ContentView.AddSubview (subjectLabelView);

                // Duration label view
                var durationLabelView = new UILabel (new RectangleF (65, 41, 150, 20));
                durationLabelView.Font = A.Font_AvenirNextMedium14;
                durationLabelView.TextColor = A.Color_999999;
                durationLabelView.Tag = DURATION_TAG;
                cell.ContentView.AddSubview (durationLabelView);

                // Location image view
                var locationIconView = new UIImageView (new RectangleF (65, 65, 12, 12));
                locationIconView.Tag = LOCATION_ICON_TAG;
                cell.ContentView.AddSubview (locationIconView);

                // Location label view
                var locationLabelView = new UILabel (new RectangleF (80, 61, 150, 20));
                locationLabelView.Font = A.Font_AvenirNextRegular14;
                locationLabelView.TextColor = A.Color_999999;
                locationLabelView.Tag = LOCATION_TEXT_TAG;
                cell.ContentView.AddSubview (locationLabelView);

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

            if (cell.ReuseIdentifier.Equals (CalendarEventReuseIdentifier)) {
                ConfigureCalendarCell (cell, indexPath);
                return;
            }
            NachoAssert.CaseError ();
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
        protected void ConfigureCalendarCell (UITableViewCell cell, NSIndexPath indexPath)
        {
            var c = calendar.GetCalendarItem (indexPath.Section, indexPath.Row);

            // Save calendar item index
            cell.ContentView.Tag = c.Id;

            var cellWidth = cell.Frame.Width;

            // Subject label view
            var subjectLabelView = cell.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (c.Subject);

            // Duration label view
            var durationLabelView = cell.ViewWithTag (DURATION_TAG) as UILabel;
            if (c.AllDayEvent) {
                durationLabelView.Text = "ALL DAY";
            } else {
                var start = Pretty.ShortTimeString (c.StartTime);
                var duration = Pretty.CompactDuration (c);
                durationLabelView.Text = String.Join (" - ", new string[] { start, duration });
            }

            // Locaion view
            var locationLabelView = cell.ViewWithTag (LOCATION_TEXT_TAG) as UILabel;
            var locationIconView = cell.ViewWithTag (LOCATION_ICON_TAG) as UIImageView;
            if (String.IsNullOrEmpty (c.Location)) {
                locationIconView.Hidden = true;
                locationLabelView.Hidden = true;
            } else {
                locationIconView.Hidden = false;
                locationLabelView.Hidden = false;
                locationIconView.Image = UIImage.FromBundle ("cal-icn-pin");
                locationLabelView.Text = Pretty.SubjectString (c.Location);
            }

            ConfigureSwipes (cell as MCSwipeTableViewCell, c.Id);
        }

        /// <summary>
        /// Configures the swipes.
        /// </summary>
        void ConfigureSwipes (MCSwipeTableViewCell cell, int calendarIndex)
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
                    ArchiveThisMessage (calendarIndex);
                });
                crossView = ViewWithImageName ("cross");
                redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    DeleteThisMessage (calendarIndex);
                });
                clockView = ViewWithImageName ("clock");
                yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (clockView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    //                    PerformSegue ("MessageToMessagePriority", new SegueHolder (messageThreadIndex));
                });
                listView = ViewWithImageName ("list");
                brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                cell.SetSwipeGestureWithView (listView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    //                    PerformSegue ("MessageToMessageAction", new SegueHolder (messageThreadIndex));
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
            string cellIdentifier = (NoCalendarEvents () ? UICellReuseIdentifier : CalendarEventReuseIdentifier);

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
           
        }

        public void MoveThisMessage (int messageThreadIndex, McFolder folder)
        {

        }

        public void DeleteThisMessage (int messageThreadIndex)
        {

        }

        public void ArchiveThisMessage (int messageThreadIndex)
        {
           
        }

        public void ScrollToNow(UITableView tableView)
        {
            if (calendar.NumberOfDays () > 0) {
                var i = calendar.IndexOfDate (DateTime.UtcNow);
                if (i >= 0) {
                    var p = NSIndexPath.FromItemSection (0, i);
                    tableView.ScrollToRow (p, UITableViewScrollPosition.Top, true);
                }
            }
        }
    }
}

