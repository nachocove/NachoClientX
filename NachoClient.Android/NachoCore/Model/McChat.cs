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
        public int ParticipantCount { get; set; }
        public string CachedParticipantsLabel { get; set; }
        public int CachedPortraitId1 { get; set; }
        public string CachedInitials1 { get; set; }
        public int CachedPortraitId2 { get; set; }
        public string CachedInitials2 { get; set; }
        public int CachedColor1 { get; set; }
        public int CachedColor2 { get; set; }

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

        public static McChat ChatForAddresses (int accountId, List<NcEmailAddress> addresses)
        {
            var account = McAccount.QueryById<McAccount> (accountId);
            var mcaddresses = new List<McEmailAddress> (addresses.Count);
            var addressesById = new Dictionary<int, McEmailAddress> (addresses.Count);
            foreach (var address in addresses) {
                McEmailAddress mcaddress;
                if (McEmailAddress.Get (accountId, address.address, out mcaddress)) {
                    if (!addressesById.ContainsKey(mcaddress.Id) && !String.Equals (mcaddress.CanonicalEmailAddress, account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                        mcaddresses.Add (mcaddress);
                        addressesById.Add (mcaddress.Id, mcaddress);
                    }
                }
            }
            if (mcaddresses.Count == 0) {
                return null;
            }
            var participantHash = HashAddresses (mcaddresses);
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
            var chat = ChatForAddresses (message.AccountId, addresses);
            if (chat != null) {
                chat.AddMessage (message);
                return chat;
            }
            return null;
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
                        if (message.DateReceived > LastMessageDate) {
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
                            UpdateParticipantCache ();
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
                "AND m.Id = (SELECT MAX(m2.Id) FROM McChatMessage cm2 JOIN McEmailMessage m2 ON cm2.MessageId = m2.Id WHERE m2.MessageID = m.MessageID AND cm2.ChatId = ? AND likelihood (m2.IsAwaitingDelete = 0, 1.0)) " +
                "ORDER BY m.DateReceived DESC LIMIT ? OFFSET ?", Id, Id, limit, offset);
        }

        public int MessageCount ()
        {
            return NcModel.Instance.Db.ExecuteScalar<int> (
                "SELECT COUNT(DISTINCT m.MessageId) FROM McChatMessage cm " +
                "JOIN McEmailMessage m ON cm.MessageId = m.Id " +
                "WHERE cm.ChatId = ? " +
                "AND likelihood (m.IsAwaitingDelete = 0, 1.0) ", Id);
        }

        void PopuplateParticipantsFromAddresses (List<NcEmailAddress> addresses)
        {
            ParticipantCount = 0;
            foreach (var address in addresses) {
                McEmailAddress mcaddress;
                if (McEmailAddress.Get (AccountId, address.address, out mcaddress)) {
                    var participant = new McChatParticipant ();
                    participant.ChatId = Id;
                    participant.AccountId = AccountId;
                    participant.EmailAddrId = mcaddress.Id;
                    participant.EmailAddress = address.address;
                    participant.PickContact (mcaddress);
                    participant.Insert ();
                    ParticipantCount += 1;
                }
            }
            ParticipantCount = addresses.Count;
            UpdateParticipantCache ();
            Update ();
        }

        void UpdateParticipantCache ()
        {
            CachedInitials1 = null;
            CachedPortraitId1 = 0;
            CachedInitials2 = null;
            CachedPortraitId2 = 0;
            CachedColor1 = 1;
            CachedColor2 = 1;
            var participants = McChatParticipant.GetChatParticipants (Id);
            if (participants.Count == 0){
                CachedParticipantsLabel = "(No Participants)";
            }else if (participants.Count == 1){
                var participant = participants [0];
                participant.UpdateCachedProperties ();
                CachedParticipantsLabel = participant.CachedName;
                CachedInitials1 = participant.CachedInitials;
                CachedPortraitId1 = participant.CachedPortraitId;
                CachedColor1 = participant.CachedColor;
            }else{
                var firsts = new List<string>();
                int i = 0;
                foreach (var participant in participants){
                    participant.UpdateCachedProperties ();
                    firsts.Add (participant.CachedInformalName);
                    if (i == 0) {
                        CachedInitials1 = participant.CachedInitials;
                        CachedPortraitId1 = participant.CachedPortraitId;
                        CachedColor1 = participant.CachedColor;
                    }
                    if (i == 1) {
                        CachedInitials2 = participant.CachedInitials;
                        CachedPortraitId2 = participant.CachedPortraitId;
                        CachedColor2 = participant.CachedColor;
                    }
                    ++i;
                }
                var final = firsts [firsts.Count - 1];
                firsts.RemoveAt (firsts.Count - 1);
                CachedParticipantsLabel = String.Join(", ", firsts) + " & " + final;
            }
        }
    }
}

