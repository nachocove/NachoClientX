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

    public class MessageComposeHeaderField : LinearLayout
    {

        TextView Label;
        LinearLayout ContainerView;
        View SeparatorView;
        protected LinearLayout ContentView { get; private set; }

        public MessageComposeHeaderField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            Orientation = Orientation.Vertical;
            CreateContainerView (attrs);
            CreateSeparator (attrs);
            CreateLabel (attrs);
            CreateContentView ();

            AddView (ContainerView);
            AddView (SeparatorView);
            ContainerView.AddView (Label);
            ContainerView.AddView (ContentView);
        }

        void CreateContainerView (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.contentPaddingLeft,
                Resource.Attribute.contentPaddingTop,
                Resource.Attribute.contentPaddingRight,
                Resource.Attribute.contentPaddingBottom
            };
            ContainerView = new LinearLayout (Context);
            ContainerView.LayoutParameters = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            ContainerView.Orientation = Orientation.Horizontal;
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                ContainerView.SetPadding (
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingLeft, ContainerView.PaddingLeft),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingTop, ContainerView.PaddingTop),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingRight, ContainerView.PaddingRight),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingBottom, ContainerView.PaddingBottom)
                );
            }
        }

        void CreateSeparator (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.separatorColor
            };
            SeparatorView = new View (Context);
            var height = (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, 1.0f, Context.Resources.DisplayMetrics);
            SeparatorView.LayoutParameters = new LinearLayout.LayoutParams (LayoutParams.MatchParent, height);
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                SeparatorView.SetBackgroundColor (values.GetColor (Resource.Attribute.separatorColor, Android.Resource.Color.White));
            }
        }

        void CreateLabel (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.labelTextAppearance,
                Resource.Attribute.labelMarginTop,
                Resource.Attribute.labelMarginRight,
                Resource.Attribute.labelWidth,
                Resource.Attribute.labelText
            };
            Label = new TextView (Context);
            Label.SetSingleLine (true);
            Label.Ellipsize = TextUtils.TruncateAt.End;
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var width = values.GetDimensionPixelSize (Resource.Attribute.labelWidth, LayoutParams.WrapContent);
                var layoutParams = new LinearLayout.LayoutParams (width, LayoutParams.WrapContent);
                layoutParams.Gravity = GravityFlags.Top | GravityFlags.Left;
                layoutParams.TopMargin = values.GetDimensionPixelSize (Resource.Attribute.labelMarginTop, layoutParams.TopMargin);
                layoutParams.RightMargin = values.GetDimensionPixelSize (Resource.Attribute.labelMarginRight, layoutParams.RightMargin);
                Label.LayoutParameters = layoutParams;

                var textAppearance = values.GetResourceId (Resource.Attribute.labelTextAppearance, 0);
                if (textAppearance != 0) {
                    Label.SetTextAppearance (textAppearance);
                }

                Label.Text = values.GetString (Resource.Attribute.labelText);
            }

        }

        void CreateContentView ()
        {
            ContentView = new LinearLayout (Context);
            var layoutParams = new LinearLayout.LayoutParams (0, LayoutParams.MatchParent);
            layoutParams.Weight = 1.0f;
            ContentView.LayoutParameters = layoutParams;
        }
    }

    public class MessageComposeHeaderAddressField : MessageComposeHeaderField
    {
        public EmailAddressField AddressField { get; private set; }

        public MessageComposeHeaderAddressField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            CreateAddressField (attrs);
        }

        void CreateAddressField (Android.Util.IAttributeSet attrs)
        {
            AddressField = new EmailAddressField (Context);
            ContentView.AddView (AddressField);
        }
    }

    public class MessageComposeHeaderView : LinearLayout
    {

        public MessageComposeHeaderViewDelegate Delegate;

        #region Subviews

        public MessageComposeHeaderAddressField ToField { get; private set; }
        public MessageComposeHeaderAddressField CcField { get; private set; }
        public MessageComposeHeaderAddressField BccField { get; private set; }

        void FindSubviews ()
        {
            ToField = FindViewById (Resource.Id.to) as MessageComposeHeaderAddressField;
            CcField = FindViewById (Resource.Id.cc) as MessageComposeHeaderAddressField;
            BccField = FindViewById (Resource.Id.bcc) as MessageComposeHeaderAddressField;
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
        }

        #endregion



        public void FocusSubject ()
        {
        	//SubjectField.RequestFocus ();
        }

        public void Cleanup ()
        {
        }

        /*
        public EditText SubjectField;
        public EmailAddressField CcField;
        public EmailAddressField BccField;
        public TextView FromField;
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
            FromField = view.FindViewById<TextView> (Resource.Id.compose_from);
            FromField.Click += FromField_Click;
            SubjectField = view.FindViewById<EditText> (Resource.Id.compose_subject);
            SubjectField.FocusChange += SubjectFieldFocused;
            IntentContainer = view.FindViewById<LinearLayout> (Resource.Id.compose_intent_container);
            IntentValueLabel = view.FindViewById<TextView> (Resource.Id.compose_intent);
            SubjectField.TextChanged += SubjectChanged;
            IntentContainer.Click += SelectIntent;
            AttachmentsView = view.FindViewById<MessageComposeAttachmentsView> (Resource.Id.compose_attachments);
            AttachmentsView.HeaderView = this;
        }

        void FromField_Click (object sender, EventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidSelectFromField (this, FromField.Text);
            }
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

        public void Cleanup ()
        {
            ((ContactAddressAdapter)ToField.Adapter).Cleanup ();
            ((ContactAddressAdapter)CcField.Adapter).Cleanup ();
            ((ContactAddressAdapter)BccField.Adapter).Cleanup ();
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

        public void Cleanup ()
        {
            filter.Cleanup ();
            filter = null;
        }
    }
}

