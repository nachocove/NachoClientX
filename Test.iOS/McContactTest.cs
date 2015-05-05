//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using TypeCode = NachoCore.ProtoControl.FolderHierarchy.TypeCode;
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

            c = new McContact ();
            c.AccountId = 1;
            c.Source = McAbstrItem.ItemSource.ActiveSync;
            c.CompanyName = "Joe's Coffee";
            c.Insert ();
            expected2.Link (c);

            var l = McContact.AllContactsSortedByName ();
            var m = McContact.AllContactsSortedByName (1);
            Assert.AreEqual (l.Count, m.Count);
            Assert.AreEqual (11, l.Count);

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
            Assert.AreEqual ("Joe's Coffee", l [10].GetContact ().GetDisplayNameOrEmailAddress ());

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

        private void CheckIndex (List<NcContactIndex> indexList, int id)
        {
            Assert.AreEqual (1, indexList.Count);
            Assert.AreEqual (id, indexList [0].Id);
        }

        // Call various query API with eclipsing and make sure they return the epxected id
        private void CheckSearch (int accountId, int id, int id2 = 0)
        {
            var indexList = McContact.AllContactsSortedByName (true);
            if (0 < id2) {
                CheckIndexes (indexList, id, id2);
            } else {
                CheckIndex (indexList, id);
            }

            indexList = McContact.AllContactsSortedByName (accountId, true);
            if (0 < id2) {
                CheckIndexes (indexList, id, id2);
            } else {
                CheckIndex (indexList, id);
            }
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
            string email2 = "bob@home.net";

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
            // Eclipsed by gleanedContact.
            CheckEmailAddressEclisped (gleanedContact2);
            CheckSearch (accountId, gleanedContact.Id);

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
            gleanedContact2 = ReRead (gleanedContact2);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (gleanedContact2);
            CheckEmailAddressNotEclipsed (ricContact);
            CheckSearch (accountId, ricContact.Id);

            // Create a sync contact. It should eclipse the GAL, RIC and gleaned contact.
            var syncContact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
                Source = McAbstrItem.ItemSource.ActiveSync
            };
            syncContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email2);
            syncContact.AddEmailAddressAttribute (accountId, "Email1Address", null, email);
            syncContact.Insert ();
            syncFolder.Link (syncContact);

            gleanedContact = ReRead (gleanedContact);
            gleanedContact2 = ReRead (gleanedContact2);
            ricContact = ReRead (ricContact);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (gleanedContact2);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id);

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
            gleanedContact2 = ReRead (gleanedContact2);
            ricContact = ReRead (ricContact);
            syncContact = ReRead (syncContact);

            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (gleanedContact2);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressEclisped (galContact);
            CheckEmailAddressNotEclipsed (syncContact);
            CheckSearch (accountId, syncContact.Id);

            // Delete the sync contact. This should uneclipse the GAL contact
            syncContact.Delete ();

            galContact = ReRead (galContact);
            ricContact = ReRead (ricContact);
            gleanedContact = ReRead (gleanedContact);
            gleanedContact2 = ReRead (gleanedContact2);

            CheckEmailAddressNotEclipsed (galContact);
            CheckEmailAddressEclisped (ricContact);
            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (gleanedContact2);
            CheckSearch (accountId, galContact.Id);

            // Delete the RIC contact. This should change any eclipsing status
            ricContact.Delete ();

            galContact = ReRead (galContact);
            gleanedContact = ReRead (gleanedContact);
            gleanedContact2 = ReRead (gleanedContact2);

            CheckEmailAddressNotEclipsed (galContact);
            CheckEmailAddressEclisped (gleanedContact);
            CheckEmailAddressEclisped (gleanedContact2);
            CheckSearch (accountId, galContact.Id);

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

        [Test]
        public void QueryForPortraitsTest ()
        {
            List<NcContactPortraitIndex> list;

            list = McContact.QueryForPortraits (new List<int> ());
            Assert.AreEqual (0, list.Count);

            int bobIndex = McEmailAddress.Get (1, "bob@foo.com");
            Assert.AreNotEqual (0, bobIndex);

            int harryIndex = McEmailAddress.Get (1, "harry@foo.com");
            Assert.AreNotEqual (0, harryIndex);

            var bob1 = new McContact ();
            bob1.AccountId = 1;
            bob1.AddEmailAddressAttribute (1, "bob", "home", "bob@foo.com");
            bob1.Insert ();

            list = McContact.QueryForPortraits (new List<int> ());
            Assert.AreEqual (0, list.Count);

            list = McContact.QueryForPortraits (new List<int> { bobIndex });
            Assert.AreEqual (0, list.Count);

            var harry1 = new McContact ();
            harry1.AccountId = 1;
            harry1.AddEmailAddressAttribute (1, "harry", "home", "harry@foo.com");
            harry1.Insert ();

            list = McContact.QueryForPortraits (new List<int> ());
            Assert.AreEqual (0, list.Count);

            list = McContact.QueryForPortraits (new List<int> { bobIndex, harryIndex });
            Assert.AreEqual (0, list.Count);

            harry1.PortraitId = 33;
            harry1.Update ();

            list = McContact.QueryForPortraits (new List<int> { bobIndex, harryIndex });
            Assert.AreEqual (1, list.Count);
            Assert.AreEqual (harryIndex, list [0].EmailAddress);
            Assert.AreEqual (33, list [0].PortraitId);

            var harry2 = new McContact ();
            harry2.AccountId = 1;
            harry2.AddEmailAddressAttribute (1, "harry", "home", "harry@foo.com");
            harry2.Insert ();

            list = McContact.QueryForPortraits (new List<int> { bobIndex, harryIndex });
            Assert.AreEqual (1, list.Count);
            Assert.AreEqual (harryIndex, list [0].EmailAddress);
            Assert.AreEqual (33, list [0].PortraitId);

            harry2.PortraitId = 44;
            harry2.Update ();

            list = McContact.QueryForPortraits (new List<int> { bobIndex, harryIndex });
            Assert.AreEqual (2, list.Count);
        }

        protected void CheckContactAttributeId (McAbstrContactAttribute newAttribute, McAbstrContactAttribute oldAttribute, bool changed)
        {
            var oldId = oldAttribute.Id;
            var newId = newAttribute.Id;
            if (changed) {
                Assert.AreNotEqual (oldId, newId);
            } else {
                Assert.AreEqual (oldId, newId);
            }
        }

        protected List<McContactAddressAttribute> CheckContactAddress (int id, McContactAddressAttribute oldAddressAttribute, bool changed)
        {
            var newAddressAttributes = McContactAddressAttribute.QueryByContactId<McContactAddressAttribute> (id);
            Assert.AreEqual (1, newAddressAttributes.Count);
            CheckContactAttributeId (newAddressAttributes [0], oldAddressAttribute, changed);
            return newAddressAttributes;
        }

        protected List<McContactStringAttribute> CheckContactCategory (int id, McContactStringAttribute oldCategoryAttribute, bool changed)
        {
            var newCategoryAttributes = McContactStringAttribute.QueryByContactIdAndType (id, McContactStringType.Category);
            Assert.AreEqual (1, newCategoryAttributes.Count);
            CheckContactAttributeId (newCategoryAttributes [0], oldCategoryAttribute, changed);
            return newCategoryAttributes;
        }

        protected List<McContactDateAttribute> CheckContactDate (int id, McContactDateAttribute oldDateAttribute, bool changed)
        {
            var newDateAttributes = McContactDateAttribute.QueryByContactId<McContactDateAttribute> (id);
            Assert.AreEqual (1, newDateAttributes.Count);
            CheckContactAttributeId (newDateAttributes [0], oldDateAttribute, changed);
            return newDateAttributes;
        }

        protected List<McContactEmailAddressAttribute> CheckContactEmailAddress (int id, McContactEmailAddressAttribute oldEmailAddressAttribute, bool changed)
        {
            var newEmailAddressAttributes = McContactEmailAddressAttribute.QueryByContactId<McContactEmailAddressAttribute> (id);
            Assert.AreEqual (1, newEmailAddressAttributes.Count);
            CheckContactAttributeId (newEmailAddressAttributes [0], oldEmailAddressAttribute, changed);
            return newEmailAddressAttributes;
        }

        protected List<McContactStringAttribute> CheckContactIMAddress (int id, McContactStringAttribute oldIMAddressAttribute, bool changed)
        {
            var newIMAddressAttributes = McContactStringAttribute.QueryByContactIdAndType (id, McContactStringType.IMAddress);
            Assert.AreEqual (1, newIMAddressAttributes.Count);
            CheckContactAttributeId (newIMAddressAttributes [0], oldIMAddressAttribute, changed);
            return newIMAddressAttributes;
        }

        protected List<McContactStringAttribute> CheckContactPhoneNumber (int id, McContactStringAttribute oldPhoneNumberAttribute, bool changed)
        {
            var newPhoneNumberAttributes = McContactStringAttribute.QueryByContactIdAndType (id, McContactStringType.PhoneNumber);
            Assert.AreEqual (1, newPhoneNumberAttributes.Count);
            CheckContactAttributeId (newPhoneNumberAttributes [0], oldPhoneNumberAttribute, changed);
            return newPhoneNumberAttributes;
        }

        protected List<McContactStringAttribute> CheckContactRelationship (int id, McContactStringAttribute oldRelationshipAttribute, bool changed)
        {
            var newRelationshipAttributes = McContactStringAttribute.QueryByContactIdAndType (id, McContactStringType.Relationship);
            Assert.AreEqual (1, newRelationshipAttributes.Count);
            CheckContactAttributeId (newRelationshipAttributes [0], oldRelationshipAttribute, changed);
            return newRelationshipAttributes;
        }

        [Test]
        public void AncillaryData ()
        {
            int accountId = 1;
            McContact contact = new McContact () {
                AccountId = accountId,
                FirstName = "Bob",
                LastName = "Smith",
            };
            contact.AddAddressAttribute (accountId, "Address", "Work",
                new McContactAddressAttribute () {
                    Street = "1 Main St.",
                    City = "Frmeont",
                    PostalCode = "94539",
                    Country = "U.S.A.",
                }
            );
            contact.AddCategoryAttribute (accountId, "Important");
            contact.AddDateAttribute (accountId, "Birthday", "Birthday", new DateTime (1970, 1, 1));
            contact.AddEmailAddressAttribute (accountId, "Email1Address", "Email", "bob@company.net");
            contact.AddIMAddressAttribute (accountId, "IMAddress", "Skype", "bob_company");
            contact.AddPhoneNumberAttribute (accountId, "PhoneNumber", "Work", "1-234-555-6789");
            contact.AddRelationshipAttribute (accountId, "Relationship", "Relationship", "Colleague");
            contact.Insert ();
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_ALL, contact.TestHasReadAncillaryData);
            Assert.True (0 != contact.Id);

            // Query all ancillary data individual. These will be used for determine if they
            // are updated individually
            var addressAttribute = McContactAddressAttribute.QueryByContactId<McContactAddressAttribute> (contact.Id);
            Assert.AreEqual (1, addressAttribute.Count);
            var dateAttribute = McContactDateAttribute.QueryByContactId<McContactDateAttribute> (contact.Id);
            Assert.AreEqual (1, dateAttribute.Count);
            var emailAddressAttribute = McContactEmailAddressAttribute.QueryByContactId<McContactEmailAddressAttribute> (contact.Id);
            Assert.AreEqual (1, emailAddressAttribute.Count);
            var imaddressAttribute = McContactStringAttribute.QueryByContactIdAndType (contact.Id, McContactStringType.IMAddress);
            Assert.AreEqual (1, imaddressAttribute.Count);
            var phoneNumberAttribute = McContactStringAttribute.QueryByContactIdAndType (contact.Id, McContactStringType.PhoneNumber);
            Assert.AreEqual (1, phoneNumberAttribute.Count);
            var relationshpiAttribute = McContactStringAttribute.QueryByContactIdAndType (contact.Id, McContactStringType.Relationship);
            Assert.AreEqual (1, relationshpiAttribute.Count);
            var categoryAttribute = McContactStringAttribute.QueryByContactIdAndType (contact.Id, McContactStringType.Category);
            Assert.AreEqual (1, categoryAttribute.Count);

            // Read address
            var newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.Addresses.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_ADDRESSES, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            addressAttribute = CheckContactAddress (contact.Id, addressAttribute [0], true);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read category
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.Categories.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_CATEGORIES, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            categoryAttribute = CheckContactCategory (contact.Id, categoryAttribute [0], true);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read date
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.Dates.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_DATES, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            dateAttribute = CheckContactDate (contact.Id, dateAttribute [0], true);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read email address
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.EmailAddresses.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_EMAILADDRESSES, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            emailAddressAttribute = CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], true);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read IM address
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.IMAddresses.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_IMADDRESSES, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            imaddressAttribute = CheckContactIMAddress (contact.Id, imaddressAttribute [0], true);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read phone number
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.PhoneNumbers.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_PHONENUMBERS, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            phoneNumberAttribute = CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], true);
            CheckContactRelationship (contact.Id, relationshpiAttribute [0], false);

            // Read relationship
            newContact = McContact.QueryById<McContact> (contact.Id);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_NONE, newContact.TestHasReadAncillaryData);

            Assert.AreEqual (1, newContact.Relationships.Count);
            Assert.AreEqual (McContact.McContactAncillaryDataEnum.READ_RELATIONSHIPS, newContact.TestHasReadAncillaryData);
            newContact.Update ();

            CheckContactAddress (contact.Id, addressAttribute [0], false);
            CheckContactCategory (contact.Id, categoryAttribute [0], false);
            CheckContactDate (contact.Id, dateAttribute [0], false);
            CheckContactEmailAddress (contact.Id, emailAddressAttribute [0], false);
            CheckContactIMAddress (contact.Id, imaddressAttribute [0], false);
            CheckContactPhoneNumber (contact.Id, phoneNumberAttribute [0], false);
            relationshpiAttribute = CheckContactRelationship (contact.Id, relationshpiAttribute [0], true);
        }
    }
}

