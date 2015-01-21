//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using SQLite;

using NachoCore.Utils;

namespace NachoCore.Model
{
    /// <summary>
    /// This is an universal map between all db objects and (canonical) email addresses.
    /// TODO - Currently, contact email addresses are exempted. This is mainly for
    /// coordinating between development efforts from different developers. We'll
    /// merge McContactEmailAddressAttribute into this table later.
    /// </summary>
    public class McMapEmailAddressEntry : McAbstrObject
    {
        [Indexed]
        public NcEmailAddress.Kind AddressType { set; get; }

        [Indexed]
        public int EmailAddressId { set; get; }

        [Indexed]
        public int ObjectId { set; get; }

        public McMapEmailAddressEntry ()
        {
            AddressType = NcEmailAddress.Kind.Unknown;
        }

        public static List<int> QueryAddressIds (int objectId, NcEmailAddress.Kind addressType)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                  "SELECT * FROM McMapEmailAddressEntry AS e " +
                                  "WHERE e.ObjectId = ? AND e.AddressType = ?", objectId, addressType);
            return (from address in addressList
                             select address.EmailAddressId).ToList ();
        }

        public static List<int> QueryObjectIds (int emailAddressId, NcEmailAddress.Kind addressType)
        {
            var objectList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                 "SELECT * FROM McMapEmailAddressEntry as e " +
                                 "WHERE e.EmailAddressId = ? AND e.AddressType = ?", emailAddressId, addressType);
            return (from obj in objectList
                             select obj.ObjectId).ToList ();
        }


        // Email message -> address queries
        public static int QueryMessageFromAddressId (int emailMessageId)
        {
            var ids = QueryAddressIds (emailMessageId, NcEmailAddress.Kind.From);
            if (0 == ids.Count) {
                return 0;
            }
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static int QueryMessageSenderAddressId (int emailMessageId)
        {
            var ids = QueryAddressIds (emailMessageId, NcEmailAddress.Kind.Sender);
            if (0 == ids.Count) {
                return 0;
            }
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static List<int> QueryMessageToAddressIds (int emailMessageId)
        {
            return QueryAddressIds (emailMessageId, NcEmailAddress.Kind.To);
        }

        public static List<int> QueryMessageCcAddressIds (int emailMessageId)
        {
            return QueryAddressIds (emailMessageId, NcEmailAddress.Kind.Cc);
        }

        public static List<int> QueryMessageAddressIds (int emailMessageId)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                  "SELECT * FROM McMapEmailAddressEntry as e " +
                                  "WHERE e.ObjectId = ? AND " +
                                  "e.AddressType IN (?, ?, ?, ?)", emailMessageId,
                                  NcEmailAddress.Kind.From, NcEmailAddress.Kind.Sender,
                                  NcEmailAddress.Kind.To, NcEmailAddress.Kind.Cc);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        // Address -> email message queries
        public static List<int> QueryMessageIdsByToAddress (int toEmailAddressId)
        {
            return QueryObjectIds (toEmailAddressId, NcEmailAddress.Kind.To);
        }

        public static List<int> QueryMessageIdsByCcAddress (int ccEmailAddressId)
        {
            return QueryObjectIds (ccEmailAddressId, NcEmailAddress.Kind.Cc);
        }

        public static List<int> QueryMessageIdsByFromAddress (int fromEmailAddressId)
        {
            return QueryObjectIds (fromEmailAddressId, NcEmailAddress.Kind.From);
        }

        public static List<int> QueryMessageIdsBySenderAddress (int senderEmailAddressId)
        {
            return QueryObjectIds (senderEmailAddressId, NcEmailAddress.Kind.Sender);
        }

        // Attendee -> address queries
        public static List<int> QueryOptionalAddressIds (int attendeeId)
        {
            return QueryObjectIds (attendeeId, NcEmailAddress.Kind.Optional);
        }

        public static List<int> QueryRequiredAddressIds (int attendeeId)
        {
            return QueryObjectIds (attendeeId, NcEmailAddress.Kind.Required);
        }

        public static List<int> QueryResourceAddressIds (int attendeeId)
        {
            return QueryObjectIds (attendeeId, NcEmailAddress.Kind.Resource);
        }

        // Address -> attendee query
        public static List<int> QueryAttendeeIdsByOptionalAddress (int optionalEmailAddressId)
        {
            return QueryObjectIds (optionalEmailAddressId, NcEmailAddress.Kind.Optional);
        }

        public static List<int> QueryAttendeeIdsByRequiredAddress (int requiredEmailAddressId)
        {
            return QueryObjectIds (requiredEmailAddressId, NcEmailAddress.Kind.Required);
        }

        public static List<int> QueryAttendeeIdsByResourceAddress (int resourceEmailAddressId)
        {
            return QueryObjectIds (resourceEmailAddressId, NcEmailAddress.Kind.Resource);
        }
    }
}

