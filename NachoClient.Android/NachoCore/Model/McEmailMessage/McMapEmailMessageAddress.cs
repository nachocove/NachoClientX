//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using SQLite;

namespace NachoCore.Model
{
    public enum EmailAddressType
    {
        NONE = 0,
        TO = 1,
        CC = 2}

    ;

    public class McMapEmailMessageAddress : McAbstrObject
    {
        [Indexed]
        public int EmailMessageId { set; get; }

        [Indexed]
        public EmailAddressType AddressType { set; get; }

        [Indexed]
        public int EmailAddressId { set; get; }

        public McMapEmailMessageAddress ()
        {
            AddressType = EmailAddressType.NONE;
        }

        public static List<int> QueryAddressId (int emailMessageId)
        {
            var addressList = NcModel.Instance.Db.Query<McEmailAddress> (
                                  "SELECT * FROM McEmailMessageAddress AS e " +
                                  "WHERE e.EmailMessageId = ?", emailMessageId);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static List<int> QueryAddressId (int emailMessageId, EmailAddressType addressType)
        {
            var addressList = NcModel.Instance.Db.Query<McEmailAddress> (
                                  "SELECT * FROM McEmailMessageAddress AS e " +
                                  "WHERE e.EmailMessageId = ? AND e.AddressType = ?", emailMessageId, addressType);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static List<int> QueryToAddressId (int emailMessageId)
        {
            return QueryAddressId (emailMessageId, EmailAddressType.TO);
        }

        public static List<int> QueryCcAddressId (int emailMessageId)
        {
            return QueryAddressId (emailMessageId, EmailAddressType.CC);
        }

        public static List<int> QueryMessageId (int emailAddressId)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailMessageAddress> (
                                  "SELECT * FROM McMapEmailMessageAddress as e" +
                                  "WHERE e.EmailAddressId = ?", emailAddressId);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public static List<int> QueryMessageId (int emailAddressId, EmailAddressType addressType)
        {
            var addressList = NcModel.Instance.Db.Query<McMapEmailMessageAddress> (
                                  "SELECT * FROM McMapEmailMessageAddress as e" +
                                  "WHERE e.EmailAddress = ? AND e.AddressType = ?", emailAddressId, addressType);
            return (from address in addressList
                             select address.Id).ToList ();
        }

        public List<int> QueryMessageIdByToAddress (int toEmailAddressId)
        {
            return QueryMessageId (toEmailAddressId, EmailAddressType.TO);
        }

        public List<int> QueryMesasgeIdByCcAddresss (int ccEmailAddressId)
        {
            return QueryMessageId (ccEmailAddressId, EmailAddressType.CC);
        }
    }
}

