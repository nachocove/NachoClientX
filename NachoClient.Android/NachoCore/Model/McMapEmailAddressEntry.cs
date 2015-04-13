//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using SQLite;

using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMapEmailAddressEntryEmailAddressId
    {
        public int EmailAddressId { get; set; }
    }

    public class NcMapEmailAddressEntryObjectId
    {
        public int ObjectId { get; set; }
    }

    /// <summary>
    /// This is an universal map between all db objects and (canonical) email addresses.
    /// TODO - Currently, contact email addresses are exempted. This is mainly for
    /// coordinating between development efforts from different developers. We'll
    /// merge McContactEmailAddressAttribute into this table later.
    /// </summary>
    public class McMapEmailAddressEntry : McAbstrObjectPerAcc
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

        public static List<int> QueryAddressIds (int accountId, int objectId, NcEmailAddress.Kind addressType)
        {
            var addressList = NcModel.Instance.Db.Query<NcMapEmailAddressEntryEmailAddressId> (
                                  "SELECT EmailAddressId FROM McMapEmailAddressEntry AS e " +
                "WHERE likelihood (e.AccountId = ?, 1.0) AND likelihood (e.ObjectId = ?, 0.001) AND likelihood (e.AddressType = ?, 0.2) ",
                                  accountId, objectId, addressType);
            return (from address in addressList
                             select address.EmailAddressId).ToList ();
        }

        public static List<int> QueryObjectIds (int accountId, int emailAddressId, NcEmailAddress.Kind addressType)
        {
            var objectList = NcModel.Instance.Db.Query<NcMapEmailAddressEntryObjectId> (
                                 "SELECT ObjectId FROM McMapEmailAddressEntry as e " +
                "WHERE likelihood (e.AccountId = ?, 1.0) AND likelihood (e.EmailAddressId = ?, 0.001) AND likelihood (e.AddressType = ?, 0.2) ",
                                 accountId, emailAddressId, addressType);
            return (from obj in objectList
                             select obj.ObjectId).ToList ();
        }


        // Email message -> address queries
        public static int QueryMessageFromAddressId (int accountId, int emailMessageId)
        {
            var ids = QueryAddressIds (accountId, emailMessageId, NcEmailAddress.Kind.From);
            if (0 == ids.Count) {
                return 0;
            }
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static int QueryMessageSenderAddressId (int accountId, int emailMessageId)
        {
            var ids = QueryAddressIds (accountId, emailMessageId, NcEmailAddress.Kind.Sender);
            if (0 == ids.Count) {
                return 0;
            }
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static List<int> QueryMessageToAddressIds (int accountId, int emailMessageId)
        {
            return QueryAddressIds (accountId, emailMessageId, NcEmailAddress.Kind.To);
        }

        public static List<int> QueryMessageCcAddressIds (int accountId, int emailMessageId)
        {
            return QueryAddressIds (accountId, emailMessageId, NcEmailAddress.Kind.Cc);
        }

        public static List<int> QueryMessageAddressIds (int accountId, int emailMessageId)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                  "SELECT * FROM McMapEmailAddressEntry as e " +
                "WHERE likelihood (e.AccountId = ?, 1.0) AND likelihood (e.ObjectId = ?, 0.001) AND " +
                                  "e.AddressType IN (?, ?, ?, ?)", accountId, emailMessageId,
                                  NcEmailAddress.Kind.From, NcEmailAddress.Kind.Sender,
                                  NcEmailAddress.Kind.To, NcEmailAddress.Kind.Cc);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static void DeleteMessageMapEntries (int accountId, int emailMessageId)
        {
            NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                "DELETE FROM McMapEmailAddressEntry " +
                "WHERE likelihood (AccountId = ?, 1.0) AND likelihood (ObjectId = ?, 0.001) AND " +
                "AddressType IN (?, ?, ?, ?)", accountId, emailMessageId,
                NcEmailAddress.Kind.From, NcEmailAddress.Kind.Sender,
                NcEmailAddress.Kind.To, NcEmailAddress.Kind.Cc);
        }

        public static void DeleteAttendeeMapEntries (int accountId, int attendeeId)
        {
            NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                "DELETE FROM McMapEmailAddressEntry " +
                "WHERE likelihood (AccountId = ?, 1.0) AND likelihood (ObjectId = ?, 0.001) AND " +
                "AddressType IN (?, ?, ?, ?)", accountId, attendeeId,
                NcEmailAddress.Kind.Optional, NcEmailAddress.Kind.Required,
                NcEmailAddress.Kind.Resource, NcEmailAddress.Kind.Unknown);
        }

        public static void DeleteMapEntries (int accountId, int objectId, NcEmailAddress.Kind addressType)
        {
            NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                "DELETE FROM McMapEmailAddressEntry " +
                "WHERE likelihood (AccountId = ?, 1.0) AND likelihood (ObjectId = ?, 0.001) AND likelihood (AddressType = ?, 0.2)",
                accountId, objectId, addressType);
        }

        // Address -> email message queries
        public static List<int> QueryMessageIdsByToAddress (int accountId, int toEmailAddressId)
        {
            return QueryObjectIds (accountId, toEmailAddressId, NcEmailAddress.Kind.To);
        }

        public static List<int> QueryMessageIdsByCcAddress (int accountId, int ccEmailAddressId)
        {
            return QueryObjectIds (accountId, ccEmailAddressId, NcEmailAddress.Kind.Cc);
        }

        public static List<int> QueryMessageIdsByFromAddress (int accountId, int fromEmailAddressId)
        {
            return QueryObjectIds (accountId, fromEmailAddressId, NcEmailAddress.Kind.From);
        }

        public static List<int> QueryMessageIdsBySenderAddress (int accountId, int senderEmailAddressId)
        {
            return QueryObjectIds (accountId, senderEmailAddressId, NcEmailAddress.Kind.Sender);
        }

        // Attendee -> address queries
        public static List<int> QueryOptionalAddressIds (int accountId, int attendeeId)
        {
            return QueryObjectIds (accountId, attendeeId, NcEmailAddress.Kind.Optional);
        }

        public static List<int> QueryRequiredAddressIds (int accountId, int attendeeId)
        {
            return QueryObjectIds (accountId, attendeeId, NcEmailAddress.Kind.Required);
        }

        public static List<int> QueryResourceAddressIds (int accountId, int attendeeId)
        {
            return QueryObjectIds (accountId, attendeeId, NcEmailAddress.Kind.Resource);
        }

        // Address -> attendee query
        public static List<int> QueryAttendeeIdsByOptionalAddress (int accountId, int optionalEmailAddressId)
        {
            return QueryObjectIds (accountId, optionalEmailAddressId, NcEmailAddress.Kind.Optional);
        }

        public static List<int> QueryAttendeeIdsByRequiredAddress (int accountId, int requiredEmailAddressId)
        {
            return QueryObjectIds (accountId, requiredEmailAddressId, NcEmailAddress.Kind.Required);
        }

        public static List<int> QueryAttendeeIdsByResourceAddress (int accountId, int resourceEmailAddressId)
        {
            return QueryObjectIds (accountId, resourceEmailAddressId, NcEmailAddress.Kind.Resource);
        }
    }
}

