//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;

using MimeKit;

using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface EmailAddressTokenTextFieldDelegate
    {
        void EmailAddressFieldAutocompleteText (EmailAddressTokenTextField field, string text);
        void EmailAddressFieldDidChange (EmailAddressTokenTextField field);
    }

    public class EmailAddressTokenTextField : TokenTextField<NcEmailAddress>
    {

        public EmailAddressTokenTextFieldDelegate EmailTokenDelegate {
            get {
                EmailAddressTokenTextFieldDelegate tokenDelegate;
                if (WeakTokenDelegate.TryGetTarget (out tokenDelegate)) {
                    return tokenDelegate;
                }
                return null;
            }
            set {
                WeakTokenDelegate.SetTarget (value);
            }
        }
        private WeakReference<EmailAddressTokenTextFieldDelegate> WeakTokenDelegate = new WeakReference<EmailAddressTokenTextFieldDelegate> (null);

        public EmailAddressTokenTextField () : base ()
        {
            Initialize ();
        }

        public EmailAddressTokenTextField (CGRect frame) : base (frame)
        {
            Initialize ();
        }

        public EmailAddressTokenTextField (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            KeyboardType = UIKeyboardType.EmailAddress;
            AutocapitalizationType = UITextAutocapitalizationType.None;
            AutocorrectionType = UITextAutocorrectionType.No;
        }

        public NcEmailAddress [] Addresses {
            get {
                return RepresentedObjects;
            }
            set {
                RepresentedObjects = value;
            }
        }

        public string AddressString {
            get {
                var addressList = new List<NcEmailAddress> (RepresentedObjects);
                return EmailHelper.AddressStringFromList (addressList);
            }
            set {
                var addressList = EmailHelper.AddressList (NcEmailAddress.Kind.Unknown, null, new string [] { value });
                RepresentedObjects = addressList.ToArray ();
            }
        }

        protected override UIView ViewForRepresentedObject (NcEmailAddress address)
        {
            var view = new UIView ();
            view.BackgroundColor = UIColor.Clear;
            var label = new UILabel ();
            label.BackgroundColor = TintColor;
            label.TextColor = UIColor.White;
            label.Font = Font;
            label.Text = address.address;
            label.LineBreakMode = UILineBreakMode.MiddleTruncation;
            label.Lines = 1;
            label.TextAlignment = UITextAlignment.Center;
            label.Layer.MasksToBounds = true;
            label.Layer.CornerRadius = 3.0f;
            var horizontalLabelPadding = 4.0f;
            var horizontalViewPadding = 2.0f;
            var horizontalPadding = horizontalViewPadding + horizontalLabelPadding;
            var size = label.SizeThatFits (new CGSize (TextContainer.Size.Width - 2.0f * horizontalPadding, Font.LineHeight));
            label.Frame = new CGRect (horizontalViewPadding, 0, size.Width + 2.0f * horizontalLabelPadding, Font.LineHeight);
            view.Frame = new CGRect (0, Font.Descender, label.Frame.Width + 2.0f * horizontalViewPadding, label.Frame.Height);
            view.AddSubview (label);
            return view;
        }

        protected override void Autocomplete (string text)
        {
            var emailTokenDelegate = EmailTokenDelegate;
            if (emailTokenDelegate != null) {
                emailTokenDelegate.EmailAddressFieldAutocompleteText (this, text);
            }
        }

        protected override NcEmailAddress RepresentedObjectForText (string text)
        {
            InternetAddressList addresses;
            if (InternetAddressList.TryParse (text, out addresses)) {
                foreach (var mailbox in addresses.Mailboxes) {
                    return new NcEmailAddress (NcEmailAddress.Kind.Unknown, mailbox.ToString ());
                }
            }
            return null;
        }

        protected override void DidChange ()
        {
            var emailTokenDelegate = EmailTokenDelegate;
            if (emailTokenDelegate != null) {
                emailTokenDelegate.EmailAddressFieldDidChange (this);
            }
        }
    }
}
