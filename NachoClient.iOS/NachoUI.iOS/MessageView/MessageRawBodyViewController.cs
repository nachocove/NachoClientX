//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class MessageRawBodyViewController : UIViewController
    {

        public string BodyContents;
        UITextView TextView;

        public MessageRawBodyViewController () : base ()
        {
        }

        public override void LoadView ()
        {
            base.LoadView ();
            View.BackgroundColor = UIColor.White;
            TextView = new UITextView (View.Bounds);
            TextView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            TextView.Editable = false;
            TextView.Font = UIFont.FromName ("Courier New", 14.0f);
            View.AddSubview (TextView);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            TextView.Text = BodyContents;
        }
    }
}

