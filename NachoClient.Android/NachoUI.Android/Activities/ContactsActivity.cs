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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ContactsActivity")]            
    public class ContactsActivity : NcActivity
    {
        ContactViewFragment contactViewFragment;
        ContactsListFragment contactsListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.ContactsActivity);

            contactsListFragment = ContactsListFragment.newInstance ();
            contactsListFragment.onContactClick += ContactsListFragment_onContactClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, contactsListFragment).AddToBackStack ("Contacts").Commit ();
        }

        void ContactsListFragment_onContactClick (object sender, McContact contact)
        {
            Console.WriteLine ("ContactsListFragment_onContactClick: {0}", contact);
            contactViewFragment = ContactViewFragment.newInstance (contact);
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, contactViewFragment).AddToBackStack ("View").Commit ();
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is ContactViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
