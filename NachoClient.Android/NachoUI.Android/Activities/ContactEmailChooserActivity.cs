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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ContactEmailChooserActivity")]
    public class ContactEmailChooserActivity : NcActivity
    {
        private const string EXTRA_SEARCH_STRING = "com.nachocove.nachomail.EXTRA_SEARCH_STRING";
        private const string EXTRA_RESULT_EMAIL = "com.nachocove.nachomail.EXTRA_RESULT_EMAIL";
        private const string EXTRA_RESULT_CONTACT = "com.nachocove.nachomail.EXTRA_RESULT_CONTACT";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.ContactEmailchooserActivity);

            string initialSearch = "";
            if (Intent.HasExtra (EXTRA_SEARCH_STRING)) {
                initialSearch = Intent.GetStringExtra (EXTRA_SEARCH_STRING);
            }

            var fragment = FragmentManager.FindFragmentById<ContactEmailChooserFragment> (Resource.Id.contact_email_chooser_fragment);
            fragment.SetInitialValues (initialSearch);
        }

        public static Intent EmptySearchIntent (Context context)
        {
            var intent = new Intent (context, typeof(ContactEmailChooserActivity));
            intent.SetAction (Intent.ActionSearch);
            return intent;
        }

        public static Intent SearchIntent (Context context, string initialSearchString)
        {
            var intent = new Intent (context, typeof(ContactEmailChooserActivity));
            intent.SetAction (Intent.ActionSearch);
            intent.PutExtra (EXTRA_SEARCH_STRING, initialSearchString);
            return intent;
        }

        public static Intent ResultIntent (string emailAddress, McContact contact)
        {
            var intent = new Intent ();
            intent.PutExtra (EXTRA_RESULT_EMAIL, emailAddress);
            intent.PutExtra (EXTRA_RESULT_CONTACT, IntentHelper.StoreValue (contact));
            return intent;
        }

        public static void GetSearchResults (Intent resultIntent, out string emailAddress, out McContact contact)
        {
            emailAddress = null;
            contact = null;
            if (resultIntent.HasExtra (EXTRA_RESULT_EMAIL)) {
                emailAddress = resultIntent.GetStringExtra (EXTRA_RESULT_EMAIL);
            }
            if (resultIntent.HasExtra (EXTRA_RESULT_CONTACT)) {
                contact = IntentHelper.RetrieveValue<McContact> (resultIntent.GetStringExtra (EXTRA_RESULT_CONTACT));
            }
        }
    }
}

