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
        ContactsListFragment contactsListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.ContactsActivity);

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            contactsListFragment = ContactsListFragment.newInstance ();
            contactsListFragment.onContactClick += ContactsListFragment_onContactClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, contactsListFragment).AddToBackStack ("Contacts").Commit ();
        }

        void ContactsListFragment_onContactClick (object sender, McContact contact)
        {
            StartActivity (ContactViewActivity.ShowContactIntent (this, contact));
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is ContactsListFragment) {
                ((ContactsListFragment)f).OnBackPressed ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
