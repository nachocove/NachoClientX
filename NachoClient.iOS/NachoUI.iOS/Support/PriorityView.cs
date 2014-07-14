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
    [Register ("PriorityView")]
    public class PriorityView: UIView
    {
        private int numRows = 3;
        private int numCols = 3;
        private List<UIButton> ActionButtons = new List<UIButton> ();
        MessagePriorityViewController owner;
        protected List<ButtonInfo> buttonManager;

        public PriorityView ()
        {

        }

        public PriorityView (RectangleF frame)
        {
            this.Frame = frame;
        }

        public PriorityView (IntPtr handle) : base (handle)
        {

        }

        public void SetOwner (MessagePriorityViewController owner)
        {
            this.owner = owner;
        }

        public void initButtonManager ()
        {
            buttonManager = new List<ButtonInfo> (new ButtonInfo[] {

                //TODO
                //For datepicker actions (CreateDeadline & Custom Defer) Cole is going to focus
                //On those screens since he has been working with DatePickers. 
                new ButtonInfo ("Meeting", "navbar-icn-newevent", () => owner.CreateMeeting ()),
                new ButtonInfo ("Heat", "inbox-icn-chilli", () => owner.AddChili ()),
                new ButtonInfo ("Deadline", "inbox-icn-deadline@2x", () => owner.CreateDeadline ()),
                new ButtonInfo ("Later Today", "navbar-icn-defer", () => owner.DelayRequest ("Later")),
                new ButtonInfo ("Tonight", "navbar-icn-defer", () => owner.DelayRequest ("Tonight")),
                new ButtonInfo ("Tomorrow", "navbar-icn-defer", () => owner.DelayRequest ("Tomorrow")),
                new ButtonInfo ("Next Week", "navbar-icn-defer", () => owner.DelayRequest ("NextWeek")),
                new ButtonInfo ("Next Month", "navbar-icn-defer", () => owner.DelayRequest ("NextMonth")),
                new ButtonInfo ("Pick Date", "navbar-icn-defer", () => owner.DelayRequest ("Custom"))
            });
        }

        public UIButton AddEscapeButton ()
        {
            var escapeButton = UIButton.FromType (UIButtonType.RoundedRect);
            escapeButton.SetImage (UIImage.FromBundle ("navbar-icn-close"), UIControlState.Normal);
            escapeButton.Frame = new RectangleF (10, 10, 24, 24);
            escapeButton.TouchUpInside += (object sender, EventArgs e) => {
                owner.DismissViewController (true, null);
            };
            this.Add (escapeButton);
            return escapeButton;
        }

        public void AddActionMessageLabel ()
        {
            var buttonLabelView = new UILabel (new RectangleF (90, 24, 100, 16));
            buttonLabelView.TextColor = A.Color_11464F;
            buttonLabelView.Text = "Select Action";
            buttonLabelView.Font = (A.Font_AvenirNextDemiBold14);
            buttonLabelView.TextAlignment = UITextAlignment.Center;
            //this.Add (buttonLabelView); 
        }

        public void AddDeferMessageLabel ()
        {
            var buttonLabelView = new UILabel (new RectangleF (80, 177, 120, 16));
            buttonLabelView.TextColor = A.Color_11464F;
            buttonLabelView.Text = "Defer Message";
            buttonLabelView.Font = (A.Font_AvenirNextDemiBold14);
            buttonLabelView.TextAlignment = UITextAlignment.Center;
            this.Add (buttonLabelView); 
        }

        public void MakeButtonLabels ()
        {
            int i = 0;
            int j = 0;
            int k = 0;
            int horizontalSpacing = 0;
            int VerticalSpacing = 0;
            while (i < numRows) {
                while (j < numCols) {
                    var buttonLabelView = new UILabel (new RectangleF (7 + horizontalSpacing, 130 + VerticalSpacing, 80, 16));
                    //buttonLabelView.TextColor = A.Color_999999;
                    buttonLabelView.TextColor = UIColor.Black;
                    buttonLabelView.Text = buttonManager [k].buttonLabel;
                    buttonLabelView.Font = (A.Font_AvenirNextMedium14);
                    buttonLabelView.TextAlignment = UITextAlignment.Center;
                    this.Add (buttonLabelView);
                    horizontalSpacing += 90;
                    j++;
                    k++;
                }
                if (i == 0) {
                    VerticalSpacing += 50;
                }
                VerticalSpacing += 107;
                i++;
                j = 0;
                horizontalSpacing = 0;
            }
        }

        public List<UIButton> MakeActionButtons ()
        {
            int i = 0;
            int j = 0;
            int k = 0;
            int horizontalSpacing = 0;
            int verticalSpacing = 0;
            while (i < numRows) {
                while (j < numCols) {
                    var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
                    buttonRect.Tag = k;
                    buttonRect.Layer.CornerRadius = 35;
                    buttonRect.Layer.MasksToBounds = true;
                    //buttonRect.Layer.BorderColor = A.Color_999999.CGColor;
                    buttonRect.Layer.BorderColor = UIColor.Black.CGColor;
                    buttonRect.Layer.BorderWidth = 1.0f;
                    buttonRect.Frame = new RectangleF (12 + horizontalSpacing, 50 + verticalSpacing, 70, 70);
                    buttonRect.SetImage (UIImage.FromBundle (buttonManager [k].buttonIcon), UIControlState.Normal);
                    buttonRect.TouchUpInside += (object sender, EventArgs e) => {
                        buttonManager [buttonRect.Tag].buttonAction ();
                    };

                    ActionButtons.Add (buttonRect);
                    this.Add (buttonRect);
                    horizontalSpacing += 90;
                    j++;
                    k++;
                }

                if (i == 0) {
                    verticalSpacing += 50;
                }

                verticalSpacing += 107;
                i++;
                j = 0;
                horizontalSpacing = 0;
            }
            return ActionButtons;
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


