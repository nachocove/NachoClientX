//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;

namespace Test.iOS
{
    public class McCredTest
    {
        public McCredTest ()
        {
        }

        public McAccount Account { get; set; }

        [SetUp]
        public void SetUp ()
        {
            Account = new McAccount () {
                
            };
            Account.Insert ();
            NcModel.Instance.Db.DeleteAll<McCred> ();
        }

        [TearDown]
        public void TearDown ()
        {
            Account.Delete ();
            NcModel.Instance.Db.DeleteAll<McCred> ();
        }

        [Test]
        public void TestQueryByCredType ()
        {
            McCred c1 = new McCred () {
                AccountId = Account.Id,
                CredType = McCred.CredTypeEnum.OAuth2,
            };
            c1.Insert ();

            Assert.AreEqual (1, McCred.QueryByCredType (McCred.CredTypeEnum.OAuth2).Count);

            McCred c2 = new McCred () {
                AccountId = Account.Id,
                CredType = McCred.CredTypeEnum.Password,
            };
            c2.Insert ();
            Assert.AreEqual (1, McCred.QueryByCredType (McCred.CredTypeEnum.OAuth2).Count);
        }
    }
}

