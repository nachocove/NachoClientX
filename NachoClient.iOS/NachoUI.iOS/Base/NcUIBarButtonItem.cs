//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public class NcUIBarButtonItem : UIBarButtonItem
    {
        protected void Record (object sender, EventArgs e)
        {
            NcApplication.Instance.TelemetryService.RecordUiBarButtonItem (AccessibilityLabel);
        }

        public NcUIBarButtonItem ()
        {
            Clicked += Record;
            Enabled = true;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item) : base (item)
        {
            Clicked += Record;
            Enabled = true;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item, EventHandler handler) : base (item, handler)
        {
            Clicked += Record;
            Enabled = true;
        }

        public NcUIBarButtonItem (UIBarButtonSystemItem item, NSObject obj, ObjCRuntime.Selector selector) : base (item, obj, selector)
        {
            Clicked += Record;
            Enabled = true;
        }

        // Xammit?  Apparent bug in overloading keeps the button disabled
        public NcUIBarButtonItem (UIImage image, UIBarButtonItemStyle style, EventHandler handler) : base ()
        {
            this.Image = image;
            this.Style = style;
            this.Clicked += handler;
            Clicked += Record;
            Enabled = true;
        }
    }
}

