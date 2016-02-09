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
                    builder.AppendFormat ("{x2}", x);
                }
                return builder.ToString ();
            }
        }

        static McChat ChatForAddresses (int accountId, List<McEmailAddress> addresses)
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
            var chatMessage = new McChatMessage ();
            chatMessage.ChatId = chat.Id;
            chatMessage.MessageId = message.Id;
            return chat;
        }

        void PopuplateParticipantsFromAddresses (List<McEmailAddress> addresses)
        {
            foreach (var address in addresses) {
                var contacts = McContact.QueryByEmailAddress (AccountId, address.CanonicalEmailAddress);
                var participant = new McChatParticipant ();
                participant.ChatId = Id;
                participant.EmailAddrId = address.Id;
                if (contacts.Count > 0) {
                    participant.ContactId = contacts [0].Id;
                }
                participant.Insert ();
            }
        }
    }
}

