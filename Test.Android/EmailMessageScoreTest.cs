//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Model;
using Test.iOS;

namespace Test.Common
{
    public class EmailMessageScoreTest : NcTestBase
    {
        string alan = "alan@company.net";
        string bob = "bob@company.net";
        string charles = "charles@company.net";
        string david = "david@company.net";
        string ellen = "ellen@company.net";

        [Test]
        public void TestAnalyzeFromAddress ()
        {
            int accountId = 8;

            ///// Email from Alan that is read /////
            var message1 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
            };
            InsertAndCheck (message1);
            message1.AnalyzeFromAddress ();

            // Verify address map is created
            var fromId = McMapEmailAddressEntry.QueryMessageFromAddressId (accountId, message1.Id);
            CheckAddressId (accountId, fromId, alan);

            // Verify the statistics
            McEmailAddress address;
            address = EmailAddress (fromId); // alan
            CheckAddressFromStatistics (address, 1, 1, 0);

            // Verify the score and version
            Assert.AreEqual (1.0, message1.Classify ().Item1);
            Assert.AreEqual (1, message1.ScoreVersion);

            ///// Email from Alan that is not read /////
            var message2 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                IsRead = false,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
                DateReceived = DateTime.UtcNow,
            };
            InsertAndCheck (message2);
            message2.AnalyzeFromAddress ();

            // Verify address map is created
            fromId = McMapEmailAddressEntry.QueryMessageFromAddressId (accountId, message2.Id);
            CheckAddressId (accountId, fromId, alan);

            // Verify the statistics
            address = EmailAddress (fromId); // alan
            CheckAddressFromStatistics (address, 2, 1, 0);

            // Verify the score and version
            Assert.AreEqual (1.0 / 2.0, message1.Classify ().Item1);
            Assert.AreEqual (1, message1.ScoreVersion);
        }

        [Test]
        public void TestAnalyzeReplyStatistics ()
        {
            int accountId = 9;

            ///// Email from Alan that is read /////
            var message1 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.REPLYTOALL,
                DateReceived = DateTime.UtcNow,
                ScoreVersion = 1,
            };
            InsertAndCheck (message1);
            McEmailAddress address;
            address = EmailAddress (message1.FromEmailAddressId);
            address.ScoreStates.EmailsReceived = 1;
            address.ScoreStates.EmailsRead = 1;
            address.ScoreStates.Update ();

            message1.AnalyzeReplyStatistics ();
            address = EmailAddress (message1.FromEmailAddressId);
            CheckAddressFromStatistics (address, 1, 0, 1);
        }

        protected void InsertAndCheck (McEmailMessage item)
        {
            // Fudge the update that is done in AsSyncEmailCommand.
            item.FromEmailAddressId = McEmailAddress.Get (item.AccountId, item.From);

            var rows = item.Insert ();
            Assert.True ((1 == rows) && (0 < item.Id));
        }

        protected void CheckGleanedContact (int accountId, string emailAddressString)
        {
            var contacts = McContact.QueryGleanedContactsByEmailAddress (accountId, emailAddressString);
            Assert.AreEqual (1, contacts.Count);
            Assert.AreEqual (McAbstrItem.ItemSource.Internal, contacts [0].Source);
            Assert.AreEqual (1, contacts [0].EmailAddresses.Count);
            Assert.AreEqual (emailAddressString, contacts [0].EmailAddresses [0].Value);
        }

        protected void CheckAddressFromStatistics (McEmailAddress address, int received, int read, int replied)
        {
            Assert.AreEqual (received, address.ScoreStates.EmailsReceived);
            Assert.AreEqual (read, address.ScoreStates.EmailsRead);
            Assert.AreEqual (replied, address.ScoreStates.EmailsReplied);
        }

        protected void CheckAddressToStatistics (McEmailAddress address, int received, int read, int replied)
        {
            Assert.AreEqual (received, address.ScoreStates.ToEmailsReceived);
            Assert.AreEqual (read, address.ScoreStates.ToEmailsRead);
            Assert.AreEqual (replied, address.ScoreStates.ToEmailsReplied);
        }

        protected void CheckAddressCcStatistics (McEmailAddress address, int received, int read, int replied)
        {
            Assert.AreEqual (received, address.ScoreStates.CcEmailsReceived);
            Assert.AreEqual (read, address.ScoreStates.CcEmailsRead);
            Assert.AreEqual (replied, address.ScoreStates.CcEmailsReplied);
        }

        protected void CheckAddressIds (int accountId, List<int> addressIds, params string[] emailAddressStrings)
        {
            Assert.AreEqual (emailAddressStrings.Length, addressIds.Count);
            for (int n = 0; n < emailAddressStrings.Length; n++) {
                var expectedId = McEmailAddress.Get (accountId, emailAddressStrings [n]);
                Assert.AreNotEqual (0, expectedId);
                Assert.AreEqual (expectedId, addressIds [n]);
            }
        }

        protected void CheckAddressId (int accountId, int addressId, string emailAddressString)
        {
            CheckAddressIds (accountId, new List<int> () { addressId }, emailAddressString);
        }

        protected McEmailAddress EmailAddress (int id)
        {
            return McEmailAddress.QueryById<McEmailAddress> (id);
        }

        [Test]
        public void TestAnalyzeOtherAddresses ()
        {
            int accountId = 10;

            // Create a glean folder
            McFolder expectedGleaned = FolderOps.CreateFolder (accountId, serverId: McFolder.ClientOwned_Gleaned, isClientOwned: true);
            Assert.NotNull (expectedGleaned); // for avoiding compilation warning

            // Insert one email that isn't read
            var message1 = new McEmailMessage () {
                AccountId = accountId,
                From = alan,
                To = String.Join (",", bob, charles),
                Cc = String.Join (",", david, ellen),
                IsRead = false,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,

            };
            InsertAndCheck (message1);
            message1.AnalyzeOtherAddresses ();

            // Not verifying the gleaned contact. That is done in NcBrain.AnalyzeEmailMessage().

            // Verify that address maps are created
            var fromId = McMapEmailAddressEntry.QueryMessageFromAddressId (accountId, message1.Id);
            CheckAddressId (accountId, fromId, alan);
            var toIds = McMapEmailAddressEntry.QueryMessageToAddressIds (accountId, message1.Id);
            CheckAddressIds (accountId, toIds, bob, charles);
            var ccIds = McMapEmailAddressEntry.QueryMessageCcAddressIds (accountId, message1.Id);
            CheckAddressIds (accountId, ccIds, david, ellen);

            // Verify the statistics
            McEmailAddress emailAddress;
            emailAddress = EmailAddress (fromId); // alan
            // From address is analyzed by McEmailMessage.AnalyzeFromAddress().
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (toIds [0]); // bob
            /////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 1, 0, 0);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (toIds [1]); // charles
            ////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 1, 0, 0);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (ccIds [0]); // david
            ////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 1, 0, 0);

            emailAddress = EmailAddress (ccIds [1]); // ellen
            ////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 1, 0, 0);

            // Insert one email that is read
            var message2 = new McEmailMessage () {
                AccountId = accountId,
                From = bob,
                To = alan,
                Cc = ellen,
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.UNKNOWN,
            };
            InsertAndCheck (message2);
            message2.AnalyzeOtherAddresses ();

            // Verify address maps
            fromId = McMapEmailAddressEntry.QueryMessageFromAddressId (accountId, message2.Id);
            CheckAddressId (accountId, fromId, bob);
            toIds = McMapEmailAddressEntry.QueryMessageToAddressIds (accountId, message2.Id);
            CheckAddressIds (accountId, toIds, alan);
            ccIds = McMapEmailAddressEntry.QueryMessageCcAddressIds (accountId, message2.Id);
            CheckAddressIds (accountId, ccIds, ellen);

            // Verify statistics
            emailAddress = EmailAddress (fromId); // bob
            ////CheckAddressFromStatistics (emailAddress, 1, 1, 0);
            CheckAddressToStatistics (emailAddress, 1, 0, 0);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (toIds [0]); // alan
            ////CheckAddressFromStatistics (emailAddress, 1, 0, 0);
            CheckAddressToStatistics (emailAddress, 1, 1, 0);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (ccIds [0]); // ellen
            ////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 2, 1, 0);

            // Re-read email #1 and verify the update count
            message1 = McEmailMessage.QueryById<McEmailMessage> (message1.Id);
            var needsUpdate1 = McEmailMessageNeedsUpdate.Get (message1);
            Assert.AreEqual (5, needsUpdate1);

            // Insert one email that is replied
            var message3 = new McEmailMessage () {
                AccountId = accountId,
                From = david,
                To = bob,
                Cc = String.Join (",", david, ellen),
                IsRead = true,
                LastVerbExecuted = (int)AsLastVerbExecutedType.REPLYTOSENDER,
            };
            InsertAndCheck (message3);
            message3.AnalyzeOtherAddresses ();

            // Verify address maps
            fromId = McMapEmailAddressEntry.QueryMessageFromAddressId (accountId, message3.Id);
            CheckAddressId (accountId, fromId, david);
            toIds = McMapEmailAddressEntry.QueryMessageToAddressIds (accountId, message3.Id);
            CheckAddressIds (accountId, toIds, bob);
            ccIds = McMapEmailAddressEntry.QueryMessageCcAddressIds (accountId, message3.Id);
            CheckAddressIds (accountId, ccIds, david, ellen);

            // Verify statistics
            emailAddress = EmailAddress (fromId); // david
            ////CheckAddressFromStatistics (emailAddress, 1, 0, 1);
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 2, 0, 1);

            emailAddress = EmailAddress (toIds [0]); // bob
            /////CheckAddressFromStatistics (emailAddress, 1, 1, 0);
            CheckAddressToStatistics (emailAddress, 2, 0, 1);
            CheckAddressCcStatistics (emailAddress, 0, 0, 0);

            emailAddress = EmailAddress (ccIds [1]); // ellen
            /////CheckAddressFromStatistics (emailAddress, 0, 0, 0);
            CheckAddressToStatistics (emailAddress, 0, 0, 0);
            CheckAddressCcStatistics (emailAddress, 3, 1, 1);

            // Re-read email #1 & #2 and verify the update counts
            message1 = McEmailMessage.QueryById<McEmailMessage> (message1.Id);
            needsUpdate1 = McEmailMessageNeedsUpdate.Get (message1);
            Assert.AreEqual (8, needsUpdate1);
            message2 = McEmailMessage.QueryById<McEmailMessage> (message2.Id);
            var needsUpdate2 = McEmailMessageNeedsUpdate.Get (message2);
            Assert.AreEqual (3, needsUpdate2);
        }
    }
}

