using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;

namespace Test.iOS
{
    [TestFixture]
    public class NcCalendarTest
    {
        NachoCore.Model.NcCalendar c = new NachoCore.Model.NcCalendar ();

        public void GoodCompactDateTime (string compactDateTime, DateTime match)
        {
            var d = c.ParseCompactDateTime (compactDateTime);
            Assert.False (d.Equals (DateTime.MinValue));
            Assert.True (d.Equals (match));
        }

        public void BadCompactDateTime (string compactDateTime)
        {
            var d = c.ParseCompactDateTime (compactDateTime);
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
            var t = c.DecodeTimeZone (encodedTimeZone);
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
            var t = c.DecodeTimeZone (encodedTimeZone);
            Assert.IsNull (t);
        }

        public void GoodExtractStringFromTimeZone (string s)
        {
            byte[] b = new byte[64];
            int l = s.Length * sizeof(char);
            Array.Clear (b, 0, 64);
            System.Buffer.BlockCopy (s.ToCharArray (), 0, b, 0, l);
            var e = c.ExtractStringFromTimeZone (b, 0, l);
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
        public void NewEntryWithAdd()
        {
            var asSync = new NachoCore.ActiveSync.AsSyncCommand(null);        
            var command = System.Xml.Linq.XElement.Parse (addString_01);
            Assert.IsNotNull (command);
            Assert.AreEqual(command.Name.LocalName, Xml.AirSync.Add);
            asSync.AddEvent (command, null);
        }

//        [Test]
        public void UpdateEntryWithAdd()
        {
//            asSync.UpdateEvent (addString_02, null);
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


