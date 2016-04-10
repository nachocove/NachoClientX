//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{


    public class ChatParticipantListViewController : NcUITableViewController
    {
        public ChatMessagesViewController MessagesViewController;
        ChatParticipantTableViewSource Source;
        public List<McChatParticipant> Participants;
        UIStoryboard mainStorybaord;
        UIStoryboard MainStoryboard {
            get {
                if (mainStorybaord == null) {
                    mainStorybaord = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                }
                return mainStorybaord;
            }
        }

        public ChatParticipantListViewController () : base()
        {
            NavigationItem.Title = "Chat Participants";
            using (var image = UIImage.FromBundle ("chat-add-contact")) {
                NavigationItem.RightBarButtonItem = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, AddContact);
            }
        }

        public override void LoadView ()
        {
            TableView = new UITableView ();
            TableView.RowHeight = ChatParticipantTableViewCell.HEIGHT;
            Source = new ChatParticipantTableViewSource (this);
            TableView.Source = Source;
            View = TableView;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public void ParticipantSelected (McChatParticipant participant)
        {
            var contactDetailViewController = new ContactDetailViewController ();
            contactDetailViewController.contact = McContact.QueryById<McContact> (participant.ContactId);
            NavigationController.PushViewController (contactDetailViewController, true);
        }

        void AddContact (object sender, EventArgs e)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.To);
            ShowContactSearch (address);
        }

        public void ShowContactSearch (NcEmailAddress address)
        {
            var searchController = new ContactSearchViewController ();
            searchController.SetOwner (MessagesViewController, MessagesViewController.Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, searchController);
        }
    }

    public class ChatParticipantTableViewSource : UITableViewSource
    {

        const string ParticipantIdentifier = "Participant";
        
        ChatParticipantListViewController ParticipantsViewController;

        public ChatParticipantTableViewSource (ChatParticipantListViewController vc)
        {
            ParticipantsViewController = vc;
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return ParticipantsViewController.Participants.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (ParticipantIdentifier) as ChatParticipantTableViewCell;
            if (cell == null) {
                cell = new ChatParticipantTableViewCell (ParticipantIdentifier);
            }
            var participant = ParticipantsViewController.Participants [indexPath.Row];
            cell.Participant = participant;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var participant = ParticipantsViewController.Participants [indexPath.Row];
            ParticipantsViewController.ParticipantSelected (participant);
        }

    }

    public class ChatParticipantTableViewCell : UITableViewCell
    {

        PortraitView PortraitView;
        UILabel NameLabel;
        UILabel EmailLabel;
        UIImageView DisclosureIndicator;
        public static nfloat HEIGHT = 60.0f;

        McChatParticipant _Participant;
        public McChatParticipant Participant {
            get {
                return _Participant;
            }
            set {
                _Participant = value;
                Update ();
            }
        }

        public ChatParticipantTableViewCell (string identifier) : base (UITableViewCellStyle.Default, identifier)
        {
            nfloat portraitSize = 40.0f;
            nfloat portraitSpacing = (HEIGHT - portraitSize) / 2.0f;
            PortraitView = new PortraitView (new CGRect(portraitSpacing, portraitSpacing, portraitSize, portraitSize));
            using (var image = UIImage.FromBundle ("chat-arrow-more")) {
                DisclosureIndicator = new UIImageView (image);
                DisclosureIndicator.Frame = new CGRect (Bounds.Width - image.Size.Width - 7.0f, (Bounds.Height - image.Size.Height) / 2.0f, image.Size.Width, image.Size.Height);
            }
            DisclosureIndicator.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            var nameFont = A.Font_AvenirNextDemiBold17;
            var emailFont = A.Font_AvenirNextRegular14;
            var topOffset = (HEIGHT - nameFont.LineHeight - emailFont.LineHeight) / 2.0f;
            NameLabel = new UILabel (new CGRect (HEIGHT, topOffset, DisclosureIndicator.Frame.X - HEIGHT, nameFont.LineHeight));
            NameLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            NameLabel.Font = nameFont;
            NameLabel.TextColor = A.Color_NachoGreen;
            NameLabel.TextAlignment = UITextAlignment.Left;
            NameLabel.Lines = 1;
            NameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            EmailLabel = new UILabel (new CGRect (HEIGHT, NameLabel.Frame.Y + NameLabel.Frame.Height, DisclosureIndicator.Frame.X - HEIGHT, emailFont.LineHeight));
            EmailLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            EmailLabel.Font = emailFont;
            EmailLabel.TextColor = A.Color_NachoTextGray;
            EmailLabel.TextAlignment = UITextAlignment.Left;
            EmailLabel.Lines = 1;
            EmailLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            ContentView.AddSubview (PortraitView);
            ContentView.AddSubview (DisclosureIndicator);
            ContentView.AddSubview (NameLabel);
            ContentView.AddSubview (EmailLabel);
        }

        void Update ()
        {
            if (Participant == null) {
                PortraitView.SetPortrait (0, 0, "");
                NameLabel.Text = "";
                EmailLabel.Text = "";
            }else{
                PortraitView.SetPortrait (Participant.CachedPortraitId, Participant.CachedColor, Participant.CachedInitials);
                NameLabel.Text = Participant.CachedName;
                var mailbox = MimeKit.MailboxAddress.Parse (Participant.EmailAddress) as MimeKit.MailboxAddress;
                if (mailbox != null){
                    if (String.Equals (Participant.CachedName, mailbox.Address, StringComparison.OrdinalIgnoreCase)) {
                        EmailLabel.Text = "";
                    } else {
                        EmailLabel.Text = mailbox.Address;
                    }
                }else{
                    EmailLabel.Text = "";
                }
            }
        }

    }
}

