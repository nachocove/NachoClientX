﻿
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
using NachoCore.ActiveSync;

namespace NachoClient.AndroidClient
{
    public interface IContactViewFragmentOwner
    {
        McContact ContactToView { get; }
    }

    public class ContactViewFragment : Fragment
    {
        private const int EDIT_REQUEST_CODE = 1;
        private const int NOTE_REQUEST_CODE = 2;

        McContact contact;

        ContactViewAdapter contactViewAdapter;

        ButtonBar buttonBar;

        public enum CurrentTab
        {
            Notes,
            Contact,
            Interactions,

        };

        private TextView notesTab;
        private TextView contactTab;
        private TextView interactionsTab;

        private View notesView;
        private ListView contactView;
        private ListView interactionsView;

        protected CurrentTab state;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);

            buttonBar = new ButtonBar (view);

            notesTab = view.FindViewById<TextView> (Resource.Id.notes_tab);
            contactTab = view.FindViewById<TextView> (Resource.Id.contact_tab);
            interactionsTab = view.FindViewById<TextView> (Resource.Id.interactions_tab);

            notesTab.Click += NotesTab_Click;
            contactTab.Click += ContactTab_Click;
            interactionsTab.Click += InteractionsTab_Click;

            notesView = view.FindViewById<View> (Resource.Id.notes_view);
            contactView = view.FindViewById<ListView> (Resource.Id.listView);
            interactionsView = view.FindViewById<ListView> (Resource.Id.interactions_listView);

            state = CurrentTab.Contact;
            HighlightTab (contactTab, contactView);

            return view;
        }

        void NotesTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.Notes != state) {
                state = CurrentTab.Notes;
                HighlightTab (notesTab, notesView);
                UnhighlightTab (contactTab, contactView);
                UnhighlightTab (interactionsTab, interactionsView);
            }
        }

        void ContactTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.Contact != state) {
                state = CurrentTab.Contact;
                HighlightTab (contactTab, contactView);
                UnhighlightTab (notesTab, notesView);
                UnhighlightTab (interactionsTab, interactionsView);
            }
        }

        void InteractionsTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.Interactions != state) {
                state = CurrentTab.Interactions;
                HighlightTab (interactionsTab, interactionsView);
                UnhighlightTab (notesTab, notesView);
                UnhighlightTab (contactTab, contactView);
            }
        }

        private void HighlightTab (TextView view, View dataView)
        {
            view.SetTextColor (Android.Graphics.Color.White);
            view.SetBackgroundResource (Resource.Color.NachoGreen);
            dataView.Visibility = ViewStates.Visible;
        }

        private void UnhighlightTab (TextView view, View dataView)
        {
            view.SetTextColor (Resources.GetColor (Resource.Color.NachoGreen));
            view.SetBackgroundResource (Resource.Drawable.BlackBorder);
            dataView.Visibility = ViewStates.Invisible;
        }

        private void EditButton_Click (object sender, EventArgs e)
        {
            switch (state) {
            case CurrentTab.Contact:
                break;
            case CurrentTab.Interactions:
                break;
            case CurrentTab.Notes:
                string noteText = ContactsHelper.GetNoteText (contact);
                var title = Pretty.NoteTitle (contact.GetDisplayNameOrEmailAddress ());
                StartActivityForResult (
                    NoteActivity.EditNoteIntent (this.Activity, title, null, noteText, insertDate: true),
                    NOTE_REQUEST_CODE);
                break;
            }
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);
            contact = ((IContactViewFragmentOwner)Activity).ContactToView;
            contactViewAdapter = new ContactViewAdapter (contact);
            var listview = View.FindViewById<ListView> (Resource.Id.listView);
            listview.Adapter = contactViewAdapter;
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {

            case EDIT_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    // The contact was edited. Refresh the UI.
                    BindValues (View);
                }
                break;

            case NOTE_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    string newNoteText = NoteActivity.ModifiedNoteText (data);
                    if (null != newNoteText) {
                        ContactsHelper.SaveNoteText (contact, newNoteText);
                        contact = McContact.QueryById<McContact> (contact.Id);
                        BindValues (View);
                    }
                }
                break;
            }
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
            if (contact.CanUserEdit ()) {
                buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.gen_edit, EditButton_Click);
            } else {
                buttonBar.ClearButton (ButtonBar.Button.Right1);
            }

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

            var vipView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.vip);
            BindContactVip (contact, vipView);
            vipView.Click += VipView_Click;
            vipView.Tag = contact.Id;

            var notesTextView = view.FindViewById<TextView> (Resource.Id.notes_text);
            if (!contact.CanUserEdit ()) {
                notesTextView.Text = "This contact has not been synced. Adding or editing notes is disabled.";
            } else {
                notesTextView.Text = ContactsHelper.GetNoteText (contact);
                if (string.IsNullOrEmpty (notesTextView.Text)) {
                    notesTextView.Text = "You have not entered any " +
                    "notes for this contact. You can add and " +
                    "edit notes by tapping the edit button in the top" +
                    " right corner of this screen.";
                }
            }
        }

        void BindContactVip (McContact contact, ImageView vipView)
        {
            vipView.SetImageResource (contact.IsVip ? Resource.Drawable.contacts_vip_checked : Resource.Drawable.contacts_vip);
            vipView.Tag = contact.Id;
        }

        void VipView_Click (object sender, EventArgs e)
        {
            var vipView = (Android.Widget.ImageView)sender;
            var contactId = (int)vipView.Tag;
            var contact = McContact.QueryById<McContact> (contactId);
            contact.SetVIP (!contact.IsVip);
            BindContactVip (contact, vipView);
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
            
    }


    public class ContactViewAdapter : Android.Widget.BaseAdapter<object>
    {
        McContact contact;

        class DisplayInfo
        {
            public int imageId;
            public string labelString;
            public string valueString;

            public DisplayInfo (int imageId, string labelString, string valueString)
            {
                this.imageId = imageId;
                this.labelString = labelString;
                this.valueString = valueString;
            }
        };

        List<DisplayInfo> displayList;

        void Add (int imageId, string labelString, string valueString)
        {
            displayList.Add (new DisplayInfo (imageId, labelString, valueString));
        }

        public ContactViewAdapter (McContact contact)
        {
            this.contact = contact;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            displayList = new List<DisplayInfo> ();

            if (contact.EmailAddresses.Count > 0) {
                foreach (var e in contact.EmailAddresses) {
                    if (e.IsDefault) {
                        var emailAddress = McEmailAddress.QueryById <McEmailAddress> (e.EmailAddress);
                        var canonicalEmail = (null == emailAddress ? "" : emailAddress.CanonicalEmailAddress);
                        Add (Resource.Drawable.contacts_icn_email, "EMAIL", canonicalEmail);
                        break;
                    }
                }
            }

            contact.PhoneNumbers.Sort (new ContactsHelper.PhoneAttributeComparer ());

            if (contact.PhoneNumbers.Count > 0) {
                foreach (var p in contact.PhoneNumbers) {
                    if (p.IsDefault) {
                        var label = (String.IsNullOrEmpty (p.Label) ? "PHONE" : p.Label.ToUpper ());
                        Add (Resource.Drawable.contacts_icn_phone, label, p.Value);
                        break;
                    }
                }
            }

            if (contact.EmailAddresses.Count > 0) {
                foreach (var e in contact.EmailAddresses) {
                    if (!e.IsDefault) {
                        var emailAddress = McEmailAddress.QueryById <McEmailAddress> (e.EmailAddress);
                        var canonicalEmail = (null == emailAddress ? "" : emailAddress.CanonicalEmailAddress);
                        Add (Resource.Drawable.contacts_icn_email, "EMAIL", canonicalEmail);
                        break;
                    }
                }
            }

            if (contact.PhoneNumbers.Count > 0) {
                foreach (var p in contact.PhoneNumbers) {
                    if (!p.IsDefault) {
                        var label = (String.IsNullOrEmpty (p.Label) ? "PHONE" : p.Label.ToUpper ());
                        Add (Resource.Drawable.contacts_icn_phone, label, p.Value);
                        break;
                    }
                }
            }


            if (contact.IMAddresses.Count > 0) {
                foreach (var imAddressAttribute in contact.IMAddresses) {
                    Add (imAddressAttribute.Name);
                }
            }

            if (contact.Addresses.Count > 0) {
                foreach (var a in contact.Addresses) {
                    Add (a.Name);
                }
            }

            if (DateTime.MinValue != contact.GetDateAttribute (Xml.Contacts.Birthday)) {
                Add (Xml.Contacts.Birthday);
            }

            if (DateTime.MinValue != contact.GetDateAttribute (Xml.Contacts.Anniversary)) {
                Add (Xml.Contacts.Anniversary);
            }

            if (contact.Relationships.Count > 0) {
                foreach (var c in contact.Relationships) {
                    if (c.Name == Xml.Contacts.Child) {
                        Add (Xml.Contacts.Children);
                        break;
                    }
                }
            }

            if (contact.Relationships.Count > 0) {
                foreach (var c in contact.Relationships) {
                    if (c.Name != Xml.Contacts.Child) {
                        Add (c.Name);
                    }
                }
            }

            foreach (var t in ContactsHelper.GetTakenMiscNames(contact)) {
                Add (t);
            }

        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return displayList.Count;
            }
        }

        public override object this [int position] {  
            get {
                return displayList [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ContactViewCell, parent, false);
            }
            var info = displayList [position];

            Bind (view, info.imageId, info.labelString, info.valueString);

            return view;
        }

        void Bind (View view, int resid, string label, string value, int button = 0)
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
            case NcResult.SubKindEnum.Info_ContactChanged:
                break;
            }
        }

        protected void Add (string whatType)
        {
            string value;

            var label = ContactsHelper.ExchangeNameToLabel (whatType).ToUpperInvariant ();

            switch (whatType) {
            case "Other":
            case "Home":
            case "Business":
                var address = contact.GetAddressAttribute (whatType);
                value = address.Street + " " + address.City + " " + address.State + " " + address.PostalCode + " " + address.Country;
                Add (Resource.Drawable.contacts_icn_address, label, value);
                break;
            case Xml.Contacts.Birthday:
            case Xml.Contacts.Anniversary:
                value = Pretty.BirthdayOrAnniversary (contact.GetDateAttribute (whatType));
                Add (Resource.Drawable.contacts_icn_bday, label, value);
                break;
            case Xml.Contacts.Spouse:
                value = contact.GetRelationshipAttribute (whatType);
                Add (Resource.Drawable.contacts_attendees, label, value);
                break;
            case Xml.Contacts.AssistantName:
                value = contact.GetRelationshipAttribute (whatType);
                Add (Resource.Drawable.contacts_attendees, label, value);
                break;
            case Xml.Contacts2.ManagerName:
                value = contact.GetRelationshipAttribute (whatType);
                Add (Resource.Drawable.contacts_attendees, label, value);
                break;
            case Xml.Contacts.WebPage:
                value = contact.WebPage;
                Add (Resource.Drawable.contacts_icn_url, label, value);
                break;
            case Xml.Contacts.OfficeLocation:
                value = contact.OfficeLocation;
                Add (Resource.Drawable.contacts_icn_address, label, value);
                break;
            case Xml.Contacts.Children:
                var children = new List<string> ();
                foreach (var c in contact.Relationships) {
                    if (c.Name == Xml.Contacts.Child) {
                        children.Add (c.Value);
                    }
                }
                Add (Resource.Drawable.contacts_attendees, label, String.Join (",", children));
                break;
            case Xml.Contacts2.IMAddress:
            case Xml.Contacts2.IMAddress2:
            case Xml.Contacts2.IMAddress3:
                value = contact.GetIMAddressAttribute (whatType);
                Add (Resource.Drawable.contacts_icn_url, label, value);
                break;
            default:
                value = ContactsHelper.MiscContactAttributeNameToValue (whatType, contact);
                Add (Resource.Drawable.contacts_description, label, value);
                break;
            }
        }

    }

}
