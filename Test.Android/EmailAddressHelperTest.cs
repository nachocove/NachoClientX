//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Net.Mail;
using NUnit.Framework;
using NachoCore.Utils;

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
            MailAddress[] expected1 = new MailAddress[1] {
                new MailAddress ("henryk@nachocove.com", "")
            };
            MailAddress[] expected2 = new MailAddress[2] {
                new MailAddress ("henryk@nachocove.com", ""),
                new MailAddress ("jeffe@nachocove.com", "")
            };
            MailAddress[] expected3 = new MailAddress[2] {
                new MailAddress ("henryk@nachocove.com", "Henry Kwok"),
                new MailAddress ("jeffe@nachocove.com", "Jeff Enderwick")
            };
            MailAddress[] expected4 = new MailAddress[3] {
                new MailAddress ("henryk@nachocove.com", "Henry Kwok"),
                new MailAddress ("jeffe@nachocove.com", "Jeff Enderwick"),
                new MailAddress ("steves@nachocove.com", "Steve Scalpone"),
            };

            List<MailAddress> got1 = EmailAddressHelper.ParseString ("henryk@nachocove.com");
            Compare (expected1, got1);

            List<MailAddress> got2a = EmailAddressHelper.ParseString ("henryk@nachocove.com,jeffe@nachocove.com");
            Compare (expected2, got2a);

            List<MailAddress> got2b = EmailAddressHelper.ParseString ("henryk@nachocove.com,jeffe@nachocove.com");
            Compare (expected2, got2b);

            List<MailAddress> got3a = EmailAddressHelper.ParseString ("Henry Kwok <henryk@nachocove.com>,Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3a);

            List<MailAddress> got3b = EmailAddressHelper.ParseString ("Henry Kwok <henryk@nachocove.com>, Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3b);

            List<MailAddress> got4 = EmailAddressHelper.ParseString ("\"Henry Kwok\" <henryk@nachocove.com>, \"Jeff Enderwick\" <jeffe@nachocove.com>, Steve Scalpone <steves@nachocove.com>");
            Compare (expected4, got4);
        }

        private void Compare (MailAddress[] expected, List<MailAddress> got)
        {
            Assert.AreEqual (expected.Length, got.Count);
            for (int n = 0; n < expected.Length; n++) {
                Assert.AreEqual (expected [n].Address, got [n].Address);
                Assert.AreEqual (expected [n].DisplayName, got [n].DisplayName);
            }
        }
    }
}

