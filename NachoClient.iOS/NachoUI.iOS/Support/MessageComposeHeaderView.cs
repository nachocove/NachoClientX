//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    
    public interface MessageComposeHeaderViewDelegate {
        void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject);
        void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment);
        void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment);
        void MessageComposeHeaderViewDidSelectContactChooser (MessageComposeHeaderView view, NcEmailAddress address);
        void MessageComposeHeaderViewDidSelectContactSearch (MessageComposeHeaderView view, NcEmailAddress address);
        void MessageComposeHeaderViewDidRemoveAddress (MessageComposeHeaderView view, NcEmailAddress address);
        void MessageComposeHeaderViewDidSelectFromField (MessageComposeHeaderView view);
    }

    public class MessageComposeHeaderView : UIView, IUcAddressBlockDelegate, IUcAttachmentBlockDelegate
    {

        #region Private Views

        public class MessageFieldLabel : UIView
        {
            public readonly UILabel NameLabel;
            public readonly UILabel ValueLabel;
            private UIView DisclosureIndicatorView;
            public Action Action;
            public nfloat LeftPadding = 0.0f;
            public nfloat RightPadding = 0.0f;
            private nfloat DisclosureWidth = 12.0f;

            UITapGestureRecognizer TapGesture;

            public MessageFieldLabel (CGRect frame) : base(frame)
            {
                NameLabel = new UILabel (Bounds);
                ValueLabel = new UILabel (Bounds);
                BackgroundColor = UIColor.White;
                AddSubview (NameLabel);
                AddSubview (ValueLabel);
                DisclosureIndicatorView = Util.AddArrowAccessory (Bounds.Width - RightPadding - DisclosureWidth, 0, DisclosureWidth, this);
                TapGesture = new UITapGestureRecognizer (Tap);
                AddGestureRecognizer (TapGesture);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                DisclosureIndicatorView.Frame = new CGRect (
                    Bounds.Width - RightPadding - DisclosureWidth,
                    (Bounds.Height - DisclosureIndicatorView.Frame.Height) / 2.0f,
                    DisclosureWidth,
                    DisclosureIndicatorView.Frame.Height
                );
                NameLabel.SizeToFit ();
                var x = LeftPadding;
                NameLabel.Frame = new CGRect (x, 0, NameLabel.Frame.Width, Bounds.Height);
                x += NameLabel.Frame.Width;
                ValueLabel.Frame = new CGRect (x, 0, DisclosureIndicatorView.Frame.X - x, Bounds.Height);
            }
           
            public void Tap ()
            {
                if (Action != null) {
                    Action ();
                }
            }
        }

        #endregion

        #region Properties

        public MessageComposeHeaderViewDelegate HeaderDelegate; 
        public nfloat PreferredHeight {
            get {
                if (preferredHeight == 0.0f) {
                    return (LineHeight + 1.0f) * 4.0f;
                }
                return preferredHeight;
            }
        }
        public bool AttachmentsAllowed = true;
        public readonly UcAddressBlock ToView;
        public readonly UcAddressBlock CcView;
        public readonly UcAddressBlock BccView;
        public readonly MessageFieldLabel FromView;
        public readonly NcAdjustableLayoutTextField SubjectField;
        public readonly MessageFieldLabel IntentView;
        public readonly UcAttachmentBlock AttachmentsView;
        UIView ToSeparator;
        UIView CcSeparator;
        UIView BccSeparator;
        UIView FromSeparator;
        UIView SubjectSeparator;
        UIView IntentSeparator;
        UIView AttachmentsSeparator;
        UcAddressBlock ActiveAddressView;
        bool HasOpenedSubject;
        bool CcFieldsAreCollapsed;
        nfloat preferredHeight = 0.0f;

        private nfloat LineHeight = 42.0f;
        private nfloat RightPadding = 15.0f;
        private nfloat LeftPadding = 15.0f;
        private UIFont LabelFont = A.Font_AvenirNextMedium14;
        private UIColor LabelColor = A.Color_NachoDarkText;

        bool ShouldHideIntent {
            get {
                return !HasOpenedSubject && String.IsNullOrEmpty(SubjectField.Text);
            }
        }

        public bool ShouldHideFrom;

        #endregion

        #region Constructors

        public MessageComposeHeaderView (CGRect frame) : base (frame)
        {
            CcFieldsAreCollapsed = true;

            ToView = new UcAddressBlock (this, "To:", null, Bounds.Width);
            CcView = new UcAddressBlock (this, "Cc:", "Cc/Bcc:", Bounds.Width);
            BccView = new UcAddressBlock (this, "Bcc:", null, Bounds.Width);

            ToView.SetCompact (true, -1);
            CcView.SetCompact (true, -1, true);
            BccView.SetCompact (true, -1);

            ToView.ConfigureView ();
            CcView.ConfigureView ();
            BccView.ConfigureView ();

            ToView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            CcView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            BccView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            FromView = new MessageFieldLabel (new CGRect (0, 0, Bounds.Width, LineHeight));
            FromView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            FromView.NameLabel.Font = LabelFont;
            FromView.ValueLabel.Font = LabelFont;
            FromView.NameLabel.TextColor = LabelColor;
            FromView.ValueLabel.TextColor = LabelColor;
            FromView.NameLabel.Text = "Cc/Bcc/From: ";
            FromView.Action = SelectFrom;
            FromView.LeftPadding = LeftPadding;
            FromView.RightPadding = RightPadding;
            FromView.SetNeedsLayout ();

            var label = FieldLabel ("Subject:");
            SubjectField = new NcAdjustableLayoutTextField (new CGRect (0, 0, Bounds.Width, LineHeight));
            SubjectField.BackgroundColor = UIColor.White;
            SubjectField.Font = LabelFont;
            SubjectField.TextColor = LabelColor;
            SubjectField.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            SubjectField.AccessibilityLabel = "Subject";
            SubjectField.LeftViewMode = UITextFieldViewMode.Always;
            SubjectField.AdjustedEditingInsets = new UIEdgeInsets (0.0f, LeftPadding + label.Frame.Width + 10.0f, 0.0f, RightPadding);
            SubjectField.AdjustedLeftViewRect = new CGRect(LeftPadding, (SubjectField.Frame.Height - label.Frame.Height) / 2.0, label.Frame.Width, label.Frame.Height);
            SubjectField.LeftView = label;
            SubjectField.EditingDidBegin += SubjectEditingDidBegin;
            SubjectField.EditingDidEnd += SubjectEditingDidEnd;

            IntentView = new MessageFieldLabel (new CGRect (0, 0, Bounds.Width, LineHeight));
            IntentView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            IntentView.NameLabel.Font = LabelFont;
            IntentView.ValueLabel.Font = LabelFont;
            IntentView.NameLabel.TextColor = LabelColor;
            IntentView.ValueLabel.TextColor = LabelColor;
            IntentView.NameLabel.Text = "Intent: ";
            IntentView.Action = SelectIntent;
            IntentView.LeftPadding = LeftPadding;
            IntentView.RightPadding = RightPadding;
            IntentView.SetNeedsLayout ();

//            if (!String.IsNullOrEmpty (PresetSubject)) {
//                alwaysShowIntent = true;
//                subjectField.Text += PresetSubject;
//            }

//            intentDisplayLabel.Text = "NONE";

            AttachmentsView = new UcAttachmentBlock (this, 40, true);
            AttachmentsView.Frame = new CGRect (0, 0, Bounds.Width, 40);
            AttachmentsView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            ToSeparator = SeparatorView ();
            CcSeparator = SeparatorView ();
            BccSeparator = SeparatorView ();
            FromSeparator = SeparatorView ();
            SubjectSeparator = SeparatorView ();
            IntentSeparator = SeparatorView ();
            AttachmentsSeparator = SeparatorView ();

            AddSubview (ToView);
            AddSubview (ToSeparator);
            AddSubview (CcView);
            AddSubview (CcSeparator);
            AddSubview (BccView);
            AddSubview (BccSeparator);
            AddSubview (FromView);
            AddSubview (FromSeparator);
            AddSubview (SubjectField);
            AddSubview (SubjectSeparator);
            AddSubview (IntentView);
            AddSubview (IntentSeparator);
            AddSubview (AttachmentsView);
            AddSubview (AttachmentsSeparator);

            SetNeedsLayout ();
        }

        #endregion

        #region User Actions

        public void AddressBlockWillBecomeActive (UcAddressBlock view)
        {
            if (view == CcView) {
                CcFieldsAreCollapsed = false;
            }
            view.SetNeedsLayout ();
            view.SetCompact (false, -1);
            SetNeedsLayout ();
            if (ActiveAddressView != null) {
                ActiveAddressView.SetCompact (true, -1);
                ActiveAddressView.SetNeedsLayout ();
            }
            UIView.Animate (0.2, () => {
                view.LayoutIfNeeded ();
                if (ActiveAddressView != null) {
                    ActiveAddressView.LayoutIfNeeded ();
                }
                LayoutIfNeeded ();
            });
            ActiveAddressView = view;
        }

        public void AddressBlockWillBecomeInactive (UcAddressBlock view)
        {
            if (ActiveAddressView == view) {
                view.SetCompact (true, -1);
                view.SetNeedsLayout ();
                SetNeedsLayout ();
                UIView.Animate (0.2, () => {
                    view.LayoutIfNeeded ();
                    LayoutIfNeeded ();
                });
                ActiveAddressView = null;
            }
        }

        public void AddressBlockAutoCompleteContactClicked(UcAddressBlock view, string prefix)
        {
            var address = EmailAddressForAddressView (view, prefix);
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectContactChooser (this, address);
            }
        }

        public void AddressBlockSearchContactClicked(UcAddressBlock view, string prefix)
        {
            var address = EmailAddressForAddressView (view, prefix);
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectContactSearch (this, address);
            }
        }

        public void AddressBlockRemovedAddress (UcAddressBlock view, NcEmailAddress address)
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidRemoveAddress (this, address);
            }
        }

        public void DisplayAttachmentForAttachmentBlock (McAttachment attachment)
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectAttachment (this, attachment);
            }
        }

        public void RemoveAttachmentForAttachmentBlock (McAttachment attachment)
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidRemoveAttachment (this, attachment);
            }
            SetNeedsLayout ();
            UIView.Animate (0.2, () => {
                AttachmentsView.LayoutIfNeeded ();
                LayoutIfNeeded ();
            });
        }

        public void ShowChooserForAttachmentBlock ()
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectAddAttachment (this);
            }
        }

        public void ToggleCompactForAttachmentBlock ()
        {
            AttachmentsView.ToggleCompact ();
            SetNeedsLayout ();
            UIView.Animate(0.2, () => {
                AttachmentsView.LayoutIfNeeded ();
                LayoutIfNeeded ();
            });
        }

        public void SubjectEditingDidBegin (object sender, EventArgs e)
        {
            HasOpenedSubject = true;
            SetNeedsLayout ();
            UIView.Animate (0.2, () => {
                LayoutIfNeeded ();
            });
        }

        public void SubjectEditingDidEnd (object sender, EventArgs e)
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidChangeSubject (this, SubjectField.Text);
            }
        }

        private void SelectIntent ()
        {
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectIntentField (this);
            }
        }

        private void SelectFrom ()
        {
            if (CcFieldsAreCollapsed) {
                CcFieldsAreCollapsed = false;
                UpdateCcCollapsed ();
                SetNeedsLayout ();
                UIView.Animate(0.2, () => {
                    CcView.LayoutIfNeeded ();
                    FromView.LayoutIfNeeded ();
                    LayoutIfNeeded ();
                });
            } else {
                HeaderDelegate.MessageComposeHeaderViewDidSelectFromField (this);
            }
        }

        #endregion

        #region Layout

        public void ShowIntentField ()
        {
            HasOpenedSubject = true;
            SetNeedsLayout ();
        }

        public void AddressBlockNeedsLayout (UcAddressBlock view)
        {
            SetNeedsLayout ();
        }

        public void AttachmentBlockNeedsLayout (UcAttachmentBlock view)
        {
            SetNeedsLayout ();
        }

        public void UpdateCcCollapsed ()
        {
            CcFieldsAreCollapsed = CcFieldsAreCollapsed && CcView.IsEmpty () && BccView.IsEmpty ();
            if (CcFieldsAreCollapsed) {
                FromView.NameLabel.Text = "Cc/Bcc/From: ";
                CcView.SetCompact (true, -1, true);
            } else {
                CcView.SetCompact (true, -1);
                FromView.NameLabel.Text = "From: ";
            }
            CcView.ConfigureView ();
            CcView.SetNeedsLayout ();
            FromView.SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            nfloat y = 0.0f;
            nfloat previousPreferredHeight = PreferredHeight;

            CcView.Hidden = CcSeparator.Hidden = !ShouldHideFrom && CcFieldsAreCollapsed;
            BccView.Hidden = BccSeparator.Hidden = CcFieldsAreCollapsed;
            FromView.Hidden = FromSeparator.Hidden = ShouldHideFrom;
            IntentView.Hidden = IntentSeparator.Hidden = ShouldHideIntent;
            AttachmentsView.Hidden = AttachmentsSeparator.Hidden = !AttachmentsAllowed;

            y += LayoutSubviewAtYPosition (ToView, y);
            y += LayoutSubviewAtYPosition (ToSeparator, y);
            y += LayoutSubviewAtYPosition (CcView, y);
            y += LayoutSubviewAtYPosition (CcSeparator, y);
            y += LayoutSubviewAtYPosition (BccView, y);
            y += LayoutSubviewAtYPosition (BccSeparator, y);
            y += LayoutSubviewAtYPosition (FromView, y);
            y += LayoutSubviewAtYPosition (FromSeparator, y);
            y += LayoutSubviewAtYPosition (SubjectField, y);
            y += LayoutSubviewAtYPosition (SubjectSeparator, y);
            y += LayoutSubviewAtYPosition (IntentView, y);
            y += LayoutSubviewAtYPosition (IntentSeparator, y);
            y += LayoutSubviewAtYPosition (AttachmentsView, y);
            y += LayoutSubviewAtYPosition (AttachmentsSeparator, y);

            preferredHeight = y;

            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);

            if ((Math.Abs(preferredHeight - previousPreferredHeight) > 0.5) && HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidChangeHeight (this);
            }
        }
            
        private nfloat LayoutSubviewAtYPosition(UIView subview, nfloat y, float padding = 0f, nfloat? maxHeight = null, float minHeight = 0f)
        {
            var layoutHeight = subview.Frame.Height;
            if (maxHeight.HasValue) {
                layoutHeight = subview.SizeThatFits (new CGSize (subview.Frame.Width, maxHeight.Value)).Height;
            }
            if (layoutHeight < minHeight) {
                layoutHeight = minHeight;
            }
            subview.Frame = new CGRect (subview.Frame.X, y, subview.Frame.Width, layoutHeight);
            if (subview.Hidden){
                return 0f;
            }
            return layoutHeight + padding;
        }

        #endregion

        #region Helpers

        private UIView SeparatorView ()
        {
            var separator = new UIView (new CGRect (0, 0, Bounds.Width, 1));
            separator.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            separator.BackgroundColor = A.Color_NachoNowBackground;
            return separator;
        }

        private UILabel FieldLabel (String text)
        {
            var label = new UILabel(new CGRect(0, 0, Bounds.Width, LineHeight));
            label.BackgroundColor = UIColor.White;
            label.TextColor = LabelColor;
            label.Font = LabelFont;
            label.Text = text;
            label.SizeToFit ();
            return label;
        }

        private NcEmailAddress EmailAddressForAddressView (UcAddressBlock view, string prefix)
        {
            NcEmailAddress.Kind kind = NcEmailAddress.Kind.Unknown;
            if (view == ToView) {
                kind = NcEmailAddress.Kind.To;
            } else if (view == CcView) {
                kind = NcEmailAddress.Kind.Cc;
            } else if (view == BccView) {
                kind = NcEmailAddress.Kind.Bcc;
            } else {
                NcAssert.CaseError ();
            }
            var emailAddress = new NcEmailAddress (kind);
            emailAddress.action = NcEmailAddress.Action.create;
            emailAddress.address = prefix;
            return emailAddress;
        }

        #endregion
    }
}

