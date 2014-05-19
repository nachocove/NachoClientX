//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore;
using MimeKit;

namespace Test.Android
{
    public class EmailAddressHelperTest
    {
        public EmailAddressHelperTest ()
        {
        }

        [Test]
        public void TestParseString ()
        {
            MailboxAddress[] expected1 = new MailboxAddress[1] {
                new MailboxAddress ("", "henryk@nachocove.com")
            };
            MailboxAddress[] expected2 = new MailboxAddress[2] {
                new MailboxAddress ("", "henryk@nachocove.com"),
                new MailboxAddress ("", "jeffe@nachocove.com")
            };
            MailboxAddress[] expected3 = new MailboxAddress[2] {
                new MailboxAddress ("Henry Kwok", "henryk@nachocove.com"),
                new MailboxAddress ("Jeff Enderwick", "jeffe@nachocove.com")
            };
            MailboxAddress[] expected4 = new MailboxAddress[3] {
                new MailboxAddress ("Henry Kwok", "henryk@nachocove.com"),
                new MailboxAddress ("Jeff Enderwick", "jeffe@nachocove.com"),
                new MailboxAddress ("Steve Scalpone", "steves@nachocove.com"),
            };

            // Single email address, no display name
            InternetAddressList got1 = NcEmailAddress.ParseString ("henryk@nachocove.com");
            Compare (expected1, got1);

            // Multiple email addresses, no display name
            InternetAddressList got2a = NcEmailAddress.ParseString ("henryk@nachocove.com,jeffe@nachocove.com");
            Compare (expected2, got2a);

            InternetAddressList got2b = NcEmailAddress.ParseString ("henryk@nachocove.com, jeffe@nachocove.com");
            Compare (expected2, got2b);

            // Multiple email addresses, with display name
            InternetAddressList got3a = NcEmailAddress.ParseString ("Henry Kwok <henryk@nachocove.com>,Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3a);

            InternetAddressList got3b = NcEmailAddress.ParseString ("Henry Kwok <henryk@nachocove.com>, Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3b);

            InternetAddressList got4 = NcEmailAddress.ParseString ("\"Henry Kwok\" <henryk@nachocove.com>, \"Jeff Enderwick\" <jeffe@nachocove.com>, Steve Scalpone <steves@nachocove.com>");
            Compare (expected4, got4);

            // We think this address is illegal
            InternetAddressList got5 = NcEmailAddress.ParseString(@"""Amazon Web Services, Inc."" <no-reply-aws@amazon.com<mailto:no-reply-aws@amazon.com>>");
            Assert.AreEqual (0, got5.Count);

            // We this this address is illegal
            InternetAddressList got6 = NcEmailAddress.ParseString(@"""Jeff Enderwick"" <jeffe@nachocove.com<mailto:jeffe@nachocove.com>>");
            Assert.AreEqual (0, got6.Count);
        }

        private void Compare (MailboxAddress[] expected, InternetAddressList got)
        {
            Assert.AreEqual (expected.Length, got.Count);
            for (int n = 0; n < expected.Length; n++) {
                var gotMailboxAddress = got [n] as MailboxAddress;
                Assert.NotNull (gotMailboxAddress);
                Assert.AreEqual (expected [n].Address, gotMailboxAddress.Address);
                Assert.AreEqual (expected [n].Name, gotMailboxAddress.Name);
            }
        }
    }
}
