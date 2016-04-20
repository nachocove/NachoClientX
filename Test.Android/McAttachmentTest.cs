//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Test.Common;

namespace Test.iOS
{
    [TestFixture]
    public class McAttachmentTest : NcTestBase
    {

        const int Account1 = 1000;
        const int Account2 = 1001;
        
        [Test]
        public void GetAllFiles ()
        {
            // includes McNote and McDocument too.
            var att00 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "att00",
            };
            att00.Insert ();

            var att0att = new McAttachment () {
                AccountId = att00.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
                DisplayName = "ATT0045",
            };
            att0att.Insert ();
            att0att.Link (att00);


            var keeper1 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper1",
            };
            keeper1.Insert ();

            var keeper1att = new McAttachment () {
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
                DisplayName = "keeper1",
            };
            keeper1att.Insert ();
            keeper1att.Link (keeper1);

            var keeper2 = new McCalendar () {
                AccountId = Account1,
                ServerId = "keeper2",
            };
            keeper2.Insert ();

            var keeper2att = new McAttachment () {
                AccountId = keeper2.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50002,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
                DisplayName = "keeper2",
            };
            keeper2att.Insert ();
            keeper2att.Link (keeper2);

            var doc1 = new McDocument () {
                AccountId = Account1,
                DisplayName = "doc1",
            };
            doc1.Insert ();

            var doc2 = new McDocument () {
                AccountId = Account2,
                DisplayName = "doc2",
            };
            doc2.Insert ();

            var note1 = new McNote () {
                AccountId = Account1,
                DisplayName = "note1",
            };
            note1.Insert ();

            var note2 = new McNote () {
                AccountId = Account2,
                DisplayName = "note2",
            };
            note2.Insert ();

            var fallOff = new McEmailMessage () {
                AccountId = Account2,
                ServerId = "falloff",
            };
            fallOff.Insert ();

            var fallOffatt = new McAttachment () {
                AccountId = fallOff.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            fallOffatt.Insert ();
            fallOffatt.Link (fallOff);

            var list = McAbstrFileDesc.GetAllFiles (keeper1.AccountId);
            Assert.NotNull (list);
            Assert.AreEqual (4, list.Count);
            Assert.AreEqual (1, list.Where (x => x.Id == keeper1att.Id && x.FileType == 0).Count ());
            Assert.AreEqual (1, list.Where (x => x.Id == keeper2att.Id && x.FileType == 0).Count ());
            Assert.AreEqual (1, list.Where (x => x.Id == note1.Id && x.FileType == 1).Count ());
            Assert.AreEqual (1, list.Where (x => x.Id == doc1.Id && x.FileType == 2).Count ());
        }

        [Test]
        public void TestQueryItems ()
        {
            // simple case already covered by CaledarAttachments.
            var keeper1 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper1",
            };
            keeper1.Insert ();

            var keeper1att = new McAttachment () {
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1att.Insert ();
            keeper1att.Link (keeper1);

            var keeper2 = new McCalendar () {
                AccountId = Account2,
                ServerId = "keeper2",
            };
            keeper2.Insert ();
            keeper1att.Link (keeper2);

            var items = McAttachment.QueryItems (keeper1att.Id);
            Assert.NotNull (items);
            Assert.AreEqual (2, items.Count);
            Assert.AreEqual (1, items.Where (x => x is McEmailMessage && x.Id == keeper1.Id).Count ());
            Assert.AreEqual (1, items.Where (x => x is McCalendar && x.Id == keeper2.Id).Count ());
        }

        [Test]
        public void TestQueryByItemId ()
        {
            var keeper1 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper1",
            };
            keeper1.Insert ();

            var keeper1att = new McAttachment () {
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1att.Insert ();
            keeper1att.Link (keeper1);

            var keeper2 = new McCalendar () {
                AccountId = Account2,
                ServerId = "keeper2",
            };
            keeper2.Insert ();

            var keeper2att = new McAttachment () {
                AccountId = keeper2.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50002,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper2att.Insert ();
            keeper2att.Link (keeper2);

            var keeper1attb = new McAttachment () {
                AccountId = keeper2.AccountId, // Note!
                FilePresenceFraction = 0,
                FileSize = 50003,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1attb.Insert ();
            keeper1attb.Link (keeper1);

            var attrs = McAttachment.QueryByItem (keeper1);
            Assert.IsNotNull (attrs);
            Assert.AreEqual (2, attrs.Count);
            Assert.IsTrue (attrs.Any (x => x.Id == keeper1att.Id));
            Assert.IsTrue (attrs.Any (x => x.Id == keeper1attb.Id));
            Assert.IsFalse (attrs.Any (x => x.Id == keeper2att.Id));
        }

        [Test]
        public void TestQueryByAttachmentIdItemIdClassCode ()
        {
            var keeper1 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper1",
            };
            keeper1.Insert ();

            var keeper1att = new McAttachment () {
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1att.Insert ();
            keeper1att.Link (keeper1);

            var keeper2 = new McCalendar () {
                AccountId = Account2,
                ServerId = "keeper2",
            };
            keeper2.Insert ();

            var keeper2att = new McAttachment () {
                AccountId = keeper2.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50002,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper2att.Insert ();
            keeper2att.Link (keeper2, true);

            var keeper3 = new McCalendar () {
                AccountId = Account1,
                ServerId = "keeper3",
            };
            keeper3.Insert ();

            var keeper3att = new McAttachment () {
                AccountId = keeper2.AccountId, //Note!
                FilePresenceFraction = 0,
                FileSize = 50003,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            // The AccountId of the map will match the AccountId of the item.
            keeper3att.Insert ();
            keeper3att.Link (keeper3);

            var map = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (keeper1.AccountId, keeper1att.Id, keeper1.Id,
                McAbstrFolderEntry.ClassCodeEnum.Email);
            Assert.IsNotNull (map);
            Assert.IsFalse (map.IncludedInBody);
            Assert.AreEqual (map.AccountId, keeper1.AccountId);
            Assert.AreEqual (map.AttachmentId, keeper1att.Id);
            Assert.AreEqual (map.ItemId, keeper1.Id);

            map = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (keeper2.AccountId, keeper2att.Id, keeper2.Id,
                McAbstrFolderEntry.ClassCodeEnum.Calendar);
            Assert.IsNotNull (map);
            Assert.IsTrue (map.IncludedInBody);
            Assert.AreEqual (map.AccountId, keeper2.AccountId);
            Assert.AreEqual (map.AttachmentId, keeper2att.Id);
            Assert.AreEqual (map.ItemId, keeper2.Id);

            map = McMapAttachmentItem.QueryByAttachmentIdItemIdClassCode (keeper3.AccountId, keeper3att.Id, keeper3.Id,
                McAbstrFolderEntry.ClassCodeEnum.Calendar);
            Assert.IsNotNull (map);
            Assert.IsFalse (map.IncludedInBody);
            Assert.AreEqual (map.AccountId, keeper3.AccountId);
            Assert.AreNotEqual (map.AccountId, keeper3att.AccountId);
            Assert.AreEqual (map.AttachmentId, keeper3att.Id);
            Assert.AreEqual (map.ItemId, keeper3.Id);
        }

        [Test]
        public void TestQueryNeedsFetch ()
        {
            var keeper1 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper1",
                IsAwaitingDelete = false,
                Score = 0.98,
                DateReceived = DateTime.UtcNow.AddDays (-2),
            };
            keeper1.Insert ();

            var keeper1att = new McAttachment () {
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1att.Insert ();
            keeper1att.Link (keeper1);

            var keeper2 = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "keeper2",
                IsAwaitingDelete = false,
                Score = 0.98,
                DateReceived = DateTime.UtcNow.AddDays (-3),
            };
            keeper2.Insert ();

            var keeper2att = new McAttachment () {
                AccountId = keeper2.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50002,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper2att.Insert ();
            keeper2att.Link (keeper2);

            var fallOff = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "falloff",
                IsAwaitingDelete = false,
                Score = 0.97,
                DateReceived = DateTime.UtcNow.AddDays (-1),
            };
            fallOff.Insert ();

            var fallOffatt = new McAttachment () {
                AccountId = fallOff.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            fallOffatt.Insert ();
            fallOffatt.Link (fallOff);

            var trash = new McEmailMessage () {
                AccountId = Account2,
                ServerId = "other_account",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            var trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "is_deleted",
                IsAwaitingDelete = true,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "some_download",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "low_score",
                IsAwaitingDelete = false,
                Score = 0.69,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "too_big",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 500000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "downloaded",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Complete);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "error",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Error);
            trashatt.Insert ();
            trashatt.Link (trash);

            trash = new McEmailMessage () {
                AccountId = Account1,
                ServerId = "partial",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();

            trashatt = new McAttachment () {
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
            trashatt.Insert ();
            trashatt.Link (trash);

            var result = McAttachment.QueryNeedsFetch (Account1, 2, 0.9, 100000);
            Assert.AreEqual (2, result.Count ());
            Assert.True (result.Any (x => 50001 == x.FileSize));
            Assert.True (result.Any (x => 50002 == x.FileSize));
            Assert.AreEqual (50001, result.First ().FileSize);
        }
    }
}

