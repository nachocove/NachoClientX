//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using NachoPlatform;
using System.IO;

namespace Test.iOS
{
    [TestFixture]
    public class RemoveAccountTest : ProtoOps
    {
        public const string defaultServerId = "5";
        const string Email = "bob@company.net";
        const string Password = "Plassword";

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            // Create Account
        }

        //        public void RemoveAccount ()
        [Test]
        public void TestRemoveAccount ()
        {
            // create account
            McAccount account = CreateAccount ();
            NachoCore.NcApplication.Instance.Account = account;

            // create email
            var email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: defaultServerId);
            Assert.NotNull (email);

            // create attachment
            var att = FolderOps.CreateAttachment (item: email, displayName: "My-Attachment");
            att.Link (email);
            Assert.NotNull (att);

            // create cred
            var cred = new McCred () {
                CredType = McCred.CredTypeEnum.Password,
                AccountId = account.Id,
                Username = Email,
            };
            cred.Insert ();
            cred.UpdatePassword (Password);

            // assert founds 
            McAccount foundAccount = McAccount.QueryById<McAccount> (defaultAccountId);
            Assert.NotNull (foundAccount);
            var foundEmail = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, defaultServerId);
            Assert.NotNull (foundEmail);
            List<McAttachment> foundAtts = McAttachment.QueryByItem (foundEmail);
            Assert.IsTrue (foundAtts.Count > 0);
            string AccountDirPath = NcModel.Instance.GetAccountDirPath (account.Id);
            Assert.True (Directory.Exists (AccountDirPath));

            // check password in keychain
            string retPassword = Keychain.Instance.GetPassword (cred.Id);
            Assert.AreEqual (retPassword, Password);

            // remove account
            NcAccountHandler.Instance.RemoveAccount (account.Id, stopStartServices: false);

            // assert not founds
            foundAccount = McAccount.QueryById<McAccount> (defaultAccountId);
            Assert.Null (foundAccount);
            foundEmail = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, defaultServerId);
            Assert.Null (foundEmail);
            foundAtts = McAttachment.QueryByItem (email);
            Assert.IsTrue (foundAtts.Count == 0);
            Assert.False (Directory.Exists (AccountDirPath));

            // confirm that the password is deleted from the keychain
            var gotEx = false;
            try {
                retPassword = Keychain.Instance.GetPassword (cred.Id);
                Assert.Null (retPassword);
            } catch (KeychainItemNotFoundException) {
                gotEx = true;
            }
            Assert.IsTrue (gotEx);
        }

        //Test that all tables have accountId
        [Test]
        public void TestAccountIdInAllTables ()
        {
            List<McSQLiteMaster> AllTables = McSQLiteMaster.QueryAllTables ();
            foreach (McSQLiteMaster Table in AllTables) {
                List<SQLite.SQLiteConnection.ColumnInfo> Columns = NcModel.Instance.Db.GetTableInfo (Table.name);
                bool foundAccountId = false;
                foreach (SQLite.SQLiteConnection.ColumnInfo Column in Columns) {
                    if (Column.Name == "AccountId") {
                        foundAccountId = true;
                        break;
                    }
                }
                if (!((IList<string>)NcModel.ExemptTables).Contains (Table.name)) {
                    Assert.IsTrue (foundAccountId, Table.name + " table is missing the column AccountId");
                }
            }
        }
    }
}
