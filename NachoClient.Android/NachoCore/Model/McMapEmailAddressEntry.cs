//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using SQLite;

using NachoCore.Utils;

namespace NachoCore.Model
{
    public enum EmailAddressType
    {
        NONE = 0,
        MESSAGE_TO = 1,
        MESSAGE_CC = 2,
        MESSAGE_FROM = 3,
        MESSAGE_SENDER = 4,
        ATTENDEE_EMAIL = 5,
        CALENDAR_ORGANIZER = 6}

    ;

    /// <summary>
    /// This is an universal map between all db objects and (canonical) email addresses.
    /// </summary>
    public class McMapEmailAddressEntry : McAbstrObject
    {
        [Indexed]
        public EmailAddressType AddressType { set; get; }

        [Indexed]
        public int EmailAddressId { set; get; }

        [Indexed]
        public int ObjectId { set; get; }

        public McMapEmailAddressEntry ()
        {
            AddressType = EmailAddressType.NONE;
        }

        public static List<int> QueryAddressIds (int objectId)
        {
            var addressList = NcModel.Instance.Db.Query<McEmailAddress> (
                                  "SELECT * FROM McMapEmailAddressEntry AS e " +
                                  "WHERE e.ObjectId = ?", objectId);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static List<int> QueryAddressIds (int objectId, EmailAddressType addressType)
        {
            var addressList = NcModel.Instance.Db.Query<McEmailAddress> (
                                  "SELECT * FROM McMapEmailAddressEntry AS e " +
                                  "WHERE e.ObjectId = ? AND e.AddressType = ?", objectId, addressType);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static int QueryMessageFromAddressId (int objectId)
        {
            var ids = QueryAddressIds (objectId, EmailAddressType.MESSAGE_FROM);
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static int QueryMessageSenderAddressId (int objectId)
        {
            var ids = QueryAddressIds (objectId, EmailAddressType.MESSAGE_SENDER);
            NcAssert.True (1 == ids.Count);
            return ids [0];
        }

        public static List<int> QueryMessageToAddressIds (int objectId)
        {
            return QueryAddressIds (objectId, EmailAddressType.MESSAGE_TO);
        }

        public static List<int> QueryMessageCcAddressIds (int objectId)
        {
            return QueryAddressIds (objectId, EmailAddressType.MESSAGE_CC);
        }

        public static List<int> QueryMessageIds (int emailAddressId)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                  "SELECT * FROM McMapEmailAddressEntry as e" +
                                  "WHERE e.EmailAddressId = ? AND " +
                                  "e.AddressType IN (?, ?, ?, ?)", emailAddressId,
                                  EmailAddressType.MESSAGE_FROM, EmailAddressType.MESSAGE_SENDER,
                                  EmailAddressType.MESSAGE_TO, EmailAddressType.MESSAGE_CC);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static List<int> QueryMessageIds (int emailAddressId, EmailAddressType addressType)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailAddressEntry> (
                                  "SELECT * FROM McMapEmailAddressEntry as e" +
                                  "WHERE e.EmailAddress = ? AND e.AddressType = ?", emailAddressId, addressType);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public List<int> QueryMessageIdsByToAddress (int toEmailAddressId)
        {
            return QueryMessageIds (toEmailAddressId, EmailAddressType.MESSAGE_TO);
        }

        public List<int> QueryMesasgeIdsByCcAddresss (int ccEmailAddressId)
        {
            return QueryMessageIds (ccEmailAddressId, EmailAddressType.MESSAGE_CC);
        }
    }
}

