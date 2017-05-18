//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class ManageItemsViewController : NcUIViewControllerNoLeaks
    {
        private UIButton populateCalendarButton;
        private UIButton megaMeetingButton;
        private UIButton cleanCalendarButton;

        public ManageItemsViewController ()
        {
        }

        public ManageItemsViewController (IntPtr handle)
            : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NavigationItem.Title = "Manage Items";
        }

        protected override void CreateViewHierarchy ()
        {
            // Pick an obnoxious background color to make it clear that this should not be part of the product.
            View.BackgroundColor = A.Color_NachoSwipeActionYellow;

            populateCalendarButton = new UIButton (UIButtonType.System);
            populateCalendarButton.Frame = new CGRect (10, 10, 200, 50);
            populateCalendarButton.SetTitle ("Fill Calendar", UIControlState.Normal);
            populateCalendarButton.SetTitle ("Filling Calendar...", UIControlState.Selected);
            populateCalendarButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            populateCalendarButton.TouchUpInside += PopulateCalendar;
            View.AddSubview (populateCalendarButton);

            megaMeetingButton = new UIButton (UIButtonType.System);
            megaMeetingButton.Frame = new CGRect (10, 70, 200, 50);
            megaMeetingButton.SetTitle ("Mega Meeting", UIControlState.Normal);
            megaMeetingButton.SetTitle ("Creating meeting...", UIControlState.Selected);
            megaMeetingButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            megaMeetingButton.TouchUpInside += MegaMeeting;
            View.AddSubview (megaMeetingButton);

            cleanCalendarButton = new UIButton (UIButtonType.System);
            cleanCalendarButton.Frame = new CGRect (10, 130, 200, 50);
            cleanCalendarButton.SetTitle ("Clean Calendar", UIControlState.Normal);
            cleanCalendarButton.SetTitle ("Cleaning Calendar...", UIControlState.Selected);
            cleanCalendarButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            cleanCalendarButton.TouchUpInside += CleanCalendar;
            View.AddSubview (cleanCalendarButton);
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
            populateCalendarButton.TouchUpInside -= PopulateCalendar;
            megaMeetingButton.TouchUpInside -= MegaMeeting;
            cleanCalendarButton.TouchUpInside -= CleanCalendar;
            populateCalendarButton = null;
            megaMeetingButton = null;
            cleanCalendarButton = null;
        }

        private void PopulateCalendar (object sender, EventArgs args)
        {
            ButtonTouched (populateCalendarButton, ManageItems.PopulateCalendar, "FillCalendar");
        }

        private void MegaMeeting (object sender, EventArgs args)
        {
            ButtonTouched (megaMeetingButton, ManageItems.MegaMeeting, "MegaMeeting");
        }

        private void CleanCalendar (object sender, EventArgs args)
        {
            ButtonTouched (cleanCalendarButton, ManageItems.CleanCalendar, "CleanCalendar");
        }

        private void ButtonTouched (UIButton button, Action action, string taskName)
        {
            // Select and disable the button before starting the action.
            button.Selected = true;
            button.UserInteractionEnabled = false;
            NcTask.Run (() => {
                action ();
                // Then unselect and enable the button when the action is done.
                InvokeOnUIThread.Instance.Invoke (() => {
                    button.Selected = false;
                    button.UserInteractionEnabled = true;
                });
            }, taskName);
        }
    }
}

