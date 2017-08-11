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
        void MessageComposeHeaderViewDidRequestDetails (MessageComposeHeaderView view);
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
                    _AccessoryView.BackgroundColor = UIColor.Clear;
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

        private nfloat _SeparatorSize = 1.0f;
        public nfloat SeparatorSize {
            get {
                return _SeparatorSize;
            }
            set {
                _SeparatorSize = value;
                SetNeedsLayout ();
            }
        }

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
            var nameSize = ComputedNameSize;
            NameLabel.Frame = new CGRect (new CGPoint (_ContentInsets.Left, ContentInsets.Top), nameSize);
            var contentLeft = NameLabel.Frame.X + NameLabel.Frame.Width + _NameContentSpacing;
            var contentRight = ContentInsets.Right;
            if (_AccessoryView != null) {
                var accessorySize = _AccessoryView.IntrinsicContentSize;
                var accessoryWidth = accessorySize.Width + 2.0f * _ContentInsets.Right;
                _AccessoryView.Frame = new CGRect (Bounds.Width - accessoryWidth, 0.0f, accessoryWidth, accessorySize.Height + _ContentInsets.Top + _ContentInsets.Bottom);
                _AccessoryView.SetNeedsLayout ();
                _AccessoryView.LayoutSubviews ();
                contentRight = _AccessoryView.Frame.Width;
            }
            var contentWidth = Bounds.Width - contentLeft - contentRight;
            var contentSize = ContentView.SizeThatFits (new CGSize (contentWidth, 0));
            if (contentSize.Height < nameSize.Height) {
                contentSize.Height = nameSize.Height;
            }
            ContentView.Frame = new CGRect (contentLeft, ContentInsets.Top, contentWidth, contentSize.Height);
            SeparatorView.Frame = new CGRect (0, ContentView.Frame.Y + ContentView.Frame.Height + _ContentInsets.Bottom, Bounds.Width, SeparatorSize);
        }

        CGSize ComputedNameSize {
            get {
                var size = NameLabel.SizeThatFits (new CGSize (Bounds.Width, 0));
                if (_NameWidth.HasValue) {
                    size.Width = _NameWidth.Value;
                }
                return size;
            }
        }

        public override CGSize SizeThatFits (CGSize size)
        {
            var nameSize = ComputedNameSize;
            var contentLeft = _ContentInsets.Left + nameSize.Width + _NameContentSpacing;
            var contentRight = _ContentInsets.Right;
            if (_AccessoryView != null) {
                contentRight += _AccessoryView.Frame.Width;
            }
            var contentSize = ContentView.SizeThatFits (new CGSize (size.Width - contentLeft - contentRight, 0));
            if (contentSize.Height < nameSize.Height) {
                contentSize.Height = nameSize.Height;
            }
            var height = contentSize.Height + _ContentInsets.Top + _ContentInsets.Bottom + SeparatorSize;
            if (size.Height == 0 || height < size.Height) {
                return new CGSize (size.Width, height);
            }
            return size;
        }
    }

    public class MessageComposeEmailFieldView : MessageComposeFieldView, EmailAddressTokenTextFieldDelegate
    {

        public EmailAddressTokenTextField EmailTokenField {
            get {
                return ContentView as EmailAddressTokenTextField;
            }
        }

        private UITapGestureRecognizer AccessoryTapRecognizer;

        public MessageComposeEmailFieldView (string name) : base (name, new EmailAddressTokenTextField ())
        {
            EmailTokenField.EmailTokenDelegate = this;
            EmailTokenField.ScrollEnabled = false;
            EmailTokenField.ContentInset = UIEdgeInsets.Zero;
            EmailTokenField.TextContainerInset = UIEdgeInsets.Zero;
            AccessoryView = new ImageAccessoryView ("email-add", contentMode: UIViewContentMode.Center);
            AccessoryTapRecognizer = new UITapGestureRecognizer (AccessoryTap);
            AccessoryView.AddGestureRecognizer (AccessoryTapRecognizer);
        }

        public override void Cleanup ()
        {
            base.Cleanup ();
            AccessoryView.RemoveGestureRecognizer (AccessoryTapRecognizer);
            AccessoryTapRecognizer = null;
        }

        public override void AdoptTheme (Theme theme)
        {
            base.AdoptTheme (theme);
            EmailTokenField.Font = theme.DefaultFont.WithSize (14.0f);
            EmailTokenField.TintColor = theme.TableViewTintColor;
            EmailTokenField.TextColor = theme.DefaultTextColor;
            EmailTokenField.Changed += TextChanged;
        }

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            EmailTokenField.BecomeFirstResponder ();
        }

        void TextChanged (object sender, EventArgs e)
        {
            var size = EmailTokenField.SizeThatFits (new CGSize (EmailTokenField.Frame.Size.Width, 0));
            if (size.Height != EmailTokenField.Frame.Size.Height) {
                if (WeakHeaderView.TryGetTarget (out var headerView)) {
                    headerView.SetNeedsLayout ();
                    headerView.LayoutIfNeeded ();
                }
            }
        }

        public void EmailAddressFieldAutocompleteText (EmailAddressTokenTextField field, string text)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                if (this == headerView.ToField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSearchTo (headerView, text);
                } else if (this == headerView.CcField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSearchCc (headerView, text);
                } else if (this == headerView.BccField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSearchBcc (headerView, text);
                }
            }
        }

        public void EmailAddressFieldDidChange (EmailAddressTokenTextField field)
        {
            TextChanged (null, null);
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                if (this == headerView.ToField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidChangeTo (headerView, field.AddressString);
                } else if (this == headerView.CcField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidChangeCc (headerView, field.AddressString);
                } else if (this == headerView.BccField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidChangeBcc (headerView, field.AddressString);
                }
            }
        }

        public void EmailAddressFieldDidRequsetDetails (EmailAddressTokenTextField field)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                headerView.HeaderDelegate?.MessageComposeHeaderViewDidRequestDetails (headerView);
            }
        }

        void AccessoryTap (UITapGestureRecognizer recognizer)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                if (this == headerView.ToField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSelectToChooser (headerView);
                } else if (this == headerView.CcField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSelectCcChooser (headerView);
                } else if (this == headerView.BccField) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSelectBccChooser (headerView);
                }
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
            PressRecognizer.IsCanceledByPanning = true;
            PressRecognizer.DelaysStart = true;
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

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            TextField.BecomeFirstResponder ();
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
        private UIView SeparatorView;
        private List<McAttachment> _Attachments;
        public List<McAttachment> Attachments {
            get {
                return _Attachments;
            }
            set {
                _Attachments = value;
                TableView.ReloadData ();
            }
        }

        private nfloat RowHeight = 44.0f;
        private nfloat SeparatorSize = 1.0f;

        public MessageComposeAttachmentsView (string name)
        {
            FieldView = new MessageComposeLabelFieldView (name);
            FieldView.SeparatorSize = 0.0f;
            FieldView.AccessoryView = new ImageAccessoryView ("email-add", contentMode: UIViewContentMode.Center);
            FieldView.Pressed += FieldPressed;
            AddSubview (FieldView);

            TableView = new UITableView (new CGRect (0.0f, 0.0f, Bounds.Width, RowHeight), UITableViewStyle.Plain);
            TableView.ScrollEnabled = false;
            TableView.WeakDelegate = this;
            TableView.WeakDataSource = this;
            TableView.RowHeight = RowHeight;
            TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            TableView.RegisterClassForCellReuse (typeof (AttachmentCell), AttachmentCellIdentifier);
            AddSubview (TableView);

            SeparatorView = new UIView ();
            AddSubview (SeparatorView);
        }

        public void Cleanup ()
        {
            FieldView.Pressed -= FieldPressed;
        }

        void FieldPressed (object sender, EventArgs e)
        {
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                headerView.HeaderDelegate?.MessageComposeHeaderViewDidSelectAddAttachment (headerView);
            }
        }

        Theme AdoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != AdoptedTheme) {
                AdoptedTheme = theme;
                FieldView.AdoptTheme (theme);
                TableView.AdoptTheme (theme);
                SeparatorView.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            }
        }

        public override void LayoutSubviews ()
        {
            FieldView.Frame = new CGRect (0.0f, 0.0f, Bounds.Width, FieldView.SizeThatFits (new CGSize (Bounds.Width, 0)).Height);
            TableView.Frame = new CGRect (0.0f, FieldView.Frame.Y + FieldView.Frame.Height, Bounds.Width, TableView.ContentSize.Height);
            SeparatorView.Frame = new CGRect (0.0f, TableView.Frame.Y + TableView.Frame.Height, Bounds.Width, SeparatorSize);
        }

        public override CGSize SizeThatFits (CGSize size)
        {
            var fieldSize = FieldView.SizeThatFits (size);
            var height = fieldSize.Height + TableView.ContentSize.Height + SeparatorSize;
            if (size.Height == 0 || height < size.Height) {
                return new CGSize (size.Width, height);
            }
            return size;
        }

        public void Add (McAttachment attachment)
        {
            Attachments.Add (attachment);
            TableView.ReloadData ();
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                headerView.SetNeedsLayout ();
            }
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
            cell.AccessoryView = new ImageAccessoryView ("gen-delete-small", width: 36, contentMode: UIViewContentMode.Center);
            cell.AccessoryView.AddGestureRecognizer (new UITapGestureRecognizer ((recognizer) => {
                RemoveAttachment (indexPath);
            }));
            cell.DetailTextLabel.Text = Pretty.GetAttachmentDetail (attachment);
            cell.IconView.Image = FilesTableViewSource.FileIconFromExtension (attachment);
            cell.AdoptTheme (AdoptedTheme);
            return cell;
        }

        void RemoveAttachment (NSIndexPath indexPath)
        {
            var attachment = Attachments [indexPath.Row];
            Attachments.RemoveAt (indexPath.Row);
            TableView.ReloadData ();
            if (WeakHeaderView.TryGetTarget (out var headerView)) {
                headerView.SetNeedsLayout ();
                headerView.HeaderDelegate?.MessageComposeHeaderViewDidRemoveAttachment (headerView, attachment);
            }
        }

        [Foundation.Export ("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var attachment = Attachments [indexPath.Row];
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                if (WeakHeaderView.TryGetTarget (out var headerView)) {
                    headerView.HeaderDelegate?.MessageComposeHeaderViewDidSelectAttachment (headerView, attachment);
                }
            }
            tableView.DeselectRow (indexPath, true);
        }

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
        public nfloat LineHeight = 0.0f;
        public readonly UILabel TextLabel;
        public readonly UILabel DateLabel;
        public Action Action;
        private UIView SeparatorView;
        private nfloat SeparatorSize = 1.0f;

        PressGestureRecognizer PressRecognizer;

        public MessageComposeActionSelectionView () : base ()
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

            PressRecognizer = new PressGestureRecognizer (Press);
            PressRecognizer.IsCanceledByPanning = true;
            PressRecognizer.DelaysStart = true;
            AddGestureRecognizer (PressRecognizer);
            SetNeedsLayout ();
        }

        public void Cleanup ()
        {
            RemoveGestureRecognizer (PressRecognizer);
            PressRecognizer = null;
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

        void Press ()
        {
            if (PressRecognizer.State == UIGestureRecognizerState.Began) {
                SetSelected (true, animated: false);
            } else if (PressRecognizer.State == UIGestureRecognizerState.Ended) {
                Action?.Invoke ();
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

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var height = LineHeight;
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

        public override CGSize SizeThatFits (CGSize size)
        {
            var height = LineHeight + SeparatorSize;
            if (size.Height == 0 || height < size.Height) {
                return new CGSize (size.Width, height);
            }
            return size;
        }

    }

    public class MessageComposeHeaderView : UIView, ThemeAdopter
    {

        #region Properties

        public MessageComposeHeaderViewDelegate HeaderDelegate;

        public readonly MessageComposeEmailFieldView ToField;
        public readonly MessageComposeEmailFieldView CcField;
        public readonly MessageComposeEmailFieldView BccField;
        public readonly MessageComposeLabelFieldView FromField;
        public readonly MessageComposeTextFieldView SubjectField;
        public readonly MessageComposeActionSelectionView IntentView;
        public readonly MessageComposeAttachmentsView AttachmentsView;

        private nfloat LineHeight = 37.0f;

        UIEdgeInsets FieldInsets = new UIEdgeInsets (10.0f, 14.0f, 10.0f, 10.0f);

        nfloat preferredHeight = 0.0f;
        public nfloat PreferredHeight {
            get {
                if (preferredHeight < 1.0f) {
                    return (LineHeight + 1.0f) * 4.0f;
                }
                return preferredHeight;
            }
        }

        bool CcFieldsAreCollapsed;
        private bool _ShouldHideFrom;
        public bool ShouldHideFrom {
            get {
                return _ShouldHideFrom;
            }
            set {
                _ShouldHideFrom = value;
                if (CcFieldsAreCollapsed) {
                    if (_ShouldHideFrom) {
                        FromField.Name = NSBundle.MainBundle.LocalizedString ("Cc/Bcc:", "");
                    } else {
                        FromField.Name = NSBundle.MainBundle.LocalizedString ("Cc/Bcc/From:", "");
                    }
                }
            }
        }
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
            IntentView = new MessageComposeActionSelectionView ();

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
            IntentView.LineHeight = LineHeight;

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

        #endregion

        #region Layout

        public void ShowCcFields (bool animated = true)
        {
            CcFieldsAreCollapsed = false;
            FromField.AccessoryView = new ImageAccessoryView ("gen-more-arrow", contentMode: UIViewContentMode.Center);
            FromField.LayoutIfNeeded ();
            FromField.Name = NSBundle.MainBundle.LocalizedString ("From:", "");
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
            nfloat y = 0.0f;
            nfloat previousPreferredHeight = PreferredHeight;

            CcField.Hidden = CcFieldsAreCollapsed;
            BccField.Hidden = CcFieldsAreCollapsed;
            FromField.Hidden = ShouldHideFrom && !CcFieldsAreCollapsed;
            AttachmentsView.Hidden = !AttachmentsAllowed;

            y += LayoutSubviewAtYPosition (ToField, y);
            y += LayoutSubviewAtYPosition (CcField, y);
            y += LayoutSubviewAtYPosition (BccField, y);
            y += LayoutSubviewAtYPosition (FromField, y);
            y += LayoutSubviewAtYPosition (SubjectField, y);
            y += LayoutSubviewAtYPosition (AttachmentsView, y);
            y += LayoutSubviewAtYPosition (IntentView, y);

            preferredHeight = y;

            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);

            if ((Math.Abs (preferredHeight - previousPreferredHeight) > 0.5) && HeaderDelegate != null) {
                HeaderDelegate.MessageComposeHeaderViewDidChangeHeight (this);
            }
        }

        private nfloat LayoutSubviewAtYPosition (UIView subview, nfloat y, float padding = 0f, nfloat? maxHeight = null, float minHeight = 0f)
        {
            var size = subview.SizeThatFits (new CGSize (Bounds.Width, maxHeight.HasValue ? maxHeight.Value : 0));
            var layoutHeight = size.Height;
            if (layoutHeight < minHeight) {
                layoutHeight = minHeight;
            }
            subview.Frame = new CGRect (0.0f, y, Bounds.Width, layoutHeight);
            if (subview.Hidden) {
                return 0f;
            }
            return layoutHeight + padding;
        }

        #endregion

    }
}

