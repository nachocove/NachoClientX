﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using CoreAnimation;

namespace NachoClient.iOS
{
    public class ActionCheckboxView : UIView
    {
        
        UIView BoxView;
        UIImageView _CheckView;
        UIImageView CheckView {
            get {
                if (_CheckView == null) {
                    using (var image = UIImage.FromBundle ("action-checkmark")) {
                        _CheckView = new UIImageView (image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate));
                        _CheckView.TintColor = TintColor;
                        AddSubview (_CheckView);
                    }
                }
                return _CheckView;
            }
        }

        nfloat CheckboxSize;

        public ActionCheckboxView (float viewSize = 44.0f, float checkboxSize = 20.0f) : this (new CGRect (0.0f, 0.0f, (nfloat)viewSize, (nfloat)viewSize), checkboxSize)
        {
        }

        public ActionCheckboxView (CGRect frame, float checkboxSize = 20.0f) : base (frame)
        {
            CheckboxSize = (nfloat)checkboxSize;

            BoxView = new UIView ();
            BoxView.Layer.BorderWidth = 1.0f;
            BoxView.Layer.BorderColor = A.Color_NachoGreen.CGColor;

            AddSubview (BoxView);

            TintColor = A.Color_NachoGreen;
        }

        bool _IsChecked;
        public bool IsChecked {
            get {
                return _IsChecked;
            }
            set {
                _IsChecked = value;
                if (_IsChecked) {
                    CheckView.Hidden = false;
                    SetNeedsLayout ();
                } else {
                    if (_CheckView != null) {
                        _CheckView.RemoveFromSuperview ();
                        _CheckView = null;
                    }
                }
            }
        }

        public override void TintColorDidChange ()
        {
            BoxView.Layer.BorderColor = TintColor.CGColor;
            if (_CheckView != null) {
                _CheckView.TintColor = TintColor;
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            BoxView.Frame = new CGRect ((Bounds.Width - CheckboxSize) / 2.0f, (Bounds.Height - CheckboxSize) / 2.0f, CheckboxSize, CheckboxSize);
            BoxView.Layer.CornerRadius = CheckboxSize / 2.0f;
            if (_CheckView != null) {
                CheckView.Frame = BoxView.Frame;
            }
        }
    }
}

