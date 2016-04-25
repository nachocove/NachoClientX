//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using MimeKit;
using Foundation;

namespace NachoClient.iOS
{
    public class MessageHeaderDetailViewController : NachoTableViewController
    {

        #region Properties

        private const string MessageAddressCellIdentifier = "MessageAddressCellIdentifier";
        private const string NameValueCellIdentifier = "NameValueCellIdentifier";
        private const string ActionCellIdentifier = "ActionCellIdentifier";

        private McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                Setup ();
            }
        }

        private McBody Body;
        private MimeMessage Mime;

        int FromSection = -1;
        int ToSection = -1;
        int CcSection = -1;
        int BccSection = -1;
        int DebugSection = -1;
        int SectionCount = 0;

        int FromRow = -1;
        int ReplyToRow = -1;

        int DebugAccountIdRow = 0;
        int DebugMessageIdRow = 1;
        int DebugBodyIdRow = 2;
        int DebugBodyTypeRow = -1;
        int DebugBodyRow = -1;
        int DebugMimeRow = -1;
        int DebugDeleteBodyRow = -1;
        int DebugDeleteMessageRow = -1;

        bool IsDebugEnabled = false;

        MailboxAddress FromAddress;
        MailboxAddress ReplyToAddress;

        MailboxAddress[] ToAddresses;
        MailboxAddress[] CcAddresses;
        MailboxAddress[] BccAddresses;
       
        Dictionary <string, NcContactPortraitEmailIndex> PortraitCache;

        #endregion

        #region Constructors

        public MessageHeaderDetailViewController () : base (UITableViewStyle.Grouped)
        {
        }

        public override bool CanBecomeFirstResponder {
            get {
                return true;
            }
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.RegisterClassForCellReuse (typeof(MessageAddressCell), MessageAddressCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof(NameValueCell), NameValueCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof(ActionCell), ActionCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            BecomeFirstResponder ();
        }

        #endregion

        #region Events

        public override void MotionEnded (UIEventSubtype motion, UIEvent evt)
        {
            if (motion == UIEventSubtype.MotionShake) {
                EnableDebugMode ();
            } else {
                base.MotionEnded (motion, evt);
            }
        }

        #endregion

        #region Table Delegate & Data Source

        public override nint NumberOfSections (UITableView tableView)
        {
            return SectionCount;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == FromSection) {
                if (FromAddress != null && ReplyToAddress != null) {
                    return 2;
                }
                return 1;
            } else if (section == ToSection) {
                return ToAddresses.Length;
            } else if (section == CcSection) {
                return CcAddresses.Length;
            } else if (section == BccSection) {
                return BccAddresses.Length;
            } else if (section == DebugSection) {
                return DebugDeleteMessageRow + 1;
            }
            return 0;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == FromSection || indexPath.Section == ToSection || indexPath.Section == CcSection || indexPath.Section == BccSection) {
                return MessageAddressCell.PreferredHeight;
            } else if (indexPath.Section == DebugSection) {
                return NameValueCell.PreferredHeight;
            }
            return 44.0f;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            if (section == FromSection) {
                return FromHeader.PreferredHeight;
            } else if (section == ToSection) {
                return ToHeader.PreferredHeight;
            } else if (section == CcSection) {
                return CcHeader.PreferredHeight;
            } else if (section == BccSection) {
                return BccHeader.PreferredHeight;
            } else if (section == DebugSection) {
                return DebugHeader.PreferredHeight;
            }
            return 0.0f;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (section == FromSection) {
                if (FromAddress != null && ReplyToAddress != null) {
                    FromHeader.Label.Text = "From/Reply-To";
                } else if (ReplyToAddress != null) {
                    FromHeader.Label.Text = "Reply-To";
                } else {
                    FromHeader.Label.Text = "From";
                }
                return FromHeader;
            } else if (section == ToSection) {
                return ToHeader;
            } else if (section == CcSection) {
                return CcHeader;
            } else if (section == BccSection) {
                return BccHeader;
            } else if (section == DebugSection) {
                return DebugHeader;
            }
            return null;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == DebugSection) {
                if (indexPath.Row == DebugAccountIdRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Account ID";
                    cell.ValueLabel.Text = Message.AccountId.ToString ();
                    return cell;
                } else if (indexPath.Row == DebugMessageIdRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Message ID";
                    cell.ValueLabel.Text = Message.Id.ToString ();
                    return cell;
                } else if (indexPath.Row == DebugBodyIdRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Body ID";
                    cell.ValueLabel.Text = Message.BodyId.ToString ();
                    return cell;
                } else if (indexPath.Row == DebugBodyTypeRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Body Type";
                    cell.ValueLabel.Text = Body.BodyType.ToString ();
                    return cell;
                } else if (indexPath.Row == DebugBodyRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Raw Source";
                    if (!(cell.AccessoryView is DisclosureAccessoryView)) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                } else if (indexPath.Row == DebugMimeRow) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Mime Tree";
                    cell.DetailTextLabel.Text = Mime.Body.ContentType.ToString ();
                    cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                    cell.DetailTextLabel.TextColor = A.Color_NachoTextGray;
                    if (!(cell.AccessoryView is DisclosureAccessoryView)) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                } else if (indexPath.Row == DebugDeleteBodyRow) {
                    var cell = tableView.DequeueReusableCell (ActionCellIdentifier) as ActionCell;
                    cell.TextLabel.Text = "Delete Body File";
                    cell.TextLabel.TextColor = A.Color_NachoRed;
                    return cell;
                } else if (indexPath.Row == DebugDeleteMessageRow) {
                    var cell = tableView.DequeueReusableCell (ActionCellIdentifier) as ActionCell;
                    cell.TextLabel.Text = "Delete Message From DB";
                    cell.TextLabel.TextColor = A.Color_NachoRed;
                    return cell;
                }
            } else {
                MailboxAddress mailbox = MailboxForIndexPath (indexPath);
                if (mailbox != null) {
                    var cell = tableView.DequeueReusableCell (MessageAddressCellIdentifier) as MessageAddressCell;
                    NcContactPortraitEmailIndex portraitEntry;
                    int portraitId = 0;
                    int colorIndex = 1;
                    if (PortraitCache.TryGetValue (mailbox.Address.ToLowerInvariant (), out portraitEntry)) {
                        portraitId = portraitEntry.PortraitId;
                        colorIndex = portraitEntry.ColorIndex;
                    }
                    cell.SetAddress (mailbox, portraitId, colorIndex);
                    if (!(cell.AccessoryView is DisclosureAccessoryView)) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                }
            }
            return null;
        }

        public override bool ShouldHighlightRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == DebugSection) {
                if (indexPath.Row == DebugAccountIdRow || indexPath.Row == DebugMessageIdRow || indexPath.Row == DebugBodyIdRow || indexPath.Row == DebugBodyTypeRow) {
                    return false;
                }
            }
            return base.ShouldHighlightRow (tableView, indexPath);
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == DebugSection) {
                if (indexPath.Row == DebugAccountIdRow || indexPath.Row == DebugMessageIdRow || indexPath.Row == DebugBodyIdRow || indexPath.Row == DebugBodyTypeRow) {
                    return null;
                }
            }
            return base.WillSelectRow (tableView, indexPath);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == DebugSection) {
                if (indexPath.Row == DebugBodyRow) {
                    ShowBody ();
                } else if (indexPath.Row == DebugMimeRow) {
                    ShowMime ();
                } else if (indexPath.Row == DebugDeleteBodyRow) {
                    DeleteBody ();
                } else if (indexPath.Row == DebugDeleteMessageRow) {
                    DeleteMessage ();
                }
            } else {
                var mailbox = MailboxForIndexPath (indexPath);
                if (mailbox != null) {
                    var contact = McContact.QueryBestMatchByEmailAddress (Message.AccountId, mailbox.Address);
                    if (contact != null) {
                        ShowContact (contact);
                    }
                }
            }
        }

        MailboxAddress MailboxForIndexPath (NSIndexPath indexPath)
        {
            MailboxAddress address = null;
            if (indexPath.Section == FromSection) {
                if (indexPath.Row == FromRow) {
                    address = FromAddress;
                } else if (indexPath.Row == ReplyToRow) {
                    address = ReplyToAddress;
                }
            } else if (indexPath.Section == ToSection) {
                address = ToAddresses [indexPath.Row];
            } else if (indexPath.Section == CcSection) {
                address = CcAddresses [indexPath.Row];
            } else if (indexPath.Section == BccSection) {
                address = BccAddresses [indexPath.Row];
            }
            return address;
        }

        #endregion

        #region Loading Data

        void Setup ()
        {
            ParseHeaders ();
            CachePortraits ();
            DetermineTableSections ();
        }

        void ParseHeaders ()
        {
            FromAddress = null;
            ReplyToAddress = null;
            ToAddresses = null;
            CcAddresses = null;
            BccAddresses = null;
            MailboxAddress.TryParse (Message.From, out FromAddress);
            if (!String.IsNullOrWhiteSpace (Message.ReplyTo)) {
                if (MailboxAddress.TryParse (Message.ReplyTo, out ReplyToAddress)) {
                    if (FromAddress != null && FromAddress.Address.ToLowerInvariant () == ReplyToAddress.Address.ToLowerInvariant ()) {
                        ReplyToAddress = null;
                    }
                }
            }

            InternetAddressList iList;
            if (!String.IsNullOrWhiteSpace(Message.To) && InternetAddressList.TryParse (Message.To, out iList)) {
                ToAddresses = iList.Mailboxes.ToArray ();
            }
            if (!String.IsNullOrWhiteSpace(Message.Cc) && InternetAddressList.TryParse (Message.Cc, out iList)) {
                CcAddresses = iList.Mailboxes.ToArray ();
            }
            if (!String.IsNullOrWhiteSpace(Message.Bcc) && InternetAddressList.TryParse (Message.Bcc, out iList)) {
                BccAddresses = iList.Mailboxes.ToArray ();
            }
        }

        void CachePortraits ()
        {
            var entries = McContact.QueryForMessagePortraitEmails (Message.Id);
            PortraitCache = new Dictionary<string, NcContactPortraitEmailIndex> (entries.Count);
            foreach (var entry in entries) {
                if (!PortraitCache.ContainsKey(entry.EmailAddress.ToLowerInvariant ())) {
                    PortraitCache.Add (entry.EmailAddress.ToLowerInvariant (), entry);
                }
            }
        }

        void DetermineTableSections ()
        {
            int section = 0;
            int row = 0;
            if (FromAddress != null){
                FromRow = row;
                FromSection = section;
                section += 1;
                row += 1;
            }
            if (ReplyToAddress != null) {
                if (FromSection == -1) {
                    FromSection = 1;
                    section += 1;
                }
                ReplyToRow = row;
                row += 1;
            }
            if (ToAddresses != null) {
                ToSection = section;
                section += 1;
            } else {
                ToSection = -1;
            }
            if (CcAddresses != null) {
                CcSection = section;
                section += 1;
            } else {
                CcSection = -1;
            }
            if (BccAddresses != null) {
                BccSection = section;
                section += 1;
            } else {
                BccSection = -1;
            }
            SectionCount = section;
        }

        #endregion

        #region Debug Mode

        void EnableDebugMode ()
        {
            if (!IsDebugEnabled){
                if (Message.BodyId != 0) {
                    Body = McBody.QueryById<McBody> (Message.BodyId);
                    if (Body.BodyType == McAbstrFileDesc.BodyTypeEnum.MIME_4 && Body.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        Mime = MimeMessage.Load (Body.GetFilePath());
                    }
                }
                var row = DebugBodyIdRow;
                if (Body != null && Body.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                    DebugBodyTypeRow = row;
                    row += 1;
                    DebugBodyRow = row;
                    row += 1;
                    if (Body.BodyType == McAbstrFileDesc.BodyTypeEnum.MIME_4) {
                        DebugMimeRow = row;
                        row += 1;
                    }
                    DebugDeleteBodyRow = row;
                    row += 1;
                }
                DebugDeleteMessageRow = row;
                IsDebugEnabled = true;
                DebugSection = SectionCount;
                SectionCount += 1;
                TableView.BeginUpdates ();
                TableView.InsertSections (NSIndexSet.FromIndex((nint)DebugSection), UITableViewRowAnimation.Automatic);
                TableView.EndUpdates ();
            }
        }

        #endregion

        #region Private Helpers

        void ShowContact (McContact contact)
        {
            var vc = new ContactDetailViewController ();
            vc.contact = contact;
            NavigationController.PushViewController (vc, true);
        }

        void ShowBody ()
        {
            var viewController = new MessageRawBodyViewController ();
            viewController.BodyContents = Body.GetContentsString ();
            NavigationController.PushViewController (viewController, animated: true);
        }

        void ShowMime ()
        {
            var multipart = Mime.Body as Multipart;
            if (multipart != null) {
                var viewController = new MessageMimePartViewController ();
                viewController.Part = multipart;
                NavigationController.PushViewController (viewController, animated: true);
            } else {
                var part = Mime.Body as MimePart;
                var viewController = new MessageRawBodyViewController ();
                using (var stream = new System.IO.MemoryStream ()) {
                    part.WriteTo (stream);
                    viewController.BodyContents = System.Text.UTF8Encoding.UTF8.GetString (stream.ToArray ());
                }
                NavigationController.PushViewController (viewController, animated: true);
            }
        }

        void DeleteBody ()
        {
            var bundle = new NcEmailMessageBundle (Message);
            bundle.Invalidate ();
            Body.DeleteFile ();
            NavigationController.PopToRootViewController (animated: true);
        }

        void DeleteMessage ()
        {
            Message.Delete ();
            NavigationController.PopToRootViewController (animated: true);
        }

        #endregion

        #region Cells

        private class ActionCell : SwipeTableViewCell
        {

            public static nfloat PreferredHeight = 44.0f;

            public ActionCell (IntPtr handle) : base (handle)
            {
                TextLabel.Font = A.Font_AvenirNextRegular14;
                TextLabel.TextColor = A.Color_NachoGreen;
            }
        }

        private class MessageAddressCell : SwipeTableViewCell
        {

            PortraitView PortraitView;
            nfloat PortraitSize = 40.0f;
            public static nfloat PreferredHeight = 64.0f;

            public MessageAddressCell (IntPtr handle) : base (handle)
            {
                HideDetailWhenEmpty = true;

                PortraitView = new PortraitView (new CGRect(0.0f, 0.0f, PortraitSize, PortraitSize));
                ContentView.AddSubview(PortraitView);

                SeparatorInset = new UIEdgeInsets (0.0f, PreferredHeight, 0.0f, 0.0f);

                TextLabel.Font = A.Font_AvenirNextDemiBold17;
                TextLabel.TextColor = A.Color_NachoGreen;

                DetailTextLabel.Font = A.Font_AvenirNextRegular14;
                DetailTextLabel.TextColor = A.Color_NachoTextGray;
            }

            public void SetAddress (MailboxAddress mailbox, int portraitId, int colorIndex)
            {
                var initials = EmailHelper.Initials (mailbox.ToString ());
                PortraitView.SetPortrait (portraitId, colorIndex, initials);
                if (!String.IsNullOrWhiteSpace (mailbox.Name)) {
                    TextLabel.Text = mailbox.Name;
                    DetailTextLabel.Text = mailbox.Address;
                } else {
                    TextLabel.Text = mailbox.Address;
                    DetailTextLabel.Text = "";
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                PortraitView.Center = new CGPoint (SeparatorInset.Left / 2.0f, ContentView.Bounds.Height / 2.0f);
            }

        }

        private class DisclosureAccessoryView : ImageAccessoryView
        {
            public DisclosureAccessoryView () : base ("gen-more-arrow")
            {
            }
        }

        #endregion

        #region Section Headers

        private InsetLabelView CreateCommonHeaderView ()
        {
            var view = new InsetLabelView ();
            view.LabelInsets = new UIEdgeInsets (5.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
            view.Label.Font = A.Font_AvenirNextRegular14;
            view.Label.TextColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.6f);
            view.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
            return view;
        }

        private InsetLabelView _FromHeader;
        private InsetLabelView FromHeader {
            get {
                if (_FromHeader == null) {
                    _FromHeader = CreateCommonHeaderView ();
                }
                return _FromHeader;
            }
        }

        private InsetLabelView _ToHeader;
        private InsetLabelView ToHeader {
            get {
                if (_ToHeader == null) {
                    _ToHeader = CreateCommonHeaderView ();
                    _ToHeader.Label.Text = "To";
                }
                return _ToHeader;
            }
        }

        private InsetLabelView _CcHeader;
        private InsetLabelView CcHeader {
            get {
                if (_CcHeader == null) {
                    _CcHeader = CreateCommonHeaderView ();
                    _CcHeader.Label.Text = "CC";
                }
                return _CcHeader;
            }
        }

        private InsetLabelView _BccHeader;
        private InsetLabelView BccHeader {
            get {
                if (_BccHeader == null) {
                    _BccHeader = CreateCommonHeaderView ();
                    _BccHeader.Label.Text = "BCC";
                }
                return _BccHeader;
            }
        }

        private InsetLabelView _DebugHeader;
        private InsetLabelView DebugHeader {
            get {
                if (_DebugHeader == null) {
                    _DebugHeader = CreateCommonHeaderView ();
                    _DebugHeader.Label.Text = "Debug";
                }
                return _DebugHeader;
            }
        }

        #endregion
    }
}

