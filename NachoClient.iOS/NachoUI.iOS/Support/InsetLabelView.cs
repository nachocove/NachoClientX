//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    public class InsetLabelView : UIView
    {
        public readonly UILabel Label;
        public UIEdgeInsets LabelInsets;

        public InsetLabelView () : base ()
        {
            Label = new UILabel ();
            AddSubview (Label);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            Label.Frame = new CGRect (LabelInsets.Left, LabelInsets.Top, Bounds.Width - LabelInsets.Left - LabelInsets.Right, Bounds.Height - LabelInsets.Top - LabelInsets.Bottom);
        }

        public nfloat PreferredHeight {
            get {
                return Label.Font.RoundedLineHeight (1.0f) + LabelInsets.Top + LabelInsets.Bottom;
            }
        }
    }
}

