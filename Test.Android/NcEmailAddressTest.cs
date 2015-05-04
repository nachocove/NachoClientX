// Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using Test.iOS;
using NachoCore.Model;
using NUnit.Framework;
using NachoCore.Utils;


namespace Test.Common
{
    public class NcEmailAddressTest
    {
        string[][] names = { 
            new string [] { "J.", null, "J.", null, null, null },
            new string [] { "Mr. Jones", "Mr", "Jones", null,  null, null }, // we don't want this one, do we?
            new string [] { "John (The Guy) Smith (via Google Docs)", null, "John", null, "Smith", null },
            new string [] {
                "John (The Guy) Larry (Cable) Smith (via Google Docs)",
                null,
                "John",
                "Larry",
                "Smith",
                null
            },
            new string [] { "John Smith (via Google Docs)", null, "John", null, "Smith", null },
            new string [] {
                "Mr O'Malley y Muñoz, C. Björn Roger III",
                "Mr.",
                "Björn",
                "C. Roger",
                "O'Malley y Muñoz",
                "III"
            },
            new string [] {
                "O'Malley y Muñoz, C. Björn Roger III",
                null,
                "Björn",
                "C. Roger",
                "O'Malley y Muñoz",
                "III"
            },
            new string [] {
                "Mr O'Malley y Muñoz, C. Björn Roger",
                "Mr.",
                "Björn",
                "C. Roger",
                "O'Malley y Muñoz",
                null
            },
            new string [] { "O'Malley y Muñoz, C. Björn Roger", null, "Björn", "C. Roger", "O'Malley y Muñoz", null },
            new string [] { "y O Mathews", null, "Y", "O", "Mathews", null },
            new string [] { "O'Malley, Björn", null, "Björn", null, "O'Malley", null },
            new string [] { "Björn O'Malley", null, "Björn", null, "O'Malley", null },
            new string [] { "Bin Lin", null, "Bin", null, "Lin", null },
            new string [] { "Linda", null, "Linda", null, null, null },
            new string [] { "Linda Jones", null, "Linda", null, "Jones", null },
            new string [] { "Jason H. Priem", null, "Jason", "H.", "Priem", null },
            new string [] { "Björn O'Malley-Muñoz", null, "Björn", null, "O'Malley-Muñoz", null },
            new string [] { "Björn C. O'Malley", null, "Björn", "C.", "O'Malley", null },
            new string [] { "Björn \"Bill\" O'Malley", null, "Björn", "\"Bill\"", "O'Malley", null },
            new string [] { "Björn (\"Bill\") O'Malley", null, "Björn", null, "O'Malley", null },
            new string [] { "Björn \"Wild Bill\" O'Malley", null, "Björn", "\"Wild Bill\"", "O'Malley", null },
            new string [] { "Björn (Bill) O'Malley", null, "Björn", null, "O'Malley", null },
            new string [] { "Björn 'Bill' O'Malley", null, "Björn", "'Bill'", "O'Malley", null },
            new string [] { "Björn C O'Malley", null, "Björn", "C", "O'Malley", null },
            new string [] { "Björn C. R. O'Malley", null, "Björn", "C. R.", "O'Malley", null },
            new string [] { "Björn Charles O'Malley", null, "Björn", "Charles", "O'Malley", null },
            new string [] { "Björn Charles R. O'Malley", null, "Björn", "Charles R.", "O'Malley", null },
            new string [] { "Björn van O'Malley", null, "Björn", null, "Van O'Malley", null },
            new string [] { "Björn Charles van der O'Malley", null, "Björn", "Charles", "Van Der O'Malley", null },
            new string [] { "Björn Charles O'Malley y Muñoz", null, "Björn", "Charles", "O'Malley y Muñoz", null },
            new string [] { "Björn O'Malley, Jr.", null, "Björn", null, "O'Malley", "Jr" },
            new string [] { "Björn O'Malley Jr", null, "Björn", null, "O'Malley", "Jr" },
            new string [] { "B O'Malley", null, "B", null, "O'Malley", null },
            new string [] { "William Carlos Williams", null, "William", "Carlos", "Williams", null },
            new string [] { "C. Björn Roger O'Malley", null, "Björn", "C. Roger", "O'Malley", null },
            new string [] { "B. C. O'Malley", null, "B.", "C.", "O'Malley", null },
            new string [] { "B C O'Malley", null, "B", "C", "O'Malley", null },
            new string [] { "B.J. Thomas", null, "B.J.", null, "Thomas", null },
            new string [] { "O'Malley, Björn Jr", null, "Björn", null, "O'Malley", "Jr" },
            new string [] { "O'Malley, C. Björn", null, "Björn", "C.", "O'Malley", null },
            new string [] { "O'Malley, C. Björn III", null, "Björn",  "C.", "O'Malley", "III" },
            new string [] { "Bin Lin (ECMA)", null, "Bin", null, "Lin", null },
            new string [] { "Mr Bin Lin", "Mr", "Bin", null, "Lin", null },
            new string [] {
                "Francisco José de Goya y Lucientes",
                null,
                "Francisco",
                "José",
                "De Goya y Lucientes",
                null
            },
        };

        public NcEmailAddressTest ()
        {
        }

        protected void CheckContact (McContact contact, string firstName, string middleName, string lastName, string suffix)
        {
            Assert.AreEqual (firstName, contact.FirstName);
            Assert.AreEqual (lastName, contact.LastName);
            Assert.AreEqual (middleName, contact.MiddleName);
            Assert.AreEqual (suffix, contact.Suffix);
        }

        [Test]
        public void NameParseTest ()
        {
            for (int i = 0; i < names.Length; i++) {
                var mailboxAddress = new MimeKit.MailboxAddress (names [i] [0], "");
                McContact contact = new McContact ();
                NcEmailAddress.ParseName (mailboxAddress, ref contact);                
                CheckContact (contact, names [i] [2], names [i] [3], names [i] [4], names [i] [5]);
            }
        }
    }
}
