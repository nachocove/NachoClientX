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

        public const float WIDTH = 30.0f;
        public const float HEIGHT = 20.0f;

        private bool _Expanded;
        protected bool Expanded {
            get {
                return _Expanded;
            }
            set {
                _Expanded = value;
                SetImage (_Expanded ? UIImage.FromBundle ("gen-readmore-active") :
                    UIImage.FromBundle ("gen-readmore"), UIControlState.Normal);
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
            Expanded = isExpanded;
            this.TouchDown += (object sender, EventArgs e) => {
                Expanded = !Expanded;
                StateChanged (IsExpanded);
            };
        }
    }
}

