// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using MonoTouch.CoreAnimation;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class MessagePriorityViewController : BlurryViewController, INcDatePickerDelegate, INachoDateController
    {
        public McEmailMessageThread thread;
        protected INachoDateControllerParent owner;

        const float BUTTON_SIZE = 60;
        const float BUTTON_LABEL_HEIGHT = 20;
        const float BUTTON_PADDING_HEIGHT = 25;
        const float BUTTON_PADDING_WIDTH = 20;

        private List<UIButton> ActionButtons = new List<UIButton> ();

        public MessagePriorityViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateView ();
        }

        public void CreateView ()
        {
            float frameHeight = 420; //view size isn't relative to screen size (it's fixed)
            float frameWidth = View.Frame.Width - 40;   //20 px indent l/r sides
            float windowX = (View.Frame.Width - frameWidth) / 2;
            float windowY = (View.Frame.Height - frameHeight) / 2;

            UIView priorityView = new UIView (new RectangleF (windowX, windowY, frameWidth, frameHeight));
            priorityView.Layer.CornerRadius = 15.0f;
            priorityView.ClipsToBounds = true;
            priorityView.BackgroundColor = UIColor.White;

            UILabel viewTitle = new UILabel (new RectangleF (priorityView.Frame.Width / 2 - 75, 20, 150, 20));
            viewTitle.Text = "Select a Date";
            viewTitle.Font = A.Font_AvenirNextDemiBold17;
            viewTitle.TextColor = A.Color_NachoBlack;
            viewTitle.TextAlignment = UITextAlignment.Center;
            priorityView.Add (viewTitle);

            var buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                new ButtonInfo ("Later Today", "cup-48", () => DateSelected(MessageDeferralType.Later, DateTime.MinValue)),
                new ButtonInfo ("Tonight", "navbar-icn-defer", () => DateSelected(MessageDeferralType.Tonight, DateTime.MinValue)),
                new ButtonInfo ("Tomorrow", "navbar-icn-defer", () => DateSelected(MessageDeferralType.Tomorrow, DateTime.MinValue)),
                new ButtonInfo (null, null, null),
                new ButtonInfo ("Next Week", "navbar-icn-defer", () => DateSelected(MessageDeferralType.NextWeek, DateTime.MinValue)),
                new ButtonInfo ("Next Month", "navbar-icn-defer", () => DateSelected(MessageDeferralType.NextMonth, DateTime.MinValue)),
                new ButtonInfo ("Pick Date", "navbar-icn-defer", () =>  PerformSegue ("MessagePriorityToDatePicker", this)),
                new ButtonInfo (null, null, null),
                null,
                null,
                new ButtonInfo ("None", "navbar-icn-defer", () => DateSelected(MessageDeferralType.None, DateTime.MinValue)),
            });


            var center = priorityView.Center;
            center.X = (priorityView.Frame.Width / 2);
            center.Y = center.Y;

            var xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;
            var yOffset = center.Y - (1.5F * BUTTON_PADDING_HEIGHT) - (2F * (BUTTON_SIZE + BUTTON_LABEL_HEIGHT)) + (0.5F * BUTTON_SIZE);

            foreach (var buttonInfo in buttonInfoList) {
                if (null == buttonInfo) {
                    xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
                    continue;
                }
                if (null == buttonInfo.buttonLabel) {
                    xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;
                    yOffset += BUTTON_SIZE + BUTTON_LABEL_HEIGHT + BUTTON_PADDING_HEIGHT;
                    continue;
                }

                var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
                buttonRect.Layer.CornerRadius = BUTTON_SIZE / 2;
                buttonRect.Layer.MasksToBounds = true;
                buttonRect.Layer.BorderColor = A.Color_NachoGreen.CGColor;
                buttonRect.Layer.BorderWidth = 1.0f;                  
                buttonRect.Frame = new RectangleF (0, 0, BUTTON_SIZE, BUTTON_SIZE);
                buttonRect.Center = new PointF (xOffset, yOffset);
                buttonRect.SetImage (UIImage.FromBundle (buttonInfo.buttonIcon), UIControlState.Normal);
                buttonRect.TouchUpInside += (object sender, EventArgs e) => {
                    buttonInfo.buttonAction ();
                };
                ActionButtons.Add (buttonRect);
                priorityView.Add (buttonRect);

                var label = new UILabel ();
                label.TextColor = A.Color_NachoBlack;
                label.Text = buttonInfo.buttonLabel;
                label.Font = A.Font_AvenirNextMedium14;
                label.TextAlignment = UITextAlignment.Center;
                label.SizeToFit ();
                label.Center = new PointF (xOffset, 5 + yOffset + ((BUTTON_SIZE + BUTTON_LABEL_HEIGHT) / 2));
                priorityView.Add (label);

                xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
            }

            var dismissLabel = new UILabel ();
            dismissLabel.Text = "Dismiss";
            dismissLabel.TextColor = A.Color_NachoBlack;
            dismissLabel.Font = A.Font_AvenirNextRegular12;
            dismissLabel.TextAlignment = UITextAlignment.Center;
            dismissLabel.SizeToFit ();
            dismissLabel.Frame = new RectangleF (priorityView.Frame.Width / 2 - 50, priorityView.Frame.Height - 30, 100, 20);
            priorityView.AddSubview (dismissLabel);

            var tap = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                DismissViewController (true, null);
            });
            dismissLabel.AddGestureRecognizer (tap);
            dismissLabel.UserInteractionEnabled = true;

            View.AddSubview (priorityView);
        }

        public void SetOwner (INachoDateControllerParent o)
        {
            owner = o;
        }

        public void DimissDateController (bool animated, NSAction action)
        {
            owner = null;
            DismissViewController (animated, action);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessagePriorityToDatePicker") {
                var vc = (DatePickerViewController)segue.DestinationViewController;
                vc.owner = this;
            }
        }
        // TODO: Do we need to worry about local vs. utc time?
        public void DismissDatePicker (DatePickerViewController vc, DateTime chosenDateTime)
        {
            if (DateTime.UtcNow > chosenDateTime) {
                // TODO -- Confirm that the user wants to go back in time.
                return;
            } 

            DateSelected (MessageDeferralType.Custom, chosenDateTime);

            vc.owner = null;
            vc.DismissViewController (false, new NSAction (delegate {
                owner.DismissChildDateController (this);
            }));
        }

        public void DateSelected (MessageDeferralType dateType, DateTime selectedDate)
        {
            if (MessageDeferralType.Custom == dateType) {
                owner.DateSelected (dateType, thread, selectedDate);
                owner.DismissChildDateController (this);
            } else {
                switch (dateType) {
                case MessageDeferralType.Later:
                    selectedDate = DateTime.Today;
                    break;
                case MessageDeferralType.Tonight:
                    selectedDate = DateTime.Today.AddHours(23);
                    break;
                case MessageDeferralType.Tomorrow:
                    selectedDate = DateTime.Today.AddDays (1);
                    break;
                case MessageDeferralType.NextWeek:
                    selectedDate = DateTime.Today.AddDays (8 - (int)DateTime.Today.DayOfWeek);
                    break;
                case MessageDeferralType.NextMonth:
                    selectedDate = new DateTime (DateTime.Today.AddMonths (1).Year, DateTime.Today.AddMonths (1).Month, 1);
                    break;
                default:
                    break;
                }
                owner.DateSelected (dateType, thread, selectedDate);
                owner.DismissChildDateController (this);
            }
        }

        protected class ButtonInfo
        {
            public string buttonLabel { get; set; }

            public string buttonIcon { get; set; }

            public Action buttonAction { get; set; }

            public ButtonInfo (string bl, string bi, Action ba)
            {
                buttonLabel = bl;
                buttonIcon = bi;
                buttonAction = ba;
            }
        }
    }
}
