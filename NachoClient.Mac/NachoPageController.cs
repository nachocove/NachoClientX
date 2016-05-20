//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using AppKit;

namespace NachoClient.Mac
{
    [Foundation.Register("NachoPageController")]
    public class NachoPageController : NSViewController
    {

        List<NSViewController> ViewControllers;
        
        public NachoPageController (IntPtr handle) : base (handle)
        {
            ViewControllers = new List<NSViewController> ();
        }

        public void PushViewController (NSViewController viewController, bool animated = true)
        {
            animated = false; // TODO: support animation
            viewController.View.Frame = View.Bounds;
            AddChildViewController (viewController);
            View.AddSubview (viewController.View);
            NSViewController topViewController = null;
            if (ViewControllers.Count > 0) {
                topViewController = ViewControllers [ViewControllers.Count - 1];
            }
            ViewControllers.Add (viewController);
            if (animated) {
            } else {
                if (topViewController != null) {
                    topViewController.View.RemoveFromSuperview ();
                }
            }
        }

        public void PopViewController (bool animated = true)
        {
            animated = false; // TODO: support animation
            if (ViewControllers.Count > 0) {
                var topViewController = ViewControllers [ViewControllers.Count - 1];
                if (animated) {
                } else {
                    topViewController.RemoveFromParentViewController ();
                    topViewController.View.RemoveFromSuperview ();
                    ViewControllers.RemoveAt (ViewControllers.Count - 1);
                    var viewController = ViewControllers [ViewControllers.Count - 1];
                    View.AddSubview (viewController.View);
                }
            }
        }
    }
}

