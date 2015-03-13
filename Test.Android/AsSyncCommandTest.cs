//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using NUnit.Framework;
using Test.Common;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;

namespace Test.iOS
{
    public class DummyBEContext : IBEContext
    {
        public IProtoControlOwner Owner { set; get; }
        public AsProtoControl ProtoControl { set; get; }
        public McProtocolState ProtocolState { get; set; }
        public McServer Server { get; set; }
        public McAccount Account { get; set; }
        public McCred Cred { get; set; }
    }

    [TestFixture]
    public class AsSyncCommandTest : NcTestBase
    {
        const string Email = "Email";
        const string Calendar = "Calendar";
        const string Tasks = "Tasks";
        const string Contacts = "Contacts";
        const string Notes = "Notes";

        [Test]
        public void TestTypeCodeToAirSyncClassCode ()
        {
            Assert.AreEqual (Email, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (Xml.FolderHierarchy.TypeCode.DefaultInbox_2));
            Assert.AreEqual (Calendar, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (Xml.FolderHierarchy.TypeCode.DefaultCal_8));
            Assert.AreEqual (Contacts, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (Xml.FolderHierarchy.TypeCode.DefaultContacts_9));
            Assert.AreEqual (Tasks, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (Xml.FolderHierarchy.TypeCode.DefaultTasks_7));
        }

        [Test]
        public void TestClassCodeEnumToAirSyncClassCode ()
        {
            Assert.AreEqual (Email, Xml.FolderHierarchy.ClassCodeEnumToAirSyncClassCode (McAbstrFolderEntry.ClassCodeEnum.Email));
            Assert.AreEqual (Calendar, Xml.FolderHierarchy.ClassCodeEnumToAirSyncClassCode (McAbstrFolderEntry.ClassCodeEnum.Calendar));
            Assert.AreEqual (Contacts, Xml.FolderHierarchy.ClassCodeEnumToAirSyncClassCode (McAbstrFolderEntry.ClassCodeEnum.Contact));
            Assert.AreEqual (Tasks, Xml.FolderHierarchy.ClassCodeEnumToAirSyncClassCode (McAbstrFolderEntry.ClassCodeEnum.Tasks));
        }

        [Test]
        public void TestAirSyncClassCode ()
        {
            Assert.AreEqual (Xml.AirSync.ClassCode.Email, AsSyncCommand.AirSyncClassCode (McPending.Operations.EmailClearFlag));
            Assert.AreEqual (Xml.AirSync.ClassCode.Email, AsSyncCommand.AirSyncClassCode (McPending.Operations.EmailDelete));
            Assert.AreEqual (Xml.AirSync.ClassCode.Email, AsSyncCommand.AirSyncClassCode (McPending.Operations.EmailMarkFlagDone));
            Assert.AreEqual (Xml.AirSync.ClassCode.Email, AsSyncCommand.AirSyncClassCode (McPending.Operations.EmailMarkRead));
            Assert.AreEqual (Xml.AirSync.ClassCode.Email, AsSyncCommand.AirSyncClassCode (McPending.Operations.EmailSetFlag));

            Assert.AreEqual (Xml.AirSync.ClassCode.Calendar, AsSyncCommand.AirSyncClassCode (McPending.Operations.CalCreate));
            Assert.AreEqual (Xml.AirSync.ClassCode.Calendar, AsSyncCommand.AirSyncClassCode (McPending.Operations.CalDelete));
            Assert.AreEqual (Xml.AirSync.ClassCode.Calendar, AsSyncCommand.AirSyncClassCode (McPending.Operations.CalUpdate));

            Assert.AreEqual (Xml.AirSync.ClassCode.Contacts, AsSyncCommand.AirSyncClassCode (McPending.Operations.ContactCreate));
            Assert.AreEqual (Xml.AirSync.ClassCode.Contacts, AsSyncCommand.AirSyncClassCode (McPending.Operations.ContactDelete));
            Assert.AreEqual (Xml.AirSync.ClassCode.Contacts, AsSyncCommand.AirSyncClassCode (McPending.Operations.ContactUpdate));

            Assert.AreEqual (Xml.AirSync.ClassCode.Tasks, AsSyncCommand.AirSyncClassCode (McPending.Operations.TaskCreate));
            Assert.AreEqual (Xml.AirSync.ClassCode.Tasks, AsSyncCommand.AirSyncClassCode (McPending.Operations.TaskDelete));
            Assert.AreEqual (Xml.AirSync.ClassCode.Tasks, AsSyncCommand.AirSyncClassCode (McPending.Operations.TaskUpdate));

            Assert.IsNull (AsSyncCommand.AirSyncClassCode (McPending.Operations.AttachmentDownload));
        }

        [Test]
        public void TestToXxxYyy ()
        {
            XElement xml = null;
            const int accountId = 99;
            var folder = new McFolder () {
                AccountId = accountId,
                Type = Xml.FolderHierarchy.TypeCode.DefaultInbox_2,
                ServerId = "bar",
            };
            folder.Insert ();
            var pending = new McPending (accountId) {
                ServerId = "foo",
            };
            pending.Insert ();
            var ctxt = new DummyBEContext ();
            ctxt.ProtocolState = new McProtocolState ();
            var kit = new SyncKit () {
                OverallWindowSize = 40,
                IsNarrow = false,
                PerFolders = new List<SyncKit.PerFolder> () { 
                    new SyncKit.PerFolder () {
                        Folder = null,
                        Commands = new List<McPending> () { pending }
                    }
                }
            };
            var email = new McEmailMessage () {
                AccountId = accountId,
            };
            email.Insert ();
            var cmd = new AsSyncCommand (ctxt, kit);
            // FIXME - convert this to a loop that runs across an array.
            pending.Operation = McPending.Operations.EmailClearFlag;
            pending.Update ();
            ctxt.ProtocolState.AsProtocolVersion = "14.0";
            folder.Type = Xml.FolderHierarchy.TypeCode.DefaultInbox_2;
            xml = cmd.ToEmailClearFlag (pending, folder);
            // Assert no class.
            // FIXME - what is right?
            folder.Type = Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1;
            xml = cmd.ToEmailClearFlag (pending, folder);
            // Assert Class == Email.
            ctxt.ProtocolState.AsProtocolVersion = "12.1";
            xml = cmd.ToEmailClearFlag (pending, folder);
            // Assert no class.
        }

        [Test]
        public void TestToXDocumentGenericWithCalAdd ()
        {
        }

        [Test]
        public void TestToXDocumentAllCommands ()
        {
        }
    }
}

