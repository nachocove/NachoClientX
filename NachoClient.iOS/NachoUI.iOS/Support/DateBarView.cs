//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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

    [Register("DateBarView")]
    public class DateBarView: UIView
    {

        protected static List<string> Days = new List<string>(new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" });
        public DateTime ViewDate = new DateTime();
        CalendarViewController owner;

        public DateBarView() {

        }
        public DateBarView(IntPtr handle) : base(handle) {

        }

        public void SetOwner (CalendarViewController owner)
        {
            this.owner = owner;
        }

        public static int IndexOfDayOfWeek(string dayOfWeek){
            int theIndex = 0; 
            int i = 0;
            foreach (var day in Days) {
                if (day == dayOfWeek){
                    theIndex = i;
                    break;
                }
                i++;
            }
            return theIndex;
        }

        public void MakeDayLabels () {
            int i = 0;
            int spacing = 0;
            while (i < 7) {
                var daysLabelView = new UILabel (new RectangleF (24 + spacing, 18, 12, 12));
                daysLabelView.TextColor = A.Color_999999;

                daysLabelView.Text = Days [i].Substring(0,1);
                daysLabelView.Font = (A.Font_AvenirNextRegular12);
                daysLabelView.TextAlignment = UITextAlignment.Center;
                this.Add (daysLabelView);
                spacing += 43;
                i++;

            }
        }

        public void MakeDateButtons () {
            this.ViewDate = DateTime.Today;
            int i = 0;
            int spacing = 0;
            int todaySelected = IndexOfDayOfWeek(DateTime.Today.DayOfWeek.ToString());
            while (i < 7) {
                var buttonRect = UIButton.FromType(UIButtonType.RoundedRect);
                buttonRect.Tag = i + 100;
                buttonRect.Layer.CornerRadius = 18;
                buttonRect.Layer.MasksToBounds = true;
                buttonRect.TintColor = UIColor.Clear;
                buttonRect.SetTitleColor ( A.Color_114645, UIControlState.Normal);
                buttonRect.SetTitleColor (UIColor.White, UIControlState.Selected);
                if (todaySelected == i) {
                    buttonRect.Selected = true;
                    buttonRect.BackgroundColor = A.Color_FEBA32;
                } 
                else {
                    buttonRect.BackgroundColor = A.Color_NachoNowBackground;
                }
                buttonRect.Frame = new RectangleF (12 + spacing, 34, 36, 36);
                this.Add (buttonRect);
                spacing += 43;
                i++;
                buttonRect.TouchUpInside += (sender, e) => {
                    if (true == buttonRect.Selected) {
                        buttonRect.Selected = false;
                        buttonRect.BackgroundColor = A.Color_NachoNowBackground;

                    }
                    else {
                        buttonRect.Selected = true;
                        buttonRect.BackgroundColor = A.Color_FEBA32;
                        owner.ScrollToDate(this, Convert.ToInt32(buttonRect.TitleLabel.Text));
                    }

                };

            }

        } 

        public void UpdateButtons () {
            int i = 0;
            int dayOffset = 0;
            var today = this.ViewDate.DayOfWeek.ToString ();
            foreach (var day in Days) {
                if (day == today){
                    dayOffset = dayOffset - (i);
                    break;
                }
                i++;
            }
            i = 0;
            while (i < 7) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                string date = this.ViewDate.AddDays(dayOffset).Day.ToString ();
                button.SetTitle (date, UIControlState.Normal);
                button.SetTitle (date, UIControlState.Selected);
                button.BackgroundColor = A.Color_NachoNowBackground;
                button.Selected = false;
                button.Font = A.Font_AvenirNextDemiBold17;
                i++;
                dayOffset++;

            }

        } 

        public void ToggleButtons (int index) {
            int i = 0;
            while (i < 7) {
                UIButton button = (this.ViewWithTag (i + 100)) as UIButton;
                if (index != i) {
                    button.Selected = false;
                    button.BackgroundColor = A.Color_NachoNowBackground;
                }
                i++;

            }

        } 


          
    }
}

