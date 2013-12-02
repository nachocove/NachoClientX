//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using SQLite;

namespace Test.iOS
{

    public class TestDb : SQLiteConnectionWithEvents
    {
        public TestDb () : base (System.IO.Path.GetTempFileName (), true)
        {
            // Calendar
            CreateTable<NcCalendar> ();
            DropTable<NcCalendar> ();
            CreateTable<NcCalendar> ();

            // TimeZone
            CreateTable<NcTimeZone> ();
            DropTable<NcTimeZone> ();
            CreateTable<NcTimeZone> ();

            // Attendee
            CreateTable<NcAttendee> ();
            DropTable<NcAttendee> ();
            CreateTable<NcAttendee> ();

            // Categorie
            CreateTable<NcCategory> ();
            DropTable<NcCategory> ();
            CreateTable<NcCategory> ();
        }
    }

    public class MockDataSource : IAsDataSource
    {
        public IProtoControlOwner Owner { set; get; }
        public AsProtoControl Control { set; get; }
        public NcProtocolState ProtocolState { get; set; }
        public NcServer Server { get; set; }
        public NcAccount Account { get; set; }
        public NcCred Cred { get; set; }

        public MockDataSource()
        {
            Owner = new MockProtoControlOwner ();
            Owner.Db = new TestDb ();
        }

    }

        public class MockProtoControlOwner : IProtoControlOwner
    {
        public SQLiteConnectionWithEvents Db { set; get; }
        public string AttachmentsDir { set; get; }

        public void CredReq (ProtoControl sender) {  }
        public void ServConfReq (ProtoControl sender) {  }
        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate) { }
        public void HardFailInd (ProtoControl sender) { }
        public void TempFailInd (ProtoControl sender) { }
        public bool RetryPermissionReq (ProtoControl sender, uint delaySeconds) { return true; }
        public void ServerOOSpaceInd (ProtoControl sender) { }
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
            var ds = new MockDataSource ();
            var asSync = new NachoCore.ActiveSync.AsSyncCommand (ds);        
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            asSync.AddCalendarItem (command, null);
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
            db.CreateTable<NcCategory> ();
            db.DropTable<NcCategory> ();

            // Create a new db
            db.CreateTable<NcCategory> ();

            var c01 = new NcCategory (5, "test");
            db.Insert (c01);

            var c02 = db.Get<NcCategory> (x => x.CalendarId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.CalendarId, 5);
            Assert.AreEqual (c02.Name, "test");

            var c03 = db.Get<NcCategory> (x => x.Name == "test");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.CalendarId, 5);
            Assert.AreEqual (c03.Name, "test");

            c03.Name = "changed";
            db.Update (c03);

            Assert.AreEqual (db.Table<NcCategory> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => db.Get<NcCategory> (x => x.Name == "test"));

            var c05 = db.Get<NcCategory> (x => x.Name == "changed");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.CalendarId, 5);
            Assert.AreEqual (c05.Name, "changed");

            var c06 = new NcCategory (5, "second");
            db.Insert (c06);
            var c07 = new NcCategory (6, "do not see");
            db.Insert (c07);

            Assert.AreEqual (db.Table<NcCategory> ().Count (), 3);

            var c08 = db.Get<NcCategory> (x => x.CalendarId == 5);
            NachoCore.Utils.Log.Info ("c08 {0}", c08.ToString ());

//            // TODO: Implement Query in SQLConnectionWithEvents
//            var c09 = db.Query<NcCategory> ("select * from NcCategory where CalendarId = ?", 5);
//            NachoCore.Utils.Log.Info ("c09 {0}", c09.ToString ());
//            foreach (var c in c09) {
//                Assert.IsTrue (c.Name.Equals ("changed") || c.Name.Equals ("second"));
//            }

            var c10 = db.Table<NcCategory> ().Where (x => x.CalendarId == 5);
            NachoCore.Utils.Log.Info ("c10 {0}", c10.ToString ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("changed") || c.Name.Equals ("second"));
            }
                                  
        }

        [Test]
        public void CalendarAttendee ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<NcAttendee> ();
            db.DropTable<NcAttendee> ();

            // Create a new db
            db.CreateTable<NcAttendee> ();

            var c01 = new NcAttendee (5, "Steve", "rascal2210@hotmail.com");
            db.Insert (c01);

            var c02 = db.Get<NcAttendee> (x => x.CalendarId == 5);
            Assert.IsNotNull (c02);
            Assert.AreEqual (c02.Id, 1);
            Assert.AreEqual (c02.CalendarId, 5);
            Assert.AreEqual (c02.Name, "Steve");
            Assert.AreEqual (c02.Email, "rascal2210@hotmail.com");

            var c03 = db.Get<NcAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c03);
            Assert.AreEqual (c03.Id, 1);
            Assert.AreEqual (c03.CalendarId, 5);
            Assert.AreEqual (c03.Name, "Steve");
            Assert.AreEqual (c03.Email, "rascal2210@hotmail.com");

            c03.Email = "steves@nachocove.com";
            db.Update (c03);

            Assert.AreEqual (db.Table<NcAttendee> ().Count (), 1);

            Assert.Throws<System.InvalidOperationException> (() => db.Get<NcAttendee> (x => x.Email == "rascal2210@hotmail.com"));

            var c05 = db.Get<NcAttendee> (x => x.Name == "Steve");
            Assert.IsNotNull (c05);
            Assert.AreEqual (c05.Id, 1);
            Assert.AreEqual (c05.CalendarId, 5);
            Assert.AreEqual (c05.Name, "Steve");
            Assert.AreEqual (c05.Email, "steves@nachocove.com");

            var c05a = db.Get<NcAttendee> (x => x.Email == "steves@nachocove.com");
            Assert.IsNotNull (c05a);
            Assert.AreEqual (c05a.Id, 1);
            Assert.AreEqual (c05a.CalendarId, 5);
            Assert.AreEqual (c05a.Name, "Steve");
            Assert.AreEqual (c05a.Email, "steves@nachocove.com");


            var c06 = new NcAttendee (5, "Chris", "chrisp@nachocove.com");
            db.Insert (c06);
            var c07 = new NcAttendee (6, "Jeff", "jeffe@nachocove.com");
            db.Insert (c07);

            Assert.AreEqual (db.Table<NcAttendee> ().Count (), 3);

            var c08 = db.Get<NcAttendee> (x => x.CalendarId == 5);
            NachoCore.Utils.Log.Info ("c08 {0}", c08.ToString ());

//            // TODO: implement Query in SQLConnectionWithEvents
//            var c09 = db.Query<NcAttendee> ("select * from NcAttendee where CalendarId = ?", 5);
//            NachoCore.Utils.Log.Info ("c09 {0}", c09.ToString ());
//            foreach (var c in c09) {
//                Assert.IsTrue (c.Name.Equals ("Steve") || c.Name.Equals ("Chris"));
//            }

            var c10 = db.Table<NcAttendee> ().Where (x => x.CalendarId == 5);
            NachoCore.Utils.Log.Info ("c10 {0}", c10.ToString ());
            foreach (var c in c10) {
                Assert.IsTrue (c.Name.Equals ("Steve") || c.Name.Equals ("Chris"));
            }

        }

        [Test]
        public void CalendarTimezoneDB ()
        {
            TestDb db = new TestDb ();

            // Start with a clean db
            db.CreateTable<NcTimeZone> ();
            db.DropTable<NcTimeZone> ();

            // Create a new db
            db.CreateTable<NcTimeZone> ();

            NcTimeZone t01 = new NcTimeZone ();
            t01.Bias = 10;
            t01.DaylightBias = 11;
            t01.StandardBias = 12;
            t01.DaylightDate = new DateTime (2013, 1, 1, 1, 1, 1, 111);
            t01.StandardDate = new DateTime (1013, 1, 1, 1, 1, 1, 222);
            t01.DaylightName = "Daylight Name 10";
            t01.StandardName = "Standard Name 10";
            db.Insert (t01);

            NcTimeZone t02 = new NcTimeZone ();
            t02.Bias = 20;
            t02.DaylightBias = 21;
            t02.StandardBias = 22;
            t02.DaylightDate = new DateTime (2013, 2, 1, 1, 1, 1, 111);
            t02.StandardDate = new DateTime (2013, 2, 2, 2, 2, 2, 222);
            t02.DaylightName = "Daylight Name 20";
            t02.StandardName = "Standard Name 20";
            db.Insert (t02);

            Assert.AreEqual (db.Table<NcTimeZone> ().Count (), 2);

            NcTimeZone t03 = db.Get<NcTimeZone> (x => x.Id == 2);
            Assert.AreEqual (t03.StandardName, "Standard Name 20");
            Assert.AreEqual (t03.StandardDate, new DateTime (2013, 2, 2, 2, 2, 2, 222));

            t03.DaylightName = "New Daylight Name 20";
            t03.DaylightDate = new DateTime (2013, 2, 1, 1, 1, 1, 222);
            db.Update (t03);

            Assert.AreEqual (db.Table<NcTimeZone> ().Count (), 2);

            NcTimeZone t04 = db.Get<NcTimeZone> (x => x.Id == 2);
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
            var ds = new MockDataSource ();
            var asSync = new NachoCore.ActiveSync.AsSyncCommand (ds);        
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual (command.Name.LocalName, Xml.AirSync.Add);
            // <ApplicationData>...</ApplicationData>
            var appData = command.Element (asSync.m_ns + Xml.AirSync.ApplicationData);
            Assert.IsNotNull (appData);
            var h = new NachoCore.ActiveSync.AsHelpers ();
            NcResult r = h.CreateNcCalendarFromXML (asSync.m_ns, appData);
            Assert.IsNotNull (r.GetObject ());
            var c = (NcCalendar)r.GetObject ();
            Assert.AreEqual (c.DTStamp, new DateTime (2013, 11, 26, 12, 49, 29));
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
    }
}


