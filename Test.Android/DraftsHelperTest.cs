//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using NachoCore;

namespace Test.Android
{
    public class DraftsHelperTest
    {
        [Test]
        public void TestGetEmailDrafts ()
        {
            int accountId = 1;
            McEmailMessage emailOne = new McEmailMessage ();
            McEmailMessage emailTwo = new McEmailMessage ();
            McEmailMessage emailThree = new McEmailMessage ();
            emailOne.AccountId = accountId;
            emailTwo.AccountId = accountId;
            emailThree.AccountId = accountId;
            emailOne.Insert ();
            emailTwo.Insert ();
            emailThree.Insert ();

            McFolder draftsFolder = McFolder.GetDefaultDraftsFolder (accountId);
            Assert.IsTrue (0 == DraftsHelper.GetEmailDrafts (accountId).Count);

            draftsFolder.Link (emailOne);
            draftsFolder.Link (emailTwo);
            draftsFolder.Link (emailThree);

            Assert.IsTrue (3 == DraftsHelper.GetEmailDrafts (accountId).Count);

        }
    }
}

