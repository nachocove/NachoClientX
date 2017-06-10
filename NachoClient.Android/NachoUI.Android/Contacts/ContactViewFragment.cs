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
using Android.Support.V7.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;


namespace NachoClient.AndroidClient
{
    public class ContactViewFragment : Fragment, ContactViewAdapter.Listener
    {

        public McContact Contact;
        ContactViewAdapter Adapter;

        public ContactViewFragment () : base ()
        {
            RetainInstance = true;
        }

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
            var view = inflater.Inflate (Resource.Layout.ContactViewFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactViewAdapter (this, Contact);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Listener

        public void OnEmailSelected (string email)
        {
            var account = McAccount.EmailAccountForContact (Contact);
            var intent = MessageComposeActivity.NewMessageIntent (Activity, account.Id, email);
            StartActivity (intent);
        }

        public void OnPhoneSelected (string phone)
        {
            Util.CallNumber (Activity, Contact, phone);
        }

        public void OnMessageSelected (McEmailMessageThread thread, McEmailMessage message)
        {
            if (thread.HasMultipleMessages ()) {
                ShowThread (thread);
            } else {
                ShowMessage (message);
            }
        }

        public void OnMoreMessagesSelected ()
        {
            var intent = MessageListActivity.BuildContactIntent (Activity, Contact);
            StartActivity (intent);
        }

        #endregion

        #region Private Helpers

        public void Update ()
        {
            Adapter.SetContact (Contact);
            Adapter.NotifyDataSetChanged ();
        }

        void ShowThread (McEmailMessageThread thread)
        {
            var folder = Adapter.Messages.GetFolderForThread (thread);
            var intent = MessageListActivity.BuildThreadIntent (Activity, folder, thread);
            StartActivity (intent);
        }

        void ShowMessage (McEmailMessage message)
        {
            var intent = MessageViewActivity.BuildIntent (Activity, message.Id);
            StartActivity (intent);
        }

        #endregion

    }

    public class ContactViewAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnEmailSelected (string email);
            void OnPhoneSelected (string phone);
            void OnMessageSelected (McEmailMessageThread thread, McEmailMessage message);
            void OnMoreMessagesSelected ();
        }

        enum ViewType
        {
            Info,
            Message,
            BasicItem
        }

        WeakReference<Listener> WeakListener;
        public NachoEmailMessages Messages { get; private set; }
        int MaxMessageCount = 5;
        bool HasMoreMessages;
        McContact Contact;
        List<ContactField> Fields;

        public ContactViewAdapter (Listener listener, McContact contact) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            SetContact (contact);
            Messages.BackgroundRefresh ((changed, adds, deletes) => {
                if (changed) {
                    ConfigureGroups ();
                    NotifyDataSetChanged ();
                }
            });
        }

        public void SetContact (McContact contact)
        {
            Contact = contact;
            Messages = new UserInteractionEmailMessages (Contact);
            Fields = ContactField.FieldsFromContact (Contact);
            ConfigureGroups ();
            NotifyDataSetChanged ();
        }

        int _GroupCount = 0;
        int InfoGroupPosition = 0;
        int MessagesGroupPosition = -1;
        int MessagesExtraItemCount = 0;
        int MessagesMoreExtraPosition = -1;
        int FieldsGroupPosition = -1;

        void ConfigureGroups ()
        {
            _GroupCount = 1;
            InfoGroupPosition = 0;
            MessagesGroupPosition = -1;
            MessagesExtraItemCount = 0;
            MessagesMoreExtraPosition = -1;
            FieldsGroupPosition = -1;

            if (Messages.Count () > 0) {
                MessagesGroupPosition = _GroupCount++;
                HasMoreMessages = Messages.Count () > MaxMessageCount;
                if (HasMoreMessages) {
                    MessagesMoreExtraPosition = MessagesExtraItemCount++;
                }
            }
            if (Fields.Count > 0) {
                FieldsGroupPosition = _GroupCount++;
            }
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == InfoGroupPosition) {
                return 1;
            }
            if (groupPosition == MessagesGroupPosition) {
                return (HasMoreMessages ? MaxMessageCount : Messages.Count ()) + MessagesExtraItemCount;
            }
            if (groupPosition == FieldsGroupPosition) {
                return Fields.Count;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactViewFragment.GroupItemCount unexpected groupPosition: {0}", groupPosition));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.Info:
                return ContactInfoViewHolder.Create (parent);
            case ViewType.Message:
                return MessageViewHolder.Create (parent);
            case ViewType.BasicItem:
                return SettingsBasicItemViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactViewFragment.OnCreateGroupedViewHolder unknown view type: {0}", viewType));
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                return (int)ViewType.Info;
            }
            if (groupPosition == MessagesGroupPosition) {
                var count = HasMoreMessages ? MaxMessageCount : Messages.Count ();
                if (position < count) {
                    return (int)ViewType.Message;
                } else {
                    var extraPosition = position - count;
                    if (extraPosition == MessagesMoreExtraPosition) {
                        return (int)ViewType.BasicItem;
                    }
                }
            }
            if (groupPosition == FieldsGroupPosition) {
                return (int)ViewType.BasicItem;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactViewFragment.GetItemViewType unexpected position: {0}.{1}", groupPosition, position));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == MessagesGroupPosition) {
                return context.GetString (Resource.String.contact_interactions);
            }
            if (groupPosition == FieldsGroupPosition) {
                return context.GetString (Resource.String.contact_fields);
            }
            return null;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == InfoGroupPosition) {
                var infoHolder = (holder as ContactInfoViewHolder);
                infoHolder.SetContact (Contact);
                infoHolder.SetEventHandlers ((sender, e) => {
                    Listener listener;
                    if (WeakListener.TryGetTarget (out listener)) {
                        listener.OnEmailSelected (Contact.GetPrimaryCanonicalEmailAddress ());
                    }
                }, (sender, e) => {
                    Listener listener;
                    if (WeakListener.TryGetTarget (out listener)) {
                        listener.OnPhoneSelected (Contact.GetPrimaryPhoneNumber ());
                    }
                });
                return;
            }
            if (groupPosition == MessagesGroupPosition) {
                var count = HasMoreMessages ? MaxMessageCount : Messages.Count ();
                if (position < count) {
                    var messageHolder = (holder as MessageViewHolder);
                    var message = Messages.GetCachedMessage (position);
                    var thread = Messages.GetEmailThread (position);
                    messageHolder.SetMessage (message, thread.MessageCount);
                    messageHolder.IndicatorColor = 0;
                    return;
                } else {
                    var extraPosition = position - count;
                    if (extraPosition == MessagesMoreExtraPosition) {
                        holder.ItemView.Clickable = true;
                        (holder as SettingsBasicItemViewHolder).SetLabels (Resource.String.contact_more_messages);
                        return;
                    }
                }
            }
            if (groupPosition == FieldsGroupPosition) {
                if (position < Fields.Count) {
                    var fieldHolder = (holder as SettingsBasicItemViewHolder);
                    var field = Fields [position];
                    fieldHolder.SetLabels (field.Name, field.DisplayValue);
                    fieldHolder.ItemView.Clickable = field.Email != null || field.Phone != null;
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("ContactViewFragment.OnBindViewHolder unexpected position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == MessagesGroupPosition) {
                    var count = HasMoreMessages ? MaxMessageCount : Messages.Count ();
                    if (position < count) {
                        var message = Messages.GetCachedMessage (position);
                        var thread = Messages.GetEmailThread (position);
                        listener.OnMessageSelected (thread, message);
                    } else {
                        var extraPosition = position - count;
                        if (extraPosition == MessagesMoreExtraPosition) {
                            listener.OnMoreMessagesSelected ();
                        }
                    }
                } else if (groupPosition == FieldsGroupPosition) {
                    var field = Fields [position];
                    if (field.Email != null) {
                        listener.OnEmailSelected (field.Email);
                    } else if (field.Phone != null) {
                        listener.OnPhoneSelected (field.Phone);
                    }
                }
            }
        }

        class ContactInfoViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView NameLabel;
            TextView DetailLabel;
            PortraitView PortraitView;
            View EmailButton;
            View CallButton;

            EventHandler EmailClickHandler;
            EventHandler CallClickHandler;

            public static ContactInfoViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactViewInfoItem, parent, false);
                return new ContactInfoViewHolder (view);
            }

            public ContactInfoViewHolder (View view) : base (view)
            {
                NameLabel = view.FindViewById (Resource.Id.name) as TextView;
                DetailLabel = view.FindViewById (Resource.Id.detail) as TextView;
                PortraitView = view.FindViewById (Resource.Id.portrait) as PortraitView;
                EmailButton = view.FindViewById (Resource.Id.email_button);
                CallButton = view.FindViewById (Resource.Id.call_button);
                EmailButton.Click += EmailClicked;
                CallButton.Click += CallClicked;
            }

            public void SetContact (McContact contact)
            {
                var name = contact.GetDisplayName ();
                var email = contact.GetPrimaryCanonicalEmailAddress ();
                var phone = contact.GetPrimaryPhoneNumber ();

                if (!String.IsNullOrEmpty (name)) {
                    NameLabel.Text = name;

                    if (!String.IsNullOrEmpty (email)) {
                        DetailLabel.Text = email;
                    } else if (!String.IsNullOrEmpty (phone)) {
                        DetailLabel.Text = phone;
                    } else {
                        DetailLabel.Text = "";
                    }
                } else {
                    if (!String.IsNullOrEmpty (email)) {
                        NameLabel.Text = email;
                        if (!String.IsNullOrEmpty (phone)) {
                            DetailLabel.Text = phone;
                        } else {
                            DetailLabel.Text = "";
                        }
                    } else if (!String.IsNullOrEmpty (phone)){
                        NameLabel.Text = phone;
                        DetailLabel.Text = "";
                    } else {
                        NameLabel.Text = "Unnamed";
                        DetailLabel.Text = "";
                    }
                }

                PortraitView.SetPortrait(contact.PortraitId, contact.CircleColor, NachoCore.Utils.ContactsHelper.GetInitials (contact));

                if (!String.IsNullOrEmpty (email)) {
                    EmailButton.Visibility = ViewStates.Visible;
                } else {
                    EmailButton.Visibility = ViewStates.Gone;
                }

                if (!String.IsNullOrEmpty (phone)) {
                    CallButton.Visibility = ViewStates.Visible;
                } else {
                    CallButton.Visibility = ViewStates.Gone;
                }
            }

            void EmailClicked (object sender, EventArgs e)
            {
                EmailClickHandler (sender, e);
            }

            void CallClicked (object sender, EventArgs e)
            {
                CallClickHandler (sender, e);
            }

            public void SetEventHandlers (EventHandler emailClickHandler, EventHandler callClickHandler)
            {
                EmailClickHandler = emailClickHandler;
                CallClickHandler = callClickHandler;
            }
        }

    }
}
