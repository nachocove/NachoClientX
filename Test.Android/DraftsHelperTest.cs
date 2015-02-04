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



        [Test]
        public void TestWasReferencedMessageDeleted ()
        {
            McEmailMessage draftMessage = new McEmailMessage ();
            draftMessage.AccountId = 2;
            draftMessage.Insert ();
            try {
                DraftsHelper.WasReferencedMessageDeleted(draftMessage);
            } catch (Exception) {
                Assert.IsTrue (0 == draftMessage.ReferencedEmailId);
            }

            McEmailMessage referencedMessage = new McEmailMessage ();
            referencedMessage.AccountId = 2;
            referencedMessage.Insert ();

            draftMessage.ReferencedEmailId = referencedMessage.Id;
            draftMessage.Update ();
            Assert.IsFalse (DraftsHelper.WasReferencedMessageDeleted (draftMessage));

            referencedMessage.Delete ();
            Assert.IsTrue (DraftsHelper.WasReferencedMessageDeleted (draftMessage));
        }

        [Test]
        public void TestGetReferencedMessage ()
        {
            McEmailMessage draftMessage = new McEmailMessage ();
            draftMessage.AccountId = 2;
            draftMessage.Insert ();
            Assert.IsNull (DraftsHelper.GetReferencedMessage (draftMessage));

            McEmailMessage referencedMessage = new McEmailMessage ();
            referencedMessage.AccountId = 2;
            referencedMessage.Insert ();

            draftMessage.ReferencedEmailId = referencedMessage.Id;
            Assert.IsTrue (draftMessage.ReferencedEmailId == DraftsHelper.GetReferencedMessage (draftMessage).Id);
        }

        [Test]
        public void TestIsOriginalMessageEmbedded ()
        {
            McEmailMessage draftMessage = null;
            Assert.IsFalse (DraftsHelper.IsOriginalMessageEmbedded (draftMessage));

            draftMessage = new McEmailMessage ();
            Assert.IsFalse (DraftsHelper.IsOriginalMessageEmbedded (draftMessage));

            draftMessage.ReferencedBodyIsIncluded = true;
            Assert.IsTrue (DraftsHelper.IsOriginalMessageEmbedded (draftMessage));
        }
    }
}

