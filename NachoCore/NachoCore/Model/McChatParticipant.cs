//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McChatParticipant : McAbstrObjectPerAcc
    {

        [Indexed]
        public int ChatId { get; set; }
        public string EmailAddress { get; set; }
        public int EmailAddrId { get; set; }
        public int ContactId { get; set; }
        public string CachedName { get; set; }
        public string CachedInformalName { get; set; }
        public string CachedInitials { get; set; }
        public int CachedPortraitId { get; set; }
        public int CachedColor { get; set; }

        public McChatParticipant () : base ()
        {
        }

        public static List<McChatParticipant> GetChatParticipants (int chatId)
        {
            return NcModel.Instance.Db.Query<McChatParticipant> ("SELECT * FROM McChatParticipant WHERE ChatId = ? ORDER BY Id", chatId);
        }

        public static Dictionary<int, McChatParticipant> GetChatParticipantsByEmailId (int chatId)
        {
            var participants = GetChatParticipants (chatId);
            var map = new Dictionary<int, McChatParticipant> (participants.Count);
            foreach (var participant in participants) {
                map [participant.EmailAddrId] = participant;
            }
            return map;
        }

        public void PickContact (McEmailAddress mcaddress = null)
        {
            McContact contact = null;
            if (mcaddress == null) {
                mcaddress = McEmailAddress.QueryById<McEmailAddress> (EmailAddrId);
            }
            if (mcaddress != null) {
                var contacts = McContact.QueryByEmailAddress (AccountId, mcaddress.CanonicalEmailAddress);
                if (contacts.Count == 0) {
                } else if (contacts.Count == 1) {
                    contact = contacts [0];
                } else {
                    var contactsWithPortraits = new List<McContact> (contacts.Where ((McContact c) => {
                        return c.PortraitId != 0;
                    }));
                    if (contactsWithPortraits.Count > 0) {
                        contactsWithPortraits.Sort ((McContact x, McContact y) => {
                            return x.Id - y.Id;
                        });
                        contact = contactsWithPortraits [0];
                    } else {
                        var contactsWithName = new List<McContact> (contacts.Where ((McContact c) => {
                            return !String.IsNullOrEmpty (c.GetDisplayName ());
                        }));
                        if (contactsWithName.Count > 0) {
                            contactsWithName.Sort ((McContact x, McContact y) => {
                                return x.Id - y.Id;
                            });
                            contact = contactsWithName [0];
                        } else {
                            contacts.Sort ((McContact x, McContact y) => {
                                return x.Id - y.Id;
                            });
                            contact = contacts [0];
                        }
                    }
                }
            }
            if (contact != null) {
                ContactId = contact.Id;
            }
        }

        public void UpdateCachedProperties ()
        {
            var email = McEmailAddress.QueryById<McEmailAddress> (EmailAddrId);
            PickContact (email);
            CachedName = null;
            CachedInformalName = null;
            CachedInitials = null;
            CachedPortraitId = 0;
            CachedColor = email.ColorIndex;
            if (ContactId != 0) {
                var contact = McContact.QueryById<McContact> (ContactId);
                if (contact != null) {
                    CachedName = contact.GetDisplayName ();
                    CachedInformalName = contact.GetInformalDisplayName ();
                    CachedPortraitId = contact.PortraitId;
                    CachedInitials = ContactsHelper.GetInitials (contact);
                }
            }
            if (String.IsNullOrEmpty (CachedInitials)) {
                CachedInitials = EmailHelper.Initials (EmailAddress);
            }
            var mailbox = MimeKit.MailboxAddress.Parse (EmailAddress);
            if (mailbox != null) {
                if (String.IsNullOrEmpty (CachedName)) {
                    CachedName = mailbox.Name;
                }
                if (String.IsNullOrEmpty (CachedInformalName) && !String.IsNullOrEmpty (mailbox.Name)) {
                    CachedInformalName = mailbox.Name.Split (' ') [0];
                }
            }
            if (String.IsNullOrEmpty (CachedName)) {
                CachedName = email.CanonicalEmailAddress;
            }
            if (String.IsNullOrEmpty (CachedInformalName)) {
                CachedInformalName = email.CanonicalEmailAddress.Split ('@') [0];
            }
            Update ();
        }

        public static List<NcEmailAddress> ConvertToAddressList (List<McChatParticipant> participants)
        {
            var result = new List<NcEmailAddress> ();
            foreach (var participant in participants) {
                var address = new NcEmailAddress (NcEmailAddress.Kind.Unknown);
                address.index = participant.EmailAddrId;
                address.address = participant.EmailAddress;
                address.contact = McContact.QueryById<McContact> (participant.ContactId);
                result.Add (address);
            }
            return result;
        }

    }
}

