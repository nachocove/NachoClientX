//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore.Imap;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NUnit.Framework;
using System.Text;

namespace Test.iOS
{
    public class ImapOperationTest
    {
        public ImapOperationTest ()
        {
        }

        [Test]
        public void TestMakeEmailMessage()
        {
            string TestSubject = "Foo12345";
            var TestFrom = new MailboxAddress("Test From", "testfrom@example.com");
            var TestTo = new MailboxAddress("Test To", "testto@example.com");
            var TestUniqueId = new UniqueId (1);
            int someIndex = 1;
            int AccountId = 244;
            MessageSummary summary = new MessageSummary (someIndex) {
                UniqueId = TestUniqueId,
                InternalDate = DateTimeOffset.UtcNow,
                Envelope = new Envelope (),
            };

            summary.Envelope.Subject = TestSubject;
            summary.Envelope.To.Add (TestTo);
            summary.Envelope.From.Add (TestFrom);

            McFolder folder = McFolder.Create (AccountId, false, false, true, "0", "someServerId", "MyFolder", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            NcImap imap = new NcImap (AccountId, "foo", 143, false, "someuser", "somepassword");
            var emailMessage = imap.ServerSaysAddOrChangeEmail(summary, folder);

            Assert.AreEqual (emailMessage.Subject, TestSubject);
            Assert.True (emailMessage.FromEmailAddressId > 0);
            Assert.AreEqual (emailMessage.From, TestFrom.Address);
            Assert.AreEqual (emailMessage.To, TestTo.Address);
        }
    }
}

