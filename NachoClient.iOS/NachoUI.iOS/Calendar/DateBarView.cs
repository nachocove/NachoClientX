//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Utils;
using NachoCore;


namespace NachoClient.iOS
{

    [Register ("DateBarView")]
    public class DateBarView: UIView
    {

        protected static List<string> Days = new List<string> (new string[] {
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday"
        });

        UIView parentView;
        INcEventProvider calendar;
        public DateTime ViewDate = new DateTime ();
        CalendarViewController owner;
        UILabel monthLabelView;

        protected static int dateBarHeight = 97;
        protected static int dateBarRowHeight = 60;


        public DateBarView (UIView parentView, INcEventProvider calendar)
        {
            this.parentView = parentView;
            this.calendar = calendar;
        }

        public DateBarView (IntPtr handle) : base (handle)
        {

        }

        public void SetOwner (CalendarViewController owner)
        {
            this.owner = owner;
        }

        public void InitializeDateBar ()
        {
            monthLabelView = new UILabel (new CGRect (0, 2, parentView.Frame.Width, 20));
            this.Frame = (new CGRect (0, 0, parentView.Frame.Width, dateBarHeight + (dateBarRowHeight * 5)));
            this.BackgroundColor = UIColor.White;
            this.MakeDayLabels ();
            this.MakeDateDotButtons ();
        }

        public static int IndexOfDayOfWeek (string dayOfWeek)
        {
            int theIndex = 0; 
            int i = 0;
            foreach (var day in Days) {
                if (day == dayOfWeek) {
                    theIndex = i;
                    break;
                }
                i++;
            }
            return theIndex;
        }

        public void MakeDayLabels ()
        {
            int i = 0;
            nfloat spacing = 0;
            nfloat spacer = (owner.View.Frame.Width - (24 * 2) - 12) / 6f;
            nfloat startingX = 24f;
            while (i < 7) {
                var daysLabelView = new UILabel (new CGRect (startingX + spacing, 18 + 5, 12, 12));
                daysLabelView.TextColor = A.Color_NachoIconGray;
                daysLabelView.Tag = 99 - i;
                daysLabelView.Text = Days [i].Substring (0, 1);
                daysLabelView.Font = (A.Font_AvenirNextMedium10);
                daysLabelView.TextAlignment = UITextAlignment.Center;
                this.Add (daysLabelView);
                spacing += spacer;
                i++;
            }
        }

        public void MakeDateDotButtons ()
        {
            monthLabelView.TextColor = A.Color_NachoIconGray;
            monthLabelView.Font = A.Font_AvenirNextMedium12;
            monthLabelView.TextAlignment = UITextAlignment.Center;

            this.AddSubview (monthLabelView);
            this.ViewDate = DateTime.Today;
            int i = 0;
            nfloat spacing = 0;
            nfloat spacerWidth = (owner.View.Frame.Width - (12 * 2) - 36) / 6f;
            nfloat startingX = 12;
            int j = 0;
            var tagIncrement = 0;
            int row = 0;
            while (j < 6) {
                i = 0;
                spacing = 0;
                while (i < 7) {
                    var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
                    buttonRect.Frame = new CGRect (startingX + spacing, 5 + 34 + row, 36, 36);
                    buttonRect.Tag = tagIncrement + 100;
                    buttonRect.Layer.CornerRadius = 18;
                    buttonRect.Layer.BorderColor = A.Card_Border_Color;
                    buttonRect.Layer.BorderWidth = A.Card_Border_Width;
                    buttonRect.Layer.MasksToBounds = true;
                    buttonRect.TintColor = UIColor.Clear;
                    buttonRect.BackgroundColor = UIColor.White;
                    buttonRect.AccessibilityLabel = "Date";
                    buttonRect.TouchUpInside += (sender, e) => {
                        ToggleButtons (buttonRect.Tag);
                        owner.ScrollToDate (this, buttonRect);
                    };
                    this.AddSubview (buttonRect);

                    var eventIndicatorDot = new UIImageView (new CGRect (buttonRect.Center.X - 2, buttonRect.Center.Y + 24, 4, 4));
                    eventIndicatorDot.Image = Util.DrawCalDot (A.Color_NachoBorderGray, new CGSize (4, 4));
                    eventIndicatorDot.Hidden = true;
                    eventIndicatorDot.Tag = tagIncrement + 200;
                    this.AddSubview (eventIndicatorDot);

                    spacing += spacerWidth;
                    i++;
                    tagIncrement++;
                }
                row += dateBarRowHeight;
                j++;
            }
        }

        public bool todayWeekTagSet = false;
        public bool todayMonthTagSet = false;

        public void UpdateButtons ()
        {   
            UpdateMonthLabel ();
            int dayOffset = -(IndexOfDayOfWeek (this.ViewDate.DayOfWeek.ToString ()));
            int selectedIndex = IndexOfDayOfWeek (owner.selectedDate.DayOfWeek.ToString ());
            int i = 0;
            while (i < 7) {
                UIButton button = (UIButton)(this.ViewWithTag (i + 100));
                string date = this.ViewDate.AddDays (dayOffset).Day.ToString ();
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_NachoDarkText, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;
                if (!todayWeekTagSet) {
                    if (selectedIndex == i && owner.selectedDate.Day.ToString () == date) {
                        owner.todayWeekTag = button.Tag;
                        todayWeekTagSet = true;
                    }
                }
                if (selectedIndex == i && owner.selectedDate.Day.ToString () == date) {
                    SetButtonState (true, button, false);
                } else {
                    SetButtonState (false, button, false);
                }

                UIImageView eventIndicator = (UIImageView)(this.ViewWithTag (i + 200));
                eventIndicator.Hidden = HideEventIndicatorForDate (this.ViewDate.AddDays (dayOffset));

                i++;
                dayOffset++;
            }
            while (i < 42) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                SetButtonState (false, button, false);
                i++;
            }

        }

        public void UpdateMonthLabel ()
        {
            monthLabelView.Text = Pretty.LongMonthYear (this.ViewDate);
        }

        public DateTime GetFirstDay (DateTime date)
        {
            var day = date.Day;
            var firstDay = this.ViewDate.AddDays (-day + 1); 
            return firstDay;
        }

        public void SetButtonState (bool selected, UIButton button, bool differentMonth)
        {
            if (selected) {
                button.Selected = true;
                button.BackgroundColor = A.Color_NachoGreen;
                button.Layer.BorderColor = UIColor.Clear.CGColor;
                owner.selectedDateTag = button.Tag;
            } else {
                button.Selected = false;
                if (differentMonth) {
                    button.BackgroundColor = A.Color_NachoLightGrayBackground;
                    button.Layer.BorderColor = UIColor.Clear.CGColor;
                } else {
                    button.BackgroundColor = UIColor.White;
                    button.Layer.BorderColor = A.Card_Border_Color;
                }
            }
            button.Layer.BorderWidth = A.Card_Border_Width;
        }

        public void UpdateButtonsMonth ()
        {
            UpdateMonthLabel ();
            var firstDate = GetFirstDay (this.ViewDate);
            int rows = RowsInAMonth (this.ViewDate);
            int dayOffset = IndexOfDayOfWeek (firstDate.DayOfWeek.ToString ());
            var numDays = DateTime.DaysInMonth (this.ViewDate.Year, this.ViewDate.Month);
            int i = 0;
            int dayIncrement = -dayOffset;
            while (i < dayOffset) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                string date = firstDate.AddDays (dayIncrement).Day.ToString ();
                DateTime buttonDate = firstDate.AddDays (dayIncrement);
                if (owner.selectedDate == buttonDate) {
                    SetButtonState (true, button, true);
                } else {
                    SetButtonState (false, button, true);
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_NachoIconGray, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;

                UIImageView eventIndicator = (UIImageView)(this.ViewWithTag (i + 200));
                eventIndicator.Hidden = HideEventIndicatorForDate (firstDate.AddDays (dayIncrement));

                i++;
                dayIncrement++;
            }
            dayIncrement = 0;
            var numDaysOffset = numDays + dayOffset;
            while (i < numDaysOffset) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                string date = firstDate.AddDays (dayIncrement).Day.ToString ();
                DateTime buttonDate = firstDate.AddDays (dayIncrement);
                if (owner.selectedDate == buttonDate) {
                    SetButtonState (true, button, false);
                } else {
                    SetButtonState (false, button, false);
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_NachoDarkText, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;

                UIImageView eventIndicator = (UIImageView)(this.ViewWithTag (i + 200));
                eventIndicator.Hidden = HideEventIndicatorForDate (firstDate.AddDays (dayIncrement));

                i++;
                dayOffset++;
                dayIncrement++;
            }
            var monthEnd = rows * 7;
            while (i < monthEnd) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                string date = firstDate.AddDays (dayIncrement).Day.ToString ();
                DateTime buttonDate = firstDate.AddDays (dayIncrement);
                if (owner.selectedDate == buttonDate) {
                    SetButtonState (true, button, true);
                } else {
                    SetButtonState (false, button, true);
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_NachoIconGray, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;

                UIImageView eventIndicator = (UIImageView)(this.ViewWithTag (i + 200));
                eventIndicator.Hidden = HideEventIndicatorForDate (firstDate.AddDays (dayIncrement));

                i++;
                dayIncrement++;
            }

        }

        public void ToggleButtons (nint selectedButtonTag)
        {
            if (!CalendarViewController.BasicView) {
                var firstDate = GetFirstDay (this.ViewDate);
                int rows = RowsInAMonth (this.ViewDate);
                int dayOffset = IndexOfDayOfWeek (firstDate.DayOfWeek.ToString ());
                var numDays = DateTime.DaysInMonth (this.ViewDate.Year, this.ViewDate.Month);
                int i = 0;
                while (i < dayOffset) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (true == button.Selected) {
                        SetButtonState (false, button, true);
                    } 
                    if (i == selectedButtonTag - 100) {
                        SetButtonState (true, button, true);
                    }
                    i++;
                }
                var numDaysOffset = numDays + dayOffset;
                while (i < numDaysOffset) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (true == button.Selected) {
                        SetButtonState (false, button, false);
                    } 
                    if (i == selectedButtonTag - 100) {
                        SetButtonState (true, button, false);
                    }
                    i++;
                }
                var monthEnd = rows * 7;
                while (i < monthEnd) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (true == button.Selected) {
                        SetButtonState (false, button, true);
                    } 
                    if (i == selectedButtonTag - 100) {
                        SetButtonState (true, button, true);
                    }
                    i++;
                }
            } else {
                int i = 0;
                while (i < 42) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (i == selectedButtonTag - 100) {
                        SetButtonState (true, button, false);
                    } else {
                        SetButtonState (false, button, false);
                    } 
                    i++;
                }
            }
        }

        public void ToggleButtonBackgrounds ()
        {
            int i = 0;
            while (i < 42) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                button.BackgroundColor = A.Color_NachoLightGrayBackground;
                i++;
            }
        }

        public int RowsInAMonth (DateTime date)
        {
            if (date.Month.ToString () == "1" || date.Month.ToString () == "3" ||
                date.Month.ToString () == "5" || date.Month.ToString () == "7" ||
                date.Month.ToString () == "8" || date.Month.ToString () == "10" ||
                date.Month.ToString () == "12") {
                var day = date.Day;
                var firstDate = date.AddDays (-day + 1); 
                if ("Saturday" == firstDate.DayOfWeek.ToString () || "Friday" == firstDate.DayOfWeek.ToString ()) {
                    return 6;
                }
                return 5;
            } else if (date.Month.ToString () == "4" || date.Month.ToString () == "6" ||
                       date.Month.ToString () == "9" || date.Month.ToString () == "11") {
                var day = date.Day;
                var firstDate = date.AddDays (-day + 1); 
                if ("Saturday" == firstDate.DayOfWeek.ToString ()) {
                    return 6;
                }
                return 5;
            } else {
                var day = date.Day;
                var firstDate = date.AddDays (-day + 1); 
                if (0 != date.Year % 4 && "Sunday" == firstDate.DayOfWeek.ToString ()) {
                    return 4;
                }
                return 5;
            }

        }

        public int IsButtonInWeek (nint baseButtonTag, DateTime baseButtonDate, DateTime newDate)
        {
            TimeSpan difference = baseButtonDate - newDate;
            var tempTag = baseButtonTag + difference.Days;
            if (99 >= tempTag) {
                return -1;
            }
            if (107 <= tempTag) {
                return 1;
            } 
            return 0;
        }

        public int IsButtonInMonth (nint baseButtonTag, DateTime baseButtonDate, DateTime newDate)
        {
            TimeSpan difference = newDate - baseButtonDate;
            var tempTag = baseButtonTag + difference.Days;
            int rows = RowsInAMonth (baseButtonDate);
            if (99 >= tempTag) {
                return -1;
            } 
            if (4 == rows) {
                if (128 <= tempTag) {
                    return 1;
                }
            } else if (5 == rows) {
                if (135 <= tempTag) {
                    return 1;
                }
            } else if (6 == rows) {
                if (142 <= tempTag) {
                    return 1;
                }
            }
            return 0;
        }

        public nint GetMonthTag (DateTime date)
        {
            int dayOffset = IndexOfDayOfWeek ((GetFirstDay (date)).DayOfWeek.ToString ());
            return dayOffset + date.Day + 99;
        }

        public bool HideEventIndicatorForDate (DateTime date)
        {
            if ((date.Month >= DateTime.UtcNow.Month && date.Year == DateTime.UtcNow.Year) || date.Year > DateTime.UtcNow.Year) {
                var index = calendar.IndexOfDate (date);
                if (0 <= index) {
                    return 0 == calendar.NumberOfItemsForDay (index);
                }
            }
            return true;
        }
    }

}

