//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class ExpandableView : UIView
    {
        const float EXPAND_INSET = 5.0f;

        protected ExpandButton expandedButton;

        private float _CollapsedHeight;
        public float CollapsedHeight {
            get {
                return _CollapsedHeight;
            }
            set {
                _CollapsedHeight = value;
                if (!expandedButton.IsExpanded) {
                    Layout ();
                }
            }
        }

        private float _ExpandedHeight;
        public float ExpandedHeight {
            get {
                return _ExpandedHeight;
            }
            set {
                _ExpandedHeight = value;
                if (expandedButton.IsExpanded) {
                    Layout ();
                }
            }
        }

        public ExpandButton.StateChangedCallback OnStateChanged;

        public ExpandableView (RectangleF initialFrame, bool isExpanded) : base (initialFrame)
        {
            BackgroundColor = UIColor.White;
            ClipsToBounds = true;
            CheckFrameSize (initialFrame);
            var upperLeftCorner =
                new PointF (initialFrame.Width - EXPAND_INSET - ExpandButton.WIDTH, EXPAND_INSET);
            expandedButton = new ExpandButton (upperLeftCorner, isExpanded);
            AddSubview (expandedButton);

            expandedButton.StateChanged = (bool IsExpanded) => {
                Layout ();
                if (null != OnStateChanged) {
                    OnStateChanged (expandedButton.IsExpanded);
                }
            };
        }

        void CheckFrameSize (RectangleF frame)
        {
            NcAssert.True ((ExpandButton.WIDTH + EXPAND_INSET) < frame.Width);
            NcAssert.True ((ExpandButton.HEIGHT + EXPAND_INSET) < frame.Height);
        }

        void Layout ()
        {
            UIView.Animate (0.2, 0, UIViewAnimationOptions.CurveLinear, () => {
                ViewFramer.Create (this).Height (expandedButton.IsExpanded ? ExpandedHeight : CollapsedHeight);
            }, () => {
            });
        }
    }
}

