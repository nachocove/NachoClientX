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

        public static Intent AddContactIntent (Context context)
        {
            var intent = new Intent (context, typeof(ContactEditActivity));
            intent.SetAction (Intent.ActionCreateDocument);
            return intent;
        }

        public static Intent EditContactIntent (Context context, McContact contact)
        {
            var intent = new Intent (context, typeof(ContactEditActivity));
            intent.SetAction (Intent.ActionEdit);
            intent.PutExtra (EXTRA_CONTACT, IntentHelper.StoreValue (contact));
            return intent;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            var contact = RetainedData;
            if (null == contact) {
                if (Intent.HasExtra (EXTRA_CONTACT)) {
                    contact = IntentHelper.RetrieveValue<McContact> (Intent.GetStringExtra (EXTRA_CONTACT));
                    RetainedData = contact;
                }
            }
            this.contact = contact;
            SetContentView (Resource.Layout.ContactEditActivity);
        }

        public override void OnBackPressed ()
        {
            new Android.Support.V7.App.AlertDialog.Builder (this)
                .SetTitle ("Are You Sure?")
                .SetMessage ("The contact will not be saved.")
                .SetPositiveButton ("Yes", (object sender, DialogClickEventArgs e) => {
                base.OnBackPressed ();
            })
                .SetNegativeButton ("Cancel", (EventHandler<DialogClickEventArgs>)null)
                .Create ()
                .Show ();
        }

        McContact IContactEditFragmentOwner.ContactToView {
            get {
                return this.contact;
            }
        }
    }
}
