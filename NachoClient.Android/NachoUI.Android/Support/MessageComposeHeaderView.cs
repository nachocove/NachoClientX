//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Text;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{

    public interface MessageComposeHeaderViewDelegate {
//        void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject);
        void MessageComposeHeaderViewDidChangeTo (MessageComposeHeaderView view, string to);
        void MessageComposeHeaderViewDidChangeCc (MessageComposeHeaderView view, string cc);
        void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view);
//        void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view);
//        void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment);
//        void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment);
//        void MessageComposeHeaderViewDidSelectContactChooser (MessageComposeHeaderView view, NcEmailAddress address);
//        void MessageComposeHeaderViewDidSelectContactSearch (MessageComposeHeaderView view, NcEmailAddress address);
//        void MessageComposeHeaderViewDidRemoveAddress (MessageComposeHeaderView view, NcEmailAddress address);
    }

    public class MessageComposeHeaderView : LinearLayout
    {

        public MessageComposeHeaderViewDelegate Delegate;
        public EditText SubjectField;
        public EditText ToField;
        public EditText CcField;
        public TextView IntentValueLabel;
        LinearLayout IntentContainer;
        bool HasOpenedSubject;

        bool ShouldHideIntent {
            get {
                return !HasOpenedSubject && String.IsNullOrEmpty(SubjectField.Text);
            }
        }
        
        public MessageComposeHeaderView (Context context) : base(context)
        {
            CreateSubviews ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs) : base(context, attrs)
        {
            // This is the constructor that evidently gets called by the xml
            CreateSubviews ();
        }

        public MessageComposeHeaderView (Context context, Android.Util.IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            CreateSubviews ();
        }

        void CreateSubviews ()
        {

            var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.MessageComposeHeaderView, this);

            ToField = view.FindViewById<EditText> (Resource.Id.compose_to);
            CcField = view.FindViewById<EditText> (Resource.Id.compose_cc);
            SubjectField = view.FindViewById<EditText> (Resource.Id.compose_subject);
            SubjectField.FocusChange += SubjectFieldFocused;
            IntentContainer = view.FindViewById<LinearLayout> (Resource.Id.compose_intent_container);
            IntentValueLabel = view.FindViewById<TextView> (Resource.Id.compose_intent);
            ToField.TextChanged += ToChanged;
            CcField.TextChanged += CcChanged;
            SubjectField.TextChanged += SubjectChanged;
            IntentContainer.Click += SelectIntent;
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

        void ToChanged (object sender, TextChangedEventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeTo (this, ToField.Text);
            }
        }

        void CcChanged (object sender, TextChangedEventArgs e)
        {
            if (Delegate != null) {
                Delegate.MessageComposeHeaderViewDidChangeCc (this, CcField.Text);
            }
        }

        public void FocusSubject ()
        {
            SubjectField.RequestFocus ();
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            IntentContainer.Visibility = ShouldHideIntent ? ViewStates.Gone : ViewStates.Visible;
            base.OnLayout (changed, l, t, r, b);
        }

    }
}

