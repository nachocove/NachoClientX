//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using NachoClient;

namespace Test.Common
{
    /// <summary>
    /// Test the conversion from GlobalObjId, which is used in McMeetingRequest element in e-mail messages, to UID,
    /// which is used in calendar events.  The values for GlobalObjId and UID were taken from actual e-mail messages
    /// and their corresponding calendar events.
    /// </summary>
    public class GlobalObjIdToUid : NcTestBase
    {
        private void CheckConversion (string globalObjId, string expectedUid)
        {
            McMeetingRequest mr = new McMeetingRequest () {
                GlobalObjId = globalObjId
            };
            string uid = mr.GetUID ();
            Assert.AreEqual (expectedUid, uid, "The conversion from GlobalObjId to UID is incorrect.");
        }

        [Test]
        public void OutlookId ()
        {
            // From an Exchange 2007 server
            CheckConversion (
                "BAAAAIIA4AB0xbcQGoLgCAAAAACg8Ti290bQAQAAAAAAAAAAEAAAAKzDgIkSjFhJgeiLEnspiYQ=",
                "040000008200E00074C5B7101A82E00800000000A0F138B6F746D001000000000000000010000000ACC38089128C584981E88B127B298984");

            // From the Office 365 server
            CheckConversion (
                "BAAAAIIA4AB0xbcQGoLgCAAAAAC5tWJt90bQAQAAAAAAAAAAEAAAANVInG07x5BHpMCqVfj+/kE=",
                "040000008200E00074C5B7101A82E00800000000B9B5626DF746D001000000000000000010000000D5489C6D3BC79047A4C0AA55F8FEFE41");
        }

        [Test]
        public void vCal_ID ()
        {
            // From outlook.com (a.k.a. Hotmail)
            CheckConversion (
                "BAAAAIIA4AB0xbcQGoLgCAAAAAAAAAAAAAAAAAAAAAAAAAAAMQAAAHZDYWwtVWlkAQAAADE4NGY5MWMwLWJjMTItNGI2My04MGVjLTZiMTAxMzJmNTU3NQA=",
                "184f91c0-bc12-4b63-80ec-6b10132f5575");

            // From GFE
            CheckConversion (
                "BAAAAIIA4AB0xbcQGoLgCAAAAAAAAAAAAAAAAAAAAAAAAAAAMgAAAHZDYWwtVWlkAQAAADNuazhkaDZmcnRsczdkc2M4amQycDlpaTVjQGdvb2dsZS5jb20A",
                "3nk8dh6frtls7dsc8jd2p9ii5c@google.com");
        }
    }
}

