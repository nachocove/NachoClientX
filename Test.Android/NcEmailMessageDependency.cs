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

            for (int n = 1; n <= 2; n++) {
                McEmailAddress address = new McEmailAddress ();
                address.AccountId = 1;
                address.NeedUpdate = (0 == (n % 2) ? 1 : 0); // even id already marked for update
                address.Insert ();
                NcAssert.True (address.Id == n);
            }

            for (int n = 1; n <= 15; n++) {
                McEmailMessage emailMessage = new McEmailMessage ();
                emailMessage.AccountId = 1;
                emailMessage.NeedUpdate = (0 == (n % 2) ? 1 : 0); // even id already marked for update
                emailMessage.Insert ();
                NcAssert.True (emailMessage.Id == n);
            }

            Deps [0] = new McEmailMessageDependency (1);
            Deps [0].EmailAddressId = 1;
            Deps [0].EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Deps [0].EmailMessageId = 11;
            Deps [0].Insert ();

            Deps [1] = new McEmailMessageDependency (1);
            Deps [1].EmailAddressId = 1;
            Deps [1].EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Deps [1].EmailMessageId = 12;
            Deps [1].Insert ();

            Deps [2] = new McEmailMessageDependency (1);
            Deps [2].EmailAddressId = 2;
            Deps [2].EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Deps [2].EmailMessageId = 11;
            Deps [2].Insert ();

            Deps [3] = new McEmailMessageDependency (1);
            Deps [3].EmailAddressId = 2;
            Deps [3].EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Deps [3].EmailMessageId = 13;
            Deps [3].Insert ();

            Deps [4] = new McEmailMessageDependency (1);
            Deps [4].EmailAddressId = 2;
            Deps [4].EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
            Deps [4].EmailMessageId = 14;
            Deps [4].Insert ();
        }

        public void Compare (McEmailMessageDependency expected, McEmailMessageDependency got)
        {
            Assert.AreEqual (expected.EmailAddressId, got.EmailAddressId);
            Assert.AreEqual (expected.EmailMessageId, got.EmailMessageId);
            Assert.AreEqual (expected.EmailAddressType, got.EmailAddressType);
        }

        [Test]
        public void TestQueryByContact ()
        {
            SetupEntries ();

            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailAddressId (1);
            Assert.NotNull (depList);
            Assert.AreEqual (2, depList.Count);
            Compare (Deps [0], depList [0]);
            Compare (Deps [1], depList [1]);

            depList = McEmailMessageDependency.QueryByEmailAddressId (2);
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
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailAddressId (contactId);
            Assert.NotNull (depList);
            Assert.True (0 < depList.Count);
        }

        private void AssertEmptyByContact (int contactId)
        {
            List<McEmailMessageDependency> depList = McEmailMessageDependency.QueryByEmailAddressId (contactId);
            Assert.NotNull (depList);
            Assert.True (0 == depList.Count);
        }

        private void TestDeleteOneContact (int contactId)
        {
            AssertNotEmptyByContact (contactId);
            NcModel.Instance.RunInTransaction (() => {
                McEmailMessageDependency.DeleteByEmailAddressId (contactId);
            });
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
            NcModel.Instance.RunInTransaction (() => {
                McEmailMessageDependency.DeleteByEmailMessageId (emailMessageId);
            });
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

        public void VerifyListIds<T> (List<T> list, params int[] ids) where T: McAbstrObject, new()
        {
            Assert.NotNull (list);
            Assert.AreEqual (ids.Length, list.Count);
            for (int n = 0; n < ids.Length; n++) {
                Assert.AreEqual (ids [n], list [n].Id);
            }
        }

       
    }
}

