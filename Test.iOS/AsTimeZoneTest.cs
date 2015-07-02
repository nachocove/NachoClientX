//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.ObjectModel;
using NachoCore;
using NachoCore.ActiveSync;
using NUnit.Framework;

namespace Test.iOS
{
    [TestFixture]
    public class AsTimeZoneTest
    {
        public AsTimeZoneTest ()
        {
        }

        [Test]
        public void SystemTime_01 ()
        {
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (1, 0, 0, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (1, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 1, 0, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 1, 0, 0, 0, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 1, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 1, 0, 0, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 1, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 1, 0, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 1, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 1, 0, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 1, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 1, 0, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 1, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 1, 0));
            Assert.AreEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 1), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 1));

            Assert.AreNotEqual (new AsTimeZone.SystemTime (1, 0, 0, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 1, 0, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 1, 0, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 0, 1, 0, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 1, 0, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 1, 0, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 1, 0), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
            Assert.AreNotEqual (new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 1), new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0));
        }

        [Test]
        public void Basic_01 ()
        {
            string data = @"4AEAACgARwBNAFQALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAsAAAABAAIAAAAAAAAAAAAAACgARwBNAFQALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAMAAAACAAIAAAAAAAAAxP///w==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (8 * 60, tz.Bias);
            Assert.AreEqual (-60, tz.DaylightBias);
            Assert.AreEqual ("(GMT-08:00) Pacific Time (US & C", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("(GMT-08:00) Pacific Time (US & C", tz.StandardName);
            var dstStart = new AsTimeZone.SystemTime (0, 3, 0, 2, 2, 0, 0, 0);
            Assert.AreEqual (dstStart, tz.DaylightDate);
            var dstFinish = new AsTimeZone.SystemTime (0, 11, 0, 1, 2, 0, 0, 0);
            Assert.AreEqual (dstFinish, tz.StandardDate);
            var data2 = tz.toEncodedTimeZone ();
            Assert.AreEqual (data, data2);
            var tz2 = new AsTimeZone (data2);
            Assert.AreEqual (tz, tz2);
        }

        [Test]
        public void Basic_02 ()
        {
            string data = @"4AEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAMAAAAAAAAAxP///w==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (8 * 60, tz.Bias);
            Assert.AreEqual (-60, tz.DaylightBias);
            Assert.AreEqual ("", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("", tz.StandardName);
            var dstStart = new AsTimeZone.SystemTime (0, 3, 0, 2, 3, 0, 0, 0);
            Assert.AreEqual (dstStart, tz.DaylightDate);
            var dstFinish = new AsTimeZone.SystemTime (0, 11, 0, 1, 1, 0, 0, 0);
            Assert.AreEqual (dstFinish, tz.StandardDate);
            var data2 = tz.toEncodedTimeZone ();
            Assert.AreEqual (data, data2);
            var tz2 = new AsTimeZone (data2);
            Assert.AreEqual (tz, tz2);
        }

        [Test]
        public void Basic_03 ()
        {
            string data = @"4AEAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAsAAAABAAIAAAAAAAAAAAAAACgAVQBUAEMALQAwADgAOgAwADAAKQAgAFAAYQBjAGkAZgBpAGMAIABUAGkAbQBlACAAKABVAFMAIAAmACAAQwAAAAMAAAACAAIAAAAAAAAAxP///w==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (8 * 60, tz.Bias);
            Assert.AreEqual (-60, tz.DaylightBias);
            Assert.AreEqual ("(UTC-08:00) Pacific Time (US & C", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("(UTC-08:00) Pacific Time (US & C", tz.StandardName);
            var dstStart = new AsTimeZone.SystemTime (0, 3, 0, 2, 2, 0, 0, 0);
            Assert.AreEqual (dstStart, tz.DaylightDate);
            var dstFinish = new AsTimeZone.SystemTime (0, 11, 0, 1, 2, 0, 0, 0);
            Assert.AreEqual (dstFinish, tz.StandardDate);
            var data2 = tz.toEncodedTimeZone ();
            Assert.AreEqual (data, data2);
            var tz2 = new AsTimeZone (data2);
            Assert.AreEqual (tz, tz2);
        }

        [Test]
        public void Basic_04 ()
        {
            string data = @"AAAAACgAVQBUAEMAKQAgAE0AbwBuAHIAbwB2AGkAYQAsACAAUgBlAHkAawBqAGEAdgBpAGsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACgAVQBUAEMAKQAgAE0AbwBuAHIAbwB2AGkAYQAsACAAUgBlAHkAawBqAGEAdgBpAGsAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (0, tz.Bias);
            Assert.AreEqual (0, tz.DaylightBias);
            Assert.AreEqual ("(UTC) Monrovia, Reykjavik", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("(UTC) Monrovia, Reykjavik", tz.StandardName);
            var dstStart = new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0);
            Assert.AreEqual (dstStart, tz.DaylightDate);
            var dstFinish = new AsTimeZone.SystemTime (0, 0, 0, 0, 0, 0, 0, 0);
            Assert.AreEqual (dstFinish, tz.StandardDate);
            var data2 = tz.toEncodedTimeZone ();
            Assert.AreEqual (data, data2);
            var tz2 = new AsTimeZone (data2);
            Assert.AreEqual (tz, tz2);
        }

        [Test]
        public void Basic_05 ()
        {
            string data = @"4AEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAMAAAAAAAAAxP///w==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (480, tz.Bias);
            Assert.AreEqual (-60, tz.DaylightBias);
            Assert.AreEqual ("", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("", tz.StandardName);
            var dstStart = new AsTimeZone.SystemTime (0, 3, 0, 2, 3, 0, 0, 0);
            Assert.AreEqual (dstStart, tz.DaylightDate);
            var dstFinish = new AsTimeZone.SystemTime (0, 11, 0, 1, 1, 0, 0, 0);
            Assert.AreEqual (dstFinish, tz.StandardDate);
            var data2 = tz.toEncodedTimeZone ();
            Assert.AreEqual (data, data2);
            var tz2 = new AsTimeZone (data2);
            Assert.AreEqual (tz, tz2);
        }

        [Test]
        public void Basic_06 ()
        {
            string data = @"4AEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsAAAABAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAAAACAAMAAAAAAAAAxP///w==";
            var tz = new AsTimeZone (data);
            Assert.AreEqual (480, tz.Bias);
            Assert.AreEqual (-60, tz.DaylightBias);
            Assert.AreEqual ("", tz.DaylightName);
            Assert.AreEqual (0, tz.StandardBias);
            Assert.AreEqual ("", tz.StandardName);

            var ntz = new AsTimeZone (data);
            ntz.Bias = tz.Bias;
            ntz.DaylightBias = tz.DaylightBias;
            ntz.DaylightDate = tz.DaylightDate;
            ntz.DaylightName = tz.DaylightName;
            ntz.StandardBias = tz.StandardBias;
            ntz.StandardDate = tz.StandardDate;
            ntz.StandardName = tz.StandardName;

            var s = ntz.toEncodedTimeZone ();
            Assert.AreEqual (data, s);
        }

        [Test]
        public void TimeZoneInfoTest()
        {
            ReadOnlyCollection<TimeZoneInfo> timeZones = TimeZoneInfo.GetSystemTimeZones(); 

            foreach (TimeZoneInfo timeZone in timeZones) {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById (timeZone.Id);
                var a1 = new AsTimeZone (tzi, DateTime.Now);
                var s1 = a1.toEncodedTimeZone ();
                var a2 = new AsTimeZone (s1);
                var s2 = a2.toEncodedTimeZone ();
                Assert.True (s1.Equals (s2));
            }
        }
    }
}

