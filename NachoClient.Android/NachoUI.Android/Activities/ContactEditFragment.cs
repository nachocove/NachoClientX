
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
    public interface IContactEditFragmentOwner
    {
        McContact ContactToView { get; }
    }

    public class ContactEditFragment : Fragment
    {
        private const int NOTE_REQUEST_CODE = 1;

        private const string CONTACT_LABEL_CHOOSER_FRAGMENT_TAG = "ContactLabelChooserFragment";

        public const Result DELETE_CONTACT_RESULT_CODE = Result.FirstUser;

        McContact contact;
        McContact original;

        ButtonBar buttonBar;

        View notesView;
        View deleteView;

        LinearLayout phoneList;
        LinearLayout emailList;
        LinearLayout dateList;
        LinearLayout imList;
        LinearLayout addressList;
        LinearLayout relationshipList;
        LinearLayout otherList;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactEditFragment, container, false);

            notesView = view.FindViewById (Resource.Id.notes);
            deleteView = view.FindViewById (Resource.Id.delete_contact_view);

            buttonBar = new ButtonBar (view);

            buttonBar.SetIconButton (ButtonBar.Button.Left1, Resource.Drawable.gen_close, CancelButton_Click);
            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            notesView.Click += NotesView_Click;

            phoneList = view.FindViewById<LinearLayout> (Resource.Id.phone_list);
            emailList = view.FindViewById<LinearLayout> (Resource.Id.email_list);
            dateList = view.FindViewById<LinearLayout> (Resource.Id.date_list);
            imList = view.FindViewById<LinearLayout> (Resource.Id.im_list);
            addressList = view.FindViewById<LinearLayout> (Resource.Id.address_list);
            relationshipList = view.FindViewById<LinearLayout> (Resource.Id.relationship_list);
            otherList = view.FindViewById<LinearLayout> (Resource.Id.other_list);

            var addPhone = view.FindViewById<LinearLayout> (Resource.Id.add_phone);
            addPhone.Click += AddPhone_Click;

            var addEmail = view.FindViewById<LinearLayout> (Resource.Id.add_email);
            addEmail.Click += AddEmail_Click;

            var addDate = view.FindViewById<LinearLayout> (Resource.Id.add_date);
            addDate.Click += AddDate_Click;

            var addIm = view.FindViewById<LinearLayout> (Resource.Id.add_im);
            addIm.Click += AddIm_Click;

            var addAddress = view.FindViewById<LinearLayout> (Resource.Id.add_address);
            addAddress.Click += AddAddress_Click;

            var addRelationship = view.FindViewById<LinearLayout> (Resource.Id.add_relationship);
            addRelationship.Click += AddRelationship_Click;

            var addOther = view.FindViewById<LinearLayout> (Resource.Id.add_other);
            addOther.Click += AddOther_Click;

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            contact = ((IContactEditFragmentOwner)Activity).ContactToView;

            if (null == contact) {
                contact = CreateNewContact ();
                buttonBar.SetTitle (Resource.String.new_contact);
                deleteView.Visibility = ViewStates.Gone;
            } else {
                original = CopyOriginalContact (contact);
                buttonBar.SetTitle (Resource.String.edit_contact);
                deleteView.Visibility = ViewStates.Visible;
                deleteView.Click += DeleteView_Click;
                Bind (contact, View);
            }
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {

            case NOTE_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    string newNoteText = NoteActivity.ModifiedNoteText (data);
                    if (null != newNoteText) {
                        var notesView = View.FindViewById<TextView> (Resource.Id.notes_text);
                        notesView.Text = newNoteText;
                    }
                }
                break;
            }
        }

        public override void OnStart ()
        {
            base.OnStart ();
        }

        public override void OnResume ()
        {
            base.OnResume ();
        }

        public override void OnPause ()
        {
            base.OnPause ();
        }

        private void SaveButton_Click (object sender, EventArgs e)
        {
            UpdateContactFromUI (View);

            var contactBody = McBody.QueryById<McBody> (contact.BodyId);
            var notesView = View.FindViewById<TextView> (Resource.Id.notes_text);
            if (null != contactBody) {
                contactBody.UpdateData (notesView.Text);
            } else {
                contact.BodyId = McBody.InsertFile (contact.AccountId, McAbstrFileDesc.BodyTypeEnum.PlainText_1, notesView.Text).Id;
            }
            if (0 == contact.Id) {
                contact.Insert ();
                McFolder f = McFolder.GetDefaultContactFolder (contact.AccountId);
                f.Link (contact);
                NachoCore.BackEnd.Instance.CreateContactCmd (contact.AccountId, contact.Id, f.Id);
            } else {
                contact.Update ();
                NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
            }
            Activity.SetResult (Result.Ok);
            Activity.Finish ();
        }

        private void CancelButton_Click (object sender, EventArgs e)
        {
            Activity.SetResult (Result.Canceled);
            Activity.Finish ();
        }

        private void DeleteView_Click (object sender, EventArgs e)
        {
            NcAlertView.Show (Activity,
                "Delete Contact",
                "Are you sure that you want to delete this contact? This operation cannot be undone.",
                () => {
                    BackEnd.Instance.DeleteContactCmd (contact.AccountId, contact.Id);
                    Activity.SetResult (DELETE_CONTACT_RESULT_CODE);
                    Activity.Finish ();
                }, () => {
            });
        }

        void NotesView_Click (object sender, EventArgs e)
        {
            string noteText = ContactsHelper.GetNoteText (contact);
            var title = Pretty.NoteTitle (contact.GetDisplayNameOrEmailAddress ());
            StartActivityForResult (
                NoteActivity.EditNoteIntent (this.Activity, title, null, noteText, insertDate: true),
                NOTE_REQUEST_CODE);
        }

        // New cell
        void AddAddress_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailableAddressNames (contact);
            var name = BindNewAddressCell (addressList, choices, "address", AddressLabelView_Click, AddressActionView_Click);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddAddressAttribute (contact.AccountId, name, label, new McContactAddressAttribute ());
            }
        }

        // New cell
        void AddOther_Click (object sender, EventArgs e)
        {
            var taken = GetVisibleNames (otherList);
            var choices = ContactsHelper.GetAvailableMiscNames (taken);
            var name = BindNewCell (otherList, choices, "", OtherLabelView_Click, OtherActionView_Click);
            if (null != name) {
                ContactsHelper.AssignMiscContactAttribute (contact, name, "");
            }
        }

        // New cell
        void AddRelationship_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailableRelationshipNames (contact);
            var name = BindNewCell (relationshipList, choices, "relationship", RelationshipLabelView_Click, RelationshipActionView_Click);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddRelationshipAttribute (contact.AccountId, name, label, "");
            }
        }

        // New cell
        void AddIm_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailableIMAddressNames (contact);
            var name = BindNewCell (imList, choices, "IM address", ImLabelView_Click, ImActionView_Click);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddIMAddressAttribute (contact.AccountId, name, label, "");
            }
        }

        // New cell
        void AddDate_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailableDateNames (contact);
            var name = BindNewDateCell (dateList, choices, "date", DateLabelView_Click, DateActionView_Click);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddDateAttribute (contact.AccountId, name, label, DateTime.MinValue);
            }
        }

        // New cell
        void AddEmail_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailableEmailNames (contact);
            var name = BindNewCell (emailList, choices, "email address", EmailLabelView_Click, EmailActionView_Click, showMenu: true);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddOrUpdateEmailAddressAttribute (contact.AccountId, name, label, "");
            }
        }

        // New cell
        void AddPhone_Click (object sender, EventArgs e)
        {
            var choices = ContactsHelper.GetAvailablePhoneNames (contact);
            var name = BindNewCell (phoneList, choices, "phone", PhoneLabelView_Click, PhoneActionView_Click, showMenu: true);
            if (null != name) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                contact.AddOrUpdatePhoneNumberAttribute (contact.AccountId, name, label, "");
            }
        }

        EventHandler<string> eventHandler;

        // Label & name tag changer
        void LabelView_Click (object sender, List<string> choices, Action<string, string> OnCompletion)
        {
            var labelView = (TextView)sender;
            NcAssert.True (Resource.Id.label == labelView.Id);

            var cell = (View)labelView.Tag;
            var originalName = (string)cell.Tag;

            var labelChooserFragment = ContactLabelChooserFragment.newInstance (choices, originalName);

            eventHandler = (object s, string name) => {
                cell.Tag = name;
                labelView.Text = ContactsHelper.ExchangeNameToLabel (name);
                labelChooserFragment.OnContactLabelChanged -= eventHandler;
                if (null != OnCompletion) {
                    OnCompletion (originalName, name);
                }
            };

            labelChooserFragment.OnContactLabelChanged += eventHandler;
            labelChooserFragment.Show (FragmentManager, CONTACT_LABEL_CHOOSER_FRAGMENT_TAG);
        }

        // Rename
        void PhoneLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailablePhoneNames (contact), (originalName, name) => {
                var value = contact.GetPhoneNumberAttribute (originalName);
                contact.RemovePhoneNumberAttribute (originalName);
                contact.AddPhoneNumberAttribute (contact.AccountId, name, ContactsHelper.ExchangeNameToLabel (name), value);
            });
        }

        // Rename
        void EmailLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailableEmailNames (contact), (originalName, name) => {
                var value = contact.GetEmailAddressAttribute (originalName);
                contact.RemoveEmailAddressAttribute (originalName);
                contact.AddEmailAddressAttribute (contact.AccountId, name, ContactsHelper.ExchangeNameToLabel (name), value);
            });
        }

        // Rename
        void DateLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailableDateNames (contact), (originalName, name) => {
                var value = contact.GetDateAttribute (originalName);
                contact.RemoveDateAttribute (originalName);
                contact.AddDateAttribute (contact.AccountId, name, ContactsHelper.ExchangeNameToLabel (name), value);
            });
        }

        // Rename
        void AddressLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailableAddressNames (contact), (originalName, name) => {
                var value = contact.GetAddressAttribute (originalName);
                contact.RemoveAddressAttribute (originalName);
                contact.AddAddressAttribute (contact.AccountId, name, ContactsHelper.ExchangeNameToLabel (name), value);
            });
        }

        // Rename
        void ImLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailableIMAddressNames (contact), (originalName, name) => {
                var value = contact.GetIMAddressAttribute (originalName);
                contact.RemoveIMAddressAttribute (originalName);
                contact.AddIMAddressAttribute (contact.AccountId, name, ContactsHelper.ExchangeNameToLabel (name), value);
            });
        }

        // Rename
        void RelationshipLabelView_Click (object sender, EventArgs e)
        {
            LabelView_Click (sender, ContactsHelper.GetAvailableRelationshipNames (contact), (originalName, newName) => {
                string name;
                string value;
                LabelViewToNameAndValue (sender, out name, out value);
                contact.RemoveRelationshipAttributes (originalName, value);
                contact.AddRelationshipAttribute (contact.AccountId, newName, ContactsHelper.ExchangeNameToLabel (newName), value);
            });
        }

        // Rename
        void OtherLabelView_Click (object sender, EventArgs e)
        {
            var taken = GetVisibleNames (otherList);
            var choices = ContactsHelper.GetAvailableMiscNames (taken);
            LabelView_Click (sender, choices, (originalName, name) => {
                var value = ContactsHelper.MiscContactAttributeNameToValue (originalName, contact);
                ContactsHelper.AssignMiscContactAttribute (contact, originalName, null);
                ContactsHelper.AssignMiscContactAttribute (contact, name, value);
            });
        }

        // Delete or default
        void PhoneActionView_Click (object sender, EventArgs e)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetItems (new [] { "Make Default", "Delete" }, (innersender, innerargs) => {
                switch (innerargs.Which) {
                case 0:
                    break;
                case 1:
                    var tag = ActionViewToName (sender);
                    contact.RemovePhoneNumberAttribute (tag);
                    RemoveCell (phoneList, tag);
                    break;
                }
            });
            builder.Show ();
        }

        // Delete or default
        void EmailActionView_Click (object sender, EventArgs e)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetItems (new [] { "Make Default", "Delete" }, (innersender, innerargs) => {
                switch (innerargs.Which) {
                case 0:
                    break;
                case 1:
                    var tag = ActionViewToName (sender);
                    contact.RemoveEmailAddressAttribute (tag);
                    RemoveCell (emailList, tag);
                    break;
                }
            });
            builder.Show ();
        }

        // Delete
        void DateActionView_Click (object sender, EventArgs e)
        {
            var tag = ActionViewToName (sender);
            contact.RemoveDateAttribute (tag);
            RemoveCell (dateList, tag);
        }

        // Delete
        void AddressActionView_Click (object sender, EventArgs e)
        {
            var tag = ActionViewToName (sender);
            contact.RemoveAddressAttribute (tag);
            RemoveCell (addressList, tag);
        }

        // Delete
        void ImActionView_Click (object sender, EventArgs e)
        {
            var tag = ActionViewToName (sender);
            contact.RemoveIMAddressAttribute (tag);
            RemoveCell (imList, tag);
        }

        // Delete
        void RelationshipActionView_Click (object sender, EventArgs e)
        {
            string name;
            string value;
            ActionViewToNameAndValue (sender, out name, out value);
            contact.RemoveRelationshipAttributes (name, value);
            RemoveCell (relationshipList, name, value);
        }

        // Delete
        void OtherActionView_Click (object sender, EventArgs e)
        {
            var tag = ActionViewToName (sender);
            ContactsHelper.AssignMiscContactAttribute (contact, tag, null);
            RemoveCell (otherList, tag);
        }

        void Bind (McContact contact, View view)
        {
            var displayName = view.FindViewById<TextView> (Resource.Id.display_name);
            displayName.Text = contact.GetDisplayName ();

            var firstName = view.FindViewById<EditText> (Resource.Id.first_name);
            firstName.Text = contact.FirstName;

            var middleName = view.FindViewById<EditText> (Resource.Id.middle_name);
            middleName.Text = contact.MiddleName;

            var lastName = view.FindViewById<EditText> (Resource.Id.last_name);
            lastName.Text = contact.LastName;

            var suffix = view.FindViewById<EditText> (Resource.Id.suffix);
            suffix.Text = contact.Suffix;

            var company = view.FindViewById<EditText> (Resource.Id.company);
            company.Text = contact.CompanyName;

            foreach (var p in contact.PhoneNumbers) {
                BindCell (phoneList, p.Name, p.Label, p.Value, PhoneLabelView_Click, PhoneActionView_Click, showMenu: true);
            }

            foreach (var e in contact.EmailAddresses) {
                BindCell (emailList, e.Name, e.Label, e.Value, EmailLabelView_Click, EmailActionView_Click, showMenu: true);
            }

            foreach (var d in contact.Dates) {
                BindDateCell (dateList, d.Name, d.Label, d.Value, DateLabelView_Click, DateActionView_Click);
            }

            foreach (var a in contact.Addresses) {
                // Address special case -- no label, just repeat the name
                var cell = BindAddressCell (addressList, a.Name, a.Name, "", AddressLabelView_Click, AddressActionView_Click);
                cell.FindViewById<EditText> (Resource.Id.street).Text = a.Street;
                cell.FindViewById<EditText> (Resource.Id.city).Text = a.City;
                cell.FindViewById<EditText> (Resource.Id.state).Text = a.State;
                cell.FindViewById<EditText> (Resource.Id.zip).Text = a.PostalCode;
                cell.FindViewById<EditText> (Resource.Id.country).Text = a.Country;
            }

            foreach (var im in contact.IMAddresses) {
                BindCell (imList, im.Name, im.Label, im.Value, ImLabelView_Click, ImActionView_Click);
            }

            foreach (var r in contact.Relationships) {
                BindCell (relationshipList, r.Name, r.Label, r.Value, RelationshipLabelView_Click, RelationshipActionView_Click);
            }

            foreach (var name in ContactsHelper.GetTakenMiscNames(contact)) {
                var label = ContactsHelper.ExchangeNameToLabel (name);
                var value = ContactsHelper.MiscContactAttributeNameToValue (name, contact);
                BindCell (otherList, name, label, value, OtherLabelView_Click, OtherActionView_Click);
            }

            var notesView = view.FindViewById<TextView> (Resource.Id.notes_text);
            notesView.Text = ContactsHelper.GetNoteText (contact);
        }

        View BindCell (LinearLayout list, string name, string label, string value, EventHandler labelClick, EventHandler actionClick, bool showMenu = false)
        {
            var cell = Activity.LayoutInflater.Inflate (Resource.Layout.ContactEditCell, null);
            list.AddView (cell, 0);

            var labelView = cell.FindViewById<TextView> (Resource.Id.label);
            var valueView = cell.FindViewById<TextView> (Resource.Id.value);
            var actionView = cell.FindViewById<ImageView> (Resource.Id.action);

            if (showMenu) {
                actionView.SetImageResource (Resource.Drawable.contacts_more_options);
            } else {
                actionView.SetImageResource (Resource.Drawable.email_delete);
            }

            cell.Tag = name;
            labelView.Tag = cell;
            actionView.Tag = cell;

            labelView.Text = label;
            valueView.Text = value;

            labelView.Click += labelClick;
            actionView.Click += actionClick;
            return cell;
        }

        string BindNewCell (LinearLayout list, List<string> names, string typeSingular, EventHandler labelClick, EventHandler actionClick, bool showMenu = false)
        {
            if (0 < names.Count) {
                var name = names [0];
                var label = ContactsHelper.ExchangeNameToLabel (name);
                BindCell (list, name, label, "", labelClick, actionClick, showMenu);
                return name;
            }
            var header = String.Format ("No Available Labels");
            var message = String.Format ("There are no more {0} labels available for this contact.", typeSingular);
            NcAlertView.ShowMessage (Activity, header, message);
            return null;
        }

        View BindAddressCell (LinearLayout list, string name, string label, string value, EventHandler labelClick, EventHandler actionClick)
        {
            var cell = BindCell (list, name, label, value, labelClick, actionClick);
            ConfigureAddressCell (cell);
            return cell;
        }

        string BindNewAddressCell (LinearLayout list, List<string> names, string typeSingular, EventHandler labelClick, EventHandler actionClick)
        {
            var name = BindNewCell (list, names, typeSingular, labelClick, actionClick);
            if (null == name) {
                return null;
            }
            var cell = FindCell (list, name);
            ConfigureAddressCell (cell);
            return name;
        }

        void ConfigureAddressCell (View cell)
        {
            cell.FindViewById (Resource.Id.value).Visibility = ViewStates.Invisible;
            cell.FindViewById (Resource.Id.street).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.city).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.state).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.zip).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.country).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.street_line).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.city_line).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.state_line).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.zip_line).Visibility = ViewStates.Visible;
            cell.FindViewById (Resource.Id.country_line).Visibility = ViewStates.Visible;
        }

        View BindDateCell (LinearLayout list, string name, string label, DateTime value, EventHandler labelClick, EventHandler actionClick)
        {
            var cell = BindCell (list, name, label, Pretty.BirthdayOrAnniversary (value), labelClick, actionClick);
            ConfigureDateCell (cell, value);
            return cell;
        }

        string BindNewDateCell (LinearLayout list, List<string> names, string typeSingular, EventHandler labelClick, EventHandler actionClick)
        {
            var name = BindNewCell (list, names, typeSingular, labelClick, actionClick);
            if (null == name) {
                return null;
            }
            var cell = FindCell (list, name);
            ConfigureDateCell (cell, DateTime.MinValue);
            return name;
        }

        void ConfigureDateCell (View cell, DateTime date)
        {
            var valueView = cell.FindViewById (Resource.Id.value);
            valueView.SetOnLongClickListener (null);
            valueView.Focusable = false;
            valueView.Tag = new JavaObjectWrapper<DateTime> () { Item = date };
            valueView.Click += DateCell_Click;
        }

        void DateCell_Click (object sender, EventArgs e)
        {
            var valueView = (EditText)sender;
            var initialDate = ((JavaObjectWrapper<DateTime>)valueView.Tag).Item;

            if (DateTime.MinValue == initialDate) {
                initialDate = DateTime.Now;
            } else {
                initialDate = DateTime.SpecifyKind (initialDate, DateTimeKind.Local);
            }

            DatePicker.Show (this.Activity, initialDate, DateTime.MinValue, DateTime.MaxValue, (DateTime date) => {
                date = DateTime.SpecifyKind (date.Date, DateTimeKind.Utc);
                valueView.Text = Pretty.BirthdayOrAnniversary (date);
                valueView.Tag = new JavaObjectWrapper<DateTime> () { Item = date };
            });
        }

        View FindCell (LinearLayout list, string name)
        {
            // Search views with exception of the 'add' button
            for (int i = 0; i < (list.ChildCount - 1); i++) {
                var cell = list.GetChildAt (i);
                if (name == ((string)cell.Tag)) {
                    return cell;
                }
            }
            NcAssert.CaseError ();
            return null;
        }

        void RemoveCell (LinearLayout list, string name, string value = null)
        {
            // Search views with exception of the 'add' button
            for (int i = 0; i < (list.ChildCount - 1); i++) {
                var cell = list.GetChildAt (i);
                if (name == ((string)cell.Tag)) {
                    var valueView = cell.FindViewById<TextView> (Resource.Id.value);
                    if ((null == value) || (value == valueView.Text)) {
                        list.RemoveView (cell);
                        return;
                    }
                }
            }
            NcAssert.CaseError ();
        }

        List<string> GetVisibleNames (LinearLayout list)
        {
            var names = new List<string> ();
            for (int i = 0; i < (list.ChildCount - 1); i++) {
                var cell = list.GetChildAt (i);
                names.Add ((string)cell.Tag);
            }
            return names;
        }

        string ActionViewToName (object sender)
        {
            string name;
            string value;
            ActionViewToNameAndValue (sender, out name, out value);
            return name;
        }

        void ActionViewToNameAndValue (object sender, out string name, out string value)
        {
            var actionView = (View)sender;
            NcAssert.True (Resource.Id.action == actionView.Id);
            var cell = (View)actionView.Tag;
            var valueView = cell.FindViewById<TextView> (Resource.Id.value);
            name = (string)cell.Tag;
            value = valueView.Text;
        }

        void LabelViewToNameAndValue (object sender, out string name, out string value)
        {
            var labelView = (View)sender;
            NcAssert.True (Resource.Id.label == labelView.Id);
            var cell = (View)labelView.Tag;
            var valueView = cell.FindViewById<TextView> (Resource.Id.value);
            name = (string)cell.Tag;
            value = valueView.Text;
        }

        protected McContact CreateNewContact ()
        {
            var c = new McContact ();
            c.AccountId = NcApplication.Instance.DefaultContactAccount.Id;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            return c;
        }

        protected McContact CopyOriginalContact (McContact o)
        {
            var c = new McContact ();
            c.AccountId = o.AccountId;
            c.Source = o.Source;
            ContactsHelper.CopyContact (o, ref c);
            return c;
        }

        void GetNameLabelValue (View view, out string name, out string label, out string value)
        {
            name = (string)view.Tag;
            label = view.FindViewById<TextView> (Resource.Id.label).Text;
            value = view.FindViewById<TextView> (Resource.Id.value).Text;
        }

        void UpdateContactFromUI (View view)
        {
            string name;
            string label;
            string value;

            contact.FirstName = view.FindViewById<EditText> (Resource.Id.first_name).Text;

            contact.MiddleName = view.FindViewById<EditText> (Resource.Id.middle_name).Text;

            contact.LastName = view.FindViewById<EditText> (Resource.Id.last_name).Text;

            contact.Suffix = view.FindViewById<EditText> (Resource.Id.suffix).Text;

            contact.CompanyName = view.FindViewById<EditText> (Resource.Id.company).Text;

            contact.PhoneNumbers.Clear ();
            for (int i = 0; i < (phoneList.ChildCount - 1); i++) {
                var cell = phoneList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                contact.AddPhoneNumberAttribute (contact.AccountId, name, label, value);
            }

            contact.EmailAddresses.Clear ();
            for (int i = 0; i < (emailList.ChildCount - 1); i++) {
                var cell = emailList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                contact.AddEmailAddressAttribute (contact.AccountId, name, label, value);
            }

            contact.Dates.Clear ();
            for (int i = 0; i < (dateList.ChildCount - 1); i++) {
                var cell = dateList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                var valueView = cell.FindViewById<TextView> (Resource.Id.value);
                var date = ((JavaObjectWrapper<DateTime>)valueView.Tag).Item;
                if (DateTime.MinValue != date) {
                    contact.AddDateAttribute (contact.AccountId, name, label, date);
                }
            }

            contact.Addresses.Clear ();
            for (int i = 0; i < (addressList.ChildCount - 1); i++) {
                var cell = addressList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                var address = new McContactAddressAttribute ();
                address.Street = cell.FindViewById<EditText> (Resource.Id.street).Text;
                address.City = cell.FindViewById<EditText> (Resource.Id.city).Text;
                address.State = cell.FindViewById<EditText> (Resource.Id.state).Text;
                address.PostalCode = cell.FindViewById<EditText> (Resource.Id.zip).Text;
                address.Country = cell.FindViewById<EditText> (Resource.Id.country).Text;
                contact.AddAddressAttribute (contact.AccountId, name, label, address);
            }

            contact.IMAddresses.Clear ();
            for (int i = 0; i < (imList.ChildCount - 1); i++) {
                var cell = imList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                contact.AddIMAddressAttribute (contact.AccountId, name, label, value);
            }

            contact.Relationships.Clear ();
            for (int i = 0; i < (relationshipList.ChildCount - 1); i++) {
                var cell = relationshipList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                if (Xml.Contacts.Child == name) {
                    contact.AddChildAttribute (contact.AccountId, name, label, value);
                } else {
                    contact.AddRelationshipAttribute (contact.AccountId, name, label, value);
                }
            }

            foreach (var miscName in ContactsHelper.MiscNames) {
                ContactsHelper.AssignMiscContactAttribute (contact, miscName, null);
            }
            for (int i = 0; i < (otherList.ChildCount - 1); i++) {
                var cell = otherList.GetChildAt (i);
                GetNameLabelValue (cell, out name, out label, out value);
                ContactsHelper.AssignMiscContactAttribute (contact, name, value);
            }
        }
    }

}
