//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using MimeKit;
using NUnit.Framework;
using NachoCore.Index;
using NachoCore.Brain;

namespace Test.Common
{
    public class IndexTest
    {
        const string IndexPath = "test.index";

        const string EmailPath = "email";

        NcIndex Index;

        private void CleanupIndexDirectory ()
        {
            if (!Directory.Exists (IndexPath)) {
                return;
            }
            Directory.Delete (IndexPath, true);
        }

        private void CleanupEmailFile ()
        {
            if (!File.Exists (EmailPath)) {
                return;
            }
            File.Delete (EmailPath);
        }

        [SetUp]
        public void SetUp ()
        {
            CleanupIndexDirectory (); // make sure there is no index to begin with
            CleanupEmailFile ();
            Index = new NcIndex (IndexPath);
        }

        [TearDown]
        public void TearDown ()
        {
            CleanupIndexDirectory (); // clean up anything we create
            CleanupEmailFile ();
            Index.Dispose ();
        }

        public IndexTest ()
        {
        }

        private MimeMessage GenerateTextMessage (string subject, string body,
                                                 InternetAddressList to = null,
                                                 InternetAddressList from = null,
                                                 InternetAddressList cc = null)
        {
            var message = new MimeMessage ();
            if (null != subject) {
                message.Subject = subject;
            }
            if (null != body) {
                message.Body = new TextPart ("plain") {
                    Text = body
                };
            }
            if (null != to) {
                foreach (var addr in to) {
                    message.To.Add (addr);
                }
            }
            if (null != from) {
                foreach (var addr in from) {
                    message.From.Add (addr);
                }
            }
            if (null != cc) {
                foreach (var addr in cc) {
                    message.Cc.Add (addr);
                }
            }
            return message;
        }

        private MimeMessage [] GenerateTextMessages (string [] subjects, string [] bodies,
                                                    InternetAddressList [] tos = null,
                                                    InternetAddressList [] froms = null,
                                                    InternetAddressList [] ccs = null)
        {
            int length = 0;
            // Find the length
            if (null != subjects) {
                length = subjects.Length;
            }
            if (null != bodies) {
                if (0 == length) {
                    length = bodies.Length;
                } else if (length != bodies.Length) {
                    throw new ArgumentException ("bodies length is different from others");
                }
            }
            if (null != tos) {
                if (0 == length) {
                    length = tos.Length;
                } else if (length != tos.Length) {
                    throw new ArgumentException ("tos length is different from others");
                }
            }
            if (null != froms) {
                if (0 == length) {
                    length = froms.Length;
                } else if (length != froms.Length) {
                    throw new ArgumentException ("froms length is different from others");
                }
            }
            if (null != ccs) {
                if (0 == length) {
                    length = ccs.Length;
                } else if (length != ccs.Length) {
                    throw new ArgumentException ("ccs length is different from others");
                }
            }

            var messages = new MimeMessage [length];

            for (int n = 0; n < length; n++) {
                string subject = null;
                if (null != subjects) {
                    subject = subjects [n];
                }
                string body = null;
                if (null != bodies) {
                    body = bodies [n];
                }
                InternetAddressList to = null;
                if (null != tos) {
                    to = tos [n];
                }
                InternetAddressList from = null;
                if (null != froms) {
                    from = froms [n];
                }
                InternetAddressList cc = null;
                if (null != ccs) {
                    cc = ccs [n];
                }

                messages [n] = GenerateTextMessage (subject, body, to, from, cc);
            }

            return messages;
        }

        private void AddEmail (string id, MimeMessage message, bool useBatch = false)
        {
            using (var stream = new FileStream (EmailPath, FileMode.CreateNew, FileAccess.ReadWrite)) {
                message.WriteTo (stream);
            }
            long bytesIndexed;
            var tokenizer = new NcMimeTokenizer (message, null);
            var parameters = new EmailMessageIndexParameters () {
                From = message.From,
                To = message.To,
                Cc = message.Cc,
                Bcc = message.Bcc,
                Subject = message.Subject,
                Content = tokenizer.Content,
                ReceivedDate = message.Date.DateTime,
            };
            var indexDoc = new EmailMessageIndexDocument (id, parameters);
            if (useBatch) {
                bytesIndexed = Index.BatchAdd (indexDoc);
            } else {
                bytesIndexed = Index.Add (indexDoc);
            }
            Assert.True (0 < bytesIndexed);
            File.Delete (EmailPath);
        }

        private void TestEmailIdSearchAllEmails (int count, bool doesExist)
        {
            for (int n = 0; n < count; n++) {
                var id = n.ToString ();
                var matches = Index.Search ("id:" + id);
                Assert.NotNull (matches);
                if (doesExist) {
                    Assert.AreEqual (1, matches.Count);
                    Assert.AreEqual ("message", matches [0].Type);
                    Assert.AreEqual (id, matches [0].Id);
                } else {
                    Assert.AreEqual (0, matches.Count);
                }
            }
        }

        [Test]
        public void TestEmailId ()
        {
            // We do a thorough testing of all Index API for Id test. And we focus on the 
            // search test for other field tests.
            string [] subjects = new string [3] {
                "subject 1",
                "subject 2",
                "subject 3",
            };
            string [] bodies = new string [3] {
                "body 1",
                "body 2",
                "body 3",
            };
            var emails = GenerateTextMessages (subjects, bodies);

            // Add all emails
            for (int n = 0; n < emails.Length; n++) {
                AddEmail (n.ToString (), emails [n], false);
            }

            // Search each email
            TestEmailIdSearchAllEmails (emails.Length, true);

            // Remove each email
            for (int n = 0; n < emails.Length; n++) {
                var id = n.ToString ();
                var removed = Index.Remove ("message", id);
                Assert.True (removed);

                // Search by Id to make sure it is removed
                var matches = Index.Search ("id:" + id);
                Assert.NotNull (matches);
                Assert.AreEqual (0, matches.Count);
            }

            // Now use batch add
            Index.BeginAddTransaction ();
            for (int n = 0; n < emails.Length; n++) {
                AddEmail (n.ToString (), emails [n], true);
                File.Delete (EmailPath);
            }
            Index.EndAddTransaction ();

            // Verify that all emails are in the index
            TestEmailIdSearchAllEmails (emails.Length, true);

            // Test batch remove
            Index.BeginRemoveTransaction ();
            for (int n = 0; n < emails.Length; n++) {
                var id = n.ToString ();
                var removed = Index.BatchRemove ("message", id);
                Assert.True (removed);
            }
            Index.EndRemoveTransaction ();

            // Verify that all emails are gone
            TestEmailIdSearchAllEmails (emails.Length, false);
        }

        private void VerifyMatches (string type, List<MatchedItem> matches, int [] expectedIds)
        {
            Assert.AreEqual (expectedIds.Length, matches.Count);

            SortedSet<int> ids = new SortedSet<int> ();
            foreach (var match in matches) {
                Assert.AreEqual (type, match.Type);
                ids.Add (Convert.ToInt32 (match.Id));
            }

            int n = 0;
            foreach (var id in ids) {
                Assert.AreEqual (expectedIds [n], id);
                n += 1;
            }
        }

        private void VerifyEmailMatches (List<MatchedItem> matches, int [] expectedIds)
        {
            VerifyMatches ("message", matches, expectedIds);
        }

        private void VerifyContacts (List<MatchedItem> matches, int [] expectedIds)
        {
            VerifyMatches ("contact", matches, expectedIds);
        }

        [Test]
        public void TestEmailBody ()
        {
            string [] bodies = new string [8] {
                "words are words",
                "sentences have words",
                "nonsense have words",
                "words aren't necessarily nonsense",
                "Preseason will start soon",
                "preparing for examines",
                // Corner case to test insertion is ok
                null,
                ""
            };

            // Add emails to the index
            var emails = GenerateTextMessages (null, bodies);
            for (int n = 0; n < emails.Length; n++) {
                AddEmail (n.ToString (), emails [n]);
            }

            // Search for "words". Should return 4 matches
            List<MatchedItem> matches;
            matches = Index.SearchType ("message", "words");
            VerifyEmailMatches (matches, new int [4] { 0, 1, 2, 3 });

            // Search for "nonsense". Should return 2 matches
            matches = Index.Search ("body:nonsense");
            VerifyEmailMatches (matches, new int [2] { 2, 3 });

            // Search for "sentences". Should return 1 match
            matches = Index.Search ("body:sentences");
            VerifyEmailMatches (matches, new int [1] { 1 });

            // Search for "pre*". Should return 2 matches
            matches = Index.Search ("body:pre*");
            VerifyEmailMatches (matches, new int [2] { 4, 5 });

            // Search for "sentences" and "necessarily". Should return 2 matches
            matches = Index.Search ("body:sentences OR body:necessarily");
            VerifyEmailMatches (matches, new int [2] { 1, 3 });
        }

        [Test]
        public void TestEmailSubject ()
        {
            string [] subjects = new string [6] {
                "important - please read",
                "not important - please ignore",
                "daily rambling",
                "random status",
                // Corner case to test insertion is ok
                null,
                ""
            };

            // Add emails to the index
            var emails = GenerateTextMessages (subjects, null);
            for (int n = 0; n < emails.Length; n++) {
                AddEmail (n.ToString (), emails [n]);
            }
            TestEmailIdSearchAllEmails (emails.Length, true);

            // Search "important" 
            List<MatchedItem> matches;
            matches = Index.Search ("subject:important");
            VerifyEmailMatches (matches, new int [2] { 0, 1 });

            // Search "daily"
            matches = Index.Search ("subject:daily");
            VerifyEmailMatches (matches, new int [1] { 2 });

            // Search "status"
            matches = Index.Search ("subject:status");
            VerifyEmailMatches (matches, new int [1] { 3 });

            // Search "ra*"
            matches = Index.Search ("subject:ra*");
            VerifyEmailMatches (matches, new int [2] { 2, 3 });
        }

        [Test]
        public void TestEmailAddressAndDomainFields ()
        {
            string [] subjects = new string [3] {
                "email #1",
                "email #2",
                "email #3",
            };
            string [] [] names = new string [] [] {
                new string[2] {
                    "Bob Villa",
                    "John Johnson",
                },
                new string[1] {
                    "Bob Villa",
                },
                new string[2] {
                    "Bob Johnson",
                    "Dave Davidson",
                },
            };
            string [] [] addresses = new string [] [] {
                new string[2] {
                    "bvilla@company.net",
                    "jjohnson@startup.net"
                },
                new string[1] {
                    "bvilla@company.net",
                },
                new string[] {
                    "bjohnson@company.net",
                    "ddavidson@startup.net",
                },
            };

            InternetAddressList [] tos = new InternetAddressList [3];
            for (int i = 0; i < subjects.Length; i++) {
                tos [i] = new InternetAddressList ();
                for (int j = 0; j < names [i].Length; j++) {
                    tos [i].Add (new MailboxAddress (names [i] [j], addresses [i] [j]));
                }
            }

            // Add emails to the index
            var emails = GenerateTextMessages (subjects, null, tos);
            for (int n = 0; n < emails.Length; n++) {
                AddEmail (n.ToString (), emails [n]);
            }
            TestEmailIdSearchAllEmails (emails.Length, true);

            // Search "bob"
            List<MatchedItem> matches;
            matches = Index.Search ("to:bob");
            VerifyEmailMatches (matches, new int [3] { 0, 1, 2 });

            // Search "bvilla@company.net"
            matches = Index.Search ("to:bvilla@company.net");
            VerifyEmailMatches (matches, new int [2] { 0, 1 });

            // Search "ddavidson@startup.net"
            matches = Index.Search ("to:ddavidson@startup.net");
            VerifyEmailMatches (matches, new int [1] { 2 });
        }

        [Test]
        public void TestContact ()
        {
            string [] first_name = new string [] {
                "Bob",
                "Fred",
                "JoAnn",
                "John",
            };
            string [] middle_name = new string [] {
                null,
                "John",
                "Mary",
                null,
            };
            string [] last_name = new string [] {
                "Smith",
                "Johnson",
                "Fabrics",
                "Brown",
            };
            string [] [] email_addresses = new string [] [] {
                new string[] {
                    "bob@company.net",
                    "bsmithm@home.net",
                },
                new string[] {
                    "fred@company.net",
                },
                new string[] {
                    "support@joannfrabrics.com",
                },
                new string[] {
                },
            };
            string [] [] phone_numbers = new string [] [] {
                new string[] {
                    "1-408-555-1111",
                    "1(650)555-2222",
                },
                new string[] {
                    "408-555-1234",
                },
                new string[] {
                    "510-555-4321",
                },
                new string[] {
                },
            };
            string [] [] addresses = new string [] [] {
                new string[] {
                    "123 Main St., Fremont, CA 94538",
                },
                new string[] {
                    "1 Woodfield Blvd, Rockford, IL 61820",
                    "171 Cunningham Rd, Winnebago, IL 61801",
                },
                new string[] {
                },
                new string[] {
                    "100 Seaside Ave., San Carlos, CA 94070",
                },
            };
            string [] notes = new string [] {
                "This entry is a note",
                "This entry is an experiment",
                null,
                "測試",
            };

            // Generate all 4 contact parameters objects
            var contactParams = new ContactIndexParameters [first_name.Length];
            for (int n = 0; n < first_name.Length; n++) {
                contactParams [n] = new ContactIndexParameters ();
                contactParams [n].FirstName = first_name [n];
                contactParams [n].MiddleName = middle_name [n];
                contactParams [n].LastName = last_name [n];
                contactParams [n].Note = notes [n];
                for (int m = 0; m < email_addresses [n].Length; m++) {
                    var addr = email_addresses [n] [m];
                    contactParams [n].EmailAddresses.Add (addr);
                    var idx = addr.IndexOf ("@");
                    contactParams [n].EmailDomains.Add (addr.Substring (idx + 1));
                }
                for (int m = 0; m < phone_numbers [n].Length; m++) {
                    contactParams [n].PhoneNumbers.Add (phone_numbers [n] [m]);
                }
                for (int m = 0; m < addresses [n].Length; m++) {
                    contactParams [n].Addresses.Add (addresses [n] [m]);
                }
            }

            var ok = Index.BeginAddTransaction ();
            Assert.True (ok);

            for (int n = 0; n < contactParams.Length; n++) {
                Index.BatchAdd (new ContactIndexDocument (n.ToString (), contactParams [n]));
            }

            Index.EndAddTransaction ();

            // Search each by id
            List<MatchedItem> matches;
            for (int n = 0; n < contactParams.Length; n++) {
                var id = n.ToString ();
                matches = Index.Search ("id:" + id);
                Assert.AreEqual (1, matches.Count);
                Assert.AreEqual ("contact", matches [0].Type);
                Assert.AreEqual (id, matches [0].Id);
            }

            // Search by first name
            matches = Index.Search ("first_name:fred");
            VerifyContacts (matches, new int [] { 1 });
            matches = Index.Search ("first_name:Jo*");
            VerifyContacts (matches, new int [] { 2, 3 });

            // Search by middle name
            matches = Index.Search ("middle_name:Mary");
            VerifyContacts (matches, new int [] { 2 });
            matches = Index.Search ("middle_name:M*");
            VerifyContacts (matches, new int [] { 2 });

            // Search by last name
            matches = Index.Search ("last_name:Brown");
            VerifyContacts (matches, new int [] { 3 });
            matches = Index.Search ("last_name:J*");
            VerifyContacts (matches, new int [] { 1 });

            // Search by email address
            matches = Index.Search ("email_address:support@joannfrabrics.com");
            VerifyContacts (matches, new int [] { 2 });
            matches = Index.Search ("email_address:b*");
            VerifyContacts (matches, new int [] { 0 });

            // Search by email domain
            matches = Index.Search ("email_domain:company.net");
            VerifyContacts (matches, new int [] { 0, 1 });
            matches = Index.Search ("email_domain:joann*");
            VerifyContacts (matches, new int [] { 2 });

            // Search by phone #
            matches = Index.Search ("phone_number:510-555-4321");
            VerifyContacts (matches, new int [] { 2 });
            matches = Index.Search ("phone_number:408*");
            VerifyContacts (matches, new int [] { 1 });

            // Search by address
            matches = Index.Search ("address:cunningham");
            VerifyContacts (matches, new int [] { 1 });
            matches = Index.Search ("address:CA");
            VerifyContacts (matches, new int [] { 0, 3 });

            // Search by notes
            matches = Index.Search ("note:note");
            VerifyContacts (matches, new int [] { 0 });
            matches = Index.Search ("note:experiment");
            VerifyContacts (matches, new int [] { 1 });
            matches = Index.Search ("note:entry");
            VerifyContacts (matches, new int [] { 0, 1 });
            matches = Index.Search ("note:測試");
            VerifyContacts (matches, new int [] { 3 });
            matches = Index.Search ("note:happy");
            Assert.AreEqual (0, matches.Count);
        }

        [Test]
        public void TestMarkForDeletion ()
        {
            bool succeeded;
            succeeded = Index.BeginAddTransaction ();
            Assert.True (succeeded);
            Index.EndAddTransaction ();

            succeeded = Index.BeginRemoveTransaction ();
            Assert.True (succeeded);
            Index.EndRemoveTransaction ();

            succeeded = Index.MarkForDeletion ();
            Assert.True (succeeded);

            succeeded = Index.BeginAddTransaction ();
            Assert.False (succeeded);

            succeeded = Index.BeginRemoveTransaction ();
            Assert.False (succeeded);
        }
    }
}
