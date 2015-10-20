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
//        void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view);
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
        LinearLayout SubjectGroup;
        TextView SubjectLabel;
        public EditText SubjectField;
        LinearLayout ToGroup;
        TextView ToLabel;
        public EditText ToField;
        LinearLayout CcGroup;
        TextView CcLabel;
        public EditText CcField;
        
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
            ToGroup = new LinearLayout (Context);
            ToGroup.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            ToGroup.Orientation = Orientation.Horizontal;
            ToLabel = new TextView (Context);
            ToLabel.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            ToLabel.SetTextColor (Android.Graphics.Color.Black);
            ToLabel.SetBackgroundColor (Android.Graphics.Color.White);
            ToLabel.Text = "To:";
            ToField = new EditText (Context);
            ToField.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            ToField.TextChanged += ToChanged;
            ToField.SetTextColor (Android.Graphics.Color.Black);
            ToField.SetBackgroundColor (Android.Graphics.Color.White);
            ToField.InputType = InputTypes.TextVariationEmailAddress;
            ToGroup.AddView (ToLabel);
            ToGroup.AddView (ToField);
            AddView (ToGroup);

            CcGroup = new LinearLayout (Context);
            CcGroup.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            CcGroup.Orientation = Orientation.Horizontal;
            CcLabel = new TextView (Context);
            CcLabel.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            CcLabel.SetTextColor (Android.Graphics.Color.Black);
            CcLabel.SetBackgroundColor (Android.Graphics.Color.White);
            CcLabel.Text = "CC:";
            CcField = new EditText (Context);
            CcField.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            CcField.TextChanged += CcChanged;
            CcField.SetTextColor (Android.Graphics.Color.Black);
            CcField.SetBackgroundColor (Android.Graphics.Color.White);
            CcField.InputType = InputTypes.TextVariationEmailAddress;
            CcGroup.AddView (CcLabel);
            CcGroup.AddView (CcField);
            AddView (CcGroup);

            SubjectGroup = new LinearLayout (Context);
            SubjectGroup.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            SubjectGroup.Orientation = Orientation.Horizontal;
            SubjectLabel = new TextView (Context);
            SubjectLabel.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            SubjectLabel.SetTextColor (Android.Graphics.Color.Black);
            SubjectLabel.SetBackgroundColor (Android.Graphics.Color.White);
            SubjectLabel.Text = "Subject:";
            SubjectField = new EditText (Context);
            SubjectField.LayoutParameters = new ViewGroup.LayoutParams (ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
            SubjectField.TextChanged += SubjectChanged;
            SubjectField.SetTextColor (Android.Graphics.Color.Black);
            SubjectField.SetBackgroundColor (Android.Graphics.Color.White);
            SubjectField.InputType = InputTypes.TextVariationEmailSubject;
            SubjectGroup.AddView (SubjectLabel);
            SubjectGroup.AddView (SubjectField);
            AddView (SubjectGroup);
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

    }
}

