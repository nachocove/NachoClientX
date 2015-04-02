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
        public static string[] exemptTables = new string[]  { 
            "McAccount", "sqlite_sequence", "McMigration",
        };

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
            McAccount account = CreateAccount();
            NachoCore.NcApplication.Instance.Account = account;

            // create email
            var email = FolderOps.CreateUniqueItem<McEmailMessage> (accountId: defaultAccountId, serverId: defaultServerId);
            Assert.NotNull (email);

            // create attachment
            var att = FolderOps.CreateAttachment (item: email, displayName: "My-Attachment");
            Assert.NotNull (att);

            // create cred
            var cred = new McCred () {
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
            List<McAttachment> foundAtts = McAttachment.QueryByItemId(foundEmail);
            Assert.IsTrue (foundAtts.Count > 0);
            string AccountDirPath = NcModel.Instance.GetAccountDirPath (account.Id);
            Assert.True(Directory.Exists(AccountDirPath));

            // check password in keychain
            if (Keychain.Instance.HasKeychain ()) {
                string retPassword = Keychain.Instance.GetPassword (cred.Id);
                Assert.AreEqual (retPassword, Password);
            }

            // remove account
            NcAccountHandler.Instance.RemoveAccount (stopStartServices: false);

            // assert not founds
            foundAccount = McAccount.QueryById<McAccount> (defaultAccountId);
            Assert.Null (foundAccount);
            foundEmail = McEmailMessage.QueryByServerId<McEmailMessage> (defaultAccountId, defaultServerId);
            Assert.Null (foundEmail);
            foundAtts = McAttachment.QueryByItemId(email);
            Assert.IsTrue (foundAtts.Count == 0);
            Assert.False(Directory.Exists(AccountDirPath));

            // confirm that the password is deleted from the keychain
            if (Keychain.Instance.HasKeychain ()) {
                string retPassword = Keychain.Instance.GetPassword (cred.Id);
                Assert.Null (retPassword);
            }
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
                if (!((IList<string>) exemptTables).Contains (Table.name)) {
                    Assert.IsTrue (foundAccountId, Table.name + " table is missing the column AccountId");
                }
            }
        }
    }
}
