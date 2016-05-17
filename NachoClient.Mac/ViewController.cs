//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using AppKit;
using Foundation;

namespace NachoClient.Mac
{
    public partial class ViewController : NSViewController
    {
        public ViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Do any additional setup after loading the view.
        }

        public override NSObject RepresentedObject {
            get {
                return base.RepresentedObject;
            }
            set {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }
    }
}
