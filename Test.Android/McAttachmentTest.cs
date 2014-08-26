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
                EmailMessageId = keeper1.Id,
                AccountId = keeper1.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50001,
                IsDownloaded = false,
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
                EmailMessageId = keeper2.Id,
                AccountId = keeper2.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50002,
                IsDownloaded = false,
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
                EmailMessageId = fallOff.Id,
                AccountId = fallOff.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50000,
                IsDownloaded = false,
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50000,
                IsDownloaded = false,
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50000,
                IsDownloaded = false,
            };
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 1,
                EstimatedDataSize = 50000,
                IsDownloaded = false,
            };
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50000,
                IsDownloaded = false,
            };
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 500000,
                IsDownloaded = false,
            };
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
                EmailMessageId = trash.Id,
                AccountId = trash.AccountId,
                PercentDownloaded = 0,
                EstimatedDataSize = 50000,
                IsDownloaded = true,
            };
            trashatt.Insert ();

            var result = McAttachment.QueryNeedsFetch (1, 2, 0.9, 100000);
            Assert.AreEqual (2, result.Count ());
            Assert.True (result.Any (x => 50001 == x.EstimatedDataSize));
            Assert.True (result.Any (x => 50002 == x.EstimatedDataSize));
            Assert.AreEqual (50001, result.First ().EstimatedDataSize);
        }
    }
}

