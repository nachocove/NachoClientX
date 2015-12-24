//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using SQLite;
using DDay.iCal;
using DDay.iCal.Serialization;
using DDay.iCal.Serialization.iCalendar;
using System.IO;

namespace Test.Common
{
    public class MockDataSource : IBEContext
    {
        public INcProtoControlOwner Owner { set; get; }

        public NcProtoControl ProtoControl { set; get; }

        public McProtocolState ProtocolState { get; set; }

        public McServer Server { get; set; }

        public McAccount Account { get; set; }

        public McCred Cred { get; set; }

        public MockDataSource ()
        {
            Owner = new MockProtoControlOwner ();
            Account = new McAccount ();
            Account.Id = 1;
        }
    }

    public class MockProtoControlOwner : INcProtoControlOwner
    {
        public string AttachmentsDir { set; get; }

        public void CredReq (NcProtoControl sender)
        {
        }

        public void ServConfReq (NcProtoControl sender, NachoCore.BackEnd.AutoDFailureReasonEnum arg)
        {
        }

        public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate)
        {
        }

        public void StatusInd (NcProtoControl sender, NcResult status)
        {
        }

        public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
        {
        }

        public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
        {
        }

        public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
        {
        }

        public void BackendAbateStart ()
        {
        }

        public void BackendAbateStop ()
        {
        }
    }

    public class MockNcFolder : McFolder
    {
        public MockNcFolder ()
        {
            this.AccountId = 1;
            this.Id = 86;
            this.ServerId = "mock folder";
            this.ParentId = "mock folder parent";
            this.AsSyncKey = "mock folder sync key";
            this.DisplayName = "mock folder";
            this.Type = Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1;
        }
    }

    public class NcCalendarTest : NcTestBase
    {
        NachoCore.ActiveSync.AsHelpers c = new NachoCore.ActiveSync.AsHelpers ();

        public void GoodCompactDateTime (string compactDateTime, DateTime match)
        {
            var d = c.ParseAsCompactDateTime (compactDateTime);
            Assert.False (d.Equals (DateTime.MinValue));
            Assert.True (d.Equals (match));
        }

        public void BadCompactDateTime (string compactDateTime)
        {
            var d = c.ParseAsCompactDateTime (compactDateTime);
            Assert.True (d.Equals (DateTime.MinValue));
        }

        [Test]
        public void CompactDateTimeParsing ()
        {
            GoodCompactDateTime ("20131123T190243Z", new DateTime (2013, 11, 23, 19, 2, 43, 00));
            GoodCompactDateTime ("20131123T190243123Z", new DateTime (2013, 11, 23, 19, 2, 43, 123));
            BadCompactDateTime (null);
            BadCompactDateTime ("");
            BadCompactDateTime ("20131123T190243Z1");
            BadCompactDateTime ("20131123T1902431Z");
            BadCompactDateTime ("20131123T19024312Z");
            BadCompactDateTime ("20131123T1902431234Z");
        }

        /// <A:DateReceived>2009-11-12T00:45:06.000Z</A:DateReceived>
        /// 
        public void GoodDateTime (string compactDateTime, DateTime match)
        {
            var d = AsHelpers.ParseAsDateTime (compactDateTime);
            Assert.False (d.Equals (DateTime.MinValue));
            Assert.True (d.Equals (match));
        }

        public void BadDateTime (string compactDateTime)
        {
            var d = c.ParseAsCompactDateTime (compactDateTime);
            Assert.True (d.Equals (DateTime.MinValue));
        }

        [Test]
        public void DateTimeParsing ()
        {
            GoodDateTime ("2013-11-23T19:02:43.000Z", new DateTime (2013, 11, 23, 19, 2, 43, 00));
            GoodDateTime ("2013-11-23T19:02:43.123Z", new DateTime (2013, 11, 23, 19, 2, 43, 123));
            BadDateTime (null);
            BadDateTime ("");
            BadDateTime ("2013-11-23T19:02:43Z");
            BadDateTime ("2013-11-23T19:02:43Z1");
            BadDateTime ("2013-11-23T19:02:43.1Z");
            BadDateTime ("2013-11-23T19:02:43.12Z");
            BadDateTime ("2013-11-23T19:02:43.1234Z");
        }

        public void GoodExtractStringFromTimeZone (string s)
        {
            byte[] b = new byte[64];
            int l = s.Length * sizeof(char);
            Array.Clear (b, 0, 64);
            System.Buffer.BlockCopy (s.ToCharArray (), 0, b, 0, l);
            var e = c.ExtractStringFromAsTimeZone (b, 0, l);
            Assert.IsTrue (s.Equals (e));
        }

        public void CreateMcBody (MockDataSource mds, int id)
        {
            var body = new McBody () {
                AccountId = mds.Account.Id,
            };
            body.Insert ();
            Assert.AreEqual (id, body.Id);
        }

        [Test]
        public void NewEntryWithAdd ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeCalendarItem (1, command, new MockNcFolder ());
        }
        //        [Test]
        public void UpdateEntryWithAdd ()
        {
//            asSync.UpdateEvent (addString_02, null);
        }

        [Test]
        public void CalendarCategories ()
        {
            var c01 = new McCalendarCategory (1, "test");
            c01.ParentId = 5;
            c01.ParentType = McCalendarCategory.CALENDAR;
            c01.Insert ();

            var c02 = NcModel.Instance.Db.Get<McCalendarCategory> (x => x.ParentId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.ParentId, 5);
            Assert.AreEqual (c02.Name, "test");

            var c03 = NcModel.Instance.Db.Get<McCalendarCategory> (x => x.Name == "test");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.ParentId, 5);
            Assert.AreEqual (c03.Name, "test");

            c03.Name = "changed";
            c03.Update ();

            Assert.AreEqual (NcModel.Instance.Db.Table<McCalendarCategory> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => NcModel.Instance.Db.Get<McCalendarCategory> (x => x.Name == "test"));

            var c05 = NcModel.Instance.Db.Get<McCalendarCategory> (x => x.Name == "changed");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.ParentId, 5);
            Assert.AreEqual (c05.Name, "changed");

            var c06 = new McCalendarCategory (1, "second");
            c06.ParentId = 5;
            c06.Insert ();
            var c07 = new McCalendarCategory (1, "do not see");
            c07.ParentId = 6;
            c07.Insert ();

            Assert.AreEqual (3, NcModel.Instance.Db.Table<McCalendarCategory> ().Count ());

            var c10 = NcModel.Instance.Db.Table<McCalendarCategory> ().Where (x => x.ParentId == 5);
            Assert.AreEqual (2, c10.Count ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("changed") || c.Name.Equals ("second"));
            }
                                  
        }

        [Test]
        public void CalendarAttendee ()
        {
            var c01 = new McAttendee (1, "Steve", "rascal2210@hotmail.com");
            c01.ParentId = 5;
            c01.AttendeeType = NcAttendeeType.Required;
            c01.Insert ();

            var c02 = NcModel.Instance.Db.Get<McAttendee> (x => x.ParentId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.ParentId, 5);
            Assert.AreEqual (c02.Name, "Steve");
            Assert.AreEqual (c02.Email, "rascal2210@hotmail.com");

            var c03 = NcModel.Instance.Db.Get<McAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.ParentId, 5);
            Assert.AreEqual (c03.Name, "Steve");
            Assert.AreEqual (c03.Email, "rascal2210@hotmail.com");

            c03.Email = "steves@nachocove.com";
            c03.Update ();

            Assert.AreEqual (NcModel.Instance.Db.Table<McAttendee> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => NcModel.Instance.Db.Get<McAttendee> (x => x.Email == "rascal2210@hotmail.com"));

            var c05 = NcModel.Instance.Db.Get<McAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.ParentId, 5);
            Assert.AreEqual (c05.Name, "Steve");
            Assert.AreEqual (c05.Email, "steves@nachocove.com");

            var c05a = NcModel.Instance.Db.Get<McAttendee> (x => x.Email == "steves@nachocove.com");
            Assert.IsNotNull (c05a);
            Assert.AreEqual (c05a.Id, 1);
            Assert.AreEqual (c05a.ParentId, 5);
            Assert.AreEqual (c05a.Name, "Steve");
            Assert.AreEqual (c05a.Email, "steves@nachocove.com");


            var c06 = new McAttendee (1, "Chris", "chrisp@nachocove.com");
            c06.AttendeeType = NcAttendeeType.Optional;
            c06.ParentId = 5;
            c06.Insert ();
            var c07 = new McAttendee (1, "Jeff", "jeffe@nachocove.com");
            c07.ParentId = 6;
            c07.AttendeeType = NcAttendeeType.Optional;
            c07.Insert ();

            Assert.AreEqual (3, NcModel.Instance.Db.Table<McAttendee> ().Count ());

            var c10 = NcModel.Instance.Db.Table<McAttendee> ().Where (x => x.ParentId == 5);
            Assert.AreEqual (2, c10.Count ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("Steve") || c.Name.Equals ("Chris"));
            }

        }

        [Test]
        public void CaledarAttachments ()
        {
            McCalendar cal = InsertSimpleEvent ("");

            // Create three unowned attachments.
            McAttachment attachment1 = new McAttachment () {
                AccountId = cal.AccountId,
            };
            attachment1.Insert ();
            attachment1.UpdateData ("attachment #1");
            McAttachment attachment2 = new McAttachment () {
                AccountId = cal.AccountId,
            };
            attachment2.Insert ();
            attachment2.UpdateData ("attachment #2");
            McAttachment attachment3 = new McAttachment () {
                AccountId = cal.AccountId,
            };
            attachment3.Insert ();
            attachment3.UpdateData ("attachment #3");

            foreach (int id in new int[] { attachment1.Id, attachment2.Id, attachment3.Id }) {
                Assert.AreNotEqual (0, id, "Attachment was not given an ID when it was added to the database.");
                var dbAttachment = McAttachment.QueryById<McAttachment> (id);
                Assert.IsNotNull (dbAttachment, "The attachment could not be looked up in the database.");
                Assert.AreEqual (dbAttachment.Id, id, "The wrong attachment was retrieved from the database.");
                var count = McAttachment.QueryItems (dbAttachment.Id).Count;
                Assert.AreEqual (0, count, "Attachment is already owned by something.");
            }

            // Assign two of the attachments to the calendar event.
            List<McAttachment> attachments = new List<McAttachment> ();
            attachments.Add (attachment1);
            attachments.Add (attachment2);
            cal.attachments = attachments;

            // Since the event hasn't been saved, the attachments should still be unowned,
            // but they should be findable though the event.
            attachments = McAttachment.QueryByItem (cal);
            Assert.AreEqual (0, attachments.Count, "attachments are assigned to the event before they should be");
            Assert.AreEqual (2, cal.attachments.Count, "The event is not reporting the correct number of attachments.");

            // Update the event, which should update the attachments to be owned by event.
            cal.Update ();
            attachments = McAttachment.QueryByItem (cal);
            Assert.AreEqual (2, attachments.Count, "The attachments were not changed to be owned by the event.");
            foreach (var attachment in attachments) {
                var attcal = McAttachment.QueryItems (attachment.AccountId, attachment.Id).Where (x => x is McCalendar).FirstOrDefault ();
                Assert.AreEqual (attcal.Id, cal.Id, "Attachment is owned by the wrong item.");
            }

            // Deleting the event should also delete its attachments.
            cal.Delete ();
            McAttachment att = McAttachment.QueryById<McAttachment> (attachment1.Id);
            Assert.IsNull (att, "Attachment was not deleted from the database when its event was deleted.");
            att = McAttachment.QueryById<McAttachment> (attachment2.Id);
            Assert.IsNull (att, "Attachment was not deleted from the database when its event was deleted.");
            // But the third attachment should still be there.
            att = McAttachment.QueryById<McAttachment> (attachment3.Id);
            Assert.IsNotNull (att, "The unrelated attachment3 has disappeared from the database.");
            att.Delete (); // Clean up attachment3.
        }

        [Test]
        public void ParseInteger ()
        {
            // String int to boolean
            Assert.IsTrue ("1".ToBoolean ());
            Assert.IsFalse ("0".ToBoolean ());

            // Int to enums
            Assert.AreEqual (NcRecurrenceType.Daily, "0".ParseInteger<NcRecurrenceType> ());
            Assert.AreEqual (NcResponseType.Accepted, "3".ParseInteger<NcResponseType> ());
            Assert.AreEqual (NcSensitivity.Confidential, "3".ParseInteger<NcSensitivity> ());
            Assert.AreEqual (NcMeetingStatus.Appointment, "0".ParseInteger<NcMeetingStatus> ());
            Assert.AreEqual (NcDayOfWeek.Friday, "32".ParseInteger<NcDayOfWeek> ());
            Assert.AreEqual (NcCalendarType.ChineseLunar, "15".ParseInteger<NcCalendarType> ());
            Assert.AreEqual (NcBusyStatus.Busy, "2".ParseInteger<NcBusyStatus> ());
            Assert.AreEqual (NcAttendeeType.Optional, "2".ParseInteger<NcAttendeeType> ());
            Assert.AreEqual (NcAttendeeStatus.Accept, "3".ParseInteger<NcAttendeeStatus> ());
            Assert.AreNotEqual (NcRecurrenceType.YearlyOnDay, "0".ParseInteger<NcRecurrenceType> ());
            Assert.AreNotEqual (NcResponseType.Tentative, "3".ParseInteger<NcResponseType> ());
            Assert.AreNotEqual (NcSensitivity.Private, "3".ParseInteger<NcSensitivity> ());
            Assert.AreNotEqual (NcMeetingStatus.MeetingOrganizerCancelled, "0".ParseInteger<NcMeetingStatus> ());
            Assert.AreNotEqual (NcDayOfWeek.Tuesday, "32".ParseInteger<NcDayOfWeek> ());
            Assert.AreNotEqual (NcCalendarType.UmalQuraReservedMustNotBeUsed, "15".ParseInteger<NcCalendarType> ());
            Assert.AreNotEqual (NcBusyStatus.Tentative, "2".ParseInteger<NcBusyStatus> ());
            Assert.AreNotEqual (NcAttendeeType.Unknown, "2".ParseInteger<NcAttendeeType> ());
            Assert.AreNotEqual (NcAttendeeStatus.Tentative, "3".ParseInteger<NcAttendeeStatus> ());

            // String to int
            Assert.AreEqual ("1".ToInt (), 1);
            Assert.AreEqual ("1".ToUint (), 1);
           
        }

        [Test]
        public void CreateNcCalendarFromXML ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (1, Ns, command);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var c = r.GetValue<McCalendar> ();
            Assert.IsNotNull (c);
            Assert.AreEqual (c.DtStamp, new DateTime (2013, 11, 26, 12, 49, 29));
            Assert.AreEqual (c.StartTime, new DateTime (2013, 11, 28, 01, 00, 00));
            Assert.AreEqual (c.EndTime, new DateTime (2013, 11, 29, 02, 00, 00));
            Assert.AreEqual (c.Location, "the Dogg House!");
            Assert.AreEqual (c.Subject, "Big dog party at the Dogg House!");
            Assert.AreEqual (c.UID, "3rrr5stn6eld9qmv8dviolj3u0@google.com");
            Assert.AreEqual (c.Sensitivity, NcSensitivity.Normal);
            Assert.AreEqual ((int)c.Sensitivity, 0);
            Assert.AreEqual (c.BusyStatus, NcBusyStatus.Busy);
            Assert.AreEqual ((int)c.BusyStatus, 2);
            Assert.False (c.AllDayEvent);
            Assert.AreEqual (c.Reminder, 10);
            Assert.AreEqual (c.MeetingStatus, NcMeetingStatus.Appointment);
            Assert.AreEqual ((int)c.MeetingStatus, 0);
            Assert.AreEqual (c.OrganizerEmail, "steves@nachocove.com");
            Assert.AreEqual (c.OrganizerName, "Steve Scalpone");
            Assert.IsNotNull (c.attendees);
            Assert.AreEqual (c.attendees.Count, 4);

            c.AccountId = mds.Account.Id;

            c.Insert ();
            var d = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (c.attendees.Count, 4);
            Assert.AreEqual (d.attendees.Count, 4);

            c.Update ();
            var e = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (c.attendees.Count, 4);
            Assert.AreEqual (e.attendees.Count, 4);
        }

        [Test]
        public void CreateNcCalendarFromXML2 ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (addString_02);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (1, Ns, command);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var c = r.GetValue<McCalendar> ();
            Assert.IsNull (c.Location);
            Assert.AreEqual (c.Subject, "Re-dog");
            Assert.AreEqual (c.UID, "7j5do4kr7q8fi67ubq7bdpr01c@google.com");
            Assert.AreEqual (c.Sensitivity, NcSensitivity.Normal);
            Assert.AreEqual ((int)c.Sensitivity, 0);
            Assert.AreEqual (c.BusyStatus, NcBusyStatus.Busy);
            Assert.AreEqual ((int)c.BusyStatus, 2);
            Assert.False (c.AllDayEvent);
            Assert.AreEqual (c.Reminder, 10);
            Assert.AreEqual (c.MeetingStatus, NcMeetingStatus.Appointment);
            Assert.AreEqual ((int)c.MeetingStatus, 0);
            Assert.AreEqual (c.OrganizerEmail, "steves@nachocove.com");
            Assert.AreEqual (c.OrganizerName, "Steve Scalpone");
            Assert.IsNotNull (c.attendees);
            Assert.AreEqual (c.attendees.Count, 0);
            Assert.IsNotNull (c.exceptions);
            Assert.AreEqual (c.exceptions.Count, 2);
        }

        [Test]
        public void CreateNcCalendarFromXML3 ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (addString_03);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (1, Ns, command);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var c = r.GetValue<McCalendar> ();
            Assert.IsNotNull (c);
        }

        [Test]
        public void CreateNcCalendarFromXML4 ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (addString_04);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (1, Ns, command);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var c = r.GetValue<McCalendar> ();
            Assert.IsNotNull (c);

            var changeCommand = System.Xml.Linq.XElement.Parse (changeString_04);
            r = h.ParseCalendar (1, Ns, changeCommand);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var d = r.GetValue<McCalendar> ();
            Assert.IsNotNull (d);
            Assert.AreEqual (1, d.exceptions.Count);
        }

        [Test]
        public void ExceptionParse01 ()
        {
            var mds = new MockDataSource ();
            CreateMcBody (mds, 1);
            var command = System.Xml.Linq.XElement.Parse (Exception_String_01);
            Assert.IsNotNull (command);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.Calendar.Ns;
            var exception = h.ParseExceptions (1, Ns, command);
            Assert.IsNotNull (exception);
        }


        protected McCalendar InsertSimpleEvent (string type)
        {
            var c = new McCalendar ();

            var mds = new MockDataSource ();
            c.AccountId = mds.Account.Id;

            if (type.Equals ("attendees")) {
                List<McAttendee> attendees = new List<McAttendee> ();
                attendees.Add (new McAttendee (1, "Bob", "bob@foo.com") {
                    AttendeeType = NcAttendeeType.Required,
                });
                attendees.Add (new McAttendee (1, "Joe", "joe@foo.com") {
                    AttendeeType = NcAttendeeType.Optional,
                });
                c.attendees = attendees;
            }
            if (type.Equals ("categories")) {
                List<McCalendarCategory> categories = new List<McCalendarCategory> ();
                categories.Add (new McCalendarCategory (1, "red"));
                categories.Add (new McCalendarCategory (1, "blue"));
                c.categories = categories;
            }
            if (type.Equals ("recurs")) {
                List<McRecurrence> recurrences = new List<McRecurrence> ();
                recurrences.Add (new McRecurrence (1));
                recurrences.Add (new McRecurrence (1));
                c.recurrences = recurrences;
            }
            if (type.Equals ("exceptions")) {
                List<McException> exceptions = new List<McException> ();
                exceptions.Add (new McException () {
                    AccountId = c.AccountId,
                    ExceptionStartTime = new DateTime (2011, 3, 17),
                });
                exceptions.Add (new McException () {
                    AccountId = c.AccountId,
                    ExceptionStartTime = new DateTime (2011, 3, 18),
                });
                c.exceptions = exceptions;
            }

            c.Insert ();
            var e = McCalendar.QueryById<McCalendar> (c.Id);

            if (type.Equals ("attendees")) {
                Assert.AreEqual (2, c.attendees.Count);
                Assert.AreEqual (2, e.attendees.Count);
            }
            if (type.Equals ("categories")) {
                Assert.AreEqual (2, c.categories.Count);
                Assert.AreEqual (2, e.categories.Count);
            }
            if (type.Equals ("recurs")) {
                Assert.AreEqual (2, c.recurrences.Count);
                Assert.AreEqual (2, e.recurrences.Count);
            }
            if (type.Equals ("exceptions")) {
                Assert.AreEqual (2, c.exceptions.Count);
                var list = c.QueryRelatedExceptions ();
                Assert.AreEqual (2, list.Count);
            }

            return c;
        }

        [Test]
        public void CreateNcCalendarAttendeeAdd ()
        {
            var c = InsertSimpleEvent ("attendees");

            var attendees = new List<McAttendee> (c.attendees);
            attendees.Add (new McAttendee (1, "Harry", "harry@foo.com") {
                AttendeeType = NcAttendeeType.Resource,
            });
            c.attendees = attendees;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (3, c.attendees.Count);
            Assert.AreEqual (3, f.attendees.Count);
        }

        [Test]
        public void CreateNcCalendarAttendeeDelete ()
        {
            var c = InsertSimpleEvent ("attendees");

            var attendees = new List<McAttendee> (c.attendees);
            attendees.RemoveAt (0);
            c.attendees = attendees;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (1, c.attendees.Count);
            Assert.AreEqual (1, f.attendees.Count);
        }

        [Test]
        public void CreateNcCalendarAttendeeClear ()
        {
            var c = InsertSimpleEvent ("attendees");

            c.attendees = new List<McAttendee> ();
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (0, c.attendees.Count);
            Assert.AreEqual (0, f.attendees.Count);
        }

        [Test]
        public void CreateNcCalendarCategoryAdd ()
        {
            var c = InsertSimpleEvent ("categories");

            var categories = new List<McCalendarCategory> (c.categories);
            categories.Add (new McCalendarCategory (1, "green"));
            c.categories = categories;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (3, c.categories.Count);
            Assert.AreEqual (3, f.categories.Count);
        }

        [Test]
        public void CreateNcCalendarCategoryDelete ()
        {
            var c = InsertSimpleEvent ("categories");

            var categories = new List<McCalendarCategory> (c.categories);
            categories.RemoveAt (0);
            c.categories = categories;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (1, c.categories.Count);
            Assert.AreEqual (1, f.categories.Count);
        }

        [Test]
        public void CreateNcCalendarCategoryClear ()
        {
            var c = InsertSimpleEvent ("categories");

            c.categories = new List<McCalendarCategory> ();
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (0, c.categories.Count);
            Assert.AreEqual (0, f.categories.Count);
        }


        [Test]
        public void CreateNcCalendarRecurrenceAdd ()
        {
            var c = InsertSimpleEvent ("recurs");

            var recurrences = new List<McRecurrence> (c.recurrences);
            recurrences.Add (new McRecurrence (1));
            c.recurrences = recurrences;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (3, c.recurrences.Count);
            Assert.AreEqual (3, f.recurrences.Count);
        }

        [Test]
        public void CreateNcCalendarRecurrenceDelete ()
        {
            var c = InsertSimpleEvent ("recurs");

            var recurrences = new List<McRecurrence> (c.recurrences);
            recurrences.RemoveAt (0);
            c.recurrences = recurrences;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (1, c.recurrences.Count);
            Assert.AreEqual (1, f.recurrences.Count);
        }

        [Test]
        public void CreateNcCalendarRecurrenceClear ()
        {
            var c = InsertSimpleEvent ("recurs");

            c.recurrences = new List<McRecurrence> ();
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (0, c.recurrences.Count);
            Assert.AreEqual (0, f.recurrences.Count);
        }

        [Test]
        public void CreateNcCalendarExceptionAdd ()
        {
            var c = InsertSimpleEvent ("exceptions");

            var exceptions = new List<McException> (c.exceptions);
            exceptions.Add (new McException () { AccountId = c.AccountId });
            c.exceptions = exceptions;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (3, c.exceptions.Count);
            f.exceptions = f.QueryRelatedExceptions ();
            Assert.AreEqual (3, f.exceptions.Count);
        }

        [Test]
        public void CreateNcCalendarExceptionDelete ()
        {
            var c = InsertSimpleEvent ("exceptions");

            var exceptions = new List<McException> (c.exceptions);
            exceptions.RemoveAt (0);
            c.exceptions = exceptions;
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (1, c.exceptions.Count);
            f.exceptions = f.QueryRelatedExceptions ();
            Assert.AreEqual (1, f.exceptions.Count);
        }

        [Test]
        public void CreateNcCalendarExceptionClear ()
        {
            var c = InsertSimpleEvent ("exceptions");

            c.exceptions = new List<McException> ();
            c.Update ();
            var f = McCalendar.QueryById<McCalendar> (c.Id);
            Assert.AreEqual (0, c.exceptions.Count);
            f.exceptions = f.QueryRelatedExceptions ();
            Assert.AreEqual (0, f.exceptions.Count);
        }

        [Test]
        public void QueryExceptionDateLimits ()
        {
            var e0 = McException.QueryForExceptionId (0, DateTime.MinValue);
            Assert.AreEqual (0, e0.Count);
            var e1 = McException.QueryForExceptionId (1, DateTime.MinValue);
            Assert.AreEqual (0, e1.Count);
            var e2 = McException.QueryForExceptionId (1, DateTime.MaxValue);
            Assert.AreEqual (0, e2.Count);

            var c = InsertSimpleEvent ("exceptions");
            var e3 = McException.QueryForExceptionId (c.Id, DateTime.MinValue);
            Assert.AreEqual (0, e3.Count);
            var e4 = McException.QueryForExceptionId (c.Id, DateTime.MaxValue);
            Assert.AreEqual (0, e4.Count);
        }

        [Test]
        public void QueryExceptionDuplicate ()
        {
            var c = InsertSimpleEvent ("exceptions");
            foreach (var e in c.exceptions) {
                e.ExceptionStartTime = new DateTime (2011, 3, 17);
            }
            c.Update ();
            var e5 = McException.QueryForExceptionId (c.Id, new DateTime (2011, 3, 17));
            Assert.IsNotNull (e5);
        }

        [Test]
        public void iCalParse ()
        {
            IICalendar iCal;
            using (var stringReader = new StringReader (ical_string01_good)) {
                iCal = iCalendar.LoadFromStream (stringReader) [0];
            }

//            Issue https://github.com/nachocove/NachoClientX/issues/1298
//            using (var stringReader = new StringReader (ical_string01_bad)) {
//                iCal = iCalendar.LoadFromStream (stringReader) [0];
//            }

        }

        String ical_string01_good = @"
BEGIN:VCALENDAR
METHOD:REQUEST
PRODID:Microsoft Exchange Server 2010
VERSION:2.0
BEGIN:VTIMEZONE
TZID:Greenwich Standard Time
BEGIN:STANDARD
DTSTART:16010101T000000
TZOFFSETFROM:+0000
TZOFFSETTO:+0000
END:STANDARD
BEGIN:DAYLIGHT
DTSTART:16010101T000000
TZOFFSETFROM:+0000
TZOFFSETTO:+0000
END:DAYLIGHT
END:VTIMEZONE
BEGIN:VEVENT
ORGANIZER;CN=Cole Britton:MAILTO:coleb@nachocove.com
ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE;CN=timedout@d
 2.officeburrito.com:MAILTO:timedout@d2.officeburrito.com
DESCRIPTION;LANGUAGE=en-US:Your meeting was found to be out of date and has
  been automatically updated.\n\n________________________________\nSent by 
 Microsoft Exchange Server 2013\n
UID:F0913F5194204D4B8E9EC350258932E4
SUMMARY;LANGUAGE=en-US:Fw: Zurfs ⬆️ 🏄
DTSTART;TZID=Greenwich Standard Time:20150110T190000
DTEND;TZID=Greenwich Standard Time:20150110T200000
CLASS:PUBLIC
PRIORITY:5
DTSTAMP:20150109T191927Z
TRANSP:OPAQUE
STATUS:CONFIRMED
SEQUENCE:1
LOCATION;LANGUAGE=en-US:Da beach
X-MICROSOFT-CDO-APPT-SEQUENCE:1
X-MICROSOFT-CDO-OWNERAPPTID:2112965529
X-MICROSOFT-CDO-BUSYSTATUS:FREE
X-MICROSOFT-CDO-INTENDEDSTATUS:FREE
X-MICROSOFT-CDO-ALLDAYEVENT:FALSE
X-MICROSOFT-CDO-IMPORTANCE:1
X-MICROSOFT-CDO-INSTTYPE:0
X-MICROSOFT-DISALLOW-COUNTER:FALSE
BEGIN:VALARM
DESCRIPTION:REMINDER
TRIGGER;RELATED=START:-PT15M
ACTION:DISPLAY
END:VALARM
END:VEVENT
END:VCALENDAR
";

        String ical_string01_bad = @"
BEGIN:VCALENDAR
METHOD:REQUEST
PRODID:Microsoft Exchange Server 2010
VERSION:2.0
BEGIN:VTIMEZONE
TZID:Greenwich Standard Time
BEGIN:STANDARD
DTSTART:16010101T000000
TZOFFSETFROM:+0000
TZOFFSETTO:+0000
END:STANDARD
BEGIN:DAYLIGHT
DTSTART:16010101T000000
TZOFFSETFROM:+0000
TZOFFSETTO:+0000
END:DAYLIGHT
END:VTIMEZONE
BEGIN:VEVENT
ORGANIZER;CN=Cole Britton:MAILTO:coleb@nachocove.com
ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE;CN=timedout@d
 2.officeburrito.com:MAILTO:timedout@d2.officeburrito.com
DESCRIPTION;LANGUAGE=en-US:Your meeting was found to be out of date and has
  been automatically updated.\n\n________________________________\nSent by 
 Microsoft Exchange Server 2013\n
UID:F0913F5194204D4B8E9EC350258932E4
SUMMARY;LANGUAGE=en-US:Zurfs  <�
DTSTART;TZID=Greenwich Standard Time:20150110T190000
DTEND;TZID=Greenwich Standard Time:20150110T200000
CLASS:PUBLIC
PRIORITY:5
DTSTAMP:20150109T191927Z
TRANSP:OPAQUE
STATUS:CONFIRMED
SEQUENCE:1
LOCATION;LANGUAGE=en-US:Da beach
X-MICROSOFT-CDO-APPT-SEQUENCE:1
X-MICROSOFT-CDO-OWNERAPPTID:2112965529
X-MICROSOFT-CDO-BUSYSTATUS:FREE
X-MICROSOFT-CDO-INTENDEDSTATUS:FREE
X-MICROSOFT-CDO-ALLDAYEVENT:FALSE
X-MICROSOFT-CDO-IMPORTANCE:1
X-MICROSOFT-CDO-INSTTYPE:0
X-MICROSOFT-DISALLOW-COUNTER:FALSE
BEGIN:VALARM
DESCRIPTION:REMINDER
TRIGGER;RELATED=START:-PT15M
ACTION:DISPLAY
END:VALARM
END:VEVENT
END:VCALENDAR
";


        String addString_01 = @"
                <Add xmlns=""AirSync"">
                  <ServerId>beb8a513-a054-4829-a3c8-81fc27bf9033</ServerId>
                  <ApplicationData>
                    <Body xmlns=""AirSyncBase"">
                      <Type>1</Type>
                      <Data> </Data>
                    </Body>
                    <DtStamp xmlns=""Calendar"">20131126T124929Z</DtStamp>
                    <StartTime xmlns=""Calendar"">20131128T010000Z</StartTime>
                    <EndTime xmlns=""Calendar"">20131129T020000Z</EndTime>
                    <Location xmlns=""Calendar"">the Dogg House!</Location>
                    <Subject xmlns=""Calendar"">Big dog party at the Dogg House!</Subject>
                    <UID xmlns=""Calendar"">3rrr5stn6eld9qmv8dviolj3u0@google.com</UID>
                    <Sensitivity xmlns=""Calendar"">0</Sensitivity>
                    <BusyStatus xmlns=""Calendar"">2</BusyStatus>
                    <AllDayEvent xmlns=""Calendar"">0</AllDayEvent>
                    <Reminder xmlns=""Calendar"">10</Reminder>
                    <MeetingStatus xmlns=""Calendar"">0</MeetingStatus>
                    <TimeZone xmlns=""Calendar"">WAIAAEgAUwBUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==</TimeZone>
                    <OrganizerEmail xmlns=""Calendar"">steves@nachocove.com</OrganizerEmail>
                    <OrganizerName xmlns=""Calendar"">Steve Scalpone</OrganizerName>
                    <Attendees xmlns=""Calendar"">
                      <Attendee>
                        <Email>sscalpone@gmail.com</Email>
                        <Name>Steve Scalpone</Name>
                        <AttendeeStatus>5</AttendeeStatus>
                      </Attendee>
                      <Attendee>
                        <Email>rascal2210@hotmail.com</Email>
                        <Name>rascal2210@hotmail.com</Name>
                        <AttendeeStatus>5</AttendeeStatus>
                      </Attendee>
                      <Attendee>
                        <Email>steves@nachocove.com</Email>
                        <Name>Steve Scalpone</Name>
                        <AttendeeStatus>3</AttendeeStatus>
                      </Attendee>
                      <Attendee>
                        <Email>rascal2210@yahoo.com</Email>
                        <Name>rascal2210@yahoo.com</Name>
                        <AttendeeStatus>5</AttendeeStatus>
                      </Attendee>
                    </Attendees>
                  </ApplicationData>
                </Add>
        ";
        String addString_02 = @"
            <Add xmlns=""AirSync"">
              <ServerId>40f792c2-1370-44dc-ba9a-2eab5db56102</ServerId>
              <ApplicationData>
                <Body xmlns=""AirSyncBase"">
                  <Type>1</Type>
                  <Data> </Data>
                </Body>
                <DtStamp xmlns=""Calendar"">20191203T172804Z</DtStamp>
                <StartTime xmlns=""Calendar"">20191204T120000Z</StartTime>
                <EndTime xmlns=""Calendar"">20191204T130000Z</EndTime>
                <Subject xmlns=""Calendar"">Re-dog</Subject>
                <UID xmlns=""Calendar"">7j5do4kr7q8fi67ubq7bdpr01c@google.com</UID>
                <Sensitivity xmlns=""Calendar"">0</Sensitivity>
                <BusyStatus xmlns=""Calendar"">2</BusyStatus>
                <AllDayEvent xmlns=""Calendar"">0</AllDayEvent>
                <Reminder xmlns=""Calendar"">10</Reminder>
                <MeetingStatus xmlns=""Calendar"">0</MeetingStatus>
                <TimeZone xmlns=""Calendar"">4AEAAFAAUwBUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAIAAAAAAAAAAAAAAFAARABUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAIAAAAAAAAAxP///w==</TimeZone>
                <OrganizerEmail xmlns=""Calendar"">steves@nachocove.com</OrganizerEmail>
                <OrganizerName xmlns=""Calendar"">Steve Scalpone</OrganizerName>
                <Recurrence xmlns=""Calendar"">
                  <Type>1</Type>
                  <Interval>1</Interval>
                  <DayOfWeek>42</DayOfWeek>
                  <Until>20200108T080000Z</Until>
                </Recurrence>
                <Exceptions xmlns=""Calendar"">
                  <Exception>
                    <DtStamp>20191203T172914Z</DtStamp>
                    <StartTime>20191204T120000Z</StartTime>
                    <EndTime>20191204T130000Z</EndTime>
                    <Subject>Re-dog</Subject>
                    <Sensitivity>0</Sensitivity>
                    <BusyStatus>2</BusyStatus>
                    <AllDayEvent>0</AllDayEvent>
                    <MeetingStatus>0</MeetingStatus>
                    <Body xmlns=""AirSyncBase"">
                      <Type>1</Type>
                      <Data> </Data>
                    </Body>
                    <ExceptionStartTime>20191204T120000Z</ExceptionStartTime>
                  </Exception>
                  <Exception>
                    <DtStamp>20191203T172843Z</DtStamp>
                    <StartTime>20191206T120000Z</StartTime>
                    <EndTime>20191206T130000Z</EndTime>
                    <Subject>Re-dog</Subject>
                    <Sensitivity>0</Sensitivity>
                    <BusyStatus>2</BusyStatus>
                    <AllDayEvent>0</AllDayEvent>
                    <MeetingStatus>0</MeetingStatus>
                    <Body xmlns=""AirSyncBase"">
                      <Type>1</Type>
                      <Data> </Data>
                    </Body>
                    <Deleted>1</Deleted>
                    <ExceptionStartTime>20191206T120000Z</ExceptionStartTime>
                  </Exception>
                </Exceptions>
              </ApplicationData>
            </Add>
            ";
        String addString_03 = @"
            <Add xmlns=""AirSync"">
              <ServerId>1:1</ServerId>
              <ApplicationData>
                <TimeZone xmlns=""Calendar"">4AEAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAsAAAABAAIAAAAAAAAAAAAAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAMAAAACAAIAAAAAAAAAxP///w==</TimeZone>
                <DtStamp xmlns=""Calendar"">20140107T193959Z</DtStamp>
                <StartTime xmlns=""Calendar"">20140110T200000Z</StartTime>
                <Subject xmlns=""Calendar"">Test event #2</Subject>
                <UID xmlns=""Calendar"">040000008200E00074C5B7101A82E00800000000743B700EE00BCF010000000000000000100000007FB086F7F2E23E4889D76035FFF1CE41</UID>
                <OrganizerName xmlns=""Calendar"">steve scalpone</OrganizerName>
                <OrganizerEmail xmlns=""Calendar"">steves@nac01.com</OrganizerEmail>
                <Attendees xmlns=""Calendar"">
                  <Attendee>
                    <Email>jeffe@nac01.com</Email>
                    <Name>jeff enderwick</Name>
                    <AttendeeStatus>0</AttendeeStatus>
                    <AttendeeType>1</AttendeeType>
                  </Attendee>
                  <Attendee>
                    <Email>steves@nachocove.com</Email>
                    <Name>steves@nachocove.com</Name>
                    <AttendeeStatus>0</AttendeeStatus>
                    <AttendeeType>1</AttendeeType>
                  </Attendee>
                </Attendees>
                <Location xmlns=""Calendar"">Online Meeting</Location>
                <EndTime xmlns=""Calendar"">20140110T203000Z</EndTime>
                <Body xmlns=""AirSyncBase"">
                  <Type>3</Type>
                  <EstimatedDataSize>271</EstimatedDataSize>
                  <Truncated>1</Truncated>
                </Body>
                <Categories xmlns=""Calendar"">
                  <Category>Green category</Category>
                </Categories>
                <Sensitivity xmlns=""Calendar"">0</Sensitivity>
                <BusyStatus xmlns=""Calendar"">2</BusyStatus>
                <AllDayEvent xmlns=""Calendar"">0</AllDayEvent>
                <Reminder xmlns=""Calendar"">15</Reminder>
                <MeetingStatus xmlns=""Calendar"">1</MeetingStatus>
                <NativeBodyType xmlns=""AirSyncBase"">3</NativeBodyType>
                <ResponseRequested xmlns=""Calendar"">1</ResponseRequested>
                <ResponseType xmlns=""Calendar"">1</ResponseType>
                <OnlineMeetingConfLink xmlns=""Calendar"">sip:steves@nac01.com;gruu;opaque=app:conf:focus:id:VI98EBFU</OnlineMeetingConfLink>
                <OnlineMeetingExternalLink xmlns=""Calendar"">https://meet.lync.com/nac01-com/steves/VI98EBFU</OnlineMeetingExternalLink>
              </ApplicationData>
            </Add>
        ";

        String addString_04 = @"
          <Add xmlns=""AirSync"">
          <ServerId>5:67</ServerId>
          <ApplicationData>
            <Timezone xmlns=""Calendar"">4AEAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAsAAAABAAIAAAAAAAAAAAAAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAMAAAACAAIAAAAAAAAAxP///w==</Timezone>
            <DtStamp xmlns=""Calendar"">20190811T141138Z</DtStamp>
            <StartTime xmlns=""Calendar"">20190826T150000Z</StartTime>
            <Subject xmlns=""Calendar"">R Weekly</Subject>
            <UID xmlns=""Calendar"">040000008200E00074C5B7101A82E00800000000F08BBD2A6EB5CF010000000000000000100000006FF9F5F91D67394BAC4C77BAA41B1436</UID>
            <OrganizerName xmlns=""Calendar"">Steve Scalpone</OrganizerName>
            <OrganizerEmail xmlns=""Calendar"">steves@nachocove.com</OrganizerEmail>
            <Location xmlns=""Calendar"" />
            <EndTime xmlns=""Calendar"">20190826T153000Z</EndTime>
            <Recurrence xmlns=""Calendar"">
              <Type>1</Type>
              <Interval>1</Interval>
              <DayOfWeek>4</DayOfWeek>
              <FirstDayOfWeek>0</FirstDayOfWeek>
            </Recurrence>
            <Body xmlns=""AirSyncBase"">
              <Type>4</Type>
              <EstimatedDataSize>12457</EstimatedDataSize>
              <Data nacho-body-id=""1"" />
            </Body>
            <Sensitivity xmlns=""Calendar"">0</Sensitivity>
            <BusyStatus xmlns=""Calendar"">2</BusyStatus>
            <AllDayEvent xmlns=""Calendar"">0</AllDayEvent>
            <Reminder xmlns=""Calendar"">15</Reminder>
            <MeetingStatus xmlns=""Calendar"">0</MeetingStatus>
            <NativeBodyType xmlns=""AirSyncBase"">3</NativeBodyType>
            <ResponseRequested xmlns=""Calendar"">1</ResponseRequested>
            <ResponseType xmlns=""Calendar"">1</ResponseType>
          </ApplicationData>
        </Add>
";

        String changeString_04 = @"
        <Change xmlns=""AirSync"">
          <ServerId>5:67</ServerId>
          <ApplicationData>
            <Timezone xmlns=""Calendar"">4AEAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAsAAAABAAIAAAAAAAAAAAAAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAMAAAACAAIAAAAAAAAAxP///w==</Timezone>
            <DtStamp xmlns=""Calendar"">20190811T141209Z</DtStamp>
            <StartTime xmlns=""Calendar"">20190826T150000Z</StartTime>
            <Subject xmlns=""Calendar"">R Weekly</Subject>
            <UID xmlns=""Calendar"">040000008200E00074C5B7101A82E00800000000F08BBD2A6EB5CF010000000000000000100000006FF9F5F91D67394BAC4C77BAA41B1436</UID>
            <OrganizerName xmlns=""Calendar"">Steve Scalpone</OrganizerName>
            <OrganizerEmail xmlns=""Calendar"">steves@nachocove.com</OrganizerEmail>
            <Location xmlns=""Calendar"" />
            <EndTime xmlns=""Calendar"">20190826T153000Z</EndTime>
            <Recurrence xmlns=""Calendar"">
              <Type>1</Type>
              <Interval>1</Interval>
              <DayOfWeek>4</DayOfWeek>
              <FirstDayOfWeek>0</FirstDayOfWeek>
            </Recurrence>
            <Body xmlns=""AirSyncBase"">
              <Type>4</Type>
              <EstimatedDataSize>21111</EstimatedDataSize>
              <Data nacho-body-id=""1"" />
            </Body>
            <Sensitivity xmlns=""Calendar"">0</Sensitivity>
            <BusyStatus xmlns=""Calendar"">2</BusyStatus>
            <AllDayEvent xmlns=""Calendar"">0</AllDayEvent>
            <Reminder xmlns=""Calendar"">15</Reminder>
            <Exceptions xmlns=""Calendar"">
              <Exception>
                <StartTime>20190903T190000Z</StartTime>
                <Subject>R Weekly (X)</Subject>
                <EndTime>20190903T193000Z</EndTime>
                <Body xmlns=""AirSyncBase"">
                  <Type>4</Type>
                  <EstimatedDataSize>4223</EstimatedDataSize>
                  <Data nacho-body-id=""1"" />
                </Body>
                <Categories />
                <ExceptionStartTime>20190902T150000Z</ExceptionStartTime>
                <OnlineMeetingConfLink />
                <OnlineMeetingExternalLink />
              </Exception>
            </Exceptions>
            <MeetingStatus xmlns=""Calendar"">0</MeetingStatus>
            <NativeBodyType xmlns=""AirSyncBase"">3</NativeBodyType>
            <ResponseRequested xmlns=""Calendar"">1</ResponseRequested>
            <ResponseType xmlns=""Calendar"">1</ResponseType>
          </ApplicationData>
        </Change>
";

        string Exception_String_01 = @"
<Exceptions xmlns=""Calendar"">
  <Exception>
    <DtStamp>20140811T060455Z</DtStamp>
    <StartTime>20140811T210000Z</StartTime>
    <Location>Skype</Location>
    <EndTime>20140811T213000Z</EndTime>
    <Body xmlns=""AirSyncBase"">
      <Type>4</Type>
      <EstimatedDataSize>4211</EstimatedDataSize>
      <Data nacho-body-id=""1"" />
    </Body>
    <Categories />
    <ExceptionStartTime>20140811T160000Z</ExceptionStartTime>
    <Reminder />
    <Attendees>
      <Attendee>
        <Email>nerds@nachocove.com</Email>
        <Name>Nacho Nerds</Name>
        <AttendeeType>1</AttendeeType>
      </Attendee>
      <Attendee>
        <Email>zachq@nachocove.com</Email>
        <Name>Zach Quiring</Name>
        <AttendeeType>2</AttendeeType>
      </Attendee>
      <Attendee>
        <Email>coleb@nachocove.com</Email>
        <Name>Cole Britton</Name>
        <AttendeeType>2</AttendeeType>
      </Attendee>
      <Attendee>
        <Email>henryk@nachocove.com</Email>
        <Name>Henry Kwok</Name>
        <AttendeeType>2</AttendeeType>
      </Attendee>
    </Attendees>
    <AppointmentReplyTime>20140811T062113Z</AppointmentReplyTime>
    <OnlineMeetingConfLink />
    <MeetingStatus>3</MeetingStatus>
    <OnlineMeetingExternalLink />
  </Exception>
</Exceptions>
";
    }
}


