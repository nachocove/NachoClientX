using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class LogTest
    {
        [Test]
        public void LoggingToConsole ()
        {
            // Save the old value
            int save = Log.logLevel;

            // Error

            // Make sure a single filter works
            Log.logLevel = Log.LOG_SYNC;
            Log.Error (Log.LOG_SYNC, "Test no args.");

            // Make sure multiple filters work
            Log.logLevel |= Log.LOG_CALENDAR;
            Log.Error (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            Log.Error (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);

            // Make sure filters block too
            Log.logLevel = Log.LOG_CALENDAR;
            Log.Error (Log.LOG_SYNC, "You should not see this message.");

            // Warnings

            // Make sure a single filter works
            Log.logLevel = Log.LOG_SYNC;
            Log.Warn (Log.LOG_SYNC, "Test no args.");

            // Make sure multiple filters work
            Log.logLevel |= Log.LOG_CALENDAR;
            Log.Warn (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            Log.Warn (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);

            // Make sure filters block too
            Log.logLevel = Log.LOG_CALENDAR;
            Log.Warn (Log.LOG_SYNC, "You should not see this message.");

            // Info

            // Make sure a single filter works
            Log.logLevel = Log.LOG_SYNC;
            Log.Info (Log.LOG_SYNC, "Test no args.");

            // Make sure multiple filters work
            Log.logLevel |= Log.LOG_CALENDAR;
            Log.Info (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            Log.Info (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);

            // Make sure filters block too
            Log.logLevel = Log.LOG_CALENDAR;
            Log.Info (Log.LOG_SYNC, "You should not see this message.");

            // Restore the old
            Log.logLevel = save;
            Assert.True (true);
        }
    }
}