//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using TypeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode;
using ClassCode = NachoCore.Model.McAbstrFolderEntry.ClassCodeEnum;
using NachoAssertionFailure = NachoCore.Utils.NcAssert.NachoAssertionFailure;
using Test.iOS;

namespace Test.Common
{
    public class McContactTest : NcTestBase
    {

        [Test]
        public void Update_01 ()
        {
            var c = new McContact ();
            c.AccountId = 1;
            c.AddEmailAddressAttribute (c.AccountId, "bob", "home", "bob@foo.com");
            c.Insert ();
            var d = McContact.QueryById<McContact> (c.Id);
            // Don't assert anything about 'd' because we don't want to read it in
            d.Update ();
            var e = McContact.QueryById<McContact> (d.Id);
            Assert.AreEqual (1, e.EmailAddresses.Count);
        }

        public void CreateFolder ()
        {


        }

        [Test]
        public void Query_01 ()
        {
            // Same name, parent id; different typecodes
            int accountId = 1;
            string parentId = "0";
            string name = "Name";

            TypeCode typeCode2 = TypeCode.DefaultContacts_9;

            FolderOps.CreateFolder (accountId, typeCode: typeCode2, parentId: parentId, name: name);

            McFolder expected2 = McFolder.GetUserFolders (accountId, typeCode2, parentId.ToInt (), name).First ();

            var c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.FirstName = "Bob";
            c.LastName = "Smith";
            c.AddEmailAddressAttribute (c.AccountId, "bob", "home", "bob@foo.com");
            c.AddPhoneNumberAttribute (c.AccountId, "mobile", null, "123");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.LastName = "Adleman";
            c.AddEmailAddressAttribute (c.AccountId, "aaron", "home", "aaron@foo.com");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.FirstName = "Charlie";
            c.LastName = "Clark";
            c.AddEmailAddressAttribute (c.AccountId, "Charlie", "home", "charlie@foo.com");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.FirstName = "David";
            c.LastName = "Dark";
            c.AddPhoneNumberAttribute (c.AccountId, "mobile", null, "123");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.Internal;
            c.AddEmailAddressAttribute (c.AccountId, "Eddie", "home", "eddie@foo.com");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = accountId;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.AddPhoneNumberAttribute (c.AccountId, "mobile", null, "1234567890");
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = 1;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.FirstName = "Gary";
            c.LastName = "Glitter";
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = 1;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.FirstName = "Ingrid";
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = 1;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.LastName = "Holmes";
            c.Insert ();
            expected2.Link (c);

            c = new McContact ();
            c.AccountId = 1;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.Insert ();
            expected2.Link (c);

            var l = McContact.AllContactsSortedByName ();
            var m = McContact.AllContactsSortedByName (1);
            Assert.AreEqual (l.Count, m.Count);
            Assert.AreEqual (10, l.Count);

            for (int i = 0; i < l.Count; i++) {
                Assert.AreEqual (l [i].Id, m [i].Id);
            }

            Assert.AreEqual (String.Empty, l [0].FirstLetter);
            Assert.AreEqual (String.Empty, l [1].FirstLetter);
            Assert.AreEqual ("Adleman", l [2].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Bob Smith", l [3].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Charlie Clark", l [4].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("David Dark", l [5].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("eddie@foo.com", l [6].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Gary Glitter", l [7].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Holmes", l [8].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Ingrid", l [9].GetContact ().GetDisplayNameOrEmailAddress ());

            var e = McContact.AllContactsWithEmailAddresses ();
            Assert.AreEqual (4, e.Count);
            Assert.AreEqual ("Adleman", e [0].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Bob Smith", e [1].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("Charlie Clark", e [2].GetContact ().GetDisplayNameOrEmailAddress ());
            Assert.AreEqual ("eddie@foo.com", e [3].GetContact ().GetDisplayNameOrEmailAddress ());

            var contacts = McContact.QueryGleanedContactsByEmailAddress (1, "eddie@foo.com");
            Assert.AreEqual (1, contacts.Count);
            Assert.AreEqual ("eddie@foo.com", contacts [0].GetDisplayNameOrEmailAddress ());

            contacts = McContact.QueryGleanedContactsByEmailAddress (1, "charlie@foo.com");
            Assert.AreEqual (0, contacts.Count);
        }

        private void CheckEmailAddressEclisped (McContact contact)
        {
            Assert.True (contact.EmailAddressesEclipsed);
        }

        private void CheckEmailAddressNotEclipsed (McContact contact)
        {
            Assert.False (contact.EmailAddressesEclipsed);
        }

        private void CheckPhoneNumberEclipsed (McContact contact)
        {
            Assert.True (contact.PhoneNumbersEclipsed);
        }

        private void CheckPhoneNumberNotEclipsed (McContact contact)
        {
            Assert.False (contact.PhoneNumbersEclipsed);
        }

        // Check that there is only one returned index and it matches the expected one
        private void CheckIndexes (List<NcContactIndex> indexList, int id, int id2)
        {
            Assert.AreEqual (2, indexList.Count);
            if (id == indexList [0].Id) {
                Assert.AreEqual (id2, indexList [1].Id);
            } else {
                Assert.AreEqual (id2, indexList [0].Id);
                Assert.AreEqual (id, indexList [1].Id);
            }
        }

        // Call various query API with eclipsing and make sure they return the epxected id
        private void CheckSearch (int accountId, int id, int id2)
        {
            var indexList = McContact.AllContactsSortedByName (true);
            CheckIndexes (indexList, id, id2);

            indexList = McContact.AllContactsSortedByName (accountId, true);
            CheckIndexes (indexList, id, id2);
        }

        // Re-read a McContact from database.
        private McContact ReRead (McContact contact)
        {
            NcAssert.True (0 < contact.Id);
            var newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.NotNull (newContact);
            return newContact;
        }

        [Test]
        public void EmailAddressesEclipsing ()
        {
            int accountId = 2;
            string parentId = "0";
            string name = "name";
            string email = "bob@company.net";

            var syncFolder = FolderOps.CreateFolder (accountId, typeCode: TypeCode.DefaultContacts_9,
                                 parentId: parentId, name: name);
            var ricFolder = FolderOps.CreateFolder (accountId, typeCode: TypeCode.Ric_19,
                                parentId: parentId, name: name);

            // Create an email address
            var emailAddress = new McEmailAddress () {
                AccountId = accountId,
                CanonicalEmailAddress = email,
            };
            emailAddress.Insert ();

            // Create a gleaned contact
            var gleanedContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.Internal,
            };
            gleanedContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            gleanedContact.Insert ();

            CheckEmailAddressNotEclipsed (gleanedContact);

            // Create a 2nd gleaned contact. This contact has the same email but different
            // name. So, this gleaned contact will not be eclipsed.
            var gleanedContact2 = new McContact () {
                AccountId = accountId,
                Source = McAbstrItem.ItemSource.Internal,
            };
            gleanedContact2.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            gleanedContact2.Insert ();

            CheckEmailAddressNotEclipsed (gleanedContact);
            CheckEmailAddressNotEclipsed (gleanedContact2);
            CheckSearch (accountId, gleanedContact.Id, gleanedContact2.Id);

            // Create a RIC contact. It should eclipse the gleaned contact
            var ricContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync,
                WeightedRank = 12345678
            };
            ricContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            ricContact.Insert ();
            ricFolder.Link (ricContact);

            gleanedContact = ReRead (gleanedContact);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressNotEclipsed (ricContact);
            CheckSearch (accountId, ricContact.Id, gleanedContact2.Id);

            // Create a sync contact. It should eclipse the GAL, RIC and gleaned contact.
            var syncContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync
            };
            syncContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            syncContact.Insert ();
            syncFolder.Link (syncContact);

            gleanedContact = ReRead (gleanedContact);
            ricContact = ReRead (ricContact);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id, gleanedContact2.Id);

            // Create a GAL contact. It should eclipse the RIC and gleaned contact
            var galContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync,
                GalCacheToken = "ace11f66-43ea-4fc6-be0b-10daf5bccf5f"
            };
            galContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            galContact.Insert ();
            syncFolder.Link (galContact);

            gleanedContact = ReRead (gleanedContact);
            ricContact = ReRead (ricContact);
            syncContact = ReRead (syncContact);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressEclisped (galContact);
            CheckEmailAddressNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id, gleanedContact2.Id);

            // Delete the sync contact. This should uneclipse the GAL contact
            syncContact.Delete ();

            galContact = ReRead (galContact);
            ricContact = ReRead (ricContact);
            gleanedContact = ReRead (gleanedContact);

            CheckEmailAddressNotEclipsed (galContact);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressEclisped (gleanedContact);
            CheckSearch (accountId, galContact.Id, gleanedContact2.Id);

            // Delete the RIC contact. This should change any eclipsing status
            ricContact.Delete ();

            galContact = ReRead (galContact);
            gleanedContact = ReRead (gleanedContact);

            CheckEmailAddressNotEclipsed (galContact);
            CheckEmailAddressEclisped (gleanedContact);
            CheckSearch (accountId, galContact.Id, gleanedContact2.Id);

            // Verify specialized update function
            gleanedContact.EmailAddressesEclipsed = false;
            gleanedContact.UpdateEmailAddressesEclipsing ();
            CheckEmailAddressNotEclipsed (gleanedContact);
           
            gleanedContact.EmailAddressesEclipsed = true;
            gleanedContact.UpdateEmailAddressesEclipsing ();
            CheckPhoneNumberEclipsed (gleanedContact);
        }

        [Test]
        public void PhoneNumberEclipsing ()
        {
            int accountId = 2;
            string parentId = "0";
            string name = "name";
            string phone = "1-408-555-1234";

            var syncFolder = FolderOps.CreateFolder (accountId, typeCode: TypeCode.DefaultContacts_9,
                                 parentId: parentId, name: name);
            var ricFolder = FolderOps.CreateFolder (accountId, typeCode: TypeCode.Ric_19,
                                parentId: parentId, name: name);

            // Glean contacts do not have phone.

            // Create a RIC contact
            var ricContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync,
                WeightedRank = 12345678
            };
            ricContact.AddPhoneNumberAttribute (accountId, "PhoneNumber", null, phone);
            ricContact.Insert ();
            ricFolder.Link (ricContact);

            // Create a 2nd RIC contact with different name
            var ricContact2 = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                MiddleName = "J.",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync,
                WeightedRank = 12345678
            };
            ricContact2.AddPhoneNumberAttribute (accountId, "PhoneNumber", null, phone);
            ricContact2.Insert ();
            ricFolder.Link (ricContact2);

            CheckPhoneNumberNotEclipsed (ricContact);
            CheckPhoneNumberNotEclipsed (ricContact2);
            CheckSearch (accountId, ricContact.Id, ricContact2.Id);

            // Create a sync contact. It should eclipse the GAL, RIC and gleaned contact.
            var syncContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync
            };
            syncContact.AddPhoneNumberAttribute (accountId, "PhoneNumber", null, phone);
            syncContact.Insert ();
            syncFolder.Link (syncContact);

            ricContact = ReRead (ricContact);

            CheckPhoneNumberEclipsed (ricContact);
            CheckPhoneNumberNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id, ricContact2.Id);

            // Create a GAL contact. It should eclipse the RIC and gleaned contact
            var galContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync,
                GalCacheToken = "ace11f66-43ea-4fc6-be0b-10daf5bccf5f"
            };
            galContact.AddPhoneNumberAttribute (accountId, "PhoneNumber", null, phone);
            galContact.Insert ();
            syncFolder.Link (galContact);

            ricContact = ReRead (ricContact);
            syncContact = ReRead (syncContact);

            CheckPhoneNumberEclipsed (ricContact);
            CheckPhoneNumberEclipsed (galContact);
            CheckPhoneNumberNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id, ricContact2.Id);

            // Delete the GAL contact. RIC contact should remain eclipsed
            galContact.Delete ();

            syncContact = ReRead (syncContact);
            ricContact = ReRead (ricContact);

            CheckPhoneNumberNotEclipsed (syncContact);
            CheckPhoneNumberEclipsed (ricContact);
            CheckSearch (accountId, syncContact.Id, ricContact2.Id);

            // Delete the sync contact. RIC contact should become uneclipsed
            syncContact.Delete ();

            ricContact = ReRead (ricContact);
            CheckPhoneNumberNotEclipsed (ricContact);
            CheckSearch (accountId, ricContact.Id, ricContact2.Id);

            // Verify speicalized update function
            ricContact.PhoneNumbersEclipsed = true;
            ricContact.UpdatePhoneNumbersEclipsing ();
            CheckPhoneNumberEclipsed (ricContact);

            ricContact.PhoneNumbersEclipsed = false;
            ricContact.UpdatePhoneNumbersEclipsing ();
            CheckPhoneNumberNotEclipsed (ricContact);
        }
    }
}

