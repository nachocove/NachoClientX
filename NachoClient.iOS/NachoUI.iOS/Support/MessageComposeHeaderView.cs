//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Model;

namespace NachoClient.iOS
{
    
    // From an unmerged branch
    public class NcAdjustableLayoutTextField : UITextField
    {

        public CGRect? AdjustedLeftViewRect;
        public CGRect? AdjustedRightViewRect;
        public UIEdgeInsets? AdjustedEditingInsets;

        public NcAdjustableLayoutTextField (IntPtr handle) : base (handle)
        {
        }

        public NcAdjustableLayoutTextField (CGRect frame) : base (frame)
        {
        }

        public override CGRect LeftViewRect (CGRect forBounds)
        {
            if (AdjustedLeftViewRect.HasValue) {
                return AdjustedLeftViewRect.Value;
            }
            return base.LeftViewRect (forBounds);
        }

        public override CGRect RightViewRect (CGRect forBounds)
        {
            if (AdjustedRightViewRect.HasValue) {
                return AdjustedRightViewRect.Value;
            }
            return base.RightViewRect (forBounds);
        }

        public override CGRect EditingRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.EditingRect (forBounds);
        }

        public override CGRect TextRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.TextRect (forBounds);
        }

        public override CGRect PlaceholderRect (CGRect forBounds)
        {
            if (AdjustedEditingInsets.HasValue) {
                return new CGRect (
                    forBounds.X + AdjustedEditingInsets.Value.Left,
                    forBounds.Y + AdjustedEditingInsets.Value.Top,
                    forBounds.Width - AdjustedEditingInsets.Value.Left - AdjustedEditingInsets.Value.Right,
                    forBounds.Height - AdjustedEditingInsets.Value.Top - AdjustedEditingInsets.Value.Bottom
                );
            }
            return base.PlaceholderRect (forBounds);
        }
    }

    public interface MessageComposeHeaderViewDelegate {
        void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view);
    }

    public class MessageComposeHeaderView : UIView, IUcAddressBlockDelegate, IUcAttachmentBlockDelegate
    {

        #region Private Views

        private class MessageFieldLabel : UIView
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
                AddSubview (NameLabel);
                AddSubview (ValueLabel);
                DisclosureIndicatorView = Util.AddArrowAccessory (Bounds.Width - RightPadding - DisclosureWidth, 0, DisclosureWidth, this);
                DisclosureIndicatorView.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
                TapGesture = new UITapGestureRecognizer (Tap);
                AddGestureRecognizer (TapGesture);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                DisclosureIndicatorView.Frame = new CGRect (
                    DisclosureIndicatorView.Frame.X,
                    (Bounds.Height - DisclosureIndicatorView.Frame.Height) / 2.0f,
                    DisclosureIndicatorView.Frame.Width,
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
        nfloat preferredHeight = 0.0f;
        UcAddressBlock ToView;
        UcAddressBlock CcView;
        UcAddressBlock BccView;
        NcAdjustableLayoutTextField SubjectField;
        MessageFieldLabel IntentView;
        UcAttachmentBlock AttachmentsView;
        UIView ToSeparator;
        UIView CcSeparator;
        UIView BccSeparator;
        UIView SubjectSeparator;
        UIView IntentSeparator;
        UIView AttachmentsSeparator;
        bool HasOpenedCc;
        bool HasOpenedSubject;

        private nfloat LineHeight = 42.0f;
        private nfloat RightPadding = 15.0f;
        private nfloat LeftPadding = 15.0f;
        private UIFont LabelFont = A.Font_AvenirNextMedium14;
        private UIColor LabelColor = A.Color_NachoDarkText;

        bool ShouldHideBcc {
            get {
                return (!HasOpenedCc) && CcView.IsEmpty () && BccView.IsEmpty ();
            }
        }

        bool ShouldHideIntent {
            get {
                return !HasOpenedSubject;
            }
        }

        #endregion

        #region Constructors

        public MessageComposeHeaderView (CGRect frame) : base (frame)
        {
            ToView = new UcAddressBlock (this, "To:", null, Bounds.Width);
            CcView = new UcAddressBlock (this, "Cc:", "Cc/Bcc:", Bounds.Width);
            BccView = new UcAddressBlock (this, "Bcc:", null, Bounds.Width);

            CcView.SetCompact (true, -1, true);

            ToView.ConfigureView ();
            CcView.ConfigureView ();
            BccView.ConfigureView ();

            ToView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            CcView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            BccView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            var label = FieldLabel ("Subject:");
            SubjectField = new NcAdjustableLayoutTextField (new CGRect (0, 0, Bounds.Width, LineHeight));
            SubjectField.Font = LabelFont;
            SubjectField.TextColor = LabelColor;
            SubjectField.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            SubjectField.AccessibilityLabel = "Subject";
            SubjectField.LeftViewMode = UITextFieldViewMode.Always;
            SubjectField.AdjustedEditingInsets = new UIEdgeInsets (0.0f, LeftPadding + label.Frame.Width + 10.0f, 0.0f, RightPadding);
            SubjectField.AdjustedLeftViewRect = new CGRect(LeftPadding, (SubjectField.Frame.Height - label.Frame.Height) / 2.0, label.Frame.Width, label.Frame.Height);
            SubjectField.LeftView = label;
            // FIXME: compile error
//            SubjectField.EditingDidBegin += SubjectEditingDidBegin;

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

            AttachmentsView = new UcAttachmentBlock (this, Bounds.Width, 40, true);
            AttachmentsView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            ToSeparator = SeparatorView ();
            CcSeparator = SeparatorView ();
            BccSeparator = SeparatorView ();
            SubjectSeparator = SeparatorView ();
            IntentSeparator = SeparatorView ();
            AttachmentsSeparator = SeparatorView ();

            AddSubview (ToView);
            AddSubview (ToSeparator);
            AddSubview (CcView);
            AddSubview (CcSeparator);
            AddSubview (BccView);
            AddSubview (BccSeparator);
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
                HasOpenedCc = true;
            }
            view.SetCompact (false, -1);
            SetNeedsLayout ();
            LayoutIfNeeded ();
        }

        public void AddressBlockWillBecomeInactive (UcAddressBlock view)
        {
            view.SetCompact (true, -1);
        }

        public void AddressBlockAutoCompleteContactClicked(UcAddressBlock view, string prefix)
        {
        }

        public void AddressBlockSearchContactClicked(UcAddressBlock view, string prefix)
        {
        }

        public void DisplayAttachmentForAttachmentBlock (McAttachment attachment)
        {
        }

        public void ShowChooserForAttachmentBlock ()
        {
        }

        public void SubjectEditingDidBegin (object sender, EventArgs e)
        {
            HasOpenedSubject = true;
            SetNeedsLayout ();
            LayoutIfNeeded ();
        }

        private void SelectIntent ()
        {
            //                View.EndEditing (true);
            //                PerformSegue ("SegueToIntentSelection", this);
        }

        #endregion

        #region Layout

        public void AddressBlockNeedsLayout (UcAddressBlock view)
        {
//            SetNeedsLayout ();
//            LayoutIfNeeded ();
        }

        public void AttachmentBlockNeedsLayout (UcAttachmentBlock view)
        {
//            SetNeedsLayout ();
//            LayoutIfNeeded ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            nfloat y = 0.0f;

            BccView.Hidden = BccSeparator.Hidden = ShouldHideBcc;
            IntentView.Hidden = IntentSeparator.Hidden = ShouldHideIntent;

            y += LayoutSubviewAtYPosition (ToView, y);
            y += LayoutSubviewAtYPosition (ToSeparator, y);
            y += LayoutSubviewAtYPosition (CcView, y);
            y += LayoutSubviewAtYPosition (CcSeparator, y);
            y += LayoutSubviewAtYPosition (BccView, y);
            y += LayoutSubviewAtYPosition (BccSeparator, y);
            y += LayoutSubviewAtYPosition (SubjectField, y);
            y += LayoutSubviewAtYPosition (SubjectSeparator, y);
            y += LayoutSubviewAtYPosition (IntentView, y);
            y += LayoutSubviewAtYPosition (IntentSeparator, y);
            y += LayoutSubviewAtYPosition (AttachmentsView, y);
            y += LayoutSubviewAtYPosition (AttachmentsSeparator, y);

            preferredHeight = y;
        }
            
        private nfloat LayoutSubviewAtYPosition(UIView subview, nfloat y, float padding = 0f, nfloat? maxHeight = null, float minHeight = 0f)
        {
            if (subview.Hidden){
                return 0f;
            }
            var layoutHeight = subview.Frame.Height;
            if (maxHeight.HasValue) {
                layoutHeight = subview.SizeThatFits (new CGSize (subview.Frame.Width, maxHeight.Value)).Height;
            }
            if (layoutHeight < minHeight) {
                layoutHeight = minHeight;
            }
            subview.Frame = new CGRect (subview.Frame.X, y, subview.Frame.Width, layoutHeight);
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

        #endregion
    }
}

