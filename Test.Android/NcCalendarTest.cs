using System;
using NUnit.Framework;
using NachoCore;

namespace Test.iOS
{
    [TestFixture]
    public class NcCalendarTest
    {
        NachoCore.Model.NcCalendar c = new NachoCore.Model.NcCalendar ();

        public void GoodCompactDateTime(string compactDateTime, DateTime match)
        {
            var d = c.ParseCompactDateTime (compactDateTime);
            Assert.False (d.Equals (DateTime.MinValue));
            Assert.True (d.Equals (match));
        }

        public void BadCompactDateTime(string compactDateTime)
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

        public void GoodTimeZone(string encodedTimeZone, string targetStandardName, string targetDaylightName)
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

        public void BadTimeZone(string encodedTimeZone)
        {
            var t = c.DecodeTimeZone (encodedTimeZone);
            Assert.IsNull (t);
        }

        public void GoodExtractStringFromTimeZone(string s)
        {
            byte[] b = new byte[64];
            int l = s.Length * sizeof(char);
            Array.Clear(b, 0, 64);
            System.Buffer.BlockCopy(s.ToCharArray(), 0, b, 0, l);
            var e = c.ExtractStringFromTimeZone(b, 0, l);
            Assert.IsTrue(s.Equals(e));
        }

        [Test]
        public void TimeZoneParsing()
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
    }
}


