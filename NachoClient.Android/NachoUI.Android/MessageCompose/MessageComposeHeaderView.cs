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

        void MessageComposeHeaderViewDidSelectFromField (MessageComposeHeaderView view, string from);

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
        bool UserHasOpenedFrom;
        bool HasMultipleAccounts;

        bool ShouldCollapseFrom {
            get {
                return (!UserHasOpenedFrom) && CcField.AddressField.Objects.Count == 0 && BccField.AddressField.Objects.Count == 0;
            }
        }

        #region Subviews

        public MessageComposeHeaderAddressField ToField { get; private set; }
        public MessageComposeHeaderAddressField CcField { get; private set; }
        public MessageComposeHeaderAddressField BccField { get; private set; }
        public MessageComposeHeaderLabelField FromField { get; private set; }
        public MessageComposeHeaderLabelField CollapsedFromField { get; private set; }
        public MessageComposeHeaderTextField SubjectField { get; private set; }
        public MessageComposeHeaderAttachmentsField AttachmentsField { get; private set; }

        void FindSubviews ()
        {
            ToField = FindViewById (Resource.Id.to) as MessageComposeHeaderAddressField;
            CcField = FindViewById (Resource.Id.cc) as MessageComposeHeaderAddressField;
            BccField = FindViewById (Resource.Id.bcc) as MessageComposeHeaderAddressField;
            FromField = FindViewById (Resource.Id.from) as MessageComposeHeaderLabelField;
            CollapsedFromField = FindViewById (Resource.Id.from_collapsed) as MessageComposeHeaderLabelField;
            SubjectField = FindViewById (Resource.Id.subject) as MessageComposeHeaderTextField;
            AttachmentsField = FindViewById (Resource.Id.attachments) as MessageComposeHeaderAttachmentsField;

            ToField.AddressField.AllowDuplicates (false);
            ToField.AddressField.Adapter = new ContactAddressAdapter (Context);
            ToField.AddressField.TokensChanged += ToFieldChanged;

            CcField.AddressField.AllowDuplicates (false);
            CcField.AddressField.Adapter = new ContactAddressAdapter (Context);
            CcField.AddressField.TokensChanged += CcFieldChanged;

            BccField.AddressField.AllowDuplicates (false);
            BccField.AddressField.Adapter = new ContactAddressAdapter (Context);
            BccField.AddressField.TokensChanged += BccFieldChanged;

            FromField.Click += FromClicked;
            CollapsedFromField.Click += CollapsedFromClicked;

            SubjectField.TextField.TextChanged += SubjectChanged;
            SubjectField.TextField.InputType = InputTypes.ClassText | InputTypes.TextFlagCapSentences;

            AttachmentsField.AddAttachment += AttachmentFieldClicked;
            AttachmentsField.AttachmentClicked += AttachmentClicked;
            AttachmentsField.AttachmentRemoved += AttachmentRemoved;
        }

        #endregion

        #region Creating a Header View

        public MessageComposeHeaderView (Context context) : base (context)
        {
            Initialize ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            Initialize ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            LayoutInflater.From (Context).Inflate (Resource.Layout.MessageComposeHeaderView, this);
            FindSubviews ();
            var accounts = McAccount.GetAllConfiguredNormalAccounts ();
            HasMultipleAccounts = accounts.Count > 1;
            if (!HasMultipleAccounts) {
                CollapsedFromField.Label.Text = Context.GetString (Resource.String.message_compose_field_ccbcc_collapsed);
            }
        }

        #endregion

        public void SetFromValue (string fromValue)
        {
            FromField.ValueLabel.Text = fromValue;
            if (HasMultipleAccounts) {
                CollapsedFromField.ValueLabel.Text = fromValue;
            }
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            CcField.Visibility = ShouldCollapseFrom ? ViewStates.Gone : ViewStates.Visible;
            BccField.Visibility = ShouldCollapseFrom ? ViewStates.Gone : ViewStates.Visible;
            FromField.Visibility = ShouldCollapseFrom || !HasMultipleAccounts ? ViewStates.Gone : ViewStates.Visible;
            CollapsedFromField.Visibility = ShouldCollapseFrom ? ViewStates.Visible : ViewStates.Gone;
            base.OnLayout (changed, l, t, r, b);
        }

        public void FocusSubject ()
        {
            SubjectField.TextField.RequestFocus ();
        }

        public void Cleanup ()
        {
            ((ContactAddressAdapter)ToField.AddressField.Adapter).Cleanup ();
            ((ContactAddressAdapter)CcField.AddressField.Adapter).Cleanup ();
            ((ContactAddressAdapter)BccField.AddressField.Adapter).Cleanup ();
        }

        #region Monitoring Changed

        void ToFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeTo (this, ToField.AddressField.AddressString);
            }
        }

        void SubjectChanged (object sender, TextChangedEventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeSubject (this, SubjectField.TextField.Text);
            }
        }


        void BccFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeBcc (this, BccField.AddressField.AddressString);
            }
        }

        void CcFieldChanged (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeCc (this, CcField.AddressField.AddressString);
            }
        }

        void FromClicked (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidSelectFromField (this, FromField.ValueLabel.Text);
            }
        }

        void CollapsedFromClicked (object sender, EventArgs e)
        {
            UserHasOpenedFrom = true;
            CcField.AddressField.RequestFocus ();
            RequestLayout ();
        }

        void AttachmentFieldClicked (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidSelectAddAttachment (this);
            }
        }

        void AttachmentClicked (object sender, NachoCore.Model.McAttachment e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidSelectAttachment (this, e);
            }
        }


        void AttachmentRemoved (object sender, NachoCore.Model.McAttachment e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidRemoveAttachment (this, e);
            }
        }

        #endregion

        #region Attachments

        public void SetAttachments (List<McAttachment> attachments)
        {
            AttachmentsField.Adapter.SetAttachments (attachments);
        }

        public void AddAttachment (McAttachment attachment)
        {
            AttachmentsField.Adapter.AddAttachment (attachment);
        }

        #endregion

        /*
        public TextView IntentValueLabel;
        public MessageComposeAttachmentsView AttachmentsView;
        LinearLayout IntentContainer;
        bool HasOpenedSubject;

        bool ShouldHideIntent {
            get {
                return !HasOpenedSubject && String.IsNullOrEmpty (SubjectField.Text);
            }
        }

        void CreateSubviews ()
        {

            CcField.FocusChange += CcFieldFocused;
            SubjectField.FocusChange += SubjectFieldFocused;

            IntentContainer = view.FindViewById<LinearLayout> (Resource.Id.compose_intent_container);
            IntentValueLabel = view.FindViewById<TextView> (Resource.Id.compose_intent);
            IntentContainer.Click += SelectIntent;
            AttachmentsView = view.FindViewById<MessageComposeAttachmentsView> (Resource.Id.compose_attachments);
            AttachmentsView.HeaderView = this;
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

        public void ShowIntentField ()
        {
            HasOpenedSubject = true;
            RequestLayout ();
        }
        */
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
                    // I have seen a case where PerformFiltering is called after Cleanup().  We don't
                    // have complete control over when PerformFiltering is called, so deal with the
                    // situation rather than try to prevent it.
                    if (null != searcher) {
                        searcher.SearchFor (constraint.ToString ());
                    }
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

            public void Cleanup ()
            {
                searcher.Dispose ();
                searcher = null;
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
                view = LayoutInflater.From (Context).Inflate (Resource.Layout.ContactSearchMenuItem, null) as LinearLayout;
            }

            var primaryLabel = view.FindViewById<TextView> (Resource.Id.main_label);
            var secondaryLabel = view.FindViewById<TextView> (Resource.Id.detail_label);
            var photoView = view.FindViewById<PortraitView> (Resource.Id.portrait_view);

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
            var initials = NachoCore.Utils.ContactsHelper.GetInitials (contact);
            photoView.SetPortrait (contact.PortraitId, contact.CircleColor, initials);

            return view;
        }

        ContactsFilter filter;

        public Filter Filter {
            get {
                return filter;
            }
        }

        public void Cleanup ()
        {
            filter.Cleanup ();
            filter = null;
        }
    }
}

