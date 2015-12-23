//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using Foundation;
using UIKit;

namespace NachoClientShare.iOS
{
    public partial class ShareViewController : UIViewController
    {
        public ShareViewController (IntPtr handle) : base (handle)
        {
        }

        public override void DidReceiveMemoryWarning ()
        {
            // Releases the view if it doesn't have a superview.
            base.DidReceiveMemoryWarning ();

            // Release any cached data, images, etc that aren't in use.
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Do any additional setup after loading the view.
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            OpenApp ();
            ExtensionContext.CompleteRequest (null, null);
        }

        void OpenApp ()
        {
            UIResponder responder = this;
            var openURL = new ObjCRuntime.Selector ("openURL:");
            while (responder != null && !responder.RespondsToSelector (openURL)) {
                responder = responder.NextResponder;
            }
            if (responder != null) {
                var url = new NSUrl ("com.nachocove.nachomail://");
                responder.PerformSelector (openURL, url);
            }
        }
    }
}

