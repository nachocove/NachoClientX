using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Support.V4.Widget;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using NachoClient;
using NachoCore.Utils;
using System;
using System.Linq;
using Android.App;

namespace NachoClient.AndroidClient
{
    public class ContactListFragment : Android.Support.V4.App.Fragment
    {
        INachoContacts contacts;
        ContactListAdapter adapter;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.HasOptionsMenu = true;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);
            var listview = rootView.FindViewById<ListView> (Resource.Id.listview);

            contacts = NcContactManager.Instance.GetNachoContactsObject ();
            adapter = new ContactListAdapter (this.Activity, contacts);
            listview.Adapter = adapter;

            NcContactManager.Instance.ContactsChanged += (object sender, EventArgs e) => {
                contacts = NcContactManager.Instance.GetNachoContactsObject ();
                adapter.NotifyDataSetChanged ();
            };
                
            listview.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var fragment = new ContactViewFragment ();
                var bundle = new Bundle ();
                var contact = contacts.GetContact (e.Position);
                bundle.PutInt ("accountId", contact.AccountId);
                bundle.PutInt ("contactId", contact.Id);
                bundle.PutString ("segue", "ContactListToContactView");
                fragment.Arguments = bundle;
                Activity.SupportFragmentManager.BeginTransaction ()
                    .Add (Resource.Id.content_frame, fragment)
                    .AddToBackStack (null)
                    .Commit ();
            };

            Activity.Title = "Contacts";
            return rootView;
        }

        public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
        {
            inflater.Inflate (Resource.Menu.ContactListFragment, menu);

            var searchItem = menu.FindItem (Resource.Id.search);
//            var searchView = MenuItemCompat.GetActionView (searchItem);
            MenuItemCompat.SetShowAsAction (searchItem, MenuItemCompat.ShowAsActionAlways);

            base.OnCreateOptionsMenu (menu, inflater);
        }
    }

    public class ContactListAdapter : BaseAdapter<McContact>
    {
        Activity context;
        INachoContacts contacts;

        public ContactListAdapter (Activity context, INachoContacts contacts) : base ()
        {
            this.context = context;
            this.contacts = contacts;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override McContact this [int position] {  
            get { return contacts.GetContact (position); }
        }

        public override int Count {
            get { return contacts.Count (); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Android.Resource.Layout.SimpleListItem2, null);
            }
            var name = view.FindViewById<TextView> (Android.Resource.Id.Text1);
            var email = view.FindViewById<TextView> (Android.Resource.Id.Text2);

            var contact = contacts.GetContact (position);
            name.Text = contact.DisplayName;
            email.Text = contact.DisplayEmailAddress;

            return view;
        }
    }
}
