﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Xml.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using SQLite;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using MimeKit;
using System.Reflection;
using System.Collections;

namespace Test.Common
{
    [TestFixture]
    public class NcEmailTest : NcTestBase
    {
        public class MockDataSource : IBEContext
        {
            public IProtoControlOwner Owner { set; get; }

            public AsProtoControl ProtoControl { set; get; }

            public McProtocolState ProtocolState { get; set; }

            public McServer Server { get; set; }

            public McAccount Account { get; set; }

            public McCred Cred { get; set; }

            public MockDataSource ()
            {
                Owner = new MockProtoControlOwner ();
                Account = new McAccount ();
                Account.Id = 1;
            }
        }

        public class MockProtoControlOwner : IProtoControlOwner
        {
            public string AttachmentsDir { set; get; }

            public void CredReq (ProtoControl sender)
            {
            }

            public void ServConfReq (ProtoControl sender)
            {
            }

            public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
            {
            }

            public void StatusInd (ProtoControl sender, NcResult status)
            {
            }

            public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
            {
            }

            public void SearchContactsResp (ProtoControl sender, string prefix, string token)
            {
            }
        }

        [Test]
        public void ParseMailTo()
        {
            List<NcEmailAddress> addresses;
            string subject;
            string body;

            string urlString;

            urlString = "mailto:someone@example.com";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (1, addresses.Count);
            Assert.IsTrue (String.IsNullOrEmpty (subject));
            Assert.IsTrue (String.IsNullOrEmpty (body));

            urlString = "mailto:someone@example.com?subject=This%20is%20the%20subject&cc=someone_else@example.com&body=This%20is%20the%20body";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (2, addresses.Count);
            Assert.AreEqual ("This is the subject", subject);
            Assert.AreEqual ("This is the body", body);

            urlString = "mailto:someone@example.com,someoneelse@example.com";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (2, addresses.Count);
            Assert.IsTrue (String.IsNullOrEmpty (subject));
            Assert.IsTrue (String.IsNullOrEmpty (body));

            urlString = "mailto:someone@example.com,someoneelse@example.com?";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (2, addresses.Count);
            Assert.IsTrue (String.IsNullOrEmpty (subject));
            Assert.IsTrue (String.IsNullOrEmpty (body));

            urlString = "mailto:someone@example.com,someoneelse@example.com?&&";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (2, addresses.Count);
            Assert.IsTrue (String.IsNullOrEmpty (subject));
            Assert.IsTrue (String.IsNullOrEmpty (body));

            urlString = "mailto:?to=&subject=mailto%20with%20examples&body=http://en.wikipedia.org/wiki/Mailto";
            Assert.IsTrue (EmailHelper.ParseMailTo (urlString, out addresses, out subject, out body));
            Assert.AreEqual (0, addresses.Count);
            Assert.AreEqual ("mailto with examples", subject);
            Assert.AreEqual ("http://en.wikipedia.org/wiki/Mailto", body);

        }

        //Creates a string that represents an XML email
        public string createXMLEmail (string serverID, List<McEmailMessageCategory> categories)
        {

            string categoriesString = "";
            if (null == categories) {
                categoriesString = @"           <Categories xmlns=""Email"" />";
            } else if (categories.Count () == 0) {
                categoriesString = @"           <Categories xmlns=""Email"" />";
            } else {
                categoriesString = @"           <Categories xmlns=""Email"" >";
                foreach (var mc in categories) {
                    categoriesString += "\n" + @"                <Category> " + mc.Name.Trim () + @"</Category>";
                }
                categoriesString += "\n" + @"            </Categories>" + "\n";
            }

            string XMLEmailTemplate = @"
           <Add xmlns = ""AirSync"">
          <ServerId> " + serverID + @"</ServerId>
          <ApplicationData>
            <To xmlns=""Email"">""Nacho Nerds"" &lt;nerds@nachocove.com&gt;</To>
            <From xmlns=""Email"">""Henry Kwok"" &lt;henryk@nachocove.com&gt;</From>
        <Subject xmlns=""Email"">Telemetry summary [2014-06-05T16:56:32.433Z]</Subject>
            <DateReceived xmlns=""Email"">2014-06-05T16:57:11.641Z</DateReceived>
            <DisplayTo xmlns=""Email"">Nacho Nerds</DisplayTo>
            <ThreadTopic xmlns=""Email"">Telemetry summary [2014-06-05T16:56:32.433Z]</ThreadTopic>
            <Read xmlns=""Email"">1</Read>
            <Attachments xmlns=""AirSyncBase"">
              <Attachment>
                <DisplayName>errors_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a0</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>11159</EstimatedDataSize>
              </Attachment>
              <Attachment>
                <DisplayName>warnings_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a1</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>79374</EstimatedDataSize>
              </Attachment>
            </Attachments>
            <Body xmlns=""AirSyncBase"">
              <Type>4</Type>
              <EstimatedDataSize>308849</EstimatedDataSize>
              <Data />
            </Body>
            <MessageClass xmlns=""Email"">IPM.Note</MessageClass>
            <InternetCPID xmlns=""Email"">20127</InternetCPID>
            <Flag xmlns=""Email"" />
            <ContentClass xmlns=""Email"">urn:content-classes:message</ContentClass>
            <NativeBodyType xmlns=""AirSyncBase"">2</NativeBodyType>
            <ConversationId xmlns=""Email2"">mu4nI+aBpEq/thAPoth/ZQ==</ConversationId>
            <ConversationIndex xmlns=""Email2"">Ac+A3zE=</ConversationIndex>" + "\n"
                                      + categoriesString +
                                      @"</ApplicationData>
            </Add>";

            //Console.WriteLine(XMLEmailTemplate);

            return XMLEmailTemplate;
        }

        [Test]
        public void InsertWithoutCategories ()
        {
            McEmailMessage testEmail = InsertEmailIntoDB ("5:4", null);
            Assert.True (testEmail.Categories.Count == 0);
        }

        [Test]
        public void InsertWithCategories ()
        {
            McEmailMessage testeEmail = InsertEmailIntoDB ("5:4", getCategories (1));
            Assert.True (testeEmail.Categories.Count > 0);
        }

        [Test]
        public void EmptyOptionalFields ()
        {
            var emptyOptionalsXML = System.Xml.Linq.XElement.Parse (EmptyOptionalsXML);
            McEmailMessage emptyOptionalsEmail = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (emptyOptionalsXML, new MockNcFolder ());

            Assert.IsNull (emptyOptionalsEmail.To);
            Assert.IsNull (emptyOptionalsEmail.From);
            Assert.IsNull (emptyOptionalsEmail.Cc);
            Assert.IsNull (emptyOptionalsEmail.DisplayTo);
            Assert.IsNull (emptyOptionalsEmail.InReplyTo);
            Assert.True (emptyOptionalsEmail.Categories.Count == 0);

        }

        [Test]
        public void SetOptionalFields ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var setOptionalsXML = System.Xml.Linq.XElement.Parse (CategoryTestXML);
            McEmailMessage setOptionalsEmail = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (setOptionalsXML, new MockNcFolder ());
            Assert.NotNull (setOptionalsEmail.To);
            Assert.NotNull (setOptionalsEmail.From);
            Assert.NotNull (setOptionalsEmail.DisplayTo);
            Assert.NotNull (setOptionalsEmail.Importance);
            Assert.NotNull (setOptionalsEmail.Categories);
        }

        [Test]
        public void SameRecordTwice ()
        {
            var firstXMLcopy = System.Xml.Linq.XElement.Parse (CategoryTestXML);
            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (firstXMLcopy, new MockNcFolder ());
            bool failedCopyInsert = false;

            try {
                email1.Insert ();
            } catch {
                failedCopyInsert = true;
            }
            Assert.True (failedCopyInsert);
        }

        [Test]
        public void DeleteRecordTwice ()
        {
            List<McEmailMessageCategory> categories = getCategories (1);
            List<McEmailMessage> email = new List<McEmailMessage> ();
            email.Add (InsertEmailIntoDB (getServerIDs (1) [0], categories));
            Assert.True (email [0].Delete () > 0);
            Assert.True (0 == email [0].Delete ());
        }

        [Test]
        public void UpdateAfterDelete ()
        {
            List<McEmailMessageCategory> categories = getCategories (1);
            List<McEmailMessage> email = new List<McEmailMessage> ();
            email.Add (InsertEmailIntoDB (getServerIDs (1) [0], categories));
            email [0].Delete ();
            Assert.Throws<NcAssert.NachoAssertionFailure> (() => email [0].Update ());
        }

        [Test]
        public void UpdateToIncludeCategories ()
        {
            var noCategoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", null));
            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (noCategoriesXML, new MockNcFolder ());

            Assert.True (email1.Categories.Count == 0);
            email1.Categories = getCategories (1);
            email1.Update ();
            Assert.True (email1.Categories.Count > 0);
        }

        [Test]
        public void UpdateWithSameCategories ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());
            List<McEmailMessageCategory> categories = email1.Categories;

            AssertListsAreEquals (categories, email1.Categories);
            email1.Categories = categories;
            email1.Update ();
            AssertListsAreEquals (categories, email1.Categories);
        }

        [Test]
        public void UpdateNonCategoryField ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());

            Assert.True (email1.Categories.Count == 6);
            Assert.True (email1.getInternalCategoriesList ().Count == 6);

            email1.AccountId = 1;
            email1.Update ();

            McEmailMessage email2 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE AccountId=?", 1) [0];
            email2.IsRead = true;
            email2.Update ();
            Assert.True (email2.Categories.Count == 6);

        }

        [Test]
        public void UpdateCategories ()
        {
            List<McEmailMessageCategory> catList = new List<McEmailMessageCategory> ();
            catList.Add (getCategories (1) [0]);
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", catList));

            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());

            Assert.True (email1.Categories.Count == 1 && email1.Categories [0].Name.Trim ().Equals (catList [0].Name.Trim ()));
            email1.Categories = getCategories (1);
            email1.Update ();
            Assert.True (email1.Categories.Count == 6);
        }

        [Test]
        public void UpdateRemovesCategories ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            McEmailMessage email1 = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());

            Assert.True (email1.Categories.Count == 6);
            email1.Categories.Clear ();
            email1.Update ();
            Assert.True (email1.Categories.Count == 0);
        }
        //Check this
        [Test]
        public void ReadRecordWithoutAncillary ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());
            McEmailMessage email2 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();
            Assert.True (email2.getInternalCategoriesList ().Count () == 0);
        }

        [Test]
        public void BackToBackUpdates ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());

            List<McEmailMessageCategory> oneItem = new List<McEmailMessageCategory> ();
            oneItem.Add (getCategories (1) [0]);

            McEmailMessage email2 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();

            Assert.True (email2.Categories.Count == 6);
            email2.Categories.Clear ();
            email2.Update ();
            Assert.True (email2.Categories.Count == 0);
            email2.Categories = oneItem;
            email2.Update ();
            Assert.True (email2.Categories.Count == 1);

            email2.To = "No Recip.";
            email2.Categories.Add (getCategories (1) [2]);
            email2.Update ();

            McEmailMessage email3 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();

            Assert.True (email3.To.Equals ("No Recip.") && email3.Categories.Count == 2);

            email3.To = "Mark";
            email3.From = "Zuckerburg";
            email3.Cc = "Jonah Hill";
            email3.Subject = "FaceBook";
            email3.Categories = getCategories (1);
            email3.Update ();

            McEmailMessage email4 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();

            Assert.True (
                email4.To.Equals ("Mark") &&
                email4.From.Equals ("Zuckerburg") &&
                email4.Cc.Equals ("Jonah Hill") &&
                email4.Subject.Equals ("FaceBook") &&
                email4.Categories.Count == 6);
        }

        [Test]
        public void ChangeNonAncillaryFieldUpdate ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());
            McEmailMessage email2 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();

            email2.Cc = "ChangedTheCC";
            List<McEmailMessageCategory> listPreUpdate = email2.Categories;
            email2.Update ();
            List<McEmailMessageCategory> listPostUpdate = email2.Categories;

            AssertListsAreEquals (listPreUpdate, listPostUpdate);
        }

        [Test]
        public void ChangeAncillaryFieldUpdate ()
        {
            var categoriesXML = System.Xml.Linq.XElement.Parse (createXMLEmail ("5:4", getCategories (1)));
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXML, new MockNcFolder ());
            McEmailMessage email2 = NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage WHERE Id = ?", 1).First ();

            Assert.True (email2.Categories [0].Name.Trim ().Equals ("Green"));
            email2.Categories [0].Name = "ChangedTheName";
            email2.Update ();
            Assert.True (email2.Categories [0].Name.Trim ().Equals ("ChangedTheName"));
        }

        private static void AssertListsAreEquals (IList actualList, IList expectedList)
        {
            if (actualList.Count != expectedList.Count)
                Assert.Fail ("Property {0}.{1} does not match. Expected IList containing {2} elements but was IList containing {3} elements", expectedList.Count, actualList.Count);

            for (int i = 0; i < actualList.Count; i++)
                if (!Equals (actualList [i], expectedList [i]))
                    Assert.Fail ("Property {0}.{1} does not match. Expected IList with element {1} equals to {2} but was IList with element {1} equals to {3}", expectedList [i], actualList [i]);
        }

        public McEmailMessage InsertEmailIntoDB (string serverID, List<McEmailMessageCategory> categories)
        {
            var categoriesXMLCommand = System.Xml.Linq.XElement.Parse (createXMLEmail (serverID, categories));
            McEmailMessage insertedEmail = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXMLCommand, new MockNcFolder ());
            Console.WriteLine ("TESTLOG: Inserted Email: ServerID: " + insertedEmail.ServerId.ToString () + " AccountID: " + insertedEmail.AccountId.ToString () + " ID: " + insertedEmail.Id.ToString () + "  ");
            return insertedEmail;
        }

        public List<McEmailMessageCategory> getCategories (int accountId)
        {
            List<McEmailMessageCategory> categories = new List<McEmailMessageCategory> ();
            string[] categoriesNames = { "Green", "Red", "Blue", "Important", "Boring", "Fun" };
            foreach (string s in categoriesNames) {
                categories.Add (new McEmailMessageCategory (accountId, s));
            }

            return categories;
        }

        public List<string> getServerIDs (int howMany)
        {
            List<string> serverIDs = new List<string> ();
            for (int i = 0; i < howMany; i++) {
                serverIDs.Add ("5:" + i.ToString ());
            }

            return serverIDs;
        }

        [Test]
        public void UpdateEmailSet ()
        {
            List<McEmailMessageCategory> categories = getCategories (1);
            List<McEmailMessage> email = new List<McEmailMessage> ();
            List<McEmailMessageCategory> updatedCategories = new List<McEmailMessageCategory> ();
            updatedCategories.Add (categories [0]);
            updatedCategories.Add (categories [1]);
            List<string> serverIds = getServerIDs (6);
            for (int i = 0; i < 6; i++) {
                email.Add (InsertEmailIntoDB (serverIds [i], categories));

                for (int j = 0; j < email [i].Categories.Count (); j++) {
                    string spaces = "".PadRight (10 - email [i].Categories [j].Name.Length);
                    Console.WriteLine ("DB: " + email [i].Categories [j].Name + spaces + " ORIGINAL: " + categories [j].Name);
                }
     
                email [i].Categories = updatedCategories;
                email [i].Update ();

                for (int j = 0; j < email [i].Categories.Count (); j++) {
                    string spaces = "".PadRight (10 - email [i].Categories [j].Name.Length);
                    Console.WriteLine ("DB: " + email [i].Categories [j].Name + spaces + " ORIGINAL: " + updatedCategories [j].Name);
                    string word1 = email [i].Categories [j].Name.Trim ();
                    string word2 = updatedCategories [j].Name.Trim ();
                    Assert.True (word1.CompareTo (word2) == 0);
                }
            }
        }

        [Test]
        public void DeleteAnEmailTest ()
        {
            List<McEmailMessageCategory> categories = getCategories (1);
            List<McEmailMessage> email = new List<McEmailMessage> ();
            email.Add (InsertEmailIntoDB (getServerIDs (1) [0], categories));
            email [0].Delete ();
        }

        [Test]
        public void EmailCategories ()
        {
            var c01 = new McEmailMessageCategory (1, "test");

            c01.ParentId = 5;
            c01.Insert ();

            var c02 = NcModel.Instance.Db.Get<McEmailMessageCategory> (x => x.ParentId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.ParentId, 5);
            Assert.AreEqual (c02.Name, "test");

            var c03 = NcModel.Instance.Db.Get<McEmailMessageCategory> (x => x.Name == "test");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.ParentId, 5);
            Assert.AreEqual (c03.Name, "test");

            c03.Name = "changed";
            c03.Update ();

            Assert.AreEqual (NcModel.Instance.Db.Table<McEmailMessageCategory> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => NcModel.Instance.Db.Get<McEmailMessageCategory> (x => x.Name == "test"));

            var c05 = NcModel.Instance.Db.Get<McEmailMessageCategory> (x => x.Name == "changed");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.ParentId, 5);
            Assert.AreEqual (c05.Name, "changed");

            var c06 = new McEmailMessageCategory (1, "second");
            c06.ParentId = 5;
            c06.Insert ();
            var c07 = new McEmailMessageCategory (1, "do not see");
            c07.ParentId = 6;
            c07.Insert ();

            Assert.AreEqual (3, NcModel.Instance.Db.Table<McEmailMessageCategory> ().Count ());

            var c10 = NcModel.Instance.Db.Table<McEmailMessageCategory> ().Where (x => x.ParentId == 5);
            Assert.AreEqual (2, c10.Count ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("changed") || c.Name.Equals ("second"));
            }
        }

        public void CreateMcBody (MockDataSource mds, int id)
        {
            var body = new McBody () {
                AccountId = mds.Account.Id,
            };
            body.Insert ();
            Assert.AreEqual (id, body.Id);
        }

        [Test]
        public void BodyTouch ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var body = McBody.QueryById<McBody> (1);
            Assert.IsNotNull (body);
            body.Touch ();
        }

        [Test]
        public void EmailCategoriesTest ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var categoriesXMLCommand = System.Xml.Linq.XElement.Parse (CategoryTestXML);
            Assert.IsNotNull (categoriesXMLCommand);
            Assert.AreEqual (categoriesXMLCommand.Name.LocalName, Xml.AirSync.Add);
            McEmailMessage helloCategory = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (categoriesXMLCommand, new MockNcFolder ());
            Assert.AreEqual (2, helloCategory.Categories.Count);

            var f = McEmailMessage.QueryById<McEmailMessage> (helloCategory.Id);
            Assert.AreEqual (2, f.Categories.Count);
        }

        [Test]
        public void EmailMeetingRequestTest ()
        {
            var eXML = System.Xml.Linq.XElement.Parse (EmailWithMeetingRequest);
            Assert.IsNotNull (eXML);
            Assert.AreEqual (eXML.Name.LocalName, Xml.AirSync.Add);
            McEmailMessage e = NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeEmail (eXML, new MockNcFolder ());

            var f = McEmailMessage.QueryById<McEmailMessage> (e.Id);
            Assert.IsNotNull (f.MeetingRequest);
        }

        [Test]
        public void TestQueryNeedsFetch ()
        {
            var bodyMissing_1 = new McBody () {
                AccountId = 1,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            bodyMissing_1.Insert ();
            var bodyMissing_2 = new McBody () {
                AccountId = 2,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            bodyMissing_2.Insert ();
            var bodyPartial_1 = new McBody () {
                AccountId = 1,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Partial,
            };
            bodyPartial_1.Insert ();
            var bodyComplete_1 = new McBody () {
                AccountId = 1,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete,
            };
            bodyComplete_1.Insert ();
            var bodyError_1 = new McBody () {
                AccountId = 1,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.Error,
            };
            bodyError_1.Insert ();
            var keeper1 = new McEmailMessage () {
                AccountId = 1,
                ServerId = "keeper1",
                IsAwaitingDelete = false,
                Score = 0.98,
                BodyId = bodyMissing_1.Id,
                DateReceived = DateTime.UtcNow.AddDays (-2),
            };
            keeper1.Insert ();
            var keeper3 = new McEmailMessage () {
                AccountId = 1,
                ServerId = "keeper2",
                IsAwaitingDelete = false,
                Score = 0.97,
                BodyId = bodyMissing_1.Id,
                DateReceived = DateTime.UtcNow.AddDays (-1),
            };
            keeper3.Insert ();
            var trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "mid_download",
                IsAwaitingDelete = false,
                Score = 0.98,
                BodyId = bodyPartial_1.Id,
                DateReceived = DateTime.UtcNow.AddDays (-3),
            };
            trash.Insert ();
            trash = new McEmailMessage () {
                AccountId = 2,
                ServerId = "other_account",
                IsAwaitingDelete = false,
                Score = 0.99,
                BodyId = bodyMissing_2.Id,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "is_deleted",
                IsAwaitingDelete = true,
                Score = 0.99,
                BodyId = bodyMissing_1.Id,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "low_score",
                IsAwaitingDelete = false,
                Score = 0.69,
                BodyId = bodyMissing_1.Id,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "downloaded",
                IsAwaitingDelete = false,
                Score = 0.99,
                BodyId = bodyComplete_1.Id,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "error",
                IsAwaitingDelete = false,
                Score = 0.99,
                BodyId = bodyError_1.Id,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            var result = McEmailMessage.QueryNeedsFetch (1, 2, 0.9);
            Assert.AreEqual (2, result.Count ());
            Assert.True (result.Any (x => "keeper1" == x.ServerId));
            Assert.True (result.Any (x => "keeper2" == x.ServerId));
            Assert.AreEqual ("keeper2", result.First ().ServerId);
        }

        public string CategoryTestXML = @"
           <Add xmlns = ""AirSync"">
          <ServerId>5:4</ServerId>
          <ApplicationData>
            <To xmlns=""Email"">""Nacho Nerds"" &lt;nerds@nachocove.com&gt;</To>
            <From xmlns=""Email"">""Henry Kwok"" &lt;henryk@nachocove.com&gt;</From>
        <Subject xmlns=""Email"">Telemetry summary [2014-06-05T16:56:32.433Z]</Subject>
            <DateReceived xmlns=""Email"">2014-06-05T16:57:11.641Z</DateReceived>
            <DisplayTo xmlns=""Email"">Nacho Nerds</DisplayTo>
            <ThreadTopic xmlns=""Email"">Telemetry summary [2014-06-05T16:56:32.433Z]</ThreadTopic>
            <Read xmlns=""Email"">1</Read>
            <Attachments xmlns=""AirSyncBase"">
              <Attachment>
                <DisplayName>errors_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a0</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>11159</EstimatedDataSize>
              </Attachment>
              <Attachment>
                <DisplayName>warnings_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a1</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>79374</EstimatedDataSize>
              </Attachment>
            </Attachments>
            <Body xmlns=""AirSyncBase"">
              <Type>4</Type>
              <EstimatedDataSize>308849</EstimatedDataSize>
              <Data nacho-body-id=""1"" />
            </Body>
            <MessageClass xmlns=""Email"">IPM.Note</MessageClass>
            <Importance xmlns=""Email"">1</Importance>
            <InternetCPID xmlns=""Email"">20127</InternetCPID>
            <Flag xmlns=""Email"" />
            <ContentClass xmlns=""Email"">urn:content-classes:message</ContentClass>
            <NativeBodyType xmlns=""AirSyncBase"">2</NativeBodyType>
            <ConversationId xmlns=""Email2"">mu4nI+aBpEq/thAPoth/ZQ==</ConversationId>
            <ConversationIndex xmlns=""Email2"">Ac+A3zE=</ConversationIndex>
            <Categories xmlns=""Email"">
              <Category>Green category</Category>
              <Category>Blue category</Category>
            </Categories>
          </ApplicationData>
            </Add>";
        public string EmptyOptionalsXML = @"
           <Add xmlns = ""AirSync"">
          <ServerId>5:4</ServerId>
          <ApplicationData>
            <ThreadTopic xmlns=""Email"">Telemetry summary [2014-06-05T16:56:32.433Z]</ThreadTopic>
            <Read xmlns=""Email"">1</Read>
            <Attachments xmlns=""AirSyncBase"">
              <Attachment>
                <DisplayName>errors_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a0</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>11159</EstimatedDataSize>
              </Attachment>
              <Attachment>
                <DisplayName>warnings_2014_06_05T16_56_32_433Z.txt.zip</DisplayName>
                <FileReference>5%3a4%3a1</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>79374</EstimatedDataSize>
              </Attachment>
            </Attachments>
            <Body xmlns=""AirSyncBase"">
              <Type>4</Type>
              <EstimatedDataSize>308849</EstimatedDataSize>
              <Data nacho-body-id=""1"" />
            </Body>
            <MessageClass xmlns=""Email"">IPM.Note</MessageClass>
            <InternetCPID xmlns=""Email"">20127</InternetCPID>
            <Flag xmlns=""Email"" />
            <ContentClass xmlns=""Email"">urn:content-classes:message</ContentClass>
            <ConversationId xmlns=""Email2"">mu4nI+aBpEq/thAPoth/ZQ==</ConversationId>
            <ConversationIndex xmlns=""Email2"">Ac+A3zE=</ConversationIndex>
            <Categories xmlns=""Email"" />
          </ApplicationData>
            </Add>";

        public string EmailWithMeetingRequest = @"
           <Add xmlns = ""AirSync"">
         <ServerId>10:87</ServerId>
          <ApplicationData>
            <To xmlns=""Email"">""Steve Scalpone"" &lt;steves@nachocove.com&gt;</To>
            <From xmlns=""Email"">""Jeff Enderwick"" &lt;jeffe@nachocove.com&gt;</From>
            <Subject xmlns=""Email"">Canceled Event: TEST my NEW sh*t @ Tue Aug 12, 2014 12:15pm - 1:15pm (Steve Scalpone)</Subject>
            <ReplyTo xmlns=""Email"">""Jeff Enderwick"" &lt;jeffe@nachocove.com&gt;</ReplyTo>
            <DateReceived xmlns=""Email"">2014-08-12T19:06:15.403Z</DateReceived>
            <DisplayTo xmlns=""Email"">Steve Scalpone</DisplayTo>
            <ThreadTopic xmlns=""Email"">Canceled Event: TEST my NEW sh*t @ Tue Aug 12, 2014 12:15pm - 1:15pm (Steve Scalpone)</ThreadTopic>
            <Importance xmlns=""Email"">1</Importance>
            <Read xmlns=""Email"">0</Read>
            <Attachments xmlns=""AirSyncBase"">
              <Attachment>
                <DisplayName>invite.ics</DisplayName>
                <FileReference>10%3a87%3a0</FileReference>
                <Method>1</Method>
                <EstimatedDataSize>1078</EstimatedDataSize>
              </Attachment>
            </Attachments>
            <Body xmlns=""AirSyncBase"">
              <Type>3</Type>
              <EstimatedDataSize>3612</EstimatedDataSize>
              <Truncated>1</Truncated>
            </Body>
            <MessageClass xmlns=""Email"">IPM.Schedule.Meeting.Canceled</MessageClass>
            <MeetingRequest xmlns=""Email"">
              <AllDayEvent>0</AllDayEvent>
              <StartTime>2014-08-12T19:15:00.000Z</StartTime>
              <DtStamp>2014-08-12T19:06:08.000Z</DtStamp>
              <EndTime>2014-08-12T20:15:00.000Z</EndTime>
              <InstanceType>0</InstanceType>
              <Location>steve's table</Location>
              <Organizer>""Jeff Enderwick"" &lt;jeffe@nachocove.com&gt;</Organizer>
              <Sensitivity>0</Sensitivity>
              <TimeZone>AAAAACgAVQBUAEMAKQAgAE0AbwBuAHIAbwB2AGkAYQAsACAAUgBlAHkAawBqAGEAdgBpAGsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACgAVQBUAEMAKQAgAE0AbwBuAHIAbwB2AGkAYQAsACAAUgBlAHkAawBqAGEAdgBpAGsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==</TimeZone>
              <GlobalObjId>BAAAAIIA4AB0xbcQGoLgCAAAAAAAAAAAAAAAAAAAAAAAAAAAMQAAAHZDYWwtVWlkAQAAADkyRDVFMDBCLUU0NEEtNEQ0Qi1CMEVBLUFGNTRENjA1OTE1QwA=</GlobalObjId>
              <MeetingMessageType xmlns=""Email2"">0</MeetingMessageType>
            </MeetingRequest>
            <InternetCPID xmlns=""Email"">20127</InternetCPID>
            <Flag xmlns=""Email"" />
            <ContentClass xmlns=""Email"">urn:content-classes:calendarmessage</ContentClass>
            <NativeBodyType xmlns=""AirSyncBase"">3</NativeBodyType>
            <ConversationId xmlns=""Email2"">VMZaN6rjgUuqxrpVpFDsMw==</ConversationId>
            <ConversationIndex xmlns=""Email2"">Ac+2YH0=</ConversationIndex>
            <Categories xmlns=""Email"" />
          </ApplicationData>
</Add>
            ";
    }
}


