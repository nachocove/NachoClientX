//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using System.IO;
using SQLite;

namespace NachoCore.Model
{
    public class NcMigration60 : NcMigration
    {

        IntPtr InsertStatement;
        IntPtr ConnectionHandle;
        static IntPtr NegativePointer = new IntPtr (-1);

        public NcMigration60 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute ("DELETE FROM McMapEmailAddressEntry");
            ConnectionHandle = NcModel.Instance.Db.Handle;
            InsertStatement = SQLite3.Prepare2 (ConnectionHandle, "INSERT INTO McMapEmailAddressEntry (AccountId, ObjectId, AddressType, EmailAddressId, MigrationVersion, CreatedAt, LastModified) VALUES(?,?,?,?,?,?,?)");
            var selectStatement = SQLite3.Prepare2 (ConnectionHandle, "SELECT Id, \"From\", Sender, ReplyTo, \"To\", Cc, Bcc FROM McEmailMessage ORDER BY Id");
            SQLite3.Result result;
            McEmailMessage message;
            do {
                result = SQLite3.Step (selectStatement);
                message = new McEmailMessage ();
                message.Id = SQLite3.ColumnInt (selectStatement, 0);
                message.From = SQLite3.ColumnString (selectStatement, 1);
                message.Sender = SQLite3.ColumnString (selectStatement, 2);
                message.ReplyTo = SQLite3.ColumnString (selectStatement, 3);
                message.To = SQLite3.ColumnString (selectStatement, 4);
                message.Cc = SQLite3.ColumnString (selectStatement, 5);
                message.Bcc = SQLite3.ColumnString (selectStatement, 6);
                InsertMapEntries (message);
            } while (result == SQLite3.Result.Row);
            if (result != SQLite3.Result.Done) {
            }
            SQLite3.Finalize (InsertStatement);
            SQLite3.Finalize (selectStatement);
        }

        void InsertMapEntries (McEmailMessage message)
        {
            var entries = new List<McMapEmailAddressEntry> ();
            var sender = message.SenderMailbox;
            if (sender.HasValue && McEmailAddress.GetOrCreate (message.AccountId, sender.Value, out var senderAddress)) {
                var map = message.CreateAddressMap ();
                map.EmailAddressId = senderAddress.Id;
                map.AddressType = EmailMessageAddressType.Sender;
                entries.Add (map);
            }
            foreach (var mailbox in message.FromMailboxes) {
                if (McEmailAddress.GetOrCreate (message.AccountId, mailbox, out var address)) {
                    var map = message.CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.From;
                    entries.Add (map);
                }
            }
            foreach (var mailbox in message.ReplyToMailboxes) {
                if (McEmailAddress.GetOrCreate (message.AccountId, mailbox, out var address)) {
                    var map = message.CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.ReplyTo;
                    entries.Add (map);
                }
            }
            foreach (var mailbox in message.ToMailboxes) {
                if (McEmailAddress.GetOrCreate (message.AccountId, mailbox, out var address)) {
                    var map = message.CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.To;
                    entries.Add (map);
                }
            }
            foreach (var mailbox in message.CcMailboxes) {
                if (McEmailAddress.GetOrCreate (message.AccountId, mailbox, out var address)) {
                    var map = message.CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.Cc;
                    entries.Add (map);
                }
            }
            foreach (var mailbox in message.BccMailboxes) {
                if (McEmailAddress.GetOrCreate (message.AccountId, mailbox, out var address)) {
                    var map = message.CreateAddressMap ();
                    map.EmailAddressId = address.Id;
                    map.AddressType = EmailMessageAddressType.Bcc;
                    entries.Add (map);
                }
            }
            foreach (var entry in entries) {
                InsertEntry (entry);
            }
        }

        void InsertEntry (McMapEmailAddressEntry entry)
        {
            entry.LastModified = DateTime.UtcNow;
            entry.CreatedAt = entry.LastModified;
            // AccountId, ObjectId, AddressType, EmailAddressId, MigrationVersion, CreatedAt, LastModified
            SQLite3.Reset (InsertStatement);
            SQLite3.BindInt (InsertStatement, 1, entry.AccountId);
            SQLite3.BindInt (InsertStatement, 2, entry.ObjectId);
            SQLite3.BindInt (InsertStatement, 3, (int)entry.AddressType);
            SQLite3.BindInt (InsertStatement, 4, entry.EmailAddressId);
            SQLite3.BindInt (InsertStatement, 5, entry.MigrationVersion);
            SQLite3.BindInt64 (InsertStatement, 6, entry.CreatedAt.Ticks);
            SQLite3.BindInt64 (InsertStatement, 7, entry.LastModified.Ticks);
            SQLite3.Result result = SQLite3.Step (InsertStatement);
            if (result != SQLite3.Result.Done) {
                string msg = SQLite3.GetErrmsg (ConnectionHandle);
                throw SQLiteException.New (result, msg);
            }
        }
    }
}

