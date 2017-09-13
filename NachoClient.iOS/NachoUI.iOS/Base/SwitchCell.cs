//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    public class SwitchCell : SwipeTableViewCell, ThemeAdopter
    {

        public static nfloat PreferredHeight = 44.0f;
        public UISwitch Switch { get; private set; }
        public nfloat RightPadding = 10.0f;
        EventHandler SwitchHandler;

        public SwitchCell () : base ()
        {
            Initialize ();
        }

        public SwitchCell (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        void Initialize ()
        {

            Switch = new UISwitch ();
            ContentView.AddSubview (Switch);
        }

        public virtual void AdoptTheme (Theme theme)
        {
            TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var availableWidth = ContentView.Bounds.Width - SeparatorInset.Left - SeparatorInset.Right - RightPadding;
            var nameWidth = TextLabel.SizeThatFits (new CGSize (availableWidth, 0.0f)).Width;
            var switchSize = Switch.SizeThatFits (new CGSize (availableWidth, 0.0f));
            CGRect frame;
            frame = Switch.Frame;
            frame.X = ContentView.Bounds.Width - frame.Width - RightPadding;
            frame.Y = (ContentView.Bounds.Height - frame.Height) / 2.0f;
            Switch.Frame = frame;

            frame = TextLabel.Frame;
            frame.Width = Switch.Frame.X - frame.X;
            TextLabel.Frame = frame;
        }

        public void SetSwitchHandler (EventHandler handler)
        {
            if (SwitchHandler != null) {
                Switch.ValueChanged -= SwitchHandler;
            }
            SwitchHandler = handler;
            if (SwitchHandler != null) {
                Switch.ValueChanged += handler;
            }
        }
    }
}

