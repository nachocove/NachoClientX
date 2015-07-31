//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;

namespace Test.Common
{
    public class McEmailAddressTest : NcTestBase
    {
        [Test]
        public void TestQueryToCcAddressByMessageId ()
        {
            int accountId = 1;
            string alan = "alan@company.net";
            string bob = "bob@company.net";
            string charles = "charles@company.net";
            string david = "david@company.net";
            string ellen = "ellen@company.net";

            var message1 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                To = String.Join (",", bob, charles),
                Cc = String.Join (",", david, ellen),
            };

            McEmailAddress alanAddr, bobAddr, charlesAddr, davidAddr, ellenAddr;
            McEmailAddress.Get (accountId, alan, out alanAddr);
            message1.FromEmailAddressId = alanAddr.Id;

            McEmailAddress.Get (accountId, bob, out bobAddr);
            message1.ToEmailAddressId.Add (bobAddr.Id);

            McEmailAddress.Get (accountId, charles, out charlesAddr);
            message1.ToEmailAddressId.Add (charlesAddr.Id);

            McEmailAddress.Get (accountId, david, out davidAddr);
            message1.CcEmailAddressId.Add (davidAddr.Id);

            McEmailAddress.Get (accountId, ellen, out ellenAddr);
            message1.CcEmailAddressId.Add (ellenAddr.Id);

            message1.Insert ();
            Assert.True (0 < message1.Id);

            var emailAddresses1 = McEmailAddress.QueryToCcAddressByMessageId (message1.Id);
            Assert.AreEqual (4, emailAddresses1.Count);
            Assert.AreEqual (bobAddr.Id, emailAddresses1 [0].Id);
            Assert.AreEqual (charlesAddr.Id, emailAddresses1 [1].Id);
            Assert.AreEqual (davidAddr.Id, emailAddresses1 [2].Id);
            Assert.AreEqual (ellenAddr.Id, emailAddresses1 [3].Id);

            var emailAddresses2 = McEmailAddress.QueryToAddressesByMessageId (message1.Id);
            Assert.AreEqual (2, emailAddresses2.Count);
            Assert.AreEqual (bobAddr.Id, emailAddresses2 [0].Id);
            Assert.AreEqual (charlesAddr.Id, emailAddresses2 [1].Id);

            var emailAddresses3 = McEmailAddress.QueryCcAddressesByMessageId (message1.Id);
            Assert.AreEqual (2, emailAddresses3.Count);
            Assert.AreEqual (davidAddr.Id, emailAddresses3 [0].Id);
            Assert.AreEqual (ellenAddr.Id, emailAddresses3 [1].Id);
        }
    }
}

