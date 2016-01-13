//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NUnit.Framework;
using NachoCore.Utils;
using System.Text.RegularExpressions;
using System;

namespace Test.iOS
{
    [TestFixture]
    public class HashHelperTest
    {
        public static string[] emailAddresses = new string[] { 
            "david.jones@proseware.com",
            "d.j@server1.proseware.com",
            "jones@ms1.proseware.com",
            "j@proseware.com9",
            "js#internal@proseware.com",
            "j_9@129.126.118.1",
            "j_9@[129.126.118.1]",
            "js@proseware.com9",
            "j.s@server1.proseware.com",
            "\"j\\\"s\\\"\"@proseware.com",
            "jēn@späm.de",
            "Jan-Vee@comcast.net",
        };

        private void dumpMatch (Match m)
        {
            if (m.Success) {
                Console.WriteLine("Match: {0}", m.Value, RegexOptions.IgnoreCase);
                for (int grpCtr = 1; grpCtr < m.Groups.Count; grpCtr++) {
                    Group grp = m.Groups[grpCtr];
                    Console.WriteLine("Group {0}: {1}", grpCtr, grp.Value);
                    for (int capCtr = 0; capCtr < grp.Captures.Count; capCtr++)
                        Console.WriteLine("   Capture {0}: {1}", capCtr,
                            grp.Captures[capCtr].Value);
                }
            } else {
                Console.WriteLine ("Did not match");
            }
        }
        [Test]
        public void TestEmailAddressHash ()
        {
            foreach (string emailAddress in emailAddresses) {
                Match match = Regex.Match (emailAddress, HashHelper.EmailRegex);
                //Console.WriteLine ("Email: {0} match {1}", emailAddress, match.Success);
                Assert.AreEqual (true, match.Success, emailAddress);
                //dumpMatch (match);
                Assert.AreEqual (emailAddress, string.Format ("{0}@{1}", match.Groups ["username"].Value, match.Groups ["domain"].Value));
            }
        }

        public static string[] userNames = new string[] { 
            "david.jones@proseware.com",
            "d.j@server1.proseware.com",
            "jones@ms1.proseware.com",
            "j@proseware.com9",
            "js#internal@proseware.com",
            "j_9@129.126.118.1",
            "j_9@[129.126.118.1]",
            "js@proseware.com9",
            "j.s@server1.proseware.com",
            "\"j\\\"s\\\"\"@proseware.com",
            "jēn@späm.de",
            "Jan-Vee@comcast.net",
            @"D2\jan",
        };

        [Test]
        public void TestEmailAddressInUrlHash ()
        {
            var urlTemplate = "https://mail.d2.officeburrito.com/Microsoft-Server-ActiveSync?Cmd=ItemOperations&User={0}&DeviceId=Nchob8f6b1150c41&DeviceType=iPhone";
            foreach (string emailAddress in userNames) {
                string plainUrl = string.Format (urlTemplate, emailAddress);
                string expectedUrl = string.Format (urlTemplate, "REDACTED");
                string hashedUrl = HashHelper.HashUserInASUrl (plainUrl);
                Assert.AreEqual (expectedUrl, hashedUrl, "Hashed Email does not match.");
            }

            var errorMsg = "Illegal character in query at index 78: https://mail.d2.officeburrito.com/Microsoft-Server-ActiveSync?Cmd=Sync&User=D2\\janv&DeviceId=Ncho42afd002ba3b&DeviceType=Android";
            var hashedErrorMsg = HashHelper.HashUserInASUrl (errorMsg);
            Assert.AreNotEqual (errorMsg, hashedErrorMsg);
        }

        [Test]
        public void TestEmailAddressInImapIdHash ()
        {
            var ImapIdStringTempls = new string[] {
                "[NAME, Zimbra], [VERSION, 8.0.7_GA_6031], [RELEASE, 20140624152426], [USER, {0}], [SERVER, 1cd5ed18-e1c6-4a06-8255-66cd41c1f431]",
                "[NAME, Zimbra], [VERSION, 8.0.7_GA_6031], [RELEASE, 20140624152426], [USER, {0}], [USER1, {0}], [SERVER, 1cd5ed18-e1c6-4a06-8255-66cd41c1f431]",
            };
            foreach (var template in ImapIdStringTempls) {
                foreach (string emailAddress in emailAddresses) {
                    string plainId = string.Format (template, emailAddress);
                    string expectedId = string.Format (template, "REDACTED");
                    string hashedId = HashHelper.HashEmailAddressesInImapId (plainId);
                    Assert.AreEqual (expectedId, hashedId, "Hashed Id does not match.");
                }
            }
        }
    }
}
