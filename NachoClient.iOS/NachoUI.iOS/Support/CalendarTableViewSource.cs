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
        protected bool compactMode;

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

        public void SetCompactMode (bool compactMode)
        {
            this.compactMode = compactMode;
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
            if (compactMode) {
                return 69.0f;
            }
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

        public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        {
            if (compactMode) {
                return 69.0f;
            }
            return 87.0f;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            McCalendar c = calendar.GetCalendarItem (indexPath.Section, indexPath.Row);
            owner.PerformSegueForDelegate ("NachoNowToEventView", new SegueHolder (c));
        }

        protected const int SUBJECT_TAG = 99101;
        protected const int DURATION_TAG = 99102;
        protected const int LOCATION_ICON_TAG = 99103;
        protected const int LOCATION_TEXT_TAG = 99104;
        protected const int COMPACT_SUBJECT_TAG = 99105;
        protected const int COMPACT_ICON_TAG = 99106;
        protected const int COMPACT_TEXT_TAG = 99107;
        protected const int LINE_TAG = 99108;
        protected const int DOT_TAG = 99109;

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
                var subjectLabelView = new UILabel (new RectangleF (65, 15, cellWidth - 65, 20));
                subjectLabelView.Font = A.Font_AvenirNextDemiBold17;
                subjectLabelView.TextColor = A.Color_114645;
                subjectLabelView.Tag = SUBJECT_TAG;
                cell.ContentView.AddSubview (subjectLabelView);

                // Duration label view
                var durationLabelView = new UILabel (new RectangleF (65, 35, cellWidth - 65, 20));
                durationLabelView.Font = A.Font_AvenirNextMedium14;
                durationLabelView.TextColor = A.Color_999999;
                durationLabelView.Tag = DURATION_TAG;
                cell.ContentView.AddSubview (durationLabelView);

                // Location image view
                var locationIconView = new UIImageView (new RectangleF (65, 59, 12, 12));
                locationIconView.Tag = LOCATION_ICON_TAG;
                cell.ContentView.AddSubview (locationIconView);

                // Location label view
                var locationLabelView = new UILabel (new RectangleF (80, 55, cellWidth - 80, 20));
                locationLabelView.Font = A.Font_AvenirNextRegular14;
                locationLabelView.TextColor = A.Color_999999;
                locationLabelView.Tag = LOCATION_TEXT_TAG;
                cell.ContentView.AddSubview (locationLabelView);

                // Vertical line
                var lineView = new UIView (new RectangleF (35, 0, 1, 20));
                lineView.BackgroundColor = A.Color_NachoNowBackground;
                lineView.Tag = LINE_TAG;
                cell.ContentView.AddSubview (lineView);

                // Dot image view
                var dotView = new UIImageView (new RectangleF (29, 19, 12, 12));
                dotView.Tag = DOT_TAG;
                cell.ContentView.AddSubview (dotView);

                // Subject label view
                var compactSubjectLabelView = new UILabel (new RectangleF (56, 20, cellWidth - 56, 20));
                compactSubjectLabelView.Font = A.Font_AvenirNextDemiBold17;
                compactSubjectLabelView.TextColor = A.Color_114645;
                compactSubjectLabelView.Tag = COMPACT_SUBJECT_TAG;
                cell.ContentView.AddSubview (compactSubjectLabelView);

                // Location image view
                var compactIconView = new UIImageView (new RectangleF (56, 46, 12, 12));
                compactIconView.Tag = COMPACT_ICON_TAG;
                cell.ContentView.AddSubview (compactIconView);

                // Location label view
                var compactLabelView = new UILabel (new RectangleF (74, 40, cellWidth - 74, 20));
                compactLabelView.Font = A.Font_AvenirNextRegular14;
                compactLabelView.TextColor = A.Color_999999;
                compactLabelView.Tag = COMPACT_TEXT_TAG;
                cell.ContentView.AddSubview (compactLabelView);

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
        protected void ConfigureCalendarCell (UITableViewCell cell, NSIndexPath indexPath)
        {
            var c = calendar.GetCalendarItem (indexPath.Section, indexPath.Row);

            // Save calendar item index
            cell.ContentView.Tag = c.Id;

            // Subject label view
            var subject = Pretty.SubjectString (c.Subject);

            var dotView = cell.ContentView.ViewWithTag (DOT_TAG) as UIImageView;
            var subjectLabelView = cell.ContentView.ViewWithTag (SUBJECT_TAG) as UILabel;
            var compactSubjectLabelView = cell.ContentView.ViewWithTag (COMPACT_SUBJECT_TAG) as UILabel;
            compactSubjectLabelView.Hidden = !compactMode;
            subjectLabelView.Hidden = compactMode;
            if (compactMode) {
                compactSubjectLabelView.Text = subject;
                dotView.Frame = new RectangleF (30, 25, 9, 9);

            } else {
                subjectLabelView.Text = subject;
                dotView.Frame = new RectangleF (30, 20, 9, 9);
            }
            dotView.Image = Util.DrawCalDot(A.Color_CalDotBlue);

            // Duration label view
            var durationLabelView = cell.ContentView.ViewWithTag (DURATION_TAG) as UILabel;
            var locationLabelView = cell.ContentView.ViewWithTag (LOCATION_TEXT_TAG) as UILabel;
            var locationIconView = cell.ContentView.ViewWithTag (LOCATION_ICON_TAG) as UIImageView;
            var compactIconView = cell.ContentView.ViewWithTag (COMPACT_ICON_TAG) as UIImageView;
            var compactTextView = cell.ContentView.ViewWithTag (COMPACT_TEXT_TAG) as UILabel;

            durationLabelView.Hidden = compactMode;
            locationLabelView.Hidden = compactMode;
            locationIconView.Hidden = compactMode;
            compactIconView.Hidden = !compactMode;
            compactTextView.Hidden = !compactMode;

            var durationString = "";
            if (c.AllDayEvent) {
                durationString = "ALL DAY";
            } else {
                var start = Pretty.ShortTimeString (c.StartTime);
                var duration = Pretty.CompactDuration (c);
                durationString = String.Join (" - ", new string[] { start, duration });
            }

            var locationString = "";
            if (!String.IsNullOrEmpty (c.Location)) {
                locationIconView.Image = UIImage.FromBundle ("cal-icn-pin");
                locationString = Pretty.SubjectString (c.Location);
            }

            if (compactMode) {
                var eventString = "";
                if (String.IsNullOrEmpty (locationString)) {
                    eventString = durationString;
                } else {
                    eventString = String.Join (" : ", new string[] { locationString, durationString });
                }
                if (String.IsNullOrEmpty (eventString)) {
                    compactIconView.Hidden = true;
                    compactTextView.Hidden = true;
                } else {
                    compactIconView.Image = UIImage.FromBundle ("cal-icn-pin");
                    compactTextView.Text = eventString;
                }

            } else {
                // Duration
                durationLabelView.Text = durationString;
                // Location view
                if (String.IsNullOrEmpty (locationString)) {
                    locationIconView.Hidden = true;
                    locationLabelView.Hidden = true;
                } else {
                    locationIconView.Image = UIImage.FromBundle ("cal-icn-pin");
                    locationLabelView.Text = locationString;
                }
            }

            var lineView = cell.ContentView.ViewWithTag (LINE_TAG);
            lineView.Hidden = compactMode;
            if (!compactMode) {
                lineView.Frame = new RectangleF (34, 0, 1, HeightForCalendarEvent (c));
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

        public override float GetHeightForHeader (UITableView tableView, int section)
        {
            if (compactMode) {
                return 0;
            } else {
                return 75;
            }
        }

        public override UIView GetViewForHeader (UITableView tableView, int section)
        {
            var view = new UIView (new RectangleF (0, 0, tableView.Frame.Width, 75));
            view.BackgroundColor = UIColor.White;
            view.Layer.BorderColor = A.Color_NachoNowBackground.CGColor;
            view.Layer.BorderWidth = 1;

            var dayLabelView = new UILabel (new RectangleF (65, 21, tableView.Frame.Width - 65, 20));
            dayLabelView.Font = A.Font_AvenirNextDemiBold17;
            dayLabelView.TextColor = A.Color_114645;
            view.AddSubview (dayLabelView);

            var dateLabelView = new UILabel (new RectangleF (65, 41, tableView.Frame.Width - 65, 20));
            dateLabelView.Font = A.Font_AvenirNextMedium14;
            dateLabelView.TextColor = A.Color_999999;
            view.AddSubview (dateLabelView);

            var bigNumberView = new UILabel (new RectangleF (0, 0, 65, 75));
            bigNumberView.Font = A.Font_AvenirNextUltraLight32;
            bigNumberView.TextColor = A.Color_29CCBE;
            bigNumberView.TextAlignment = UITextAlignment.Center;
            view.AddSubview (bigNumberView);

            var date = calendar.GetDateUsingDayIndex (section);

            dayLabelView.Text = date.ToString ("dddd");
            dateLabelView.Text = date.ToString ("MMMMM d, yyyy");
            bigNumberView.Text = date.Day.ToString ();

            return view;
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

        public void ScrollToNow (UITableView tableView)
        {
            ScrollToDate (tableView, DateTime.UtcNow);
        }

        public void ScrollToDate (UITableView tableView, DateTime date)
        {
            if (calendar.NumberOfDays () > 0) {
                var i = calendar.IndexOfDate (date);
                if (i >= 0) {
                    var p = NSIndexPath.FromItemSection (0, i);
                    tableView.ScrollToRow (p, UITableViewScrollPosition.Top, true);
                }
            }
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DraggingStarted");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DecelerationEnded");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                Log.Info (Log.LOG_UI, "DraggingEnded");
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }
        }
    }
}

