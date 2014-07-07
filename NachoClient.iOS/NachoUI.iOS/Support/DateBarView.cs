﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;


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
        public DateTime ViewDate = new DateTime ();
        CalendarViewController owner;
        UILabel monthLabelView = new UILabel (new RectangleF (110, 0, 100, 14));


        public DateBarView ()
        {

        }

        public DateBarView (IntPtr handle) : base (handle)
        {

        }

        public void SetOwner (CalendarViewController owner)
        {
            this.owner = owner;
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
            int spacing = 0;
            while (i < 7) {
                var daysLabelView = new UILabel (new RectangleF (24 + spacing, 18, 12, 12));
                daysLabelView.TextColor = A.Color_999999;
                daysLabelView.Tag = 99 - i;
                daysLabelView.Text = Days [i].Substring (0, 1);
                daysLabelView.Font = (A.Font_AvenirNextRegular12);
                daysLabelView.TextAlignment = UITextAlignment.Center;
                this.Add (daysLabelView);
                spacing += 43;
                i++;

            }
        }


        public void MakeDateDotButtons ()
        {
            monthLabelView.TextColor = A.Color_999999;
            monthLabelView.Font = (A.Font_AvenirNextRegular12);
            monthLabelView.TextAlignment = UITextAlignment.Center;
            monthLabelView.Hidden = true;
            monthLabelView.Tag = 200;
            this.Add (monthLabelView);
            this.ViewDate = DateTime.Today;
            int i = 0;
            int spacing = 0;
            int j = 0;
            var tagIncrement = 0;
            int row = 0;
            while (j < 6) {
                i = 0;
                spacing = 0;
                while (i < 7) {
                    var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
                    buttonRect.Frame = new RectangleF (12 + spacing, 34 + row, 36, 36);
                    buttonRect.Tag = tagIncrement + 100;
                    buttonRect.Layer.CornerRadius = 18;
                    buttonRect.Layer.MasksToBounds = true;
                    buttonRect.TintColor = UIColor.Clear;
                    buttonRect.BackgroundColor = A.Color_NachoNowBackground;
                    buttonRect.TouchUpInside += (sender, e) => {
                        ToggleButtons (buttonRect.Tag);
                        buttonRect.Selected = true;
                        buttonRect.BackgroundColor = A.Color_FEBA32;
                        owner.ScrollToDate (this, buttonRect);
                    };
                    this.Add (buttonRect);
                    spacing += 43;
                    i++;
                    tagIncrement++;
                }
                row += 44;
                j++;
            }
        }

        public bool todayWeekTagSet = false;
        public bool todayMonthTagSet = false;

        public void UpdateButtons ()
        {   
            int dayOffset = -(IndexOfDayOfWeek (this.ViewDate.DayOfWeek.ToString ()));
            int selectedIndex = IndexOfDayOfWeek (owner.selectedDate.DayOfWeek.ToString ());
            int i = 0;
            while (i < 7) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                string date = this.ViewDate.AddDays (dayOffset).Day.ToString ();
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_114645, UIControlState.Normal);
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
                    button.Selected = true;
                    button.BackgroundColor = A.Color_FEBA32;
                    owner.selectedDateTag = button.Tag;
                } else {
                    button.Selected = false;
                    button.BackgroundColor = A.Color_NachoNowBackground;
                }
                i++;
                dayOffset++;
            }

        }

        public void MakeDateVisable ()
        {
            monthLabelView.Hidden = false;
            monthLabelView.Text = this.ViewDate.ToString ("MMMM");
        }

        public DateTime GetFirstDay (DateTime date)
        {
            var day = date.Day;
            var firstDay = this.ViewDate.AddDays (-day + 1); 
            return firstDay;
        }

        public void UpdateButtonsMonth ()
        {
            monthLabelView.Hidden = false;
            monthLabelView.Text = this.ViewDate.ToString ("MMMM");
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
                    button.Selected = true;
                    button.BackgroundColor = A.Color_FEBA32;
                    owner.selectedDateTag = button.Tag;
                } else {
                    button.Selected = false;
                    button.BackgroundColor = UIColor.White;
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_114645, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;
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
                    button.Selected = true;
                    button.BackgroundColor = A.Color_FEBA32;
                    owner.selectedDateTag = button.Tag;
                } else {
                    button.Selected = false;
                    button.BackgroundColor = A.Color_NachoNowBackground;
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_114645, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;

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
                    button.Selected = true;
                    button.BackgroundColor = A.Color_FEBA32;
                    owner.selectedDateTag = button.Tag;
                } else {
                    button.Selected = false;
                    button.BackgroundColor = UIColor.White;
                }
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.SetTitleColor (A.Color_114645, UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Selected);
                button.SetTitleColor (UIColor.White, UIControlState.Disabled);
                button.Font = A.Font_AvenirNextDemiBold17;

                i++;
                dayIncrement++;
            }

        }
           
        public void ToggleButtons (int selectedButtonTag)
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
                        button.BackgroundColor = UIColor.White;
                        button.Selected = false;
                    } 
                    if (i == selectedButtonTag - 100) {
                        button.BackgroundColor = A.Color_FEBA32;
                        button.Selected = true;
                    }
                    i++;
                }
                var numDaysOffset = numDays + dayOffset;
                while (i < numDaysOffset) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (true == button.Selected) {
                        button.BackgroundColor = A.Color_NachoNowBackground;
                        button.Selected = false;
                    } 
                    if (i == selectedButtonTag - 100) {
                        button.BackgroundColor = A.Color_FEBA32;
                        button.Selected = true;
                    }
                    i++;
                }
                var monthEnd = rows * 7;
                while (i < monthEnd) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (true == button.Selected) {
                        button.BackgroundColor = UIColor.White;
                        button.Selected = false;
                    } 
                    if (i == selectedButtonTag - 100) {
                        button.BackgroundColor = A.Color_FEBA32;
                        button.Selected = true;
                    }
                    i++;
                }
            } else {
                int i = 0;
                while (i < 42) {
                    UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                    if (i == selectedButtonTag - 100) {
                        button.BackgroundColor = A.Color_FEBA32;
                        button.Selected = true;
                    } else {
                        button.BackgroundColor = A.Color_NachoNowBackground;
                        button.Selected = false;
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
                button.BackgroundColor = A.Color_NachoNowBackground;
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

        public int IsButtonInWeek (int baseButtonTag, DateTime baseButtonDate, DateTime newDate)
        {
            TimeSpan difference = baseButtonDate - newDate;
            var tempTag = baseButtonTag + (int)difference.Days;
            if (99 >= tempTag) {
                return -1;
            }
            if (107 <= tempTag) {
                return 1;
            } 
            return 0;
        }

        public int IsButtonInMonth (int baseButtonTag, DateTime baseButtonDate, DateTime newDate)
        {
            TimeSpan difference = baseButtonDate - newDate;
            var tempTag = baseButtonTag + (int)difference.Days;
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

        public int GetMonthTag (DateTime date)
        {
            int dayOffset = IndexOfDayOfWeek ((GetFirstDay (date)).DayOfWeek.ToString ());
            return dayOffset + date.Day + 99;

        }

    }

}

