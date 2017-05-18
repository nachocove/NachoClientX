//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    public class NameValueCell : SwipeTableViewCell, ThemeAdopter
    {

        public static nfloat PreferredHeight = 44.0f;
        public UILabel ValueLabel { get; private set; }
        public nfloat RightPadding = 10.0f;

        public NameValueCell () : base ()
        {
            Initialize ();
        }

        public NameValueCell (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        void Initialize ()
        {

            ValueLabel = new UILabel ();
            ValueLabel.Lines = 1;
            ValueLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            ContentView.AddSubview(ValueLabel);
        }

        public virtual void AdoptTheme (Theme theme)
        {
            TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            ValueLabel.Font = theme.DefaultFont.WithSize (14.0f);
            ValueLabel.TextColor = theme.DefaultTextColor;
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var availableWidth = ContentView.Bounds.Width - SeparatorInset.Left - SeparatorInset.Right - RightPadding;
            var nameWidth = TextLabel.SizeThatFits (new CGSize (availableWidth, 0.0f)).Width;
            var valueWidth = ValueLabel.SizeThatFits (new CGSize (availableWidth, 0.0f)).Width;
            CGRect frame;
            if (nameWidth + valueWidth > availableWidth) {
                if (nameWidth < availableWidth) {
                    valueWidth = availableWidth - nameWidth;
                } else if (valueWidth < availableWidth) {
                    nameWidth = availableWidth - valueWidth;
                } else {
                    nameWidth = valueWidth = availableWidth / 2.0f;
                }
            }
            frame = TextLabel.Frame;
            frame.Width = nameWidth;
            TextLabel.Frame = frame;

            frame = ValueLabel.Frame;
            frame.Width = valueWidth;
            frame.Height = ValueLabel.Font.LineHeight;
            frame.X = ContentView.Bounds.Width - frame.Width - RightPadding;
            frame.Y = (ContentView.Bounds.Height - frame.Height) / 2.0f;
            ValueLabel.Frame = frame;
        }
    }
}

