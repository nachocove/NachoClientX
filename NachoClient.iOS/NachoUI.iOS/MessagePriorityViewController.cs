// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using CoreAnimation;
using CoreGraphics;

namespace NachoClient.iOS
{
    public partial class MessagePriorityViewController : BlurryViewController, INcDatePickerDelegate, INachoDateController
    {
        protected McEmailMessageThread thread;
        protected INachoDateControllerParent owner;
        protected IntentSelectionViewController intentSelector;
        protected NcMessageDeferral.MessageDateType dateControllerType = NcMessageDeferral.MessageDateType.None;

        const float BUTTON_SIZE = 64;
        const float BUTTON_LABEL_HEIGHT = 40;
        const float BUTTON_PADDING_HEIGHT = 15;
        const float BUTTON_PADDING_WIDTH = 35;

        public MessagePriorityViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateView ();
        }

        public void Setup (INachoDateControllerParent owner, McEmailMessageThread thread, NcMessageDeferral.MessageDateType dateControllerType)
        {
            this.owner = owner;
            this.thread = thread;
            this.dateControllerType = dateControllerType;
        }

        public void SetIntentSelector (IntentSelectionViewController selector)
        {
            this.intentSelector = selector;
        }

        public void CreateView ()
        {
            UIView priorityView = new UIView (View.Frame);
            priorityView.ClipsToBounds = true;
            priorityView.BackgroundColor = A.Color_NachoGreen;

            var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, 44));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            var navItem = new UINavigationItem ();
            if (NcMessageDeferral.MessageDateType.Defer == dateControllerType) {
                navItem.Title = "Defer Message";
            } else {
                navItem.Title = "Set Deadline";
            }
            using (var image = UIImage.FromBundle ("modal-close")) {
                var dismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                dismissButton.AccessibilityLabel = "Dismiss";
                dismissButton.Clicked += (object sender, EventArgs e) => {
                    DismissDateController (true, null);
                };
                navItem.LeftBarButtonItem = dismissButton;
            }
            navBar.Items = new UINavigationItem[] { navItem };

            priorityView.AddSubview (navBar);

            nfloat yOffset = 64;

            UIView sectionSeparator = new UIView (new CGRect (0, yOffset, View.Frame.Width, .5f));
            sectionSeparator.BackgroundColor = UIColor.LightGray.ColorWithAlpha (.6f);
            priorityView.AddSubview (sectionSeparator);

            yOffset = sectionSeparator.Frame.Bottom + 20;

            if (NcMessageDeferral.MessageDateType.Defer == dateControllerType) {
                UILabel messageSubject = new UILabel (new CGRect (30, yOffset, View.Frame.Width - 60, 25));
                var subject = thread.GetSubject ();
                if (null != subject) {
                    messageSubject.Text = Pretty.SubjectString (subject);
                } else {
                    messageSubject.Text = "";
                }
                messageSubject.Font = A.Font_AvenirNextRegular17;
                messageSubject.TextColor = UIColor.White;
                priorityView.Add (messageSubject);
                yOffset = messageSubject.Frame.Bottom + 60;
            } else {
                yOffset += 40;
            }

            List<ButtonInfo> buttonInfoList;

            switch (dateControllerType) {
            case NcMessageDeferral.MessageDateType.Defer:
                buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                    new ButtonInfo ("Later Today", "modal-later-today", () => DateSelected (MessageDeferralType.Later, DateTime.MinValue)),
                    new ButtonInfo ("Tonight", "modal-tonight", () => DateSelected (MessageDeferralType.Tonight, DateTime.MinValue)),
                    new ButtonInfo ("Tomorrow", "modal-tomorrow", () => DateSelected (MessageDeferralType.Tomorrow, DateTime.MinValue)),
                    new ButtonInfo (null, null, null),
                    new ButtonInfo ("Weekend", "modal-weekend", () => DateSelected (MessageDeferralType.Weekend, DateTime.MinValue)),
                    new ButtonInfo ("Next Week", "modal-next-week", () => DateSelected (MessageDeferralType.NextWeek, DateTime.MinValue)),
                    new ButtonInfo ("Forever", "modal-forever", () => DateSelected (MessageDeferralType.Forever, DateTime.MinValue)),
                    new ButtonInfo (null, null, null),
                    null,
                    new ButtonInfo ("Pick Date", "modal-pick-date", () => PerformSegue ("MessagePriorityToDatePicker", this)),
                    null,
                });
                break;
            case NcMessageDeferral.MessageDateType.Intent:
            case NcMessageDeferral.MessageDateType.Deadline:
                buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                    new ButtonInfo ("None", "modal-none", () => DateSelected (MessageDeferralType.None, DateTime.MinValue)),
                    new ButtonInfo ("One hour", "modal-later-today", () => DateSelected (MessageDeferralType.OneHour, DateTime.MinValue)),
                    new ButtonInfo ("Today", "modal-later-today", () => DateSelected (MessageDeferralType.EndOfDay, DateTime.MinValue)),
                    new ButtonInfo (null, null, null),
                    new ButtonInfo ("Tomorrow", "modal-tomorrow", () => DateSelected (MessageDeferralType.Tomorrow, DateTime.MinValue)),
                    new ButtonInfo ("Next Week", "modal-next-week", () => DateSelected (MessageDeferralType.NextWeek, DateTime.MinValue)),
                    new ButtonInfo ("Next Month", "modal-nextmonth", () => DateSelected (MessageDeferralType.NextMonth, DateTime.MinValue)),
                    new ButtonInfo (null, null, null),
                    null,
                    new ButtonInfo ("Pick Date", "modal-pick-date", () => PerformSegue ("MessagePriorityToDatePicker", this)),
                    null,
                });
                break;
            default:
                buttonInfoList = null;
                NcAssert.CaseError ();
                break;
            }

            var center = priorityView.Center;
            center.X = (priorityView.Frame.Width / 2);
            center.Y = center.Y;

            var xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;

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
                buttonRect.Layer.BorderColor = UIColor.LightGray.CGColor;
                buttonRect.Layer.BorderWidth = .5f;                 
                buttonRect.Frame = new CGRect (0, 0, BUTTON_SIZE, BUTTON_SIZE);
                buttonRect.Center = new CGPoint (xOffset, yOffset);
                using (var image = UIImage.FromBundle (buttonInfo.buttonIcon).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    buttonRect.SetImage (image, UIControlState.Normal);
                }
                buttonRect.AccessibilityLabel = buttonInfo.buttonLabel;
                buttonRect.TouchUpInside += (object sender, EventArgs e) => {
                    buttonInfo.buttonAction ();
                };
                priorityView.Add (buttonRect);

                var label = new UILabel ();
                label.TextColor = UIColor.White;
                label.Text = buttonInfo.buttonLabel;
                label.Font = A.Font_AvenirNextMedium14;
                label.TextAlignment = UITextAlignment.Center;
                label.SizeToFit ();
                label.Center = new CGPoint (xOffset, 5 + yOffset + ((BUTTON_SIZE + BUTTON_LABEL_HEIGHT) / 2));
                priorityView.Add (label);

                xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
            }

            View.AddSubview (priorityView);
        }

        public void DismissDateController (bool animated, Action action)
        {
            owner = null;
            thread = null;
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
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void DismissDatePicker (DatePickerViewController vc, DateTime chosenDateTime)
        {
            if (DateTime.UtcNow > chosenDateTime) {
                NcAlertView.ShowMessage (vc, "Date in the Past",
                    "The chosen date is in the past. You must select a date in the future.");
                return;
            }

            vc.owner = null;
            vc.DismissViewController (false, new Action (delegate {
                DateSelected (MessageDeferralType.Custom, chosenDateTime);
            }));
        }

        public void DateSelected (MessageDeferralType dateType, DateTime selectedDate)
        {
            var rc = NcMessageDeferral.ComputeDeferral (DateTime.UtcNow, dateType, selectedDate);
            if (rc.isOK ()) {
                var date = rc.GetValue<DateTime> ();   
                owner.DateSelected (dateControllerType, dateType, thread, date);
                owner.DismissChildDateController (this);
                if (null != intentSelector) {
                    intentSelector.DismissIntentChooser (false, null);
                }
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
