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

        const float BUTTON_SIZE = 60;
        const float BUTTON_LABEL_HEIGHT = 20;
        const float BUTTON_PADDING_HEIGHT = 25;
        const float BUTTON_PADDING_WIDTH = 20;

        public void InitButtonManager (MessagePriorityViewController owner)
        {
            this.owner = owner;

            var buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                new ButtonInfo ("Later Today", "cup-48", () => owner.DelayRequest ("Later")),
                new ButtonInfo ("Tonight", "navbar-icn-defer", () => owner.DelayRequest ("Tonight")),
                new ButtonInfo ("Tomorrow", "navbar-icn-defer", () => owner.DelayRequest ("Tomorrow")),
                new ButtonInfo (null, null, null),
                new ButtonInfo ("Next Week", "navbar-icn-defer", () => owner.DelayRequest ("NextWeek")),
                new ButtonInfo ("Next Month", "navbar-icn-defer", () => owner.DelayRequest ("NextMonth")),
                new ButtonInfo ("Pick Date", "navbar-icn-defer", () => owner.DelayRequest ("Custom")),
                new ButtonInfo (null, null, null),
                null,
                null,
                new ButtonInfo ("None", "navbar-icn-defer", () => owner.DelayRequest ("None")),
            });

            var contentView = this;

            var center = contentView.Center;
            center.X = (this.Frame.Width / 2);
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
                this.Add (buttonRect);

                var label = new UILabel ();
                label.TextColor = A.Color_NachoBlack;
                label.Text = buttonInfo.buttonLabel;
                label.Font = A.Font_AvenirNextMedium14;
                label.TextAlignment = UITextAlignment.Center;
                label.SizeToFit ();
                label.Center = new PointF (xOffset, 5 + yOffset + ((BUTTON_SIZE + BUTTON_LABEL_HEIGHT) / 2));
                this.Add (label);

                xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
            }

            var dismissLabel = new UILabel ();
            dismissLabel.Text = "Dismiss";
            dismissLabel.TextColor = A.Color_NachoBlack;
            dismissLabel.Font = A.Font_AvenirNextRegular12;
            dismissLabel.TextAlignment = UITextAlignment.Center;
            dismissLabel.SizeToFit ();
            dismissLabel.Center = new PointF (320 / 2, contentView.Frame.Height - dismissLabel.Frame.Height);
            contentView.AddSubview (dismissLabel);

            var tap = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                owner.DismissViewController (true, null);
            });
            dismissLabel.AddGestureRecognizer (tap);
            dismissLabel.UserInteractionEnabled = true;

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

        public void AddDeferMessageLabel ()
        {
            var buttonLabelView = new UILabel (new RectangleF (80, 177, 120, 16));
            buttonLabelView.TextColor = A.Color_11464F;
            buttonLabelView.Text = "Defer Message";
            buttonLabelView.Font = (A.Font_AvenirNextDemiBold14);
            buttonLabelView.TextAlignment = UITextAlignment.Center;
            this.Add (buttonLabelView); 
        }

        //        public void MakeButtonLabels ()
        //        {
        //            int i = 0;
        //            int j = 0;
        //            int k = 0;
        //            int horizontalSpacing = 0;
        //            int VerticalSpacing = 0;
        //            while (i < numRows) {
        //                while (j < numCols) {
        //                    var buttonLabelView = new UILabel (new RectangleF (7 + horizontalSpacing, 130 + VerticalSpacing, 80, 16));
        //                    buttonLabelView.TextColor = UIColor.Black;
        //                    buttonLabelView.Text = buttonManager [k].buttonLabel;
        //                    buttonLabelView.Font = (A.Font_AvenirNextMedium14);
        //                    buttonLabelView.TextAlignment = UITextAlignment.Center;
        //                    this.Add (buttonLabelView);
        //                    horizontalSpacing += 90;
        //                    j++;
        //                    k++;
        //                }
        //                if (i == 0) {
        //                    VerticalSpacing += 50;
        //                }
        //                VerticalSpacing += 107;
        //                i++;
        //                j = 0;
        //                horizontalSpacing = 0;
        //            }
        //        }
        //
        //        public List<UIButton> MakeActionButtons ()
        //        {
        //            int i = 0;
        //            int j = 0;
        //            int k = 0;
        //            int horizontalSpacing = 0;
        //            int verticalSpacing = 0;
        //            while (i < numRows) {
        //                while (j < numCols) {
        //                    var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
        //                    buttonRect.Tag = k;
        //                    buttonRect.Layer.CornerRadius = 35;
        //                    buttonRect.Layer.MasksToBounds = true;
        //                    buttonRect.Layer.BorderColor = UIColor.Black.CGColor; //Testing new button colors
        //                    buttonRect.Layer.BorderWidth = 1.0f;
        //                    buttonRect.Frame = new RectangleF (12 + horizontalSpacing, 50 + verticalSpacing, 70, 70);
        //                    buttonRect.SetImage (UIImage.FromBundle (buttonManager [k].buttonIcon), UIControlState.Normal);
        //                    buttonRect.TouchUpInside += (object sender, EventArgs e) => {
        //                        buttonManager [buttonRect.Tag].buttonAction ();
        //                    };
        //
        //                    ActionButtons.Add (buttonRect);
        //                    this.Add (buttonRect);
        //                    horizontalSpacing += 90;
        //                    j++;
        //                    k++;
        //                }
        //
        //                if (i == 0) {
        //                    verticalSpacing += 50;
        //                }
        //
        //                verticalSpacing += 107;
        //                i++;
        //                j = 0;
        //                horizontalSpacing = 0;
        //            }
        //            return ActionButtons;
        //        }

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


