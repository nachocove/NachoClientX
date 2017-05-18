//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreAnimation;
using CoreGraphics;

namespace NachoClient.iOS
{

    public class CornerLayer : CALayer
    {

        private CGColor _CornerBackgroundColor;
        private CGColor _CornerBorderColor;
        private nfloat _CornerBorderWidth = 0.0f;

        public CGColor CornerBackgroundColor {
            get {
                return _CornerBackgroundColor;
            }
            set {
                _CornerBackgroundColor = value;
                SetNeedsDisplay ();
            }
        }

        public CGColor CornerBorderColor {
            get {
                return _CornerBorderColor;
            }
            set {
                _CornerBorderColor = value;
                SetNeedsDisplay ();
            }
        }

        public nfloat CornerBorderWidth {
            get {
                return _CornerBorderWidth;
            }
            set {
                _CornerBorderWidth = value;
                SetNeedsDisplay ();
            }
        }

        public CornerLayer () : base ()
        {
            NeedsDisplayOnBoundsChange = true;
        }

        public override void DrawInContext (CGContext ctx)
        {
            var curveWidth = Bounds.Width - _CornerBorderWidth / 2.0f;
            var curveHeight = Bounds.Height - _CornerBorderWidth / 2.0f;
            var ellipseMagic = 0.5519f;
            ctx.SaveState ();
            ctx.ClearRect (Bounds);
            ctx.SetFillColor (CornerBackgroundColor);
            ctx.SetStrokeColor (CornerBorderColor);
            ctx.SetLineWidth (_CornerBorderWidth);
            ctx.BeginPath ();
            ctx.MoveTo (0.0f, 0.0f);
            ctx.AddLineToPoint (0.0f, Bounds.Height);
            ctx.AddLineToPoint (_CornerBorderWidth / 2.0f, Bounds.Height);
            ctx.AddCurveToPoint (_CornerBorderWidth, _CornerBorderWidth + curveHeight * (1.0f - ellipseMagic), _CornerBorderWidth + curveWidth * (1.0f - ellipseMagic), _CornerBorderWidth, Bounds.Width, _CornerBorderWidth / 2.0f);
            ctx.AddLineToPoint (Bounds.Width, 0.0f);
            ctx.ClosePath ();
            ctx.FillPath ();
            ctx.BeginPath ();
            ctx.MoveTo (_CornerBorderWidth / 2.0f, Bounds.Height);
            ctx.AddCurveToPoint (_CornerBorderWidth, _CornerBorderWidth + curveHeight * (1.0f - ellipseMagic), _CornerBorderWidth + curveWidth * (1.0f - ellipseMagic), _CornerBorderWidth, Bounds.Width, _CornerBorderWidth / 2.0f);
            ctx.StrokePath ();
            ctx.RestoreState ();
        }

    }
}

