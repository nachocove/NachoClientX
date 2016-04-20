//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.Utils;

namespace Test.Common
{
    public class McEmailMessageScoreTest : NcTestBase
    {
        [Test]
        public void TestQueryByParentId ()
        {
            // Create a McEmailMessage object
            var emailMessage = new McEmailMessage () {
                AccountId = 3,
            };
            emailMessage.Insert ();
            Assert.True (0 < emailMessage.Id);

            // Query by parent id to make sure the method works
            var score = McEmailMessageScore.QueryByParentId (emailMessage.Id);
            Assert.NotNull (score);
            Assert.True (0 < score.Id);
            Assert.AreEqual (emailMessage.Id, score.ParentId);

            // Delete it by Id
            score.Delete ();

            // Make sure the query now fails.
            var score2 = McEmailMessageScore.QueryById<McEmailMessageScore> (score.Id);
            Assert.Null (score2);
        }

        [Test]
        public void TestDeleteByParentId ()
        {
            // Create a McEmailMessage object
            var emailMessage = new McEmailMessage () {
                AccountId = 3,
            };
            emailMessage.Insert ();
            Assert.True (0 < emailMessage.Id);

            // Query by parent id to make sure insertion succeeds
            var score = McEmailMessageScore.QueryByParentId (emailMessage.Id);
            Assert.NotNull (score);
            Assert.True (0 < score.Id);
            Assert.AreEqual (emailMessage.Id, score.ParentId);

            // Delete by parent id
            NcModel.Instance.RunInTransaction (() => {
                McEmailMessageScore.DeleteByParentId (emailMessage.Id);
            });

            // Make sure the query now fails
            var score2 = McEmailMessageScore.QueryById <McEmailMessageScore> (score.Id);
            Assert.Null (score2);
        }
    }
}

