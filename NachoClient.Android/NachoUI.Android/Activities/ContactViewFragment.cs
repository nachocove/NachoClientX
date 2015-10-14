
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

using MimeKit;

namespace NachoClient.AndroidClient
{
    public class ContactViewFragment : Fragment
    {
        McContact contact;

        ContactViewAdapter contactViewAdapter;

        public static ContactViewFragment newInstance (McContact contact)
        {
            var fragment = new ContactViewFragment ();
            fragment.contact = contact;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);

            var editButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            editButton.SetImageResource (Android.Resource.Drawable.IcMenuEdit);
            editButton.Visibility = Android.Views.ViewStates.Visible;
//            editButton.Click += EditButton_Click;

            view.Click += View_Click;

            contactViewAdapter = new ContactViewAdapter (contact);
            var listview = view.FindViewById<ListView> (Resource.Id.listView);
            listview.Adapter = contactViewAdapter;

            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            BindValues (View);
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void BindValues (View view)
        {
            var title = view.FindViewById<TextView> (Resource.Id.contact_title);
            var subtitle1 = view.FindViewById<TextView> (Resource.Id.contact_subtitle1);

            title.Text = contact.GetDisplayNameOrEmailAddress ();

            if (!string.IsNullOrEmpty (contact.JobTitle)) {
                subtitle1.Text = contact.JobTitle;
            } else if (!string.IsNullOrEmpty (contact.Title)) {
                subtitle1.Text = contact.Title;
            } else {
                subtitle1.Text = GetTitleFromContact ();
            }

            var userInitials = view.FindViewById<Android.Widget.TextView> (Resource.Id.user_initials);
            userInitials.Text = NachoCore.Utils.ContactsHelper.GetInitials (contact);
            userInitials.SetBackgroundResource (Bind.ColorForUser (contact.CircleColor));
        }

        protected string GetTitleFromContact ()
        {
            if (String.IsNullOrEmpty (contact.GetDisplayName ())) {
                return "";
            } else {
                return contact.GetPrimaryCanonicalEmailAddress ();
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

        }

        void View_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "View_Click");
        }
    }


    public class ContactViewAdapter : Android.Widget.BaseAdapter<object>
    {
        McContact contact;
        List<object> stuff;

        public ContactViewAdapter (McContact contact)
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            stuff = new List<object> ();

            if (contact.EmailAddresses.Count > 0) {
                foreach (var e in contact.EmailAddresses) {
                    if (e.IsDefault) {
                        stuff.Add (e);
                        break;
                    }
                }
            }

            contact.PhoneNumbers.Sort (new ContactsHelper.PhoneAttributeComparer ());

            if (contact.PhoneNumbers.Count > 0) {
                foreach (var p in contact.PhoneNumbers) {
                    if (p.IsDefault) {
                        stuff.Add (p);
                        break;
                    }
                }
            }

            if (contact.EmailAddresses.Count > 0) {
                foreach (var emailAddressAttributes in contact.EmailAddresses) {
                    if (!emailAddressAttributes.IsDefault) {
                        stuff.Add (emailAddressAttributes);
                    }
                }
            }

            if (contact.PhoneNumbers.Count > 0) {
                foreach (var phoneNumberAttribute in contact.PhoneNumbers) {
                    if (!phoneNumberAttribute.IsDefault) {
                        stuff.Add (phoneNumberAttribute);
                    }
                }
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return stuff.Count;
            }
        }

        public override object this [int position] {  
            get {
                return stuff [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ContactViewCell, parent, false);
            }
            var thing = this [position];

            if (thing is McContactEmailAddressAttribute) {
                var a = (McContactEmailAddressAttribute)thing;
                var emailAddress = McEmailAddress.QueryById <McEmailAddress> (a.EmailAddress);
                var canonicalEmail = (null == emailAddress ? "" : emailAddress.CanonicalEmailAddress);
                Bind (view, Resource.Drawable.contacts_icn_email, "EMAIL", canonicalEmail, Resource.Drawable.contacts_email);
                return view;
            }

            if (thing is McContactStringAttribute) {
                var a = (McContactStringAttribute)thing;
                switch (a.Type) {
                case McContactStringType.Relationship:
                    break;
                case McContactStringType.PhoneNumber:
                    var label = (String.IsNullOrEmpty (a.Label) ? "PHONE" : a.Label.ToUpper ());
                    Bind (view, Resource.Drawable.contacts_icn_phone, label, a.Value, Resource.Drawable.contacts_call);
                    break;
                case McContactStringType.IMAddress:
                    break;
                case McContactStringType.Category:
                    break;
                case McContactStringType.Address:
                    break;
                case McContactStringType.Date:
                    break;
                }

                return view;
            }

            return view;
        }

        void Bind(View view, int resid, string label, string value, int button = 0)
        {
            var imageView = view.FindViewById<ImageView> (Resource.Id.image);
            var labelView = view.FindViewById<TextView> (Resource.Id.label);
            var valueView = view.FindViewById<TextView> (Resource.Id.value);
            imageView.SetImageResource (resid);
            labelView.Text = label;
            valueView.Text = value;

            var buttonView = view.FindViewById<ImageView> (Resource.Id.button);
            if (0 == button) {
                buttonView.Visibility = ViewStates.Gone;
            } else {
                buttonView.Visibility = ViewStates.Visible;
                buttonView.SetImageResource (button);
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ContactSetChanged:
                break;
            }
        }

    }

}
