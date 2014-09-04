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
    public class GlobalObjIdToUid : NcTestBase
    {
        [Test]
        public void OutlookId ()
        {
            var GOI = "BAAAAIIA4AB0xbcQGoLgCAfUCRDgQMnBJoXEAQAAAAAAAAAAEAAAAAvw7UtuTulOnjnjhns3jvM=";
            var UID = Util.GlobalObjIdToUID (GOI);
            Assert.AreEqual ("040000008200E00074C5B7101A82E00800000000E040C9C12685C4010000000000000000100000000BF0ED4B6E4EE94E9E39E3867B378EF3", UID);
        }

        [Test]
        public void vCal_ID ()
        {
            var GOI = "BAAAAIIA4AB0xbcQGoLgCAAAAAAAAAAAAAAAAAAAAAAAAAAAMwAAAHZDYWwtVWlkAQAAAHs4MTQxMkQzQy0yQTI0LTRFOUQtQjIwRS0xMUY3QkJFOTI3OTl9AA==";
            var UID = Util.GlobalObjIdToUID (GOI);
            Assert.AreEqual ("81412D3C2A244E9DB20E11F7BBE92799", UID);
        }
    }
}

