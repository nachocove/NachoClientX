//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    [Register ("NcAdjustableLayoutTextField")]
    public class NcAdjustableLayoutTextField : UITextField
    {

        public CGRect? AdjustedLeftViewRect;
        public CGRect? AdjustedRightViewRect;
        public UIEdgeInsets? AdjustedEditingInsets;

        public NcAdjustableLayoutTextField (IntPtr handle) : base (handle)
        {
        }

        public NcAdjustableLayoutTextField (CGRect frame) : base (frame)
        {
        }

        public override CGRect LeftViewRect (CGRect forBounds)
        {
            if (AdjustedLeftViewRect.HasValue) {
                return AdjustedLeftViewRect.Value;
            }
            return base.LeftViewRect (forBounds);
        }

        public override CGRect RightViewRect (CGRect forBounds)
        {
            if (AdjustedRightViewRect.HasValue) {
                return AdjustedRightViewRect.Value;
            }
            return base.RightViewRect (forBounds);
        }

        public override CGRect EditingRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.EditingRect (forBounds);
        }

        public override CGRect TextRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.TextRect (forBounds);
        }

        public override CGRect PlaceholderRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.PlaceholderRect (forBounds);
        }
    }
}

