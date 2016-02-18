//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    [Foundation.Register ("ChatsViewController")]
    public class ChatsViewController : NcUITableViewController
    {

        ChatsTableViewSource Source;
        SwitchAccountButton SwitchAccountButton;
        UIBarButtonItem NewChatButton;

        public ChatsViewController (IntPtr ptr) : base(ptr)
        {
            NavigationItem.RightBarButtonItem = NewChatButton = new UIBarButtonItem ("New", UIBarButtonItemStyle.Plain, NewChat);
        }

        public override void ViewDidLoad ()
        {
//            var account = NcApplication.Instance.DefaultEmailAccount;
//            var addresses = new List<McEmailAddress> (1);
//            McEmailAddress address;
//            McEmailAddress.Get (account.Id, "owens@d3.officeburrito.com", out address);
//            addresses.Add (address);
//            var chat = McChat.ChatForAddresses (account.Id, addresses);


            base.ViewDidLoad ();

            SwitchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = SwitchAccountButton;

            // Adjust the icon; calendar covers all account
            SwitchToAccount (NcApplication.Instance.Account);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (Source.Account.Id != NcApplication.Instance.Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            SwitchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Account != null) {
                if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified || NcApplication.Instance.Account.Id == s.Account.Id) {
                    if (s.Status.SubKind == NcResult.SubKindEnum.Info_ChatSetChanged) {
                        // TODO: show new chat
                    }
                }
            }
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            if (Source == null) {
                TableView.Source = Source = new ChatsTableViewSource (account, this);
            } else {
                Source.Account = account;
                Source.Reset ();
                TableView.ReloadData ();
            }
            NewChatButton.Enabled = account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender);
        }

        void NewChat (object sender, EventArgs args)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = NcApplication.Instance.DefaultEmailAccount;
            NavigationController.PushViewController (messagesViewController, true);
        }

        public void ChatSelected (McChat chat, Foundation.NSIndexPath indexPath)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = NcApplication.Instance.DefaultEmailAccount;
            messagesViewController.Chat = chat;
            NavigationController.PushViewController (messagesViewController, true);
        }

    }

    public class ChatsTableViewSource : UITableViewSource
    {
        const string ChatCellIdentifier = "Chat";
        public List<McChat> Chats { get; private set; }
        ChatsViewController ViewController;
        public McAccount Account;

        public ChatsTableViewSource (McAccount account, ChatsViewController viewController) : base ()
        {
            ViewController = viewController;
            Account = account;
            Reset ();
        }

        public void Reset ()
        {
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                Chats = McChat.LastestChats ();
            } else {
                Chats = McChat.LastestChatsForAccount (Account.Id);
            }
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            // TODO: should be count of total chats, not just those loaded
            return Chats.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            // TODO: might need to query for chat if outside previously queried range
            var chat = Chats [indexPath.Row];
            var cell = tableView.DequeueReusableCell (ChatCellIdentifier) as ChatTableViewCell;
            if (cell == null) {
                cell = new ChatTableViewCell (ChatCellIdentifier);
            }
            cell.Chat = chat;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ViewController.ChatSelected (chat, indexPath);
        }

    }

    public class ChatTableViewCell : UITableViewCell
    {
        McChat _Chat;
        public McChat Chat {
            get {
                return _Chat;
            }
            set {
                if (_Chat == null || value == null || _Chat.Id != value.Id) {
                    _Chat = value;
                    Update ();
                }
            }
        }

        public ChatTableViewCell (string reuseIdentifier) : base (UITableViewCellStyle.Subtitle, reuseIdentifier)
        {
        }

        void Update ()
        {
            if (Chat == null) {
                TextLabel.Text = "";
                DetailTextLabel.Text = "";
            } else {
                var participants = McChatParticipant.GetChatParticipants (Chat.Id);
                if (participants.Count > 0) {
                    var participant = participants [0];
                    var email = McEmailAddress.QueryById<McEmailAddress> (participant.EmailAddrId);
                    if (participant.ContactId != 0) {
                        var contact = McContact.QueryById<McContact> (participant.ContactId);
                        var name = contact.GetDisplayName ();
                        if (!String.IsNullOrEmpty (name)) {
                            TextLabel.Text = contact.GetDisplayName ();
                            DetailTextLabel.Text = email.CanonicalEmailAddress;
                        } else {
                            TextLabel.Text = email.CanonicalEmailAddress;
                            DetailTextLabel.Text = "";
                        }
                    } else {
                        TextLabel.Text = email.CanonicalEmailAddress;
                        DetailTextLabel.Text = "";
                    }
                } else {
                    TextLabel.Text = "(No participants)";
                    DetailTextLabel.Text = "";
                }
            }
        }
    }

}

