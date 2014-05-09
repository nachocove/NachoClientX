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

        string test_1 = @"
From:
To: ""\""Stephen Scalpone\"" <rascal2210@yahoo.com>""
    <""Stephen Scalpone"" <rascal2210@yahoo.com>
Date: Mon, 17 Mar 2014 22:18:57 +0000
Subject: Re: Test message #1
Message-ID: <635306663379746980.644.1@Stephens-iPhone>
Content-Type: text/plain; charset=""utf-8""
Content-Transfer-Encoding: quoted-printable
MIME-Version: 1.0

=0A=
> Who flung pooh?=0A=
> =0A=
> Red letters getting bigger and bigger and wider=EF=BF=BD=
";

        [Test]
        public void TestMimeParserBadAddress ()
        {
            MimeMessage mimeMsg;
            using (var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (test_1))) {
                var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                mimeMsg = bodyParser.ParseMessage ();
            }  
            var value1 = mimeMsg.To.ToString ();
            var value2 = mimeMsg.To[0].Name;
            var value3 = (mimeMsg.To[0] as MailboxAddress).Address;
        }
    }
}
