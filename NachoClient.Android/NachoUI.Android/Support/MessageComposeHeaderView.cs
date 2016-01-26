//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Text;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Generic;

namespace NachoClient.AndroidClient
{

    public interface MessageComposeHeaderViewDelegate
    {
        //        void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject);

        void MessageComposeHeaderViewDidChangeTo (MessageComposeHeaderView view, string to);

        void MessageComposeHeaderViewDidChangeCc (MessageComposeHeaderView view, string cc);

        void MessageComposeHeaderViewDidChangeBcc (MessageComposeHeaderView view, string bcc);

        void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view);

        void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view);

        void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment);

        void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment);
        //        void MessageComposeHeaderViewDidSelectContactChooser (MessageComposeHeaderView view, NcEmailAddress address);
        //        void MessageComposeHeaderViewDidSelectContactSearch (MessageComposeHeaderView view, NcEmailAddress address);
        //        void MessageComposeHeaderViewDidRemoveAddress (MessageComposeHeaderView view, NcEmailAddress address);
    }

    public class MessageComposeHeaderView : LinearLayout
    {

        public MessageComposeHeaderViewDelegate Delegate;
        public EditText SubjectField;
        public EmailAddressField ToField;
        public EmailAddressField CcField;
        public EmailAddressField BccField;
        public TextView IntentValueLabel;
        public TextView CcLabel;
        public MessageComposeAttachmentsView AttachmentsView;
        LinearLayout IntentContainer;
        LinearLayout BccContainer;
        bool HasOpenedSubject;
        bool HasOpenedCc;

        bool ShouldHideIntent {
            get {
                return !HasOpenedSubject && String.IsNullOrEmpty (SubjectField.Text);
            }
        }

        bool ShouldHideBcc {
            get {
                return (!HasOpenedCc) && CcField.Objects.Count == 0 && BccField.Objects.Count == 0;
            }
        }

        public MessageComposeHeaderView (Context context) : base (context)
        {
            CreateSubviews ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            // This is the constructor that evidently gets called by the xml
            CreateSubviews ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            CreateSubviews ();
        }

        void CreateSubviews ()
        {

            var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.MessageComposeHeaderView, this);

            ToField = view.FindViewById<EmailAddressField> (Resource.Id.compose_to);
            ToField.AllowDuplicates (false);
            ToField.Adapter = new ContactAddressAdapter (Context);
            ToField.TokensChanged += ToFieldChanged;

            CcField = view.FindViewById<EmailAddressField> (Resource.Id.compose_cc);
            CcField.AllowDuplicates (false);
            CcField.Adapter = new ContactAddressAdapter (Context);
            CcField.FocusChange += CcFieldFocused;
            CcField.TokensChanged += CcFieldChanged;

            BccField = view.FindViewById<EmailAddressField> (Resource.Id.compose_bcc);
            BccField.AllowDuplicates (false);
            BccField.Adapter = new ContactAddressAdapter (Context);
            BccField.TokensChanged += BccFieldChanged;

            CcLabel = view.FindViewById<TextView> (Resource.Id.compose_cc_label);
            BccContainer = view.FindViewById<LinearLayout> (Resource.Id.compose_bcc_container);
            SubjectField = view.FindViewById<EditText> (Resource.Id.compose_subject);
            SubjectField.FocusChange += SubjectFieldFocused;
            IntentContainer = view.FindViewById<LinearLayout> (Resource.Id.compose_intent_container);
            IntentValueLabel = view.FindViewById<TextView> (Resource.Id.compose_intent);
            SubjectField.TextChanged += SubjectChanged;
            IntentContainer.Click += SelectIntent;
            AttachmentsView = view.FindViewById<MessageComposeAttachmentsView> (Resource.Id.compose_attachments);
            AttachmentsView.HeaderView = this;
        }

        void BccFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeBcc (this, BccField.AddressString);
            }
        }

        void CcFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeCc (this, CcField.AddressString);
            }
        }

        void ToFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeTo (this, ToField.AddressString);
            }
        }

        void CcFieldFocused (object sender, FocusChangeEventArgs e)
        {
            if (CcField.HasFocus) {
                HasOpenedCc = true;
                RequestLayout ();
            }
        }

        void SubjectFieldFocused (object sender, FocusChangeEventArgs e)
        {
            if (SubjectField.HasFocus) {
                HasOpenedSubject = true;
                RequestLayout ();
            }
        }

        void SelectIntent (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidSelectIntentField (this);
            }
        }

        void SubjectChanged (object sender, TextChangedEventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeSubject (this, SubjectField.Text);
            }
        }

        public void FocusSubject ()
        {
            SubjectField.RequestFocus ();
        }

        public void ShowIntentField ()
        {
            HasOpenedSubject = true;
            RequestLayout ();
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            IntentContainer.Visibility = ShouldHideIntent ? ViewStates.Gone : ViewStates.Visible;
            BccContainer.Visibility = ShouldHideBcc ? ViewStates.Gone : ViewStates.Visible;
            CcLabel.Text = ShouldHideBcc ? "Cc/Bcc:" : "Cc:";
            base.OnLayout (changed, l, t, r, b);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                ToField.Adapter.Dispose ();
                CcField.Adapter.Dispose ();
                BccField.Adapter.Dispose ();
            }
            base.Dispose (disposing);
        }
    }

    class ContactAddressAdapter : BaseAdapter<EmailAddressField.TokenObject>, IFilterable
    {

        List<McContactEmailAddressAttribute> SearchResults;

        public SearchHelper Searcher { get; }

        Context Context;

        private class ContactsFilter : Filter
        {
            public delegate void SearchResultsFound (List<McContactEmailAddressAttribute> searchResults);

            public SearchResultsFound HandleSearch;

            private ContactsEmailSearch searcher;
            private List<McContactEmailAddressAttribute> cachedResults;

            private class ResultsWrapper : Java.Lang.Object
            {
                public readonly List<McContactEmailAddressAttribute> ContactResults;

                public ResultsWrapper (List<McContactEmailAddressAttribute> contactResults)
                {
                    ContactResults = contactResults;
                }
            }

            public ContactsFilter ()
            {
                searcher = new ContactsEmailSearch ((string searchString, List<McContactEmailAddressAttribute> results) => {
                    cachedResults = results;
                    if (null != HandleSearch) {
                        HandleSearch (results);
                    }
                });
                cachedResults = new List<McContactEmailAddressAttribute> ();
            }

            protected override FilterResults PerformFiltering (Java.Lang.ICharSequence constraint)
            {
                // ContactsEmailSearch and Filter both want to manage when the searches are run.
                // This causes a clash that doesn't have an easy resolution.  Let ContactsEmailSearch
                // manage the searches and UI updates.  This method always returns immediately with
                // whatever the UI is currently displaying.  (It can't return an empty set of results,
                // on the list will be temporarily cleared.)
                if (null == constraint) {
                    cachedResults = new List<McContactEmailAddressAttribute> ();
                } else {
                    searcher.SearchFor (constraint.ToString ());
                }
                return new FilterResults () {
                    Values = new ResultsWrapper (cachedResults),
                    Count = cachedResults.Count,
                };
            }

            protected override void PublishResults (Java.Lang.ICharSequence constraint, FilterResults results)
            {
                // Results are sent directly from ContactsEmailSearch to the UI, bypassing PublishResults.
                return;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing) {
                    searcher.Dispose ();
                    searcher = null;
                }
                base.Dispose (disposing);
            }
        }

        public ContactAddressAdapter (Context context) : base ()
        {
            Context = context;
            SearchResults = new List<McContactEmailAddressAttribute> ();
            filter = new ContactsFilter ();
            filter.HandleSearch = HandleSearch;
        }

        void HandleSearch (List<McContactEmailAddressAttribute> searchResults)
        {
            SearchResults = searchResults;
            NotifyDataSetChanged ();
        }

        public override EmailAddressField.TokenObject this [int index] {
            get {
                var addressAttr = SearchResults [index];
                var address = McEmailAddress.QueryById<McEmailAddress> (addressAttr.EmailAddress);
                var contact = addressAttr.GetContact ();
                if (null == address) {
                    NcAssert.True (McEmailAddress.Get (contact.AccountId, contact.GetEmailAddress (), out address));
                    Log.Error (Log.LOG_CONTACTS, "TokenObject address is null");
                }
                return new EmailAddressField.TokenObject (contact, new NcEmailAddress (NcEmailAddress.Kind.Unknown, address.CanonicalEmailAddress));
            }
        }

        public override int Count {
            get {
                return SearchResults.Count;
            }
        }

        public override long GetItemId (int position)
        {
            return SearchResults [position].GetContact ().Id;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView as LinearLayout;
            if (convertView == null) {
                var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
                view = inflater.Inflate (Resource.Layout.ContactSearchMenuItem, null) as LinearLayout;
            }

            var primaryLabel = view.FindViewById<TextView> (Resource.Id.contact_item_primary_label);
            var secondaryLabel = view.FindViewById<TextView> (Resource.Id.contact_item_secondary_label);
            var photoView = view.FindViewById<ContactPhotoView> (Resource.Id.contact_item_photo);

            var contact = SearchResults [position].GetContact ();
            string email = SearchResults [position].Value;
            string displayName = contact.GetDisplayName ();

            if (!string.IsNullOrEmpty (displayName) && displayName != email) {
                primaryLabel.Text = displayName;
                secondaryLabel.Text = email;
                secondaryLabel.Visibility = ViewStates.Visible;
            } else {
                primaryLabel.Text = email;
                secondaryLabel.Visibility = ViewStates.Gone;
            }
            photoView.SetContact (contact);

            return view;
        }

        ContactsFilter filter;

        public Filter Filter {
            get {
                return filter;
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                filter.Dispose ();
                filter = null;
            }
            base.Dispose (disposing);
        }
    }
}

