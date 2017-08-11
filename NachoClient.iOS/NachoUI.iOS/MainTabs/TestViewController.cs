//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;

using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class TestViewController : UIViewController, EmailAddressTokenTextFieldDelegate
    {

        EmailAddressTokenTextField EmailField;
        UITextView TextField;

        public TestViewController () : base ()
        {
        }

        public override void LoadView ()
        {
            View = new UIView ();
            EmailField = new EmailAddressTokenTextField ();
            EmailField.Font = UIFont.SystemFontOfSize (17.0f);
            EmailField.ScrollEnabled = false;
            EmailField.Changed += (sender, e) => {
                var frame = EmailField.Frame;
                var size = EmailField.SizeThatFits (new CGSize (View.Bounds.Width, nfloat.MaxValue));
                if (size.Height != frame.Height) {
                    View.SetNeedsLayout ();
                    View.LayoutIfNeeded ();
                }
            };
            EmailField.EmailTokenDelegate = this;
            TextField = new UITextView ();
            TextField.Editable = false;
            TextField.Font = UIFont.FromName ("Courier New", 17.0f);
            TextField.BackgroundColor = UIColor.FromWhiteAlpha (0.8f, 1.0f);
            TextField.TextColor = UIColor.Blue;
            View.AddSubview (EmailField);
            View.AddSubview (TextField);
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            var size = EmailField.SizeThatFits (new CGSize (View.Bounds.Width, nfloat.MaxValue));
            EmailField.Frame = new CGRect (0, 20.0f, View.Bounds.Width, size.Height);
            var y = EmailField.Frame.Top + EmailField.Frame.Height;
            TextField.Frame = new CGRect (0, y, View.Bounds.Width, View.Bounds.Height - y);
        }

        public void EmailAddressFieldAutocompleteText (EmailAddressTokenTextField field, string text)
        {
        }

        public void EmailAddressFieldDidChange (EmailAddressTokenTextField field)
        {
            TextField.Text = EmailField.AddressString;
        }

        public void EmailAddressFieldDidRequsetDetails (EmailAddressTokenTextField field)
        {
        }

    }
}
