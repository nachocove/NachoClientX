//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Text;
using Android.Support.V7.Widget;
using Android.Views.InputMethods;

using NachoCore.Model;
using NachoCore.Utils;


namespace NachoClient.AndroidClient
{
    public class ContactEditFragment : Fragment, ContactEditAdapter.Listener
    {

        public McContact Contact;
        ContactEditAdapter Adapter;

        #region Subviews

        RecyclerView ListView;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactEditFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactEditAdapter (this, Contact);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        public void EndEditing ()
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);
        }

        public void Save ()
        {
            ContactsHelper.SaveContact (Contact, Adapter.Notes);
        }

    }

    public class ContactEditAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
        }

        enum ViewType
        {
            NameFields,
            AddButton,
            StringField,
            DateField,
            AddressField,
            NotesField
        }

        public McContact Contact { get; private set; }
        public string Notes { get; private set; }
        List<McContactEmailAddressAttribute> EmailAddresses;
        List<McContactStringAttribute> PhoneNumbers;
        List<McContactDateAttribute> Dates;
        List<McContactAddressAttribute> Addresses;
        List<McContactStringAttribute> ImHandles;
        List<McContactStringAttribute> Relationships;
        List<ContactOtherAttribute> Others;
        WeakReference<Listener> WeakListener;
        List<string> AvailablePhoneNames {
            get {
                return ContactsHelper.GetAvailablePhoneNames (Contact);
            }
        }
        List<string> AvailableEmailNames {
            get {
                return ContactsHelper.GetAvailableEmailNames (Contact);
            }
        }
        List<string> AvailableDateNames {
            get {
                return ContactsHelper.GetAvailableDateNames (Contact);
            }
        }
        List<string> AvailableAddressNames {
            get {
                return ContactsHelper.GetAvailableAddressNames (Contact);
            }
        }
        List<string> AvailableRelationshipNames {
            get {
                return ContactsHelper.GetAvailableRelationshipNames (Contact);
            }
        }
        List<string> AvailableOtherNames {
            get {
                return ContactsHelper.GetAvailableMiscNames (Contact);
            }
        }
        List<string> AvailableImNames {
            get {
                return ContactsHelper.GetAvailableIMAddressNames (Contact);
            }
        }

        public ContactEditAdapter (Listener listener, McContact contact) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Contact = contact;
            EmailAddresses = Contact.EmailAddresses;
            PhoneNumbers = Contact.PhoneNumbers;
            Dates = Contact.Dates;
            Addresses = Contact.Addresses;
            ImHandles = Contact.IMAddresses;
            Relationships = Contact.Relationships;
            Others = Contact.Others;
            ConfigureGroups ();
        }

        int _GroupCount = 0;
        int NamesGroupPosition = -1;
        int EmailsGroupPosition = -1;
        int PhonesGroupPosition = -1;
        int ImsGroupPosition = -1;
        int AddressesGroupPosition = -1;
        int DatesGroupPosition = -1;
        int OthersGroupPosition = -1;
        int RelationshipsGroupPosition = -1;
        int NotesGroupPosition = -1;
        int EmailsExtraItemCount = 0;
        int PhonesExtraItemCount = 0;
        int ImsExtraItemCount = 0;
        int AddressesExtraItemCount = 0;
        int DatesExtraItemCount = 0;
        int OthersExtraItemCount = 0;
        int RelationshipsExtraItemCount = 0;
        int EmailsAddExtraPosition = -1;
        int PhonesAddExtraPosition = -1;
        int ImsAddExtraPosition = -1;
        int AddressesAddExtraPosition = -1;
        int DatesAddExtraPosition = -1;
        int OthersAddExtraPosition = -1;
        int RelationshipsAddExtraPosition = -1;

        void ConfigureGroups ()
        {
            _GroupCount = 0;
            NamesGroupPosition = -1;
            EmailsGroupPosition = -1;
            PhonesGroupPosition = -1;
            ImsGroupPosition = -1;
            AddressesGroupPosition = -1;
            DatesGroupPosition = -1;
            OthersGroupPosition = -1;
            RelationshipsGroupPosition = -1;
            NotesGroupPosition = -1;
            EmailsExtraItemCount = 0;
            PhonesExtraItemCount = 0;
            ImsExtraItemCount = 0;
            AddressesExtraItemCount = 0;
            DatesExtraItemCount = 0;
            OthersExtraItemCount = 0;
            RelationshipsExtraItemCount = 0;
            EmailsAddExtraPosition = -1;
            PhonesAddExtraPosition = -1;
            ImsAddExtraPosition = -1;
            AddressesAddExtraPosition = -1;
            DatesAddExtraPosition = -1;
            OthersAddExtraPosition = -1;
            RelationshipsAddExtraPosition = -1;
            NamesGroupPosition = _GroupCount++;
            PhonesGroupPosition = _GroupCount++;
            EmailsGroupPosition = _GroupCount++;
            DatesGroupPosition = _GroupCount++;
            AddressesGroupPosition = _GroupCount++;
            ImsGroupPosition = _GroupCount++;
            RelationshipsGroupPosition = _GroupCount++;
            OthersGroupPosition = _GroupCount++;
            NotesGroupPosition = _GroupCount++;
            if (AvailablePhoneNames.Count > 0) {
                PhonesAddExtraPosition = PhonesExtraItemCount++;
            }
            if (AvailableEmailNames.Count > 0) {
                EmailsAddExtraPosition = EmailsExtraItemCount++;
            }
            if (AvailableDateNames.Count > 0) {
                DatesAddExtraPosition = DatesExtraItemCount++;
            }
            if (AvailableAddressNames.Count > 0) {
                AddressesAddExtraPosition = AddressesExtraItemCount++;
            }
            if (AvailableImNames.Count > 0) {
                ImsAddExtraPosition = ImsExtraItemCount++;
            }
            if (AvailableRelationshipNames.Count > 0) {
                RelationshipsAddExtraPosition = RelationshipsExtraItemCount++;
            }
            if (AvailableOtherNames.Count > 0) {
                OthersAddExtraPosition = OthersExtraItemCount++;
            }
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == NamesGroupPosition) {
                return 1;
            }
            if (groupPosition == PhonesGroupPosition) {
                return PhoneNumbers.Count + PhonesExtraItemCount;
            }
            if (groupPosition == EmailsGroupPosition) {
                return EmailAddresses.Count + EmailsExtraItemCount;
            }
            if (groupPosition == DatesGroupPosition) {
                return Dates.Count + DatesExtraItemCount;
            }
            if (groupPosition == ImsGroupPosition) {
                return ImHandles.Count + ImsExtraItemCount;
            }
            if (groupPosition == AddressesGroupPosition) {
                return Addresses.Count + AddressesExtraItemCount;
            }
            if (groupPosition == RelationshipsGroupPosition) {
                return Relationships.Count + RelationshipsExtraItemCount;
            }
            if (groupPosition == OthersGroupPosition) {
                return Others.Count + OthersExtraItemCount;
            }
            if (groupPosition == NotesGroupPosition) {
                return 1;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactEditFragment.GroupItemCount unknown groupPosition: {0}", groupPosition));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.NameFields:
                return NameFieldsViewHolder.Create (parent);
            case ViewType.AddButton:
                return AddButtonViewHolder.Create (parent);
            case ViewType.StringField:
                return StringFieldViewHolder.Create (parent);
            case ViewType.DateField:
                return DateFieldViewHolder.Create (parent);
            case ViewType.AddressField:
                return AddressFieldViewHolder.Create (parent);
            case ViewType.NotesField:
                return NotesFieldViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactEditFragment.OnCreateGroupedViewHolder unknown viewType: {0}", viewType));
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == NamesGroupPosition) {
                if (position == 0) {
                    return (int)ViewType.NameFields;
                }
            }
            if (groupPosition == PhonesGroupPosition) {
                if (position < PhoneNumbers.Count) {
                    return (int)ViewType.StringField;
                } else {
                    var adjustedPosition = position - PhoneNumbers.Count;
                    if (adjustedPosition == PhonesAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == EmailsGroupPosition) {
                if (position < EmailAddresses.Count) {
                    return (int)ViewType.StringField;
                } else {
                    var adjustedPosition = position - EmailAddresses.Count;
                    if (adjustedPosition == EmailsAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == DatesGroupPosition) {
                if (position < Dates.Count) {
                    return (int)ViewType.DateField;
                } else {
                    var adjustedPosition = position - Dates.Count;
                    if (adjustedPosition == DatesAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == ImsGroupPosition) {
                if (position < ImHandles.Count) {
                    return (int)ViewType.StringField;
                } else {
                    var adjustedPosition = position - ImHandles.Count;
                    if (adjustedPosition == ImsAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == AddressesGroupPosition) {
                if (position < Addresses.Count) {
                    return (int)ViewType.AddressField;
                } else {
                    var adjustedPosition = position - Addresses.Count;
                    if (adjustedPosition == AddressesAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == RelationshipsGroupPosition) {
                if (position < Relationships.Count) {
                    return (int)ViewType.StringField;
                } else {
                    var adjustedPosition = position - Relationships.Count;
                    if (adjustedPosition == RelationshipsAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == OthersGroupPosition) {
                if (position < Others.Count) {
                    return (int)ViewType.StringField;
                } else {
                    var adjustedPosition = position - Others.Count;
                    if (adjustedPosition == OthersAddExtraPosition) {
                        return (int)ViewType.AddButton;
                    }
                }
            }
            if (groupPosition == NotesGroupPosition) {
                if (position == 0) {
                    return (int)ViewType.NotesField;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactEditFragment.GetItemViewType unknown position: {0}.{1}", groupPosition, position));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == NamesGroupPosition) {
                if (position == 0) {
                    var namesHolder = holder as NameFieldsViewHolder;
                    namesHolder.SetContact (Contact);
                    return;
                }
            }
            if (groupPosition == PhonesGroupPosition) {
                if (position < PhoneNumbers.Count) {
                    var stringHolder = holder as StringFieldViewHolder;
                    var attr = PhoneNumbers [position];
                    stringHolder.SetAttribute (attr, Resource.String.contact_edit_hint_phone, InputTypes.ClassPhone, () => AvailablePhoneNames);
                    stringHolder.SetRemoveHandler ((sender, e) => {
                        var index = PhoneNumbers.IndexOf (attr);
                        PhoneNumbers.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - PhoneNumbers.Count;
                    if (adjustedPosition == PhonesAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_phone);
                        return;
                    }
                }
            }
            if (groupPosition == EmailsGroupPosition) {
                if (position < EmailAddresses.Count) {
                    var stringHolder = holder as StringFieldViewHolder;
                    var attr = EmailAddresses [position];
                    stringHolder.SetAttribute (attr, Resource.String.contact_edit_hint_email, InputTypes.ClassText | InputTypes.TextVariationEmailAddress, () => AvailableEmailNames);
                    stringHolder.SetRemoveHandler ((sender, e) => {
                        var index = EmailAddresses.IndexOf (attr);
                        EmailAddresses.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - EmailAddresses.Count;
                    if (adjustedPosition == EmailsAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_email);
                        return;
                    }
                }
            }
            if (groupPosition == DatesGroupPosition) {
                if (position < Dates.Count) {
                    var dateHolder = holder as DateFieldViewHolder;
                    var attr = Dates [position];
                    dateHolder.SetAttribute (attr, () => AvailableDateNames);
                    dateHolder.SetRemoveHandler ((sender, e) => {
                        var index = Dates.IndexOf (attr);
                        Dates.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - Dates.Count;
                    if (adjustedPosition == DatesAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_date);
                        return;
                    }
                }
            }
            if (groupPosition == ImsGroupPosition) {
                if (position < ImHandles.Count) {
                    var stringHolder = holder as StringFieldViewHolder;
                    var attr = ImHandles [position];
                    stringHolder.SetAttribute (attr, Resource.String.contact_edit_hint_im, InputTypes.ClassText, () => AvailableImNames);
                    stringHolder.SetRemoveHandler ((sender, e) => {
                        var index = ImHandles.IndexOf (attr);
                        ImHandles.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - ImHandles.Count;
                    if (adjustedPosition == ImsAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_im);
                        return;
                    }
                }
            }
            if (groupPosition == AddressesGroupPosition) {
                if (position < Addresses.Count) {
                    var addressHolder = holder as AddressFieldViewHolder;
                    var attr = Addresses [position];
                    addressHolder.SetAttribute (attr, () => AvailableAddressNames);
                    addressHolder.SetRemoveHandler ((sender, e) => {
                        var index = Addresses.IndexOf (attr);
                        Addresses.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - Addresses.Count;
                    if (adjustedPosition == AddressesAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_address);
                        return;
                    }
                }
            }
            if (groupPosition == RelationshipsGroupPosition) {
                if (position < Relationships.Count) {
                    var stringHolder = holder as StringFieldViewHolder;
                    var attr = Relationships [position];
                    stringHolder.SetAttribute (attr, Resource.String.contact_edit_hint_relationship, InputTypes.ClassText | InputTypes.TextFlagCapWords, () => AvailableRelationshipNames);
                    stringHolder.SetRemoveHandler ((sender, e) => {
                        var index = Relationships.IndexOf (attr);
                        Relationships.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - Relationships.Count;
                    if (adjustedPosition == RelationshipsAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_relationship);
                        return;
                    }
                }
            }
            if (groupPosition == OthersGroupPosition) {
                if (position < Others.Count) {
                    var stringHolder = holder as StringFieldViewHolder;
                    var attr = Others [position];
                    stringHolder.SetAttribute (attr, Resource.String.contact_edit_hint_other, InputTypes.ClassText | InputTypes.TextFlagCapSentences, () => AvailableOtherNames);
                    stringHolder.SetRemoveHandler ((sender, e) => {
                        var index = Others.IndexOf (attr);
                        // Others work a little differently because they map to direct properties on the Contact,
                        // so removing from the list alone won't update the model like it does for the other lists.
                        // We have to clear the value to update the relevant McContact property
                        attr.Value = null;
                        Others.RemoveAt (index);
                        ConfigureGroups ();
                        NotifyDataSetChanged ();
                    });
                    return;
                } else {
                    var adjustedPosition = position - Others.Count;
                    if (adjustedPosition == OthersAddExtraPosition) {
                        (holder as AddButtonViewHolder).SetLabel (Resource.String.contact_edit_add_other);
                        return;
                    }
                }
            }
            if (groupPosition == NotesGroupPosition) {
                if (position == 0) {
                    var notesHolder = holder as NotesFieldViewHolder;
                    notesHolder.SetNotes (Notes);
                    notesHolder.SetNotesChanged ((sender, e) => {
                        Notes = e.Text.ToString ();
                    });
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactEditFragment.GetItemViewType unknown position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == PhonesGroupPosition) {
                position -= PhoneNumbers.Count;
                if (position == PhonesAddExtraPosition) {
                    AddPhoneNumber ();
                }
            } else if (groupPosition == EmailsGroupPosition) {
                position -= EmailAddresses.Count;
                if (position == EmailsAddExtraPosition) {
                    AddEmailAddress ();
                }
            } else if (groupPosition == ImsGroupPosition) {
                position -= ImHandles.Count;
                if (position == ImsAddExtraPosition) {
                    AddImHandle ();
                }
            } else if (groupPosition == DatesGroupPosition) {
                position -= Dates.Count;
                if (position == DatesAddExtraPosition) {
                    AdddDate ();
                }
            } else if (groupPosition == AddressesGroupPosition) {
                position -= Addresses.Count;
                if (position == AddressesAddExtraPosition) {
                    AddAddress ();
                }
            } else if (groupPosition == RelationshipsGroupPosition) {
                position -= Relationships.Count;
                if (position == RelationshipsAddExtraPosition) {
                    AddRelationship ();
                }
            } else if (groupPosition == OthersGroupPosition) {
                position -= Others.Count;
                if (position == OthersAddExtraPosition) {
                    AddOther ();
                }
            }
        }

        void AddPhoneNumber ()
        {
            var names = AvailablePhoneNames;
            var attr = new McContactStringAttribute ();
            attr.Type = McContactStringType.PhoneNumber;
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            PhoneNumbers.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AddEmailAddress ()
        {
            var names = AvailableEmailNames;
            var attr = new McContactEmailAddressAttribute ();
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            EmailAddresses.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AddImHandle ()
        {
            var names = AvailableImNames;
            var attr = new McContactStringAttribute ();
            attr.Type = McContactStringType.IMAddress;
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            ImHandles.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AdddDate ()
        {
            var names = AvailableDateNames;
            var attr = new McContactDateAttribute ();
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            attr.Value = DateTime.Today;
            Dates.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AddAddress ()
        {
            var names = AvailableAddressNames;
            var attr = new McContactAddressAttribute ();
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            Addresses.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AddRelationship ()
        {
            var names = AvailableRelationshipNames;
            var attr = new McContactStringAttribute ();
            attr.Type = McContactStringType.Relationship;
            attr.AccountId = Contact.AccountId;
            attr.ChangeName (names [0]);
            Relationships.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        void AddOther ()
        {
            var names = AvailableOtherNames;
            var attr = new ContactOtherAttribute (Contact, names [0]);
            Others.Add (attr);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        class NameFieldsViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            PortraitView PortraitView;
            EditText FirstNameField;
            EditText MiddleNameField;
            EditText LastNameField;
            EditText SuffixField;
            EditText CompanyField;
            McContact Contact;

            public static NameFieldsViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditNamesItem, parent, false);
                return new NameFieldsViewHolder (view);
            }

            public NameFieldsViewHolder (View view) : base (view)
            {
                PortraitView = view.FindViewById (Resource.Id.portrait) as PortraitView;
                FirstNameField = view.FindViewById (Resource.Id.first_name) as EditText;
                MiddleNameField = view.FindViewById (Resource.Id.middle_name) as EditText;
                LastNameField = view.FindViewById (Resource.Id.last_name) as EditText;
                SuffixField = view.FindViewById (Resource.Id.suffix) as EditText;
                CompanyField = view.FindViewById (Resource.Id.company) as EditText;

                FirstNameField.TextChanged += FirstNameChanged;
                MiddleNameField.TextChanged += MiddleNameChanged;
                LastNameField.TextChanged += LastNameChanged;
                SuffixField.TextChanged += SuffixChanged;
                CompanyField.TextChanged += CompanyChanged;
            }

            public void SetContact (McContact contact)
            {
                Contact = contact;
                FirstNameField.Text = Contact.FirstName;
                MiddleNameField.Text = Contact.MiddleName;
                LastNameField.Text = Contact.LastName;
                SuffixField.Text = Contact.Suffix;
                CompanyField.Text = Contact.CompanyName;
                UpdatePortrait ();
            }

            void FirstNameChanged (object sender, TextChangedEventArgs e)
            {
                Contact.FirstName = FirstNameField.Text;
                UpdatePortrait ();
            }

            void MiddleNameChanged (object sender, TextChangedEventArgs e)
            {
                Contact.MiddleName = MiddleNameField.Text;
                UpdatePortrait ();
            }

            void LastNameChanged (object sender, TextChangedEventArgs e)
            {
                Contact.LastName = LastNameField.Text;
                UpdatePortrait ();
            }

            void SuffixChanged (object sender, TextChangedEventArgs e)
            {
                Contact.Suffix = SuffixField.Text;
                UpdatePortrait ();
            }

            void CompanyChanged (object sender, TextChangedEventArgs e)
            {
                Contact.CompanyName = CompanyField.Text;
                UpdatePortrait ();
            }

            void UpdatePortrait ()
            {
                PortraitView.SetPortrait (Contact.PortraitId, Contact.CircleColor, Contact.Initials);
            }

        }

        class AddButtonViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView Label;

            public static AddButtonViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditAddButtonItem, parent, false);
                return new AddButtonViewHolder (view);
            }

            public AddButtonViewHolder (View view) : base (view)
            {
                Label = view.FindViewById (Resource.Id.label) as TextView;
            }

            public void SetLabel (int labelResource)
            {
                Label.SetText (labelResource);
            }

        }

        class FieldViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView NameLabel;
            View RemoveButton;
            McAbstrContactAttribute Attribute;
            EventHandler RemoveHandler;
            public delegate List<string> AvailableNamesDelegate ();
            AvailableNamesDelegate AvailableNames;
            AlertDialog NamePicker;

            public FieldViewHolder (View view) : base (view)
            {
                NameLabel = view.FindViewById (Resource.Id.name_label) as TextView;
                RemoveButton = view.FindViewById (Resource.Id.remove_button);
                NameLabel.Click += NameLabelClicked;
            }

            protected void SetAttribute (McAbstrContactAttribute attr, AvailableNamesDelegate availableNames)
            {
                Attribute = attr;
                AvailableNames = availableNames;
                NameLabel.Text = Attribute.GetDisplayLabel ();
            }

            protected void SetAttribute (string otherAttributeName, AvailableNamesDelegate availableNames)
            {
                Attribute = null;
            }

            public void SetRemoveHandler (EventHandler removeHandler)
            {
                if (RemoveHandler != null) {
                    RemoveButton.Click -= RemoveHandler;
                }
                RemoveHandler = removeHandler;
                if (RemoveHandler != null) {
                    RemoveButton.Click += RemoveHandler;
                }
            }

            void NameLabelClicked (object sender, EventArgs e)
            {
                var availableNames = AvailableNames ();
                var items = new string [availableNames.Count + 1];
                int selectedItem = 0;
                for (int i = 0; i < availableNames.Count; ++i) {
                    items [i + 1] = ContactsHelper.ExchangeNameToLabel (availableNames [i]);
                }
                items [0] = Attribute.GetDisplayLabel ();
                var builder = new AlertDialog.Builder (ItemView.Context);
                builder.SetSingleChoiceItems (items, selectedItem, (sender_, e_) => {
                    var name = availableNames [e_.Which - 1];
                    Attribute.ChangeName (name);
                    NameLabel.Text = Attribute.GetDisplayLabel ();
                    NamePicker.Dismiss ();
                });
                NamePicker = builder.Show ();
                NamePicker.DismissEvent += (sender_, e_) => {
                    NamePicker = null;
                };
            }

        }

        class StringFieldViewHolder : FieldViewHolder
        {

            EditText ValueField;
            McContactStringAttribute StringAttribute;
            McContactEmailAddressAttribute EmailAttribute;
            ContactOtherAttribute OtherAttibute;

            public static StringFieldViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditStringItem, parent, false);
                return new StringFieldViewHolder (view);
            }

            public StringFieldViewHolder (View view) : base (view)
            {
                ValueField = view.FindViewById (Resource.Id.value_field) as EditText;
                ValueField.TextChanged += ValueChanged;
            }

            public void SetAttribute (McContactStringAttribute attr, int hintResource, InputTypes inputType, AvailableNamesDelegate availableNames)
            {
                base.SetAttribute (attr, availableNames);
                StringAttribute = attr;
                EmailAttribute = null;
                OtherAttibute = null;
                ValueField.InputType = inputType;
                ValueField.SetHint (hintResource);
                ValueField.Text = StringAttribute.Value;
            }

            public void SetAttribute (McContactEmailAddressAttribute attr, int hintResource, InputTypes inputType, AvailableNamesDelegate availableNames)
            {
                base.SetAttribute (attr, availableNames);
                EmailAttribute = attr;
                StringAttribute = null;
                OtherAttibute = null;
                ValueField.InputType = inputType;
                ValueField.SetHint (hintResource);
                ValueField.Text = EmailAttribute.Value;
            }

            public void SetAttribute (ContactOtherAttribute attr, int hintResource, InputTypes inputType, AvailableNamesDelegate availableNames)
            {
                base.SetAttribute (attr, availableNames);
                OtherAttibute = attr;
                StringAttribute = null;
                EmailAttribute = null;
                ValueField.InputType = inputType;
                ValueField.SetHint (hintResource);
                ValueField.Text = OtherAttibute.Value;
            }

            protected virtual void ValueChanged (object sender, TextChangedEventArgs e)
            {
                if (EmailAttribute != null) {
                    EmailAttribute.Value = ValueField.Text;
                } else if (OtherAttibute != null) {
                    OtherAttibute.Value = ValueField.Text;
                } else {
                    StringAttribute.Value = ValueField.Text;
                }
            }
        }

        class DateFieldViewHolder : FieldViewHolder
        {

            TextView ValueLabel;
            McContactDateAttribute DateAttribute;

            public static DateFieldViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditDateItem, parent, false);
                return new DateFieldViewHolder (view);
            }

            public DateFieldViewHolder (View view) : base (view)
            {
                ValueLabel = view.FindViewById (Resource.Id.value_label) as TextView;
                ValueLabel.Click += ValueClicked;
            }

            public void SetAttribute (McContactDateAttribute attr, AvailableNamesDelegate availableNames)
            {
                base.SetAttribute (attr, availableNames);
                DateAttribute = attr;
                ValueLabel.Text = Pretty.BirthdayOrAnniversary (NachoPlatform.DateTimeFormatter.Instance, DateAttribute.Value);
            }

            void ValueClicked (object sender, EventArgs e)
            {
                var localInitialValue = DateAttribute.Value.ToLocalTime ();
                DatePicker.Show (ItemView.Context, localInitialValue, DateTime.MinValue, DateTime.MaxValue, (DateTime date) => {
                    DateAttribute.Value = date.ToUniversalTime ();
                    ValueLabel.Text = Pretty.BirthdayOrAnniversary (NachoPlatform.DateTimeFormatter.Instance, DateAttribute.Value);
                });
            }

        }

        class AddressFieldViewHolder : FieldViewHolder
        {

            EditText StreetField;
            EditText CityField;
            EditText StateField;
            EditText PostcodeField;
            EditText CountryField;
            McContactAddressAttribute AddressAttribute;

            public static AddressFieldViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditAddressItem, parent, false);
                return new AddressFieldViewHolder (view);
            }

            public AddressFieldViewHolder (View view) : base (view)
            {
                StreetField = view.FindViewById (Resource.Id.street) as EditText;
                CityField = view.FindViewById (Resource.Id.city) as EditText;
                StateField = view.FindViewById (Resource.Id.state) as EditText;
                PostcodeField = view.FindViewById (Resource.Id.postcode) as EditText;
                CountryField = view.FindViewById (Resource.Id.country) as EditText;

                StreetField.TextChanged += StreetChanged;
                CityField.TextChanged += CityChanged;
                StateField.TextChanged += StateChanged;
                PostcodeField.TextChanged += PostcodeChanged;
                CountryField.TextChanged += CountryChanged;
            }

            public void SetAttribute (McContactAddressAttribute attr, AvailableNamesDelegate availableNames)
            {
                base.SetAttribute (attr, availableNames);
                AddressAttribute = attr;
                StreetField.Text = attr.Street;
                CityField.Text = attr.City;
                StateField.Text = attr.State;
                PostcodeField.Text = attr.PostalCode;
                CountryField.Text = attr.Country;
            }

            void StreetChanged (object sender, TextChangedEventArgs e)
            {
                AddressAttribute.Street = StreetField.Text;
            }

            void CityChanged (object sender, TextChangedEventArgs e)
            {
                AddressAttribute.City = CityField.Text;
            }

            void StateChanged (object sender, TextChangedEventArgs e)
            {
                AddressAttribute.State = StateField.Text;
            }

            void PostcodeChanged (object sender, TextChangedEventArgs e)
            {
                AddressAttribute.PostalCode = PostcodeField.Text;
            }

            void CountryChanged (object sender, TextChangedEventArgs e)
            {
                AddressAttribute.Country = CountryField.Text;
            }

        }

        class NotesFieldViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            EditText NotesField;
            EventHandler<TextChangedEventArgs> NotesChanged;

            public static NotesFieldViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactEditNotesItem, parent, false);
                return new NotesFieldViewHolder (view);
            }

            public NotesFieldViewHolder (View view) : base (view)
            {
                NotesField = view.FindViewById (Resource.Id.notes) as EditText;
            }

            public void SetNotes (string notes)
            {
                NotesField.Text = notes;
            }

            public void SetNotesChanged (EventHandler<TextChangedEventArgs> notesChanged)
            {
                if (NotesChanged != null) {
                    NotesField.TextChanged -= NotesChanged;
                }
                NotesChanged = notesChanged;
                if (NotesChanged != null) {
                    NotesField.TextChanged += NotesChanged;
                }
            }

        }

    }
}
