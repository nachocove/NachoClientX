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
        private void InsertList (List<McMapEmailAddressEntry> entries)
        {
            foreach (McMapEmailAddressEntry entry in entries) {
                var rows = entry.Insert ();
                Assert.AreEqual (1, rows);
            }
        }

        [Test]
        public void EmailMessageIdsToEmailAddressIds ()
        {
            List<McMapEmailAddressEntry> entries = new List<McMapEmailAddressEntry> () {
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 2,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 1,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 4,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 5,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 3,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.Optional,
                    ObjectId = 1, // same object id but this is a McAttendee
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.Sender,
                    ObjectId = 2,
                    EmailAddressId = 6,
                }
            };
            InsertList (entries);

            // From id of McEmailMessage 1 - 1
            Assert.AreEqual (McMapEmailAddressEntry.QueryMessageFromAddressId (1), 1);

            // Sender id of McEmailMessage 1 - 0 (none)
            Assert.AreEqual (McMapEmailAddressEntry.QueryMessageSenderAddressId (1), 0);

            // To ids of McEmailMessage 1 - 2, 3
            var idList = McMapEmailAddressEntry.QueryMessageToAddressIds (1);
            idList.Sort ();
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (2, idList [0]);
            Assert.AreEqual (3, idList [1]);

            // Cc ids of McEmailMessage 1 - 4, 5
            idList = McMapEmailAddressEntry.QueryMessageCcAddressIds (1);
            idList.Sort ();
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (4, idList [0]);
            Assert.AreEqual (5, idList [1]);

            // Sender id of McEmailMessage 2 - 6
            Assert.AreEqual (6, McMapEmailAddressEntry.QueryMessageSenderAddressId (2));

            // All ids of McEmailMessage 1 - 1, 2, 3, 4, 5
            idList = McMapEmailAddressEntry.QueryMessageAddressIds (1);
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
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 1,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.Cc,
                    ObjectId = 1,
                    EmailAddressId = 3,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 1,
                    EmailAddressId = 2,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.From,
                    ObjectId = 2,
                    EmailAddressId = 1,
                },
                new McMapEmailAddressEntry () {
                    AddressType = NcEmailAddress.Kind.To,
                    ObjectId = 2,
                    EmailAddressId = 2,
                }
            };
            InsertList (entries);

            // Message ids with From address 1 - 1, 2
            List<int> idList = McMapEmailAddressEntry.QueryMessageIdsByFromAddress (1);
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (1, idList [0]);
            Assert.AreEqual (2, idList [1]);

            // Message ids with To address 2 - 1, 2
            idList = McMapEmailAddressEntry.QueryMessageIdsByToAddress (2);
            Assert.AreEqual (2, idList.Count);
            Assert.AreEqual (1, idList [0]);
            Assert.AreEqual (2, idList [1]);

            // Message ids with Cc address 3 - 1
            idList = McMapEmailAddressEntry.QueryMessageIdsByCcAddress (3);
            Assert.AreEqual (1, idList.Count);
            Assert.AreEqual (1, idList [0]);

            // Message ids with From address 4 - None
            idList = McMapEmailAddressEntry.QueryMessageIdsByFromAddress (4);
            Assert.AreEqual (0, idList.Count);
        }
    }
}

