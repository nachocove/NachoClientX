//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class HashHelperTest 
    {
        public static string[] emailAddresses = new string[] {"johnq@nachocove.com", "Peter@smithsonian.museum", "philip@us.gov", "ramesh@timesofindia.in", "tester@d2.officeburrito.com"};
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
