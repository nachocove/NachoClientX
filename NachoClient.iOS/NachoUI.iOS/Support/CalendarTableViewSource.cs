﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class CalendarTableViewSource : UITableViewSource
    {
        INcEventProvider calendar;
        public ICalendarTableViewSourceDelegate owner;
        static List<UIButton> preventAddButtonGC;

        protected const string EmptyCellReuseIdentifier = "EmptyCell";
        protected const string CalendarEventReuseIdentifier = "CalendarEvent";

        public CalendarTableViewSource ()
        {
            owner = null;
        }

        public void SetCalendar (INcEventProvider calendar)
        {
            this.calendar = calendar;
        }

        public void Refresh ()
        {
            if (null != calendar) {
                calendar.Refresh ();
            }
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

        protected float HeightForCalendarEvent (McAbstrCalendarRoot c)
        {
            return 87.0f;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (NoCalendarEvents ()) {
                return 44.0f;
            }
            var c = calendar.GetEventDetail (indexPath.Section, indexPath.Row);
            if (null == c) {
                return 44.0f;
            }
            return HeightForCalendarEvent (c);
        }

        public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
        {
            return 87.0f;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var e = calendar.GetEvent (indexPath.Section, indexPath.Row);
            if (null != e) {
                owner.PerformSegueForDelegate ("NachoNowToEventView", new SegueHolder (e));
            }
        }

        public const int DIAL_IN_TAG = 1;
        public const int NAVIGATE_TO_TAG = 2;
        public const int LATE_TAG = 3;
        public const int FORWARD_TAG = 4;
        public const int OPEN_TAG = 5;

        protected const int SUBJECT_TAG = 99101;
        protected const int DURATION_TAG = 99102;
        protected const int LOCATION_ICON_TAG = 99103;
        protected const int LOCATION_TEXT_TAG = 99104;
        protected const int LINE_TAG = 99108;
        protected const int DOT_TAG = 99109;
        protected const int SWIPE_TAG = 99110;

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor DIAL_IN_BUTTON =
            new SwipeActionDescriptor (DIAL_IN_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeDialIn),
                "Dial In", A.Color_NachoSwipeDialIn);
        private static SwipeActionDescriptor NAVIGATE_BUTTON =
            new SwipeActionDescriptor (NAVIGATE_TO_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeNavigate),
                "Navigate To", A.Color_NachoSwipeNavigate);
        private static SwipeActionDescriptor LATE_BUTTON =
            new SwipeActionDescriptor (LATE_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeLate),
                "I'm Late", A.Color_NachoSwipeLate);
        private static SwipeActionDescriptor FORWARD_BUTTON =
            new SwipeActionDescriptor (FORWARD_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeForward),
                "Forward", A.Color_NachoeSwipeForward);

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UITableViewCell CellWithReuseIdentifier (UITableView tableView, string identifier)
        {
            if (identifier.Equals (EmptyCellReuseIdentifier)) {
                var cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                cell.TextLabel.TextAlignment = UITextAlignment.Center;
                cell.TextLabel.TextColor = UIColor.FromRGB (0x0f, 0x42, 0x4c);
                cell.TextLabel.Font = A.Font_AvenirNextDemiBold17;
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                return cell;
            }

            if (identifier.Equals (CalendarEventReuseIdentifier)) {
                var cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;

                var cellWidth = tableView.Frame.Width;

                var frame = new RectangleF (0, 0, tableView.Frame.Width, 87);
                var view = new SwipeActionView (frame);
                view.Tag = SWIPE_TAG;

                view.SetAction (NAVIGATE_BUTTON, SwipeSide.LEFT);
                view.SetAction (DIAL_IN_BUTTON, SwipeSide.LEFT);
                view.SetAction (LATE_BUTTON, SwipeSide.RIGHT);
                view.SetAction (FORWARD_BUTTON, SwipeSide.RIGHT);
               
                cell.ContentView.AddSubview (view);

                // Subject label view
                var subjectLabelView = new UILabel (new RectangleF (65, 15, cellWidth - 65, 20));
                subjectLabelView.Font = A.Font_AvenirNextDemiBold17;
                subjectLabelView.TextColor = A.Color_NachoBlack;
                subjectLabelView.Tag = SUBJECT_TAG;
                view.AddSubview (subjectLabelView);

                // Duration label view
                var durationLabelView = new UILabel (new RectangleF (65, 35, cellWidth - 65, 20));
                durationLabelView.Font = A.Font_AvenirNextMedium14;
                durationLabelView.TextColor = A.Color_NachoBlack;
                durationLabelView.Tag = DURATION_TAG;
                view.AddSubview (durationLabelView);

                // Location image view
                var locationIconView = new UIImageView (new RectangleF (65, 59, 12, 12));
                locationIconView.Tag = LOCATION_ICON_TAG;
                view.AddSubview (locationIconView);

                // Location label view
                var locationLabelView = new UILabel (new RectangleF (80, 55, cellWidth - 80, 20));
                locationLabelView.Font = A.Font_AvenirNextRegular14;
                locationLabelView.TextColor = A.Color_NachoTextGray;
                locationLabelView.Tag = LOCATION_TEXT_TAG;
                view.AddSubview (locationLabelView);

                // Vertical line
                var lineView = new UIView (new RectangleF (35, 0, 1, 20));
                lineView.BackgroundColor = A.Color_NachoLightBorderGray;
                lineView.Tag = LINE_TAG;
                view.AddSubview (lineView);

                // Dot image view
                var dotView = new UIImageView (new RectangleF (29, 19, 12, 12));
                dotView.Tag = DOT_TAG;
                view.AddSubview (dotView);

                return cell;
            }

            return null;
        }

        /// <summary>
        /// Populate cells with data, adjust sizes and visibility.
        /// </summary>
        protected void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            if (cell.ReuseIdentifier.Equals (EmptyCellReuseIdentifier)) {
                cell.TextLabel.Text = "No messages";
                return;
            }

            if (cell.ReuseIdentifier.Equals (CalendarEventReuseIdentifier)) {
                ConfigureCalendarCell (tableView, cell, indexPath);
                return;
            }
            NcAssert.CaseError ();
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureCalendarCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            var e = calendar.GetEvent (indexPath.Section, indexPath.Row);
            var c = calendar.GetEventDetail (indexPath.Section, indexPath.Row);

            if (null == c) {
                foreach (var v in cell.ContentView.Subviews) {
                    v.Hidden = true;
                }
                var label = cell.ContentView.ViewWithTag (SUBJECT_TAG) as UILabel;
                label.Text = "Event has been deleted...";
                label.Hidden = false;
                return;
            }

            // Save calendar item index
            cell.ContentView.Tag = c.Id;

            // Subject label view
            var subject = Pretty.SubjectString (c.Subject);

            var dotView = cell.ContentView.ViewWithTag (DOT_TAG) as UIImageView;
            var subjectLabelView = cell.ContentView.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Hidden = false;
 
            subjectLabelView.Text = subject;
            dotView.Frame = new RectangleF (30, 20, 9, 9);
            var size = new SizeF (10, 10);
            dotView.Image = Util.DrawCalDot (A.Color_CalDotBlue, size);
            dotView.Hidden = false;

            // Duration label view
            var durationLabelView = cell.ContentView.ViewWithTag (DURATION_TAG) as UILabel;
            var locationLabelView = cell.ContentView.ViewWithTag (LOCATION_TEXT_TAG) as UILabel;
            var locationIconView = cell.ContentView.ViewWithTag (LOCATION_ICON_TAG) as UIImageView;

            durationLabelView.Hidden = false;
            locationLabelView.Hidden = false;
            locationIconView.Hidden = false;

            var durationString = "";
            if (c.AllDayEvent) {
                durationString = "ALL DAY";
            } else {
                var start = Pretty.ShortTimeString (e.StartTime);
                var duration = Pretty.CompactDuration (e.StartTime, e.EndTime);
                durationString = String.Join (" - ", new string[] { start, duration });
            }

            var locationString = "";
            if (!String.IsNullOrEmpty (c.Location)) {
                locationIconView.Image = UIImage.FromBundle ("cal-icn-pin");
                locationString = Pretty.SubjectString (c.Location);
            }

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

            var lineView = cell.ContentView.ViewWithTag (LINE_TAG);
            lineView.Hidden = false;
            lineView.Frame = new RectangleF (34, 0, 1, HeightForCalendarEvent (c));

            var view = (SwipeActionView)cell.ViewWithTag (SWIPE_TAG);

            view.OnClick = (int tag) => {
                switch (tag) {
                case NAVIGATE_TO_TAG:
                    // FIXME
                    break;
                case FORWARD_TAG:
                    ForwardInvite (indexPath);
                    break;
                case DIAL_IN_TAG:
                    // FIXME
                    break;
                case LATE_TAG:
                    SendRunningLateMessage (indexPath);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };
            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };
        }

        UIView ViewWithLabel (string text, string side)
        {
            var label = new UILabel ();
            label.Text = text;
            label.Font = A.Font_AvenirNextDemiBold14;
            label.TextColor = UIColor.White;
            //label.TextAlignment = ta;
            label.SizeToFit ();
            var labelView = new UIView ();
            if ("left" == side) {
                labelView.Frame = new RectangleF (0, 0, label.Frame.Width + 50, label.Frame.Height);
            } else {
                labelView.Frame = new RectangleF (65, 0, label.Frame.Width + 50, label.Frame.Height);
            }
            labelView.Add (label);
            return labelView;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            string cellIdentifier = (NoCalendarEvents () ? EmptyCellReuseIdentifier : CalendarEventReuseIdentifier);

            var cell = tableView.DequeueReusableCell (cellIdentifier);
            if (null == cell) {
                cell = CellWithReuseIdentifier (tableView, cellIdentifier);
            }
            ConfigureCell (tableView, cell, indexPath);
            return cell;

        }

        public override float GetHeightForHeader (UITableView tableView, int section)
        {
            return 75;
        }

        public override UIView GetViewForHeader (UITableView tableView, int section)
        {
            var view = new UIView (new RectangleF (0, 0, tableView.Frame.Width, 75));
            view.BackgroundColor = A.Color_NachoLightGrayBackground;
            view.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            view.Layer.BorderWidth = .5f;

            var dayLabelView = new UILabel (new RectangleF (65, 20, tableView.Frame.Width - 65, 20));
            dayLabelView.Font = A.Font_AvenirNextDemiBold17;
            dayLabelView.TextColor = A.Color_NachoBlack;
            view.AddSubview (dayLabelView);

            var dateLabelView = new UILabel (new RectangleF (65, 40, tableView.Frame.Width - 65, 20));
            dateLabelView.Font = A.Font_AvenirNextMedium14;
            dateLabelView.TextColor = A.Color_NachoTextGray;
            view.AddSubview (dateLabelView);

            var bigNumberView = new UILabel (new RectangleF (0, 0, 65, 75));
            bigNumberView.Font = A.Font_AvenirNextRegular34;
            bigNumberView.TextColor = A.Color_NachoTeal;
            bigNumberView.TextAlignment = UITextAlignment.Center;
            view.AddSubview (bigNumberView);

            var date = calendar.GetDateUsingDayIndex (section);

            if (null == preventAddButtonGC) {
                preventAddButtonGC = new List<UIButton> ();
            }

            var addButton = new UIButton (UIButtonType.ContactAdd);
            addButton.TintColor = A.Color_NachoTeal;
            addButton.Frame = new RectangleF (tableView.Frame.Width - 42, (view.Frame.Height / 2) - 15, 30, 30);
            addButton.TouchUpInside += (sender, e) => {
                owner.PerformSegueForDelegate ("CalendarToEditEventView", new SegueHolder (date));
            };

            preventAddButtonGC.Add (addButton);
            view.AddSubview (addButton);

            dayLabelView.Text = date.ToString ("dddd");
            dateLabelView.Text = date.ToString ("MMMMM d, yyyy");
            bigNumberView.Text = date.Day.ToString ();

            return view;
        }

        protected void ExtendTableViewUntil (UITableView tableView, DateTime date)
        {
            if (null == calendar) {
                return;
            }
            if (null == tableView) {
                return;
            }
            int len1 = calendar.NumberOfDays ();
            int ext2 = calendar.ExtendEventMap (date);
            int len2 = calendar.NumberOfDays ();
            NcAssert.True (len2 == (len1 + ext2));

            if (0 == ext2) {
                return;
            }

            Log.Debug (Log.LOG_UI, "ExtendTableViewUntil:  old={0} new={1} actual={2}", len1, len2, NumberOfSections (tableView));

            NSMutableIndexSet set = new NSMutableIndexSet ();
            for (int i = len1; i < len2; i++) {
                set.Add ((uint)i);
            }
            tableView.BeginUpdates ();
            tableView.InsertSections (set, UITableViewRowAnimation.None);
            tableView.EndUpdates ();
        }

        public void ScrollToNearestEvent (UITableView tableView, DateTime date, int lookaheadDays)
        {
            ExtendTableViewUntil (tableView, date.AddDays (lookaheadDays));
            int item;
            int section;
            if (calendar.FindEventNearestTo (date, out item, out section)) {
                NcAssert.True (section < NumberOfSections (tableView));
                NcAssert.True (item < RowsInSection (tableView, section));
                var p = NSIndexPath.FromItemSection (item, section);
                tableView.ScrollToRow (p, UITableViewScrollPosition.Top, false);
            }
        }

        public void ScrollToDate (UITableView tableView, DateTime date)
        {
            // Make sure there are enough rows to fill the table
            ExtendTableViewUntil (tableView, date.AddDays (7));
            var i = calendar.IndexOfDate (date);
            if (0 <= i) {
                var p = NSIndexPath.FromItemSection (NSRange.NotFound, i);
                tableView.ScrollToRow (p, UITableViewScrollPosition.Top, true);
            }
        }

        public void MaybeExtendTableView (UITableView tableView)
        {
            var visibleRows = tableView.IndexPathsForVisibleRows;
            if ((null == visibleRows) || (0 == visibleRows.Length)) {
                return;
            }
            var path = visibleRows [visibleRows.Length - 1];
            var displayedDate = calendar.GetDateUsingDayIndex (path.Section);

            DateTime finalDayInList;
            if (0 == calendar.NumberOfDays ()) {
                finalDayInList = displayedDate;
            } else {
                finalDayInList = calendar.GetDateUsingDayIndex (calendar.NumberOfDays () - 1);
            }
            if (30 > (finalDayInList - displayedDate).Days) {
                Log.Info (Log.LOG_UI, "Calendar: extending until {0}", finalDayInList.AddDays (30));
                ExtendTableViewUntil (tableView, finalDayInList.AddDays (30));
            }
        }

        public void SendRunningLateMessage (NSIndexPath indexPath)
        {
            var e = calendar.GetEvent (indexPath.Section, indexPath.Row);
            if (null != e) {
                owner.SendRunningLateMessage (e.Id);
            }
        }

        public void ForwardInvite (NSIndexPath indexPath)
        {
            var e = calendar.GetEvent (indexPath.Section, indexPath.Row);
            if (null != e) {
                owner.ForwardInvite (e.Id);
            }
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.HighPriority ("CalendarTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            if (null != owner) {
                owner.CalendarTableViewScrollingEnded ();
            }
            NachoCore.Utils.NcAbate.RegularPriority ("CalendarTableViewSource DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                if (null != owner) {
                    owner.CalendarTableViewScrollingEnded ();
                }
                NachoCore.Utils.NcAbate.RegularPriority ("CalendarTableViewSource DraggingEnded");
            }
        }
    }
}

