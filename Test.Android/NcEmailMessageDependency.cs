//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcEmailMessageDependency : NcTestBase
    {
        McEmailMessageDependency[] Deps;

        public void SetupEntries ()
        {
            const string sender = "Sender";
            Deps = new McEmailMessageDependency[5];

            for (int n = 0; n < 2; n++) {
                McContact contact = new McContact ();
                contact.AccountId = 1;
                contact.Insert ();
                NcAssert.True (contact.Id == (n+1));
            }

            for (int n = 0; n < 15; n++) {
                McEmailMessage emailMessage = new McEmailMessage ();
                emailMessage.AccountId = 1;
                emailMessage.Insert ();
                NcAssert.True (emailMessage.Id == (n+1));
            }

            Deps [0] = new McEmailMessageDependency ();
            Deps [0].ContactId = 1;
            Deps [0].ContactType = sender;
            Deps [0].EmailMessageId = 11;
            Deps [0].Insert ();

            Deps [1] = new McEmailMessageDependency ();
            Deps [1].ContactId = 1;
            Deps [1].ContactType = sender;
            Deps [1].EmailMessageId = 12;
            Deps [1].Insert ();

            Deps [2] = new McEmailMessageDependency ();
            Deps [2].ContactId = 2;
            Deps [2].ContactType = sender;
            Deps [2].EmailMessageId = 11;
            Deps [2].Insert ();

            Deps [3] = new McEmailMessageDependency ();
            Deps [3].ContactId = 2;
            Deps [3].ContactType = sender;
            Deps [3].EmailMessageId = 13;
            Deps [3].Insert ();

            Deps [4] = new McEmailMessageDependency ();
            Deps [4].ContactId = 2;
            Deps [4].ContactType = sender;
            Deps [4].EmailMessageId = 14;
            Deps [4].Insert ();
        }

        public void Compare (McEmailMessageDependency expected, McEmailMessageDependency got)
        {
            Assert.AreEqual (expected.ContactId, got.ContactId);
            Assert.AreEqual (expected.EmailMessageId, got.EmailMessageId);
            Assert.AreEqual (expected.ContactType, got.ContactType);
        }

        [Test]
        public void TestQueryByContact ()
        {
            SetupEntries ();

            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByContactId (1);
            Assert.NotNull (depList);
            Assert.AreEqual (2, depList.Count);
            Compare (Deps [0], depList [0]);
            Compare (Deps [1], depList [1]);

            depList = McEmailMessageDependency.QueryByContactId (2);
            Assert.NotNull (depList);
            Assert.AreEqual (3, depList.Count);
            Compare (Deps [2], depList [0]);
            Compare (Deps [3], depList [1]);
            Compare (Deps [4], depList [2]);
        }

        [Test]
        public void TestQueryByEmailMessage ()
        {
            SetupEntries ();

            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailMessageId (11);
            Assert.NotNull (depList);
            Assert.AreEqual (2, depList.Count);
            Compare (Deps [0], depList [0]);
            Compare (Deps [2], depList [1]);

            depList = McEmailMessageDependency.QueryByEmailMessageId (12);
            Assert.NotNull (depList);
            Assert.AreEqual (1, depList.Count);
            Compare (Deps [1], depList [0]);

            depList = McEmailMessageDependency.QueryByEmailMessageId (13);
            Assert.NotNull (depList);
            Assert.AreEqual (1, depList.Count); 
            Compare (Deps [3], depList [0]);

            depList = McEmailMessageDependency.QueryByEmailMessageId (14);
            Assert.NotNull (depList);
            Assert.AreEqual (1, depList.Count);
            Compare (Deps [4], depList [0]);
        }

        private void AssertNotEmptyByContact (int contactId)
        {
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByContactId (contactId);
            Assert.NotNull (depList);
            Assert.True (0 < depList.Count);
        }

        private void AssertEmptyByContact (int contactId)
        {
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByContactId (contactId);
            Assert.NotNull (depList);
            Assert.True (0 == depList.Count);
        }

        private void TestDeleteOneContact (int contactId)
        {
            AssertNotEmptyByContact (contactId);
            McEmailMessageDependency.DeleteByContactId (contactId);
            AssertEmptyByContact (contactId);
        }

        [Test]
        public void TestDeleteByContact ()
        {
            SetupEntries ();

            TestDeleteOneContact (1);
            TestDeleteOneContact (2);
        }

        private void AssertNotEmptyByEmailMessage (int emailMessageId)
        {
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailMessageId (emailMessageId);
            Assert.NotNull (depList);
            Assert.True (0 < depList.Count);
        }

        private void AssertEmptyByEmailMessage (int emailMessageId)
        {
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailMessageId (emailMessageId);
            Assert.NotNull (depList);
            Assert.True (0 == depList.Count);
        }

        private void TestDeleteOneEmailMessage (int emailMessageId)
        {
            AssertNotEmptyByEmailMessage (emailMessageId);
            McEmailMessageDependency.DeleteByEmailMessageId (emailMessageId);
            AssertEmptyByEmailMessage (emailMessageId);
        }

        [Test]
        public void TestDeleteByEmailMessage ()
        {
            SetupEntries ();

            TestDeleteOneEmailMessage (11);
            TestDeleteOneEmailMessage (12);
            TestDeleteOneEmailMessage (13);
            TestDeleteOneEmailMessage (14);
        }

        public void VerifyListIds<T> (List<T> list, params int [] ids) where T: McObject, new()
        {
            Assert.NotNull (list);
            Assert.AreEqual (ids.Length, list.Count);
            for (int n = 0; n < ids.Length; n++) {
                Assert.AreEqual (ids[n], list[n].Id);
            }
        }

        [Test]
        public void TestQueryDependenciesByContact ()
        {
            SetupEntries ();

            List<McEmailMessage> emailMessageList;

            emailMessageList = McEmailMessageDependency.QueryDependenciesByContactId (1);
            VerifyListIds<McEmailMessage> (emailMessageList, 11, 12);

            emailMessageList = McEmailMessageDependency.QueryDependenciesByContactId (2);
            VerifyListIds<McEmailMessage> (emailMessageList, 11, 13, 14);
        }

        [Test]
        public void TestQueryDependenciesByEmaiMessage ()
        {
            SetupEntries ();

            List<McContact> contactList;

            contactList = McEmailMessageDependency.QueryDependenciesByEmailMessageId (11);
            VerifyListIds<McContact> (contactList, 1, 2);

            contactList = McEmailMessageDependency.QueryDependenciesByEmailMessageId (12);
            VerifyListIds<McContact> (contactList, 1);

            contactList = McEmailMessageDependency.QueryDependenciesByEmailMessageId (13);
            VerifyListIds<McContact> (contactList, 2);

            contactList = McEmailMessageDependency.QueryDependenciesByEmailMessageId (14);
            VerifyListIds<McContact> (contactList, 2);
        }
    }
}

