//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    public class ExpandButton : UIButton
    {
        public delegate void StateChangedCallback (bool IsExpanded);

        public const float WIDTH = 25.0f;
        public const float HEIGHT = 10.0f;

        private bool _Expanded;
        protected bool Expanded {
            get {
                return _Expanded;
            }
            set {
                _Expanded = value;
                BackgroundColor = _Expanded ? A.Color_NachoGreen : A.Color_NachoLightGrayBackground;
            }
        }

        public bool IsExpanded {
            get {
                return Expanded;
            }
            set {
                Expanded = value;
            }
        }

        public StateChangedCallback StateChanged;
            
        public ExpandButton (PointF upperLeftCorner, bool isExpanded = true) : base ()
        {
            Frame = new RectangleF (upperLeftCorner, new SizeF (WIDTH, HEIGHT));
            // FIXME - no image yet. just use background color. Add the UIImage when it is available
            Expanded = isExpanded;
            this.TouchDown += (object sender, EventArgs e) => {
                Expanded = !Expanded;
                StateChanged (IsExpanded);
            };
        }
    }
}

