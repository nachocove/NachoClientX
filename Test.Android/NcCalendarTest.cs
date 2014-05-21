//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using System.Xml.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using SQLite;

namespace Test.iOS
{
    public class TestDb : SQLiteConnection
    {
        public TestDb () : base (System.IO.Path.GetTempFileName (), true)
        {
            // Calendar
            CreateTable<McCalendar> ();
            DropTable<McCalendar> ();
            CreateTable<McCalendar> ();

            // TimeZone
            CreateTable<McTimeZone> ();
            DropTable<McTimeZone> ();
            CreateTable<McTimeZone> ();

            // Attendee
            CreateTable<McAttendee> ();
            DropTable<McAttendee> ();
            CreateTable<McAttendee> ();

            // Category
            CreateTable<McCalendarCategory> ();
            DropTable<McCalendarCategory> ();
            CreateTable<McCalendarCategory> ();

            // Exception
            CreateTable<McException> ();
            DropTable<McException> ();
            CreateTable<McException> ();

            // Recurrence
            CreateTable<McRecurrence> ();
            DropTable<McRecurrence> ();
            CreateTable<McRecurrence> ();

            // NcContact
            CreateTable<McContact> ();
            DropTable<McContact> ();
            CreateTable<McContact> ();

            // McContactDateAttribute
            CreateTable<McContactDateAttribute> ();
            DropTable<McContactDateAttribute> ();
            CreateTable<McContactDateAttribute> ();

            // McContactStringAttribute
            CreateTable<McContactStringAttribute> ();
            DropTable<McContactStringAttribute> ();
            CreateTable<McContactStringAttribute> ();

            // McContactAddressAttribute
            CreateTable<McContactAddressAttribute> ();
            DropTable<McContactAddressAttribute> ();
            CreateTable<McContactAddressAttribute> ();

            // McFolder
            CreateTable<McFolder> ();
            DropTable<McFolder> ();
            CreateTable<McFolder> ();

            // McMapFolderItem
            CreateTable<McMapFolderFolderEntry> ();
            DropTable<McMapFolderFolderEntry> ();
            CreateTable<McMapFolderFolderEntry> ();

            // McPending
            CreateTable<McPending> ();
            DropTable<McPending> ();
            CreateTable<McPending> ();

            // McBody
            CreateTable<McBody> ();
            DropTable<McBody> ();
            CreateTable<McBody> ();

            // Telemetry
            CreateTable<McTelemetryEvent> ();
            DropTable<McTelemetryEvent> ();
            CreateTable<McTelemetryEvent> ();

            // McServer
            CreateTable<McServer> ();
            DropTable<McServer> ();
            CreateTable<McServer> ();
        }
    }

    public class MockBackEnd
    {
        private static volatile MockBackEnd instance;
        private static object syncRoot = new Object ();

        public static MockBackEnd Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new MockBackEnd ();
                    }
                }
                return instance; 
            }
        }

        public SQLiteConnection Db { set; get; }


        private MockBackEnd ()
        {
            Db = new TestDb ();
        }
    }

    public class MockDataSource : IBEContext
    {
        public IProtoControlOwner Owner { set; get; }

        public AsProtoControl ProtoControl { set; get; }

        public McProtocolState ProtocolState { get; set; }

        public McServer Server { get; set; }

        public McAccount Account { get; set; }

        public McCred Cred { get; set; }

        public MockDataSource ()
        {
            Owner = new MockProtoControlOwner ();
            Account = new McAccount ();
            Account.Id = 1;
            NcModel.Instance.Db = new TestDb ();
        }
    }

    public class MockProtoControlOwner : IProtoControlOwner
    {
        public string AttachmentsDir { set; get; }

        public void CredReq (ProtoControl sender)
        {
        }

        public void ServConfReq (ProtoControl sender)
        {
        }

        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
        {
        }

        public void StatusInd (ProtoControl sender, NcResult status)
        {
        }

        public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
        {
        }

        public void SearchContactsResp (ProtoControl sender, string prefix, string token)
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

    [TestFixture]
    public class NcCalendarTest
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

        public void GoodTimeZone (string encodedTimeZone, string targetStandardName, string targetDaylightName)
        {
            var t = c.ParseAsTimeZone (encodedTimeZone);
            Assert.IsNotNull (t);
            Assert.IsNotNull (t.StandardName);
            Assert.IsNotNull (t.DaylightName);
            Assert.IsTrue (t.StandardName.Length > 0);
            Assert.IsTrue (t.DaylightName.Length > 0);
            Assert.IsTrue (t.StandardName.Equals (targetStandardName));
            Assert.IsTrue (t.DaylightName.Equals (targetDaylightName));
        }

        public void BadTimeZone (string encodedTimeZone)
        {
            var t = c.ParseAsTimeZone (encodedTimeZone);
            Assert.IsNull (t);
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

        [Test]
        public void TimeZoneParsing ()
        {
            string s;
            s = "LAEAAEUAUwBUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAIAAAAAAAAAAAAAAEUARABUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAIAAAAAAAAAxP///w==";
            GoodTimeZone (s, "EST", "EDT");
            s = "4AEAAFAAYQBjAGkAZgBpAGMAIABTAHQAYQBuAGQAYQByAGQAIABUAGkAbQBlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAIAAAAAAAAAAAAAAFAAYQBjAGkAZgBpAGMAIABEAGEAeQBsAGkAZwBoAHQAIABUAGkAbQBlAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAIAAAAAAAAAxP///w==";
            GoodTimeZone (s, "Pacific Standard Time", "Pacific Daylight Time");
            BadTimeZone (null);
            BadTimeZone ("");
            BadTimeZone ("A");
            BadTimeZone ("AAAA");
            s = "abcdefghijklmnopqrstuvwxyz012345";
            for (int i = 1; i <= s.Length; i++) {
                GoodExtractStringFromTimeZone (s.Substring (0, i));
            }
            GoodExtractStringFromTimeZone ("");
        }

        [Test]
        public void NewEntryWithAdd ()
        {
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddCalendarItem (command, new MockNcFolder ());
        }
        //        [Test]
        public void UpdateEntryWithAdd ()
        {
//            asSync.UpdateEvent (addString_02, null);
        }

        [Test]
        public void CalendarCategories ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<McCalendarCategory> ();
            db.DropTable<McCalendarCategory> ();

            // Create a new db
            db.CreateTable<McCalendarCategory> ();

            var c01 = new McCalendarCategory ("test");
            c01.ParentId = 5;
            c01.ParentType = McCalendarCategory.CALENDAR;
            db.Insert (c01);

            var c02 = db.Get<McCalendarCategory> (x => x.ParentId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.ParentId, 5);
            Assert.AreEqual (c02.Name, "test");

            var c03 = db.Get<McCalendarCategory> (x => x.Name == "test");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.ParentId, 5);
            Assert.AreEqual (c03.Name, "test");

            c03.Name = "changed";
            db.Update (c03);

            Assert.AreEqual (db.Table<McCalendarCategory> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => db.Get<McCalendarCategory> (x => x.Name == "test"));

            var c05 = db.Get<McCalendarCategory> (x => x.Name == "changed");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.ParentId, 5);
            Assert.AreEqual (c05.Name, "changed");

            var c06 = new McCalendarCategory ("second");
            c06.ParentId = 5;
            db.Insert (c06);
            var c07 = new McCalendarCategory ("do not see");
            c07.ParentId = 6;
            db.Insert (c07);

            Assert.AreEqual (3, db.Table<McCalendarCategory> ().Count ());

            var c10 = db.Table<McCalendarCategory> ().Where (x => x.ParentId == 5);
            Assert.AreEqual (2, c10.Count ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("changed") || c.Name.Equals ("second"));
            }
                                  
        }

        [Test]
        public void CalendarAttendee ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<McAttendee> ();
            db.DropTable<McAttendee> ();

            // Create a new db
            db.CreateTable<McAttendee> ();

            var c01 = new McAttendee ("Steve", "rascal2210@hotmail.com");
            c01.ParentId = 5;
            db.Insert (c01);

            var c02 = db.Get<McAttendee> (x => x.ParentId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.ParentId, 5);
            Assert.AreEqual (c02.Name, "Steve");
            Assert.AreEqual (c02.Email, "rascal2210@hotmail.com");

            var c03 = db.Get<McAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.ParentId, 5);
            Assert.AreEqual (c03.Name, "Steve");
            Assert.AreEqual (c03.Email, "rascal2210@hotmail.com");

            c03.Email = "steves@nachocove.com";
            db.Update (c03);

            Assert.AreEqual (db.Table<McAttendee> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => db.Get<McAttendee> (x => x.Email == "rascal2210@hotmail.com"));

            var c05 = db.Get<McAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.ParentId, 5);
            Assert.AreEqual (c05.Name, "Steve");
            Assert.AreEqual (c05.Email, "steves@nachocove.com");

            var c05a = db.Get<McAttendee> (x => x.Email == "steves@nachocove.com");
            Assert.IsNotNull (c05a);
            Assert.AreEqual (c05a.Id, 1);
            Assert.AreEqual (c05a.ParentId, 5);
            Assert.AreEqual (c05a.Name, "Steve");
            Assert.AreEqual (c05a.Email, "steves@nachocove.com");


            var c06 = new McAttendee ("Chris", "chrisp@nachocove.com");
            c06.ParentId = 5;
            db.Insert (c06);
            var c07 = new McAttendee ("Jeff", "jeffe@nachocove.com");
            c07.ParentId = 6;
            db.Insert (c07);

            Assert.AreEqual (3, db.Table<McAttendee> ().Count ());

            var c10 = db.Table<McAttendee> ().Where (x => x.ParentId == 5);
            Assert.AreEqual (2, c10.Count ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("Steve") || c.Name.Equals ("Chris"));
            }

        }

        [Test]
        public void CalendarTimezoneDB ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<McTimeZone> ();
            db.DropTable<McTimeZone> ();

            // Create a new db
            db.CreateTable<McTimeZone> ();

            McTimeZone t01 = new McTimeZone ();
            t01.Bias = 10;
            t01.DaylightBias = 11;
            t01.StandardBias = 12;
            t01.DaylightDate = new DateTime (2013, 1, 1, 1, 1, 1, 111);
            t01.StandardDate = new DateTime (1013, 1, 1, 1, 1, 1, 222);
            t01.DaylightName = "Daylight Name 10";
            t01.StandardName = "Standard Name 10";
            db.Insert (t01);

            McTimeZone t02 = new McTimeZone ();
            t02.Bias = 20;
            t02.DaylightBias = 21;
            t02.StandardBias = 22;
            t02.DaylightDate = new DateTime (2013, 2, 1, 1, 1, 1, 111);
            t02.StandardDate = new DateTime (2013, 2, 2, 2, 2, 2, 222);
            t02.DaylightName = "Daylight Name 20";
            t02.StandardName = "Standard Name 20";
            db.Insert (t02);

            Assert.AreEqual (db.Table<McTimeZone> ().Count (), 2);

            McTimeZone t03 = db.Get<McTimeZone> (x => x.Id == 2);
            Assert.AreEqual (t03.StandardName, "Standard Name 20");
            Assert.AreEqual (t03.StandardDate, new DateTime (2013, 2, 2, 2, 2, 2, 222));

            t03.DaylightName = "New Daylight Name 20";
            t03.DaylightDate = new DateTime (2013, 2, 1, 1, 1, 1, 222);
            db.Update (t03);

            Assert.AreEqual (db.Table<McTimeZone> ().Count (), 2);

            McTimeZone t04 = db.Get<McTimeZone> (x => x.Id == 2);
            Assert.AreEqual (t04.DaylightName, "New Daylight Name 20");
            Assert.AreEqual (t04.DaylightDate, new DateTime (2013, 2, 1, 1, 1, 1, 222));
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
            Assert.AreNotEqual (NcMeetingStatus.MeetingCancelled, "0".ParseInteger<NcMeetingStatus> ());
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
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (Ns, command);
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
        }

        [Test]
        public void CreateNcCalendarFromXML2 ()
        {
            var command = System.Xml.Linq.XElement.Parse (addString_02);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (Ns, command);
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
            var command = System.Xml.Linq.XElement.Parse (addString_03);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            XNamespace Ns = Xml.AirSync.Ns;
            NcResult r = h.ParseCalendar (Ns, command);
            Assert.IsNotNull (r.GetValue<McCalendar> ());
            var c = r.GetValue<McCalendar> ();
            Assert.IsNotNull (c);
        }

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
                <DtStamp xmlns=""Calendar"">20131203T172804Z</DtStamp>
                <StartTime xmlns=""Calendar"">20131204T120000Z</StartTime>
                <EndTime xmlns=""Calendar"">20131204T130000Z</EndTime>
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
                  <Until>20140108T080000Z</Until>
                </Recurrence>
                <Exceptions xmlns=""Calendar"">
                  <Exception>
                    <DtStamp>20131203T172914Z</DtStamp>
                    <StartTime>20131204T120000Z</StartTime>
                    <EndTime>20131204T130000Z</EndTime>
                    <Subject>Re-dog</Subject>
                    <Sensitivity>0</Sensitivity>
                    <BusyStatus>2</BusyStatus>
                    <AllDayEvent>0</AllDayEvent>
                    <MeetingStatus>0</MeetingStatus>
                    <Body xmlns=""AirSyncBase"">
                      <Type>1</Type>
                      <Data> </Data>
                    </Body>
                    <ExceptionStartTime>20131204T120000Z</ExceptionStartTime>
                  </Exception>
                  <Exception>
                    <DtStamp>20131203T172843Z</DtStamp>
                    <StartTime>20131206T120000Z</StartTime>
                    <EndTime>20131206T130000Z</EndTime>
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
                    <ExceptionStartTime>20131206T120000Z</ExceptionStartTime>
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
    }
}


