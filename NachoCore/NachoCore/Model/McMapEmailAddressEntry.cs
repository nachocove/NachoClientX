//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using SQLite;

using NachoCore.Utils;

namespace NachoCore.Model
{

    public enum EmailMessageAddressType
    {
        Unknown,
        From,
        Sender,
        ReplyTo,
        To,
        Cc,
        Bcc
    }

    /// <summary>
    /// Maps email addresses to email messages
    /// </summary>
    /// <remarks>
    /// Previously this table referenced other object types than just email messages.  However,
    /// only the email message rows were every queried or used in any way.  Always having to qualify
    /// queries with checks on AddressType to weed out ID collisions with other kinds of objects made
    /// the queries unecessarily slow.
    /// 
    /// If we ever need to have references to other kinds of objects, they should create and use their
    /// own tables since it's highly unlikley any query will every want to return results of different
    /// kinds of objects.
    /// </remarks>
    public class McMapEmailAddressEntry : McAbstrObjectPerAcc
    {
        [Indexed]
        public EmailMessageAddressType AddressType { set; get; }

        [Indexed]
        public int EmailAddressId { set; get; }

        /// <summary>
        /// The email message id.  Historically this table referenced other kinds of objects than just email messages,
        /// which is why this is named the more generic ObjectId.  But today is will only ever reference an email message
        /// id.
        /// </summary>
        /// <value>The object identifier.</value>
        [Indexed]
        public int ObjectId { set; get; }

        public static void DeleteMessageMapEntries (int accountId, int emailMessageId)
        {
            var sql = "DELETE FROM McMapEmailAddressEntry WHERE likelihood (ObjectId = ?, 0.001)";
            NcModel.Instance.Db.Query<McMapEmailAddressEntry> (sql, accountId, emailMessageId);
        }

    }
}

