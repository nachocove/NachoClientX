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
            MailboxAddress[] expected5 = new MailboxAddress[1] {
                new MailboxAddress ("Amazon Web Services, Inc.", "no-reply-aws@amazon.com"),
            };
            MailboxAddress[] expected6 = new MailboxAddress[1] {
                new MailboxAddress ("Jeff Enderwick", "jeffe@nachocove.com"),
            };
            MailboxAddress[] expected7 = new MailboxAddress[1] {
                new MailboxAddress ("Henry Kwok", "henryk@nachocove.com"),
            };
                
            MailboxAddress[] expected9 = new MailboxAddress[1] {
                new MailboxAddress("LuXe@", "Info@Luxe1539LuxuryApartments.com"),
            };

            // Single email address, no display name
            InternetAddressList got1 = NcEmailAddress.ParseAddressListString ("henryk@nachocove.com");
            Compare (expected1, got1);

            // Multiple email addresses, no display name
            InternetAddressList got2a = NcEmailAddress.ParseAddressListString ("henryk@nachocove.com,jeffe@nachocove.com");
            Compare (expected2, got2a);

            InternetAddressList got2b = NcEmailAddress.ParseAddressListString ("henryk@nachocove.com, jeffe@nachocove.com");
            Compare (expected2, got2b);

            // Multiple email addresses, with display name
            InternetAddressList got3a = NcEmailAddress.ParseAddressListString ("Henry Kwok <henryk@nachocove.com>,Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3a);

            InternetAddressList got3b = NcEmailAddress.ParseAddressListString ("Henry Kwok <henryk@nachocove.com>, Jeff Enderwick <jeffe@nachocove.com>");
            Compare (expected3, got3b);

            InternetAddressList got4 = NcEmailAddress.ParseAddressListString ("\"Henry Kwok\" <henryk@nachocove.com>, \"Jeff Enderwick\" <jeffe@nachocove.com>, \"Steve Scalpone\" <steves@nachocove.com>");
            Compare (expected4, got4);

            // We think this address is illegal
            InternetAddressList got5 = NcEmailAddress.ParseAddressListString (@"""Amazon Web Services, Inc."" <no-reply-aws@amazon.com<mailto:no-reply-aws@amazon.com>>");
            Compare (expected5, got5);

            // We think this address is illegal
            InternetAddressList got6 = NcEmailAddress.ParseAddressListString (@"""Jeff Enderwick"" <jeffe@nachocove.com<mailto:jeffe@nachocove.com>>");
            Compare (expected6, got6);

            InternetAddressList got7 = NcEmailAddress.ParseAddressListString ("\"Henry Kwok\" <henryk@nachocove.com>");
            Compare (expected7, got7);

            InternetAddressList got8 = NcEmailAddress.ParseAddressListString ("\"LuXe@\" <1539 Info@Luxe1539LuxuryApartments.com>");
            Assert.AreEqual (0, got8.Count);

            InternetAddressList got9 = NcEmailAddress.ParseAddressListString ("\"LuXe@\" <Info@Luxe1539LuxuryApartments.com>");
            Compare (expected9, got9);
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

        private void Match (MailboxAddress[] expected, List<NcEmailAddress> got)
        {
        }

        [Test]
        public void CcListTest ()
        {
            string a = "\"Henry Kwok\" <henryk@nachocove.com>, \"Jeff Enderwick\" <jeffe@nachocove.com>, Steve Scalpone <steves@nachocove.com>";
            string b = "\"Kwok, Henry\" <henryk@nachocove.com>, \"Enderwick, Jeff\" <jeffe@nachocove.com>, \"Scalpone, Steve\" <steves@nachocove.com>";
            MailboxAddress[] expected1 = new MailboxAddress[] {
                new MailboxAddress ("", "foo@bar.com"),
            };
            MailboxAddress[] expected2 = new MailboxAddress[] {
                new MailboxAddress ("", "foo@bar.com"),
                new MailboxAddress ("", "bar@foo.com"),
            };
            MailboxAddress[] expected3 = new MailboxAddress[] {
                new MailboxAddress ("", "bar@foo.com"),
                new MailboxAddress ("", "bob@smith.com"),

            };
            MailboxAddress[] expected4 = new MailboxAddress[] {
                new MailboxAddress ("", "henryk@nachocove.com"),
                new MailboxAddress ("", "jeffe@nachocove.com"),
                new MailboxAddress ("", "steves@nachocove.com"),
            };
            MailboxAddress[] expected5 = new MailboxAddress[] {
                new MailboxAddress ("", "henryk@nachocove.com"),
                new MailboxAddress ("", "jeffe@nachocove.com"),
            };

            var l = EmailHelper.CcList (null, null, null);
            l = EmailHelper.CcList (null, "foo@bar.com", null);
            Match (expected1, l);
            l = EmailHelper.CcList (null, null, "foo@bar.com");
            Match (expected1, l);
            l = EmailHelper.CcList (null, "foo@bar.com", "bar@foo.com");
            Match (expected2, l);
            l = EmailHelper.CcList ("foo@bar.com", null, null);
            Assert.AreEqual (0, l.Count);
            l = EmailHelper.CcList ("foo@bar.com", "foo@bar.com", null);
            Assert.AreEqual (0, l.Count);
            l = EmailHelper.CcList ("foo@bar.com", null, "foo@bar.com");
            Assert.AreEqual (0, l.Count);
            l = EmailHelper.CcList ("foo@bar.com", "bar@foo.com", "bob@smith.com");
            Match (expected3, l);
            l = EmailHelper.CcList (null, a, null);
            Match (expected4, l);
            l = EmailHelper.CcList (null, b, null);
            Match (expected4, l);
            l = EmailHelper.CcList (null, null, a);
            Match (expected4, l);
            l = EmailHelper.CcList (null, null, b);
            Match (expected4, l);
            l = EmailHelper.CcList (null, a, b);
            Assert.AreEqual (6, l.Count);
            l = EmailHelper.CcList (null, b, a);
            Assert.AreEqual (6, l.Count);
            l = EmailHelper.CcList ("steves@nachocove.com", a, null);
            Match (expected5, l);
            l = EmailHelper.CcList ("steves@nachocove.com", b, null);
            Match (expected5, l);
            l = EmailHelper.CcList ("steves@nachocove.com", null, a);
            Match (expected5, l);
            l = EmailHelper.CcList ("steves@nachocove.com", null, b);
            Match (expected5, l);
            l = EmailHelper.CcList ("steves@nachocove.com", a, b);
            Assert.AreEqual (4, l.Count);
            l = EmailHelper.CcList ("steves@nachocove.com", b, a);
            Assert.AreEqual (4, l.Count);
            l = EmailHelper.CcList ("foo@bar.com", a, null);
            Match (expected4, l);
            l = EmailHelper.CcList ("foo@bar.com", b, null);
            Match (expected4, l);
            l = EmailHelper.CcList ("foo@bar.com", null, a);
            Match (expected4, l);
            l = EmailHelper.CcList ("foo@bar.com", null, b);
            Match (expected4, l);
            l = EmailHelper.CcList ("foo@bar.com", a, b);
            Assert.AreEqual (6, l.Count);
            l = EmailHelper.CcList ("foo@bar.com", b, a);
            Assert.AreEqual (6, l.Count);
        }
    }
}
