//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    public class McMapEmailAddressEntryTest : NcTestBase
    {
        const int AccountId = 1;

        private void InsertList (List<McMapEmailAddressEntry> entries)
        {
            foreach (McMapEmailAddressEntry entry in entries) {
                var rows = entry.Insert ();
                Assert.AreEqual (1, rows);
            }
        }

        private void CheckIdentical (McMapEmailAddressEntry a, McMapEmailAddressEntry b)
        {
            Assert.AreEqual (a.Id, b.Id);
            Assert.AreEqual (a.AddressType, b.AddressType);
            Assert.AreEqual (a.EmailAddressId, b.EmailAddressId);
            Assert.AreEqual (a.ObjectId, b.ObjectId);
        }

        private void CheckNotExist (int mapId)
        {
            var map = McMapEmailAddressEntry.QueryById<McMapEmailAddressEntry> (mapId);
            Assert.AreEqual (null, map);
        }

        private void CheckExist (McMapEmailAddressEntry expected)
        {
            var map = McMapEmailAddressEntry.QueryById<McMapEmailAddressEntry> (expected.Id);
            Assert.AreNotEqual (null, map);
            CheckIdentical (expected, map);
        }

        [Test]
        public void EmailMessageIdsToEmailAddressIds ()
        {
            List<McMapEmailAddressEntry> entries = new List<McMapEmailAddressEntry> () {
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 2,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 1,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 4,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 5,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 3,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Optional,
                    ObjectId = 1, // same object id but this is a McAttendee
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Sender,
                    ObjectId = 2,
                    EmailAddressId = 6,
                }
            };
            InsertList (entries);

            // From id of McEmailMessage 1 - 1
            Assert.AreEqual (McMapEmailAddressEntry.QueryMessageFromAddressId (AccountId, 1), 1);

            // Sender id of McEmailMessage 1 - 0 (none)
            Assert.AreEqual (McMapEmailAddressEntry.QueryMessageSenderAddressId (AccountId, 1), 0);

            // To ids of McEmailMessage 1 - 2, 3
            var idList = McMapEmailAddressEntry.QueryMessageToAddressIds (AccountId, 1);
            idList.Sort ();
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (2, idList [0]);
            Assert.AreEqual (3, idList [1]);

            // Cc ids of McEmailMessage 1 - 4, 5
            idList = McMapEmailAddressEntry.QueryMessageCcAddressIds (AccountId, 1);
            idList.Sort ();
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (4, idList [0]);
            Assert.AreEqual (5, idList [1]);

            // Sender id of McEmailMessage 2 - 6
            Assert.AreEqual (6, McMapEmailAddressEntry.QueryMessageSenderAddressId (AccountId, 2));

            // All ids of McEmailMessage 1 - 1, 2, 3, 4, 5
            idList = McMapEmailAddressEntry.QueryMessageAddressIds (AccountId, 1);
            idList.Sort ();
            Assert.AreEqual (idList.Count, 5);
            for (int n = 0; n < 5; n++) {
                Assert.AreEqual (n + 1, idList [n]);
            }
        }

        [Test]
        public void EmailAddressIdsToEmailMessageIds ()
        {
            List<McMapEmailAddressEntry> entries = new List<McMapEmailAddressEntry> () {
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 1,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 3,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 2,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 2,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 2,
                    EmailAddressId = 2,
                }
            };
            InsertList (entries);

            // Message ids with From address 1 - 1, 2
            List<int> idList = McMapEmailAddressEntry.QueryMessageIdsByFromAddress (AccountId, 1);
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (1, idList [0]);
            Assert.AreEqual (2, idList [1]);

            // Message ids with To address 2 - 1, 2
            idList = McMapEmailAddressEntry.QueryMessageIdsByToAddress (AccountId, 2);
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (1, idList [0]);
            Assert.AreEqual (2, idList [1]);

            // Message ids with Cc address 3 - 1
            idList = McMapEmailAddressEntry.QueryMessageIdsByCcAddress (AccountId, 3);
            Assert.AreEqual (1, idList.Count);
            Assert.AreEqual (1, idList [0]);

            // Message ids with From address 4 - None
            idList = McMapEmailAddressEntry.QueryMessageIdsByFromAddress (AccountId, 4);
            Assert.AreEqual (0, idList.Count);
        }

        [Test]
        public void DeleteEntries ()
        {
            List<McMapEmailAddressEntry> entries = new List<McMapEmailAddressEntry> () {
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 2,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 1,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 4,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Sender,
                    ObjectId = 1,
                    EmailAddressId = 5,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 2,
                    EmailAddressId = 3,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Optional,
                    ObjectId = 3,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Required,
                    ObjectId = 4,
                    EmailAddressId = 6,
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Resource,
                    ObjectId = 5,
                    EmailAddressId = 7
                },
                new McMapEmailAddressEntry () {
                    AccountId = AccountId,
                    AddressType = NcEmailAddress.Kind.Optional,
                    ObjectId = 6,
                    EmailAddressId = 8
                }
            };
            InsertList (entries);

            // Verify that every object now has a valid (> 0) id
            foreach (var e in entries) {
                Assert.True (0 < e.Id);
            }

            // Delete all email message 1 map entries. Verify that 1st 4 entries are gone
            McMapEmailAddressEntry.DeleteMessageMapEntries (AccountId, entries [0].ObjectId);
            for (int n = 0; n < 4; n++) {
                CheckNotExist (entries [n].Id);
            }

            // Verify that email message 2 map is still around
            CheckExist (entries [4]);

            // Delete just From address map for email 2. Verify the entry is still there
            McMapEmailAddressEntry.DeleteMapEntries (AccountId, entries [4].Id, NcEmailAddress.Kind.From);
            CheckExist (entries [4]);

            // Delete the To address map. The entry should be gone
            McMapEmailAddressEntry.DeleteMapEntries (AccountId, entries [4].ObjectId, NcEmailAddress.Kind.To);
            CheckNotExist (entries [4].Id);

            // Delete all maps for attendees 3, 4, and 5
            for (int m = 5; m < 8; m++) {
                CheckExist (entries [m]);
                McMapEmailAddressEntry.DeleteAttendeeMapEntries (AccountId, entries [m].ObjectId);
                CheckNotExist (entries [m].Id);
                for (int n = (m + 1); n < entries.Count; n++) {
                    CheckExist (entries [n]);
                }
            }
        }
    }
}

