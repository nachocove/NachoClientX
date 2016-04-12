//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class UnreadMessageIndicator : UIView
    {

        public enum MessageState
        {
            Read,
            Unread
        }

        MessageState _State = MessageState.Unread;
        public MessageState State {
            get {
                return _State;
            }
            set {
                _State = value;
                SetNeedsDisplay ();
            }
        }

        UIColor _Color = A.Color_NachoGreen;
        public UIColor Color {
            get {
                return _Color;
            }
            set {
                _Color = value;
                SetNeedsDisplay ();
            }
        }

        nfloat circleSize = 9.0f;

        public UnreadMessageIndicator (CGRect frame) : base(frame)
        {
        }

        public override void Draw (CGRect rect)
        {
            base.Draw (rect);
            var context = UIGraphics.GetCurrentContext ();
            context.SaveState ();
            if (_State == MessageState.Read) {
                nfloat lineWidth = 1.0f;
                var adjustedCircleSize = circleSize - lineWidth;
                var circleRect = new CGRect ((Bounds.Width - adjustedCircleSize) / 2.0f, (Bounds.Height - adjustedCircleSize) / 2.0f, adjustedCircleSize, adjustedCircleSize);
                context.SetStrokeColor (_Color.CGColor);
                context.SetLineWidth (lineWidth);
                context.StrokeEllipseInRect (circleRect);
            } else {
                var circleRect = new CGRect ((Bounds.Width - circleSize) / 2.0f, (Bounds.Height - circleSize) / 2.0f, circleSize, circleSize);
                context.SetFillColor (_Color.CGColor);
                context.FillEllipseInRect (circleRect);
            }
            context.RestoreState ();
        }
    }
}

