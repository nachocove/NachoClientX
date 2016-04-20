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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ContactsActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class ContactsActivity : NcTabBarActivity
    {
        private const string CONTACTS_LIST_FRAGMENT_TAG = "ContactsListFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.ContactsActivity);

            ContactsListFragment fragment = null;
            if (null != bundle) {
                fragment = FragmentManager.FindFragmentByTag<ContactsListFragment> (CONTACTS_LIST_FRAGMENT_TAG);
            }
            if (null == fragment) {
                fragment = ContactsListFragment.newInstance ();
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, fragment, CONTACTS_LIST_FRAGMENT_TAG).Commit ();
            }
            fragment.onContactClick += ContactsListFragment_onContactClick;
        }

        void ContactsListFragment_onContactClick (object sender, McContact contact)
        {
            StartActivity (ContactViewActivity.ShowContactIntent (this, contact));
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is ContactsListFragment) {
                ((ContactsListFragment)f).OnBackPressed ();
            }
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
