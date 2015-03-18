//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class HashHelperTest 
    {
        public static string[] emailAddresses = new string[]  { 
            "david.jones@proseware.com", "d.j@server1.proseware.com",
            "jones@ms1.proseware.com",
            "j@proseware.com9", "js#internal@proseware.com",
            "j_9@[129.126.118.1]",
            "js@proseware.com9", "j.s@server1.proseware.com",
            "\"j\\\"s\\\"\"@proseware.com", "jēn@späm.de"};
        public static string urlPrefixFrag = "https://mail.d2.officeburrito.com/Microsoft-Server-ActiveSync?Cmd=ItemOperations&User=";
        public static string urlSuffixFrag = "&DeviceId=Nchob8f6b1150c41&DeviceType=iPhone";

        [Test]
        public void TestEmailAddressHash ()
        {
            foreach (string emailAddress in emailAddresses)
            {
                string plainUrl = urlPrefixFrag + emailAddress + urlSuffixFrag;
                string expectedUrl = urlPrefixFrag + HashHelper.Sha256 (emailAddress) + urlSuffixFrag;
                string hashedUrl = HashHelper.HashEmailAddressesInString(plainUrl);
                Assert.AreEqual (expectedUrl, hashedUrl, "Hashed Email does not match.");
            }
        }
    }
}
