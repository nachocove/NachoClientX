//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface MessageComposeHeaderViewDelegate
    {
        void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject);
        void MessageComposeHeaderViewDidChangeTo (MessageComposeHeaderView view, string to);
        void MessageComposeHeaderViewDidChangeCc (MessageComposeHeaderView view, string cc);
        void MessageComposeHeaderViewDidChangeBcc (MessageComposeHeaderView view, string bcc);
        void MessageComposeHeaderViewDidSearchTo (MessageComposeHeaderView view, string search);
        void MessageComposeHeaderViewDidSearchCc (MessageComposeHeaderView view, string search);
        void MessageComposeHeaderViewDidSearchBcc (MessageComposeHeaderView view, string search);
        void MessageComposeHeaderViewDidSelectToChooser (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidSelectCcChooser (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidSelectBccChooser (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidSelectFromField (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view);
        void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment);
        void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment);
        void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view);
    }

    public class MessageComposeFieldView : UIView, ThemeAdopter
    {
        public WeakReference<MessageComposeHeaderView> WeakHeaderView = new WeakReference<MessageComposeHeaderView> (null);
        public UILabel NameLabel { get; private set; }
        public UIView ContentView { get; private set; }
        public UIView SeparatorView { get; private set; }

        private UIView _AccessoryView;
        public UIView AccessoryView {
            get {
                return _AccessoryView;
            }
            set {
                if (_AccessoryView != null) {
                    _AccessoryView.RemoveFromSuperview ();
                }
                _AccessoryView = value;
                if (_AccessoryView != null) {
                    AddSubview (_AccessoryView);
                }
                SetNeedsLayout ();
            }
        }

        public string Name {
            get {
                return NameLabel.Text;
            }
            set {
                NameLabel.Text = value;
                if (!_NameWidth.HasValue) {
                    SetNeedsLayout ();
                }
            }
        }

        private nfloat? _NameWidth;
        public nfloat? NameWidth {
            get {
                return _NameWidth;
            }
            set {
                _NameWidth = value;
                SetNeedsLayout ();
            }
        }

        private nfloat _NameContentSpacing = 0.0f;
        public nfloat NameContentSpacing {
            get {
                return _NameContentSpacing;
            }
            set {
                _NameContentSpacing = value;
                SetNeedsLayout ();
            }
        }

        private UIEdgeInsets _ContentInsets = new UIEdgeInsets (4.0f, 10.0f, 4.0f, 10.0f);
        public UIEdgeInsets ContentInsets {
            get {
                return _ContentInsets;
            }
            set {
                _ContentInsets = value;
                SetNeedsLayout ();
            }
        }

        private nfloat SeparatorSize = 1.0f;

        public MessageComposeFieldView (string name, UIView contentView) : base ()
        {
            ContentView = contentView;

            NameLabel = new UILabel ();
            NameLabel.Text = name;
            NameLabel.Lines = 1;
            NameLabel.LineBreakMode = UILineBreakMode.MiddleTruncation;

            SeparatorView = new UIView ();

            AddSubview (ContentView);
            AddSubview (NameLabel);
            AddSubview (SeparatorView);
        }

        public virtual void Cleanup ()
        {
        }

        public virtual void AdoptTheme (Theme theme)
        {
            if (ContentView is ThemeAdopter) {
                (ContentView as ThemeAdopter).AdoptTheme (theme);
            }
            TintColor = theme.TableViewTintColor;
            NameLabel.Font = theme.DefaultFont.WithSize (14.0f);
            NameLabel.TextColor = theme.DefaultTextColor;
            SeparatorView.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
        }

        public override void LayoutSubviews ()
        {
            NameLabel.Frame = new CGRect (_ContentInsets.Left, ContentInsets.Top, ComputedNameWidth, NameLabel.Font.RoundedLineHeight (1.0f));
            var contentLeft = NameLabel.Frame.X + NameLabel.Frame.Width + _NameContentSpacing;
            var contentRight = ContentInsets.Right;
            if (_AccessoryView != null) {
                _AccessoryView.Frame = new CGRect (new CGPoint (ContentInsets.Right - _AccessoryView.Frame.Size.Width, ContentInsets.Top), _AccessoryView.Frame.Size);
                contentRight += _AccessoryView.Frame.Width;
            }
            var contentWidth = Bounds.Width - contentLeft - contentRight;
            var contentSize = ContentView.SizeThatFits (new CGSize (contentWidth, 0));
            ContentView.Frame = new CGRect (contentLeft, ContentInsets.Top, contentWidth, contentSize.Height);
            SeparatorView.Frame = new CGRect (0, ContentView.Frame.Y + ContentView.Frame.Height + _ContentInsets.Bottom, Bounds.Width, SeparatorSize);
        }

        nfloat ComputedNameWidth {
            get {
                if (_NameWidth.HasValue) {
                    return _NameWidth.Value;
                } else {
                    var size = NameLabel.SizeThatFits (new CGSize (Bounds.Width, 0));
                    return size.Width;
                }
            }
        }

        public override CGSize SizeThatFits (CGSize size)
        {
            var contentLeft = _ContentInsets.Left + ComputedNameWidth + _NameContentSpacing;
            var contentRight = _ContentInsets.Right;
            if (_AccessoryView != null) {
                contentRight += _AccessoryView.Frame.Width;
            }
            var contentSize = ContentView.SizeThatFits (new CGSize (size.Width - contentLeft - contentRight, 0));
            return new CGSize (size.Width, contentSize.Height + _ContentInsets.Top + _ContentInsets.Bottom + SeparatorSize);
        }
    }

    public class MessageComposeEmailFieldView : MessageComposeFieldView, EmailAddressTokenTextFieldDelegate
    {

        public EmailAddressTokenTextField EmailTokenField {
            get {
                return ContentView as EmailAddressTokenTextField;
            }
        }

        public MessageComposeEmailFieldView (string name) : base (name, new EmailAddressTokenTextField ())
        {
            EmailTokenField.EmailTokenDelegate = this;
            // TODO: set accessory view to + image
        }

        public override void Cleanup ()
        {
            base.Cleanup ();
            // TODO: clear choose button action
        }

        public override void AdoptTheme (Theme theme)
        {
            base.AdoptTheme (theme);
            EmailTokenField.Font = theme.DefaultFont.WithSize (14.0f);
            EmailTokenField.TintColor = theme.TableViewTintColor;
            EmailTokenField.TextColor = theme.DefaultTextColor;
        }

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            EmailTokenField.BecomeFirstResponder ();
        }

        public void EmailAddressFieldAutocompleteText (EmailAddressTokenTextField field, string text)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                // TODO: ask header view delegate for autocomplete help
            }
        }

        public void EmailAddressFieldDidChange (EmailAddressTokenTextField field)
        {
            var size = field.SizeThatFits (new CGSize (Frame.Size.Width, 0));
            if (size.Height != field.Frame.Size.Height) {
                if (WeakHeaderView.TryGetTarget (out var headerView)) {
                    headerView.SetNeedsLayout ();
                    headerView.LayoutIfNeeded ();
                }
            }
        }

        void ChooseButtonClicked (object sender, EventArgs e)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                // TODO: ask header view delegate for chooser
            }
        }
    }

    public class MessageComposeLabelFieldView : MessageComposeFieldView
    {

        public UILabel ValueLabel {
            get {
                return ContentView as UILabel;
            }
        }

        PressGestureRecognizer PressRecognizer;
        public event EventHandler Pressed;

        public MessageComposeLabelFieldView (string name) : base (name, new UILabel ())
        {
            PressRecognizer = new PressGestureRecognizer (Press);
            ValueLabel.BackgroundColor = UIColor.Clear;
            NameLabel.BackgroundColor = UIColor.Clear;
            AddGestureRecognizer (PressRecognizer);
        }

        public override void Cleanup ()
        {
            base.Cleanup ();
            RemoveGestureRecognizer (PressRecognizer);
            PressRecognizer = null;
        }

        void Press ()
        {
            if (Pressed == null) {
                return;
            }
            if (PressRecognizer.State == UIGestureRecognizerState.Began) {
                SetSelected (true, animated: false);
            } else if (PressRecognizer.State == UIGestureRecognizerState.Ended) {
                Pressed?.Invoke (this, new EventArgs ());
                SetSelected (false, animated: false);
            } else if (PressRecognizer.State == UIGestureRecognizerState.Failed) {
                SetSelected (false, animated: true);
            } else if (PressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                SetSelected (false, animated: false);
            }
        }

        void SetSelected (bool selected, bool animated = true)
        {
            if (animated) {
                UIView.BeginAnimations (null, IntPtr.Zero);
                UIView.SetAnimationDuration (0.25f);
            }
            if (selected) {
                BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);
            } else {
                BackgroundColor = UIColor.White;
            }
            if (animated) {
                UIView.CommitAnimations ();
            }
        }

        public override void AdoptTheme (Theme theme)
        {
            base.AdoptTheme (theme);
            ValueLabel.Font = theme.DefaultFont.WithSize (14.0f);
            ValueLabel.TextColor = theme.DefaultTextColor;
        }
    }

    public class MessageComposeTextFieldView : MessageComposeFieldView
    {

        public UITextField TextField {
            get {
                return ContentView as UITextField;
            }
        }

        public MessageComposeTextFieldView (string name) : base (name, new UITextField ())
        {
        }

        public override void AdoptTheme (Theme theme)
        {
            base.AdoptTheme (theme);
            TextField.Font = theme.DefaultFont;
            TextField.TextColor = theme.DefaultTextColor;
        }
    }

    public class MessageComposeAttachmentsView : UIView, ThemeAdopter, IUITableViewDelegate, IUITableViewDataSource
    {

        private const string AttachmentCellIdentifier = "attachment";

        public WeakReference<MessageComposeHeaderView> WeakHeaderView {
            get {
                return FieldView.WeakHeaderView;
            }
            set {
                FieldView.WeakHeaderView = value;
            }
        }

        public UIEdgeInsets ContentInsets {
            get {
                return FieldView.ContentInsets;
            }
            set {
                FieldView.ContentInsets = value;
            }
        }

        private MessageComposeLabelFieldView FieldView;
        private UITableView TableView;
        public List<McAttachment> Attachments;

        private nfloat RowHeight = 44.0f;

        public MessageComposeAttachmentsView (string name)
        {
            FieldView = new MessageComposeLabelFieldView (name);
            // TODO: accessory view (+)
            AddSubview (FieldView);

            TableView = new UITableView (new CGRect (0.0f, 0.0f, Bounds.Width, RowHeight), UITableViewStyle.Plain);
            TableView.ScrollEnabled = false;
            TableView.WeakDelegate = this;
            TableView.WeakDataSource = this;
            TableView.RowHeight = RowHeight;
            TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            TableView.RegisterClassForCellReuse (typeof (AttachmentCell), AttachmentCellIdentifier);
            AddSubview (TableView);
        }

        public void Cleanup ()
        {
        }

        Theme AdoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != AdoptedTheme) {
                AdoptedTheme = theme;
                FieldView.AdoptTheme (theme);
                TableView.AdoptTheme (theme);
            }
        }

        public override void LayoutSubviews ()
        {
            FieldView.Frame = new CGRect (0.0f, 0.0f, Bounds.Width, FieldView.SizeThatFits (new CGSize (Bounds.Width, 0)).Height);
        }

        public void Add (McAttachment attachment)
        {
            Attachments.Add (attachment);
            TableView.ReloadData ();
            SetNeedsLayout ();
        }

        public void Update (McAttachment attachment)
        {
            for (var i = 0; i < Attachments.Count; ++i) {
                if (Attachments [i].Id == attachment.Id) {
                    Attachments [i] = attachment;
                    TableView.ReloadRows (new NSIndexPath [] { NSIndexPath.FromRowSection (i, 0) }, UITableViewRowAnimation.None);
                    break;
                }
            }
        }

        [Export ("numberOfSectionsInTableView:")]
        public nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        [Foundation.Export ("tableView:numberOfRowsInSection:")]
        public nint RowsInSection (UITableView tableView, nint section)
        {
            return Attachments.Count;
        }

        [Foundation.Export ("tableView:cellForRowAtIndexPath:")]
        public UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var attachment = Attachments [indexPath.Row];
            var cell = tableView.DequeueReusableCell (AttachmentCellIdentifier) as AttachmentCell;
            cell.TextLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);
            if (String.IsNullOrWhiteSpace (cell.TextLabel.Text)) {
                cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("(no name) (attachment)", "Fallback name for attachment with no name");
                cell.TextLabel.TextColor = AdoptedTheme.DisabledTextColor;
            } else {
                cell.TextLabel.TextColor = AdoptedTheme.DefaultTextColor;
            }
            cell.DetailTextLabel.Text = Pretty.GetAttachmentDetail (attachment);
            cell.IconView.Image = FilesTableViewSource.FileIconFromExtension (attachment);
            cell.AdoptTheme (AdoptedTheme);
            return cell;
        }

        // TODO: attachment selection/viewing
        // TODO: attachment removal

        private class AttachmentCell : SwipeTableViewCell, ThemeAdopter
        {

            public readonly UIImageView IconView;
            nfloat Inset = 14.0f;
            nfloat IconSize = 24.0f;
            nfloat TextSpacing = 5.0f;

            public AttachmentCell (IntPtr handle) : base (handle)
            {
                DetailTextSpacing = 0.0f;

                IconView = new UIImageView (new CGRect (0.0f, 0.0f, IconSize, IconSize));
                SeparatorInset = new UIEdgeInsets (0.0f, Inset + IconSize + TextSpacing, 0.0f, 0.0f);

                ContentView.AddSubview (IconView);
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.DefaultTextColor;
                DetailTextLabel.Font = theme.DefaultFont.WithSize (12.0f);
                DetailTextLabel.TextColor = theme.DisabledTextColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                IconView.Center = new CGPoint (Inset + IconSize / 2.0f, ContentView.Bounds.Height / 2.0f);
            }
        }

    }

    public class MessageComposeActionSelectionView : UIView, ThemeAdopter
    {

        ActionCheckboxView CheckboxView;
        public nfloat LeftPadding = 0.0f;
        public nfloat RightPadding = 0.0f;
        public readonly UILabel TextLabel;
        public readonly UILabel DateLabel;
        public Action Action;
        private UIView SeparatorView;
        private nfloat SeparatorSize = 1.0f;

        UITapGestureRecognizer TapRecognizer;

        public MessageComposeActionSelectionView (CGRect frame) : base (frame)
        {
            CheckboxView = new ActionCheckboxView (20.0f);
            CheckboxView.TintColor = UIColor.FromRGB (0xEE, 0x70, 0x5B);

            TextLabel = new UILabel ();
            DateLabel = new UILabel ();
            SeparatorView = new UIView ();

            AddSubview (CheckboxView);
            AddSubview (TextLabel);
            AddSubview (DateLabel);
            AddSubview (SeparatorView);

            TapRecognizer = new UITapGestureRecognizer (Tap);
            AddGestureRecognizer (TapRecognizer);
            SetNeedsLayout ();
        }

        public void Cleanup ()
        {
            RemoveGestureRecognizer (TapRecognizer);
            TapRecognizer = null;
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            adoptedTheme = theme;
            DateLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
            DateLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            SeparatorView.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
        }

        void Tap ()
        {
            Action?.Invoke ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var height = Bounds.Height - SeparatorSize;
            CheckboxView.Center = new CGPoint (LeftPadding + CheckboxView.Frame.Width / 2.0f, height / 2.0f);
            var dateSize = DateLabel.SizeThatFits (new CGSize (Bounds.Width, 0.0f));
            dateSize.Height = DateLabel.Font.RoundedLineHeight (1.0f);
            var textHeight = TextLabel.Font.RoundedLineHeight (1.0f);
            var textTop = (height - textHeight) / 2.0f;

            CGRect frame;

            frame = DateLabel.Frame;
            frame.Width = dateSize.Width;
            frame.Height = dateSize.Height;
            frame.X = Bounds.Width - RightPadding - frame.Width;
            frame.Y = textTop + ((TextLabel.Font.Ascender + (textHeight - TextLabel.Font.LineHeight) / 2.0f) - (DateLabel.Font.Ascender + (dateSize.Height - DateLabel.Font.LineHeight) / 2.0f));
            DateLabel.Frame = frame;

            frame = TextLabel.Frame;
            frame.X = CheckboxView.Frame.X + CheckboxView.Frame.Width + LeftPadding;
            frame.Y = textTop;
            frame.Width = DateLabel.Frame.X - frame.X - 3.0f;
            frame.Height = textHeight;
            TextLabel.Frame = frame;

            SeparatorView.Frame = new CGRect (0.0f, Bounds.Height - SeparatorSize, Bounds.Width, SeparatorSize);

        }

        public void SetIntent (McEmailMessage.IntentType intent, MessageDeferralType intentDateType, DateTime intentDate)
        {
            if (intent == McEmailMessage.IntentType.None) {
                TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Request an action for the recipient", "Message action label");
                TextLabel.TextColor = adoptedTheme.DisabledTextColor;
                DateLabel.Text = "";
                CheckboxView.IsChecked = false;
            } else {
                TextLabel.Text = NachoCore.Brain.NcMessageIntent.IntentEnumToString (intent, uppercase: false);
                TextLabel.TextColor = adoptedTheme.DefaultTextColor;
                if (intentDateType == MessageDeferralType.None) {
                    DateLabel.Text = "";
                } else {
                    DateLabel.Text = NachoCore.Brain.NcMessageIntent.DeferralTypeToString (intentDateType, intentDate);
                }
                CheckboxView.IsChecked = true;
            }
            SetNeedsLayout ();
        }

    }

    public class MessageComposeHeaderView : UIView, ThemeAdopter
    {

        #region Properties

        public MessageComposeHeaderViewDelegate HeaderDelegate;

        public readonly MessageComposeEmailFieldView ToField;
        public readonly MessageComposeEmailFieldView CcField;
        public readonly MessageComposeEmailFieldView BccField;
        public readonly MessageComposeLabelFieldView FromField; //"gen-more-arrow"
        public readonly MessageComposeTextFieldView SubjectField;
        public readonly MessageComposeActionSelectionView IntentView;
        public readonly MessageComposeAttachmentsView AttachmentsView;

        private nfloat LineHeight = 42.0f;


        UIEdgeInsets FieldInsets = new UIEdgeInsets (4.0f, 15.0f, 4.0f, 15.0f);

        nfloat preferredHeight = 0.0f;
        public nfloat PreferredHeight {
            get {
                if (preferredHeight < 1.0f) {
                    return (LineHeight + 1.0f) * 4.0f;
                }
                return preferredHeight;
            }
        }

        bool ShouldHideIntent {
            get {
                return false;
            }
        }

        bool CcFieldsAreCollapsed;
        public bool ShouldHideFrom;
        public bool AttachmentsAllowed = true;

        #endregion

        #region Constructors

        public MessageComposeHeaderView (CGRect frame) : base (frame)
        {
            CcFieldsAreCollapsed = true;

            ToField = new MessageComposeEmailFieldView (NSBundle.MainBundle.LocalizedString ("To:", ""));
            CcField = new MessageComposeEmailFieldView (NSBundle.MainBundle.LocalizedString ("Cc:", ""));
            BccField = new MessageComposeEmailFieldView (NSBundle.MainBundle.LocalizedString ("Bcc:", ""));
            FromField = new MessageComposeLabelFieldView (NSBundle.MainBundle.LocalizedString ("Cc/Bcc/From:", ""));
            SubjectField = new MessageComposeTextFieldView (NSBundle.MainBundle.LocalizedString ("Subject:", ""));
            AttachmentsView = new MessageComposeAttachmentsView (NSBundle.MainBundle.LocalizedString ("Attachments:", ""));
            IntentView = new MessageComposeActionSelectionView (new CGRect (0, 0, Bounds.Width, LineHeight));

            ToField.WeakHeaderView.SetTarget (this);
            CcField.WeakHeaderView.SetTarget (this);
            BccField.WeakHeaderView.SetTarget (this);
            FromField.WeakHeaderView.SetTarget (this);
            SubjectField.WeakHeaderView.SetTarget (this);
            AttachmentsView.WeakHeaderView.SetTarget (this);

            ToField.ContentInsets = FieldInsets;
            CcField.ContentInsets = FieldInsets;
            BccField.ContentInsets = FieldInsets;
            FromField.ContentInsets = FieldInsets;
            SubjectField.ContentInsets = FieldInsets;
            AttachmentsView.ContentInsets = FieldInsets;
            IntentView.LeftPadding = FieldInsets.Left;
            IntentView.RightPadding = FieldInsets.Right;

            ToField.NameWidth = 30.0f;
            CcField.NameWidth = ToField.NameWidth;
            BccField.NameWidth = ToField.NameWidth;

            FromField.NameContentSpacing = 5.0f;
            SubjectField.NameContentSpacing = 5.0f;

            AddSubview (ToField);
            AddSubview (CcField);
            AddSubview (BccField);
            AddSubview (FromField);
            AddSubview (SubjectField);
            AddSubview (AttachmentsView);
            AddSubview (IntentView);

            AdoptTheme (Theme.Active);
            SetNeedsLayout ();

            SubjectField.TextField.EditingDidEnd += SubjectEditingDidEnd;
            IntentView.Action = SelectIntent;
            FromField.Pressed += FromFieldPressed;
        }

        public void Cleanup ()
        {
            SubjectField.TextField.EditingDidEnd -= SubjectEditingDidEnd;
            IntentView.Action = null;
            FromField.Pressed -= FromFieldPressed;
            ToField.Cleanup ();
            CcField.Cleanup ();
            BccField.Cleanup ();
            FromField.Cleanup ();
            SubjectField.Cleanup ();
            AttachmentsView.Cleanup ();
            IntentView.Cleanup ();
        }

        #endregion

        #region Theme

        public void AdoptTheme (Theme theme)
        {
            ToField.AdoptTheme (theme);
            CcField.AdoptTheme (theme);
            BccField.AdoptTheme (theme);
            FromField.AdoptTheme (theme);
            SubjectField.AdoptTheme (theme);
            AttachmentsView.AdoptTheme (theme);
            IntentView.AdoptTheme (theme);
        }

        #endregion

        #region User Actions

        public void SubjectEditingDidEnd (object sender, EventArgs e)
        {
            HeaderDelegate?.MessageComposeHeaderViewDidChangeSubject (this, SubjectField.TextField.Text);
        }

        private void SelectIntent ()
        {
            HeaderDelegate?.MessageComposeHeaderViewDidSelectIntentField (this);
        }

        void FromFieldPressed (object sender, EventArgs e)
        {
            if (CcFieldsAreCollapsed) {
                ShowCcFields ();
                CcField.EmailTokenField.BecomeFirstResponder ();
            } else {
                HeaderDelegate?.MessageComposeHeaderViewDidSelectFromField (this);
            }
        }

        /*

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

        public void AddressBlockAutoCompleteContactClicked (UcAddressBlock view, string prefix)
        {
            var address = EmailAddressForAddressView (view, prefix);
            if (HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidSelectContactChooser (this, address);
            }
        }

        public void AddressBlockContactPickerRequested (UcAddressBlock view)
        {
            var address = EmailAddressForAddressView (view, null);
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
            UIView.Animate (0.2, () => {
                AttachmentsView.LayoutIfNeeded ();
                LayoutIfNeeded ();
            });
        }

        private void SelectFrom ()
        {
            if (CcFieldsAreCollapsed) {
                CcFieldsAreCollapsed = false;
                UpdateCcCollapsed ();
                SetNeedsLayout ();
                CcView.SetEditFieldAsFirstResponder ();
                UIView.Animate (0.2, () => {
                    CcView.LayoutIfNeeded ();
                    FromView.LayoutIfNeeded ();
                    LayoutIfNeeded ();
                });
            } else {
                HeaderDelegate.MessageComposeHeaderViewDidSelectFromField (this);
            }
        }
        */

        #endregion

        #region Layout

        public void ShowCcFields (bool animated = true)
        {
            CcFieldsAreCollapsed = false;
            FromField.Name = NSBundle.MainBundle.LocalizedString ("From:", "");
            FromField.NameWidth = ToField.NameWidth;
            if (animated) {
                CcField.Frame = FromField.Frame;
                BccField.Frame = FromField.Frame;
                FromField.SetNeedsLayout ();
                SetNeedsLayout ();
                UIView.Animate (0.3f, () => {
                    FromField.LayoutIfNeeded ();
                    LayoutIfNeeded ();
                });
            } else {
                SetNeedsLayout ();
            }
        }

        public void UpdateCcCollapsed ()
        {
            var shouldExpand = CcField.EmailTokenField.Addresses.Length > 0 || CcField.EmailTokenField.Addresses.Length > 0;
            if (CcFieldsAreCollapsed && shouldExpand) {
                ShowCcFields (animated: false);
            }
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
            y += LayoutSubviewAtYPosition (AttachmentsView, y);
            y += LayoutSubviewAtYPosition (AttachmentsSeparator, y);
            y += LayoutSubviewAtYPosition (IntentView, y);
            y += LayoutSubviewAtYPosition (IntentSeparator, y);

            preferredHeight = y;

            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);

            if ((Math.Abs (preferredHeight - previousPreferredHeight) > 0.5) && HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidChangeHeight (this);
            }
        }

        private nfloat LayoutSubviewAtYPosition (UIView subview, nfloat y, float padding = 0f, nfloat? maxHeight = null, float minHeight = 0f)
        {
            var layoutHeight = subview.Frame.Height;
            if (maxHeight.HasValue) {
                layoutHeight = subview.SizeThatFits (new CGSize (subview.Frame.Width, maxHeight.Value)).Height;
            }
            if (layoutHeight < minHeight) {
                layoutHeight = minHeight;
            }
            subview.Frame = new CGRect (subview.Frame.X, y, subview.Frame.Width, layoutHeight);
            if (subview.Hidden) {
                return 0f;
            }
            return layoutHeight + padding;
        }

        #endregion

        #region Helpers

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

