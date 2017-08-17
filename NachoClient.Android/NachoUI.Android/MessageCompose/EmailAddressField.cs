//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;

using Com.Tokenautocomplete;

namespace NachoClient.AndroidClient
{

    public class EmailAddressFieldChangeArgs : EventArgs
    {

        EmailAddressField.TokenObject TokenObject;

        public EmailAddressFieldChangeArgs (EmailAddressField.TokenObject tokenObject) : base ()
        {
            TokenObject = tokenObject;
        }
    }

    public class EmailAddressField : TokenCompleteTextView, TokenCompleteTextView.ITokenListener
    {

        public event EventHandler<EmailAddressFieldChangeArgs> TokenAdded;
        public event EventHandler<EmailAddressFieldChangeArgs> TokenRemoved;
        public event EventHandler<EventArgs> TokensChanged;

        public class TokenObject : Java.Lang.Object
        {

            public NcEmailAddress EmailAddress { get; set; }

            public McContact Contact { get; set; }

            string AddressStringInvariant {
                get {
                    if (Contact != null) {
                        return Contact.GetPrimaryCanonicalEmailAddress ().ToLowerInvariant ();
                    }
                    return EmailAddress.address.ToLowerInvariant ();
                }
            }

            public TokenObject (NcEmailAddress address) : base ()
            {
                EmailAddress = address;
                Contact = null;
            }

            public TokenObject (McContact contact, NcEmailAddress address) : base ()
            {
                Contact = contact;
                EmailAddress = address;
            }

            public override bool Equals (Java.Lang.Object o)
            {
                var b = o as TokenObject;
                if (b != null) {
                    return AddressStringInvariant.Equals (b.AddressStringInvariant);
                }
                return false;
            }
        }

        public EmailAddressField (Context context) : base (context)
        {
            Initialize ();
        }

        public EmailAddressField (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            Initialize ();
        }

        void Initialize ()
        {
            Threshold = 1;
            InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationEmailAddress;
            SetTokenListener (this);
        }

        protected override View GetViewForObject (Java.Lang.Object p0)
        {
            var wrapper = p0 as TokenObject;
            if (wrapper != null) {
                var textView = LayoutInflater.From (Context).Inflate (Resource.Layout.EmailAddressToken, null) as TextView;
                if (wrapper.Contact != null) {
                    var displayName = wrapper.Contact.GetDisplayName ();
                    if (!String.IsNullOrWhiteSpace (displayName)) {
                        textView.Text = displayName;
                    } else {
                        textView.Text = wrapper.EmailAddress.address;
                    }
                } else {
                    var mailbox = wrapper.EmailAddress.ToMailboxAddress ();
                    if (null == mailbox) {
                        textView.Text = wrapper.EmailAddress.address;
                    } else if (!String.IsNullOrEmpty (mailbox.Name)) {
                        textView.Text = mailbox.Name;
                    } else {
                        textView.Text = mailbox.Address;
                    }
                }
                return textView;
            }
            return null;
        }

        protected override Java.Lang.Object DefaultObject (string p0)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.Unknown, p0);
            var wrapper = new TokenObject (address);
            return wrapper;
        }

        public void OnTokenAdded (Java.Lang.Object o)
        {
            var wrapper = o as TokenObject;
            if (TokenAdded != null) {
                TokenAdded (this, new EmailAddressFieldChangeArgs (wrapper));
            }
            if (TokensChanged != null) {
                TokensChanged (this, new EventArgs ());
            }
        }

        public void OnTokenRemoved (Java.Lang.Object o)
        {
            var wrapper = o as TokenObject;
            if (TokenRemoved != null) {
                TokenRemoved (this, new EmailAddressFieldChangeArgs (wrapper));
            }
            if (TokensChanged != null) {
                TokensChanged (this, new EventArgs ());
            }
        }

        public string AddressString {
            get {
                var addresses = new List<NcEmailAddress> (Objects.Count);
                foreach (var obj in Objects) {
                    var wrapper = obj as TokenObject;
                    addresses.Add (wrapper.EmailAddress);
                }
                return EmailHelper.AddressStringFromList (addresses);
            }
            set {
                Clear ();
                var addresses = EmailHelper.AddressList (NcEmailAddress.Kind.Unknown, null, value);
                foreach (var address in addresses) {
                    AddObject (new TokenObject (address));
                }
            }
        }

        public List<NcEmailAddress> AddressList {
            get {
                var addresses = new List<NcEmailAddress> (Objects.Count);
                foreach (var obj in Objects) {
                    var wrapper = obj as TokenObject;
                    addresses.Add (wrapper.EmailAddress);
                }
                return addresses;
            }
            set {
                Clear ();
                if (null != value) {
                    foreach (var address in value) {
                        AddObject (new TokenObject (address));
                    }
                }
            }
        }

    }
}

