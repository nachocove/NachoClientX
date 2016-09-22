//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class DatePickerView : UIView, ThemeAdopter
    {

        UIDatePicker Picker;
        UIButton CancelButton;
        UIButton ClearButton;
        UIButton SetButton;
        UIView BackgroundView;
        UIView PanelView;
        UITapGestureRecognizer BackgroundTapRecognizer;
        UIView TopBorder;
        UIView PickerBorder;

        nfloat ButtonHeight = 40.0f;
        nfloat BorderWidth = 0.5f;

        public Action<DateTime> Picked;
        public Action Canceled;

        DateTime? OriginalDate;

        public DateTime Date {
            get {
                if (Picker.Date != null) {
                    return Picker.Date.ToDateTime ();
                }
                return default(DateTime);
            }
            set {
                OriginalDate = value;
                Picker.Date = value.ToNSDate ();
                UpdateSetButton ();
            }
        }

        public DatePickerView (CGRect frame) : base (frame)
        {
            BackgroundColor = UIColor.Clear;
            BackgroundView = new UIView (Bounds);
            BackgroundView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BackgroundView.BackgroundColor = UIColor.Black.ColorWithAlpha (0.4f);
            BackgroundTapRecognizer = new UITapGestureRecognizer (BackgroundTap);
            BackgroundView.AddGestureRecognizer (BackgroundTapRecognizer);

            Picker = new UIDatePicker ();

            nfloat pickerHeight = Picker.IntrinsicContentSize.Height;
            nfloat panelHeight = pickerHeight + ButtonHeight + BorderWidth + BorderWidth;

            PanelView = new UIView (new CGRect(0.0f, Bounds.Height - pickerHeight, Bounds.Width, panelHeight));
            PanelView.BackgroundColor = UIColor.White;

            TopBorder = new UIView (new CGRect (0.0f, 0.0f, PanelView.Bounds.Width, BorderWidth));
            TopBorder.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            TopBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);

            CancelButton = new UIButton (UIButtonType.Custom);
            CancelButton.Frame = new CGRect (0.0f, BorderWidth, PanelView.Bounds.Width / 2.0f, ButtonHeight);
            CancelButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin;
            CancelButton.SetTitle ("Cancel", UIControlState.Normal);
            CancelButton.TouchUpInside += Cancel;

            ClearButton = new UIButton (UIButtonType.Custom);
            ClearButton.Frame = new CGRect (CancelButton.Frame.Width, BorderWidth, PanelView.Bounds.Width / 2.0f, ButtonHeight);
            ClearButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin;
            ClearButton.SetTitle ("Clear", UIControlState.Normal);
            ClearButton.TouchUpInside += Clear;
            ClearButton.Hidden = true;

            SetButton = new UIButton (UIButtonType.Custom);
            SetButton.Frame = new CGRect (CancelButton.Frame.Width, BorderWidth, PanelView.Bounds.Width / 2.0f, ButtonHeight);
            SetButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin;
            SetButton.SetTitle ("Set", UIControlState.Normal);
            SetButton.TouchUpInside += Set;

            PickerBorder = new UIView (new CGRect (0.0f, CancelButton.Frame.Y + CancelButton.Frame.Height, PanelView.Bounds.Width, BorderWidth));
            PickerBorder.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            PickerBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.075f);

            Picker.Frame = new CGRect (0.0f, PickerBorder.Frame.Y + PickerBorder.Frame.Height, Bounds.Width, pickerHeight);
            Picker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            Picker.Mode = UIDatePickerMode.Date;
            Picker.ValueChanged += PickerChanged;

            PanelView.AddSubview (TopBorder);
            PanelView.AddSubview (PickerBorder);
            PanelView.AddSubview (CancelButton);
            PanelView.AddSubview (ClearButton);
            PanelView.AddSubview (SetButton);
            PanelView.AddSubview (Picker);

            AddSubview (BackgroundView);
            AddSubview (PanelView);
        }

        public void AdoptTheme (Theme theme)
        {
            var font = theme.MediumDefaultFont.WithSize (14.0f);
            var color = theme.ButtonTextColor;
            CancelButton.Font = font;
            CancelButton.SetTitleColor (color, UIControlState.Normal);
            ClearButton.Font = font;
            ClearButton.SetTitleColor (color, UIControlState.Normal);
            SetButton.Font = font;
            SetButton.SetTitleColor (color, UIControlState.Normal);
        }

        void PickerChanged (object sender, EventArgs e)
        {
            UpdateSetButton ();
        }

        void UpdateSetButton ()
        {
            bool showClearButton = false;
            if (OriginalDate.HasValue) {
                var date = Date;
                if (OriginalDate.Value.Year == date.Year && OriginalDate.Value.Month == date.Month && OriginalDate.Value.Day == date.Day) {
                    showClearButton = true;
                }
            }
            ClearButton.Hidden = !showClearButton;
            SetButton.Hidden = showClearButton;
        }

        public void Cleanup ()
        {
            CancelButton.TouchUpInside -= Cancel;
            ClearButton.TouchUpInside -= Clear;
            SetButton.TouchUpInside -= Set;
            Picker.ValueChanged -= PickerChanged;
            BackgroundView.RemoveGestureRecognizer (BackgroundTapRecognizer);
            BackgroundTapRecognizer = null;
        }

        void Cancel (object sender, EventArgs e)
        {
            Canceled ();
        }

        void BackgroundTap ()
        {
            Canceled ();
        }

        void Clear (object sender, EventArgs e)
        {
            Picked (default(DateTime));
        }

        void Set (object sender, EventArgs e)
        {
            Picked (Date);
        }

        public void Present ()
        {
            BackgroundView.Alpha = 0.0f;
            PanelView.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height + PanelView.Frame.Height / 2.0f);
            UIView.Animate (0.25f, () => {
                BackgroundView.Alpha = 1.0f;
                PanelView.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height - PanelView.Frame.Height / 2.0f);
            });
        }

        public void Dismiss (Action completionHandler)
        {
            UIView.Animate (0.25f, () => {
                BackgroundView.Alpha = 0.0f;
                PanelView.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height + PanelView.Frame.Height / 2.0f);
            }, completionHandler);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            PanelView.Frame = new CGRect (0.0f, Bounds.Height - PanelView.Frame.Height, Bounds.Width, PanelView.Frame.Height);
        }
    }
}

