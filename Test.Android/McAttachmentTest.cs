﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        [Test]
        public void TestQueryNeedsFetch ()
        {
            var keeper1 = new McEmailMessage () {
                AccountId = 1,
                ServerId = "keeper1",
                IsAwaitingDelete = false,
                Score = 0.98,
                DateReceived = DateTime.UtcNow.AddDays (-2),
            };
            keeper1.Insert ();
            var keeper1att = new McAttachment () {
                ItemId = keeper1.Id,
                ClassCode = keeper1.GetClassCode (),
                AccountId = keeper1.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50001,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper1att.Insert ();

            var keeper2 = new McEmailMessage () {
                AccountId = 1,
                ServerId = "keeper2",
                IsAwaitingDelete = false,
                Score = 0.98,
                DateReceived = DateTime.UtcNow.AddDays (-3),
            };
            keeper2.Insert ();
            var keeper2att = new McAttachment () {
                ItemId = keeper2.Id,
                ClassCode = keeper2.GetClassCode (),
                AccountId = keeper2.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50002,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            keeper2att.Insert ();

            var fallOff = new McEmailMessage () {
                AccountId = 1,
                ServerId = "falloff",
                IsAwaitingDelete = false,
                Score = 0.97,
                DateReceived = DateTime.UtcNow.AddDays (-1),
            };
            fallOff.Insert ();
            var fallOffatt = new McAttachment () {
                ItemId = fallOff.Id,
                ClassCode = fallOff.GetClassCode (),
                AccountId = fallOff.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            fallOffatt.Insert ();

            var trash = new McEmailMessage () {
                AccountId = 2,
                ServerId = "other_account",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            var trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FilePresenceFraction = 0,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
                FilePresence = McAbstrFileDesc.FilePresenceEnum.None,
            };
            trashatt.Insert ();

            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "is_deleted",
                IsAwaitingDelete = true,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();

            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "some_download",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
            trashatt.Insert ();

            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "low_score",
                IsAwaitingDelete = false,
                Score = 0.69,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();

            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "too_big",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FileSize = 500000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.None);
            trashatt.Insert ();

            trash = new McEmailMessage () {
                AccountId = 1,
                ServerId = "downloaded",
                IsAwaitingDelete = false,
                Score = 0.99,
                DateReceived = DateTime.UtcNow,
            };
            trash.Insert ();
            trashatt = new McAttachment () {
                ItemId = trash.Id,
                ClassCode = trash.GetClassCode (),
                AccountId = trash.AccountId,
                FileSize = 50000,
                FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Estimate,
            };
            trashatt.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Complete);
            trashatt.Insert ();

            var result = McAttachment.QueryNeedsFetch (1, 2, 0.9, 100000);
            Assert.AreEqual (2, result.Count ());
            Assert.True (result.Any (x => 50001 == x.FileSize));
            Assert.True (result.Any (x => 50002 == x.FileSize));
            Assert.AreEqual (50001, result.First ().FileSize);
        }
    }
}

