﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Brain;
using Test.iOS;

namespace Test.Common
{
    public class NcContactGleanerTest : NcTestBase
    {
        public const int AccountId = 2;

        public NcContactGleanerTest ()
        {
        }

        protected void CheckHasContact (string address, string firstName, string lastName)
        {
            var contactList = McContact.QueryGleanedContactsByEmailAddress (AccountId, address);
            Assert.AreEqual (1, contactList.Count);
            Assert.AreEqual (firstName, contactList [0].FirstName);
            Assert.AreEqual (lastName, contactList [0].LastName);
        }

        protected void CheckHasNoContact (string address)
        {
            var contactList = McContact.QueryGleanedContactsByEmailAddress (AccountId, address);
            Assert.AreEqual (0, contactList.Count);
        }

        [Test]
        public void GleanContactsTest ()
        {
            McFolder gleanedFolder = FolderOps.CreateFolder (AccountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);
            Assert.NotNull (gleanedFolder);

            var emailMessage = new McEmailMessage () {
                AccountId = AccountId,
                From = "<bob@company.net>",
                To = "John Brown <john@abc.org>, Jane Doe <jane@abc.org>",
                Cc = "Mike Jordan <mike@xyz.org>, Terry Johnson <terry@xyz.org>",
                Sender = "Bob Smith <bob@company.net>",
                ReplyTo = "Support <support@company.net>"
            };

            emailMessage.Insert ();

            Assert.False (emailMessage.HasBeenGleaned);
            bool gleaned = NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
            Assert.True (gleaned);

            Assert.False (emailMessage.HasBeenGleaned);
            CheckHasContact ("bob@company.net", null, null);
            CheckHasContact ("john@abc.org", "John", "Brown");
            CheckHasContact ("jane@abc.org", "Jane", "Doe");
            CheckHasNoContact ("mike@xyz.org");

            gleaned = NcContactGleaner.GleanContactsHeaderPart2 (emailMessage);
            Assert.True (gleaned);

            Assert.True (emailMessage.HasBeenGleaned);
            CheckHasContact ("john@abc.org", "John", "Brown");
            CheckHasContact ("jane@abc.org", "Jane", "Doe");
            CheckHasContact ("mike@xyz.org", "Mike", "Jordan");
            CheckHasContact ("terry@xyz.org", "Terry", "Johnson");
            CheckHasContact ("support@company.net", null, null);

            // bob@company.net should have two contacts with different names
            var contactList = McContact.QueryGleanedContactsByEmailAddress (AccountId, "bob@company.net");
            Assert.AreEqual (2, contactList.Count);
            if (null == contactList [0].FirstName) {
                Assert.AreEqual (null, contactList [0].LastName);
                Assert.AreEqual ("Bob", contactList [1].FirstName);
                Assert.AreEqual ("Smith", contactList [1].LastName);
            } else {
                Assert.AreEqual ("Bob", contactList [0].FirstName);
                Assert.AreEqual ("Smith", contactList [0].LastName);
                Assert.AreEqual (null, contactList [1].FirstName);
                Assert.AreEqual (null, contactList [1].LastName);
            }

            // Run part 1 again to make sure duplicate contacts are rejected
            gleaned = NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
            Assert.True (gleaned);
            CheckHasContact ("john@abc.org", "John", "Brown");
            CheckHasContact ("jane@abc.org", "Jane", "Doe");
        }
    }
}

