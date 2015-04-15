//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUIBarButtonItem : UIBarButtonItem
    {
        protected void Record (object sender, EventArgs e)
        {
            Telemetry.RecordUiBarButtonItem (AccessibilityLabel);
        }

        public NcUIBarButtonItem ()
        {
            Clicked += Record;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item) : base (item)
        {
            Clicked += Record;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item, EventHandler handler) : base (item, handler)
        {
            Clicked += Record;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item, NSObject obj, ObjCRuntime.Selector selector) : base (item, obj, selector)
        {
            Clicked += Record;
        }

        public NcUIBarButtonItem (UIImage image, UIBarButtonItemStyle style, EventHandler handler) : base (image, style, handler)
        {
            Clicked += Record;
        }
    }
}

