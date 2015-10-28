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

    public class EmailAddressField : TokenCompleteTextView
    {

        public class TokenObject : Java.Lang.Object
        {

            public NcEmailAddress EmailAddress { get; set; }
            public McContact Contact { get; set; }

            public TokenObject (NcEmailAddress address) : base()
            {
                EmailAddress = address;
                Contact = null;
            }

            public TokenObject (McContact contact) : base()
            {
                Contact = contact;
                EmailAddress = null;
            }
        }

        public EmailAddressField (Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Threshold = 1;
        }

        protected override View GetViewForObject (Java.Lang.Object p0)
        {
            var wrapper = p0 as TokenObject;
            if (wrapper != null) {
                var inflater = Context.GetSystemService (Activity.LayoutInflaterService) as LayoutInflater;
                var view = inflater.Inflate (Resource.Layout.EmailAddressToken, null);
                var textView = view.FindViewById<TextView> (Resource.Id.email_address_token_text);
                if (wrapper.Contact != null) {
                    textView.Text = wrapper.Contact.GetDisplayNameOrEmailAddress ();
                } else {
                    textView.Text = wrapper.EmailAddress.address;
                }
                return view;
            }
            return null;
        }

        protected override Java.Lang.Object DefaultObject (string p0)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.Unknown, p0);
            var wrapper = new TokenObject (address);
            return wrapper;
        }

    }
}

