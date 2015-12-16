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
using Android.Views;
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ContactEditActivity")]
    public class ContactEditActivity : NcActivityWithData<McContact>, IContactEditFragmentOwner
    {
        private const string EXTRA_CONTACT = "com.nachocove.nachomail.EXTRA_CONTACT";

        private McContact contact;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            var contact = RetainedData;
            if (null == contact) {
                contact = IntentHelper.RetrieveValue<McContact> (Intent.GetStringExtra (EXTRA_CONTACT));
                RetainedData = contact;
            }
            this.contact = contact;
            SetContentView (Resource.Layout.ContactEditActivity);
        }

        public static Intent EditContactIntent (Context context, McContact contact)
        {
            var intent = new Intent (context, typeof(ContactEditActivity));
            intent.SetAction (Intent.ActionEdit);
            intent.PutExtra (EXTRA_CONTACT, IntentHelper.StoreValue (contact));
            return intent;
        }

        McContact IContactEditFragmentOwner.ContactToView {
            get {
                return this.contact;
            }
        }
    }
}
