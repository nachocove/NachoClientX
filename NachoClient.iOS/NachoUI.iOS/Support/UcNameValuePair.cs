﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;

namespace NachoClient.iOS
{
    public class UcNameValuePair : UIView
    {
        const int SPACER = 15;

        UIColor labelColor = A.Color_NachoDarkText;
        UIFont labelFont = A.Font_AvenirNextRegular14;

        UIColor valueColor = A.Color_NachoGreen;
        UIFont valueFont = A.Font_AvenirNextMedium14;

        UIView overlayView;
        UITapGestureRecognizer TapGesture;
        UITapGestureRecognizer.Token TapGestureHandlerToken;

        UILabel labelLabel;
        UILabel valueLabel;

        public UcNameValuePair (CGRect rect, string labelString, nfloat leftPadding, nfloat rightPadding, Action<NSObject> onSelected) : base (rect)
        {
            var rightMargin = rect.Width - rightPadding;
            if (null != onSelected) {
                UIImageView disclosureArrowImageView;
                using (var disclosureArrowIcon = UIImage.FromBundle ("gen-more-arrow")) {
                    disclosureArrowImageView = new UIImageView (disclosureArrowIcon);
                }
                rightMargin = rightMargin - disclosureArrowImageView.Frame.Width;
                disclosureArrowImageView.Frame = new CGRect (rightMargin, 0, disclosureArrowImageView.Frame.Width, disclosureArrowImageView.Frame.Height);
                disclosureArrowImageView.Center = new CGPoint (disclosureArrowImageView.Center.X, rect.Height / 2);
                this.AddSubview (disclosureArrowImageView);
                rightMargin -= 5;
            }

            var leftMargin = leftPadding;

            labelLabel = new UILabel (new CGRect (leftMargin, 0, 1, rect.Height));
            labelLabel.Font = labelFont;
            labelLabel.TextColor = labelColor;
            labelLabel.TextAlignment = UITextAlignment.Left;
            labelLabel.Text = labelString;
            labelLabel.SizeToFit ();
            if (labelLabel.Frame.X + labelLabel.Frame.Width > rightMargin) {
                labelLabel.Frame = new CGRect(labelLabel.Frame.X, labelLabel.Frame.Y, rightMargin - labelLabel.Frame.X, labelLabel.Frame.Height);
            }
            ViewFramer.Create (labelLabel).Height (rect.Height);
            this.AddSubview (labelLabel);

            leftMargin += labelLabel.Frame.Width + SPACER;

            valueLabel = new UILabel (new CGRect (leftMargin, 0, rightMargin - leftMargin, rect.Height));
            valueLabel.Font = valueFont;
            valueLabel.TextColor = valueColor;
            valueLabel.TextAlignment = UITextAlignment.Right;
            valueLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            valueLabel.Text = "";
            this.AddSubview (valueLabel);

            if (null != onSelected) {
                overlayView = new UIView (new CGRect (0, 0, rect.Width, rect.Height));
                overlayView.BackgroundColor = UIColor.Clear;
                overlayView.UserInteractionEnabled = true;
                this.AddSubview (overlayView);
                TapGesture = new UITapGestureRecognizer ();
                TapGestureHandlerToken = TapGesture.AddTarget (onSelected);
                overlayView.AddGestureRecognizer (TapGesture);
            }
        }

        public void SetValue (string valueString)
        {
            valueLabel.Text = valueString;
        }

        public void SetLabel (string labelString)
        {
            labelLabel.Text = labelString;
        }

        public void Cleanup ()
        {
            if (null != TapGesture) {
                TapGesture.RemoveTarget (TapGestureHandlerToken);
                overlayView.RemoveGestureRecognizer (TapGesture);
            }
            overlayView = null;
        }
    }
}

