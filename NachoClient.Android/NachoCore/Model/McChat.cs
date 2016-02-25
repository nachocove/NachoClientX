//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using System.Collections.Generic;
using System.Security.Cryptography;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McChat : McAbstrObject
    {

        [Indexed]
        public int AccountId { get; set; }
        [Indexed]
        public string ParticipantHash { get; set; }
        [Indexed]
        public DateTime LastMessageDate { get; set; }
        public string LastMessagePreview { get; set ;}
        public string CachedParticipantsLabel { get; set; }

        public McChat () : base ()
        {
        }

        static string HashAddresses (List<McEmailAddress> addresses)
        {
            var addressIds = new List<int> (addresses.Count);
            foreach (var address in addresses) {
                addressIds.Add (address.Id);
            }
            addressIds.Sort ();
            var idString = String.Join<int> (";", addressIds);
            var hash = HashString (idString);
            return hash;
        }

        static string HashString (string s)
        {
            using (var sha1 = new SHA1Managed ()) {
                var bytes = System.Text.Encoding.UTF8.GetBytes (s);
                var hash = sha1.ComputeHash (bytes);
                var builder = new System.Text.StringBuilder ();
                foreach (var x in hash) {
                    builder.AppendFormat ("{0:X2}", x);
                }
                return builder.ToString ();
            }
        }

        public static List<McChat> LastestChatsForAccount (int accountId)
        {
            return NcModel.Instance.Db.Query<McChat> ("SELECT * FROM McChat WHERE AccountId = ? ORDER BY LastMessageDate DESC", accountId);
        }

        public static List<McChat> LastestChats ()
        {
            return NcModel.Instance.Db.Query<McChat> ("SELECT * FROM McChat ORDER BY LastMessageDate DESC");
        }

        public static McChat ChatForAddresses (int accountId, List<McEmailAddress> addresses)
        {
            var participantHash = HashAddresses (addresses);
            McChat chat = null;
            var chats = NcModel.Instance.Db.Query<McChat> ("SELECT * FROM McChat WHERE ParticipantHash = ? AND AccountId = ?", participantHash, accountId);
            if (chats.Count == 1) {
                return chats [0];
            }
            NcModel.Instance.RunInLock (() => {
                chats = NcModel.Instance.Db.Query<McChat> ("SELECT * FROM McChat WHERE ParticipantHash = ? AND AccountId = ?", participantHash, accountId);
                if (chats.Count == 1) {
                    chat = chats[0];
                }else{
                    chat = new McChat ();
                    chat.ParticipantHash = participantHash;
                    chat.AccountId = accountId;
                    chat.Insert ();
                    chat.PopuplateParticipantsFromAddresses (addresses);
                    var account = McAccount.QueryById<McAccount> (accountId);
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs() {
                        Account = account,
                        Status = NcResult.Info (NcResult.SubKindEnum.Info_ChatSetChanged)
                    });
                }
            });
            return chat;
        }

        public static McChat AssignMessageToChat (McEmailMessage message)
        {
            var account = McAccount.QueryById<McAccount> (message.AccountId);
            var exclude = new List<string> ();
            exclude.Add (account.EmailAddr);
            var addresses = EmailHelper.AddressList (NcEmailAddress.Kind.Unknown, exclude, message.From, message.To, message.Cc);
            var mcaddresses = new List<McEmailAddress> (addresses.Count);
            foreach (var address in addresses) {
                McEmailAddress mcaddress;
                if (McEmailAddress.Get (message.AccountId, address.address, out mcaddress)) {
                    mcaddresses.Add (mcaddress);
                }
            }
            var chat = ChatForAddresses (message.AccountId, mcaddresses);
            chat.AddMessage (message);
            return chat;
        }

        public void AddMessage (McEmailMessage message)
        {
            var existing = NcModel.Instance.Db.Query<McChatMessage> ("SELECT * FROM McChatMessage WHERE MessageId = ? AND ChatId = ?", message.Id, Id);
            if (existing.Count == 0) {
                NcModel.Instance.RunInLock (() => {
                    existing = NcModel.Instance.Db.Query<McChatMessage> ("SELECT * FROM McChatMessage WHERE MessageId = ? AND ChatId = ?", message.Id, Id);
                    if (existing.Count == 0) {
                        var chatMessage = new McChatMessage ();
                        chatMessage.ChatId = Id;
                        chatMessage.AccountId = AccountId;
                        chatMessage.MessageId = message.Id;
                        chatMessage.Insert ();
                        if (LastMessageDate == null || message.DateReceived > LastMessageDate) {
                            LastMessageDate = message.DateReceived;
                            var bundle = new NcEmailMessageBundle (message);
                            if (bundle.TopText != null){
                                var text = bundle.TopText;
                                var whitespacePattern = new System.Text.RegularExpressions.Regex ("\\s+");
                                text = whitespacePattern.Replace (text, " ");
                                LastMessagePreview = text;
                            }else{
                                LastMessagePreview = null;
                            }
                            CachedParticipantsLabel = ParticipantsLabel ();
                            Update ();
                        }
                        var account = McAccount.QueryById<McAccount> (AccountId);
                        NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                            Account = account,
                            Status = NcResult.Info (NcResult.SubKindEnum.Info_ChatMessageAdded),
                            Tokens = new string[] {Id.ToString(), message.Id.ToString()}
                        });
                    }
                });
            }
        }

        public List<McEmailMessage> GetMessages (int offset = 0, int limit = 50)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT m.* FROM McChatMessage cm " +
                "JOIN McEmailMessage m ON cm.MessageId = m.Id " +
                "WHERE cm.ChatId = ? " +
                "AND likelihood (m.IsAwaitingDelete = 0, 1.0) " +
                "AND m.Id = (SELECT MAX(Id) FROM McEmailMessage m2 WHERE m2.MessageID = m.MessageID AND m2.AccountID = ? AND likelihood (m2.IsAwaitingDelete = 0, 1.0)) " +
                "ORDER BY m.DateReceived DESC LIMIT ? OFFSET ?", Id, AccountId, limit, offset);
        }

        void PopuplateParticipantsFromAddresses (List<McEmailAddress> addresses)
        {
            foreach (var address in addresses) {
                var contacts = McContact.QueryByEmailAddress (AccountId, address.CanonicalEmailAddress);
                var participant = new McChatParticipant ();
                participant.ChatId = Id;
                participant.AccountId = AccountId;
                participant.EmailAddrId = address.Id;
                if (contacts.Count > 0) {
                    participant.ContactId = contacts [0].Id;
                }
                participant.Insert ();
            }
            CachedParticipantsLabel = ParticipantsLabel ();
            Update ();
        }

        public string ParticipantsLabel ()
        {
            var participants = McChatParticipant.GetChatParticipants (Id);
            if (participants.Count == 0){
                return "(No Participants)";
            }else if (participants.Count == 1){
                var participant = participants [0];
                var email = McEmailAddress.QueryById<McEmailAddress> (participant.EmailAddrId);
                string name = null;
                if (participant.ContactId != 0) {
                    var contact = McContact.QueryById<McContact> (participant.ContactId);
                    if (contact != null){
                        name = contact.GetDisplayName ();
                    }
                }
                if (String.IsNullOrEmpty (name)){
                    name = email.CanonicalEmailAddress;
                }
                return name;
            }else{
                var firsts = new List<string>();
                foreach (var participant in participants){
                    var email = McEmailAddress.QueryById<McEmailAddress> (participant.EmailAddrId);
                    string name = null;
                    if (participant.ContactId != 0) {
                        var contact = McContact.QueryById<McContact> (participant.ContactId);
                        if (contact != null) {
                            name = contact.GetInformalDisplayName ();
                        }
                    }
                    if (String.IsNullOrEmpty (name)) {
                        name = email.CanonicalEmailAddress.Split ('@')[0];
                    }
                    firsts.Add (name);
                }
                var final = firsts [firsts.Count - 1];
                firsts.RemoveAt (firsts.Count - 1);
                return String.Join(", ", firsts) + " & " + final;
            }
        }
    }
}

