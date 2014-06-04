using System;
using System.Threading;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.iOS
{
    public class MockConsole
    {
        public static string Output;

        public static void WriteLine (string fmt, object arg0)
        {
            Output = String.Format (fmt, arg0);
        }
    }

    [TestFixture]
    public class LogTest
    {
        private void CheckOutput (string expected)
        {
            Assert.AreEqual (expected, MockConsole.Output);
        }

        [Test]
        public void LoggingToConsole ()
        {
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString ();

            // Save the original logger and swap it out with a test instance
            Logger save = Log.SharedInstance;
            Log.SetLogger (new Logger ());

            // Disable all telemetry
            Log.SharedInstance.WriteLine = MockConsole.WriteLine;
            LogSettings settings = Log.SharedInstance.Settings;
            settings.Error.DisableTelemetry ();
            settings.Warn.DisableTelemetry ();
            settings.Info.DisableTelemetry ();
            settings.Debug.DisableTelemetry ();

            settings.Error.CallerInfo = false;
            settings.Warn.CallerInfo = false;
            settings.Info.CallerInfo = false;
            settings.Debug.CallerInfo = false;

            // Swap the writeline function to our mock version
            Log.SharedInstance.WriteLine = MockConsole.WriteLine;

            // In Test.Android and Test.iOS, LogSettings.cs is not part of the build.
            // So, the default is to log all subsystems.

            // Error

            // Make sure a single filter works
            settings.Error.Console = Log.LOG_SYNC;
            Log.Error (Log.LOG_SYNC, "Test no args.");
            CheckOutput (String.Format ("Error:{0}:: Test no args.", threadId));

            // Make sure multiple filters work
            settings.Error.Console |= Log.LOG_CALENDAR;
            Log.Error (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            CheckOutput (String.Format ("Error:{0}:: Test int 5 = 5", threadId));
            Log.Error (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);
            CheckOutput (String.Format ("Error:{0}:: Test int 6 7 = 6 7", threadId));

            // Make sure filters block too
            settings.Error.Console = Log.LOG_CALENDAR;
            MockConsole.Output = "";
            Log.Error (Log.LOG_SYNC, "You should not see this message.");
            CheckOutput ("");

            // Warnings

            // Make sure a single filter works
            settings.Warn.Console = Log.LOG_SYNC;
            Log.Warn (Log.LOG_SYNC, "Test no args.");
            CheckOutput (String.Format ("Warn:{0}:: Test no args.", threadId));

            // Make sure multiple filters work
            settings.Warn.Console |= Log.LOG_CALENDAR;
            Log.Warn (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            CheckOutput (String.Format ("Warn:{0}:: Test int 5 = 5", threadId));
            Log.Warn (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);
            CheckOutput (String.Format ("Warn:{0}:: Test int 6 7 = 6 7", threadId));

            // Make sure filters block too
            settings.Warn.Console = Log.LOG_CALENDAR;
            MockConsole.Output = "";
            Log.Warn (Log.LOG_SYNC, "You should not see this message.");
            CheckOutput ("");

            // Info

            // Make sure a single filter works
            settings.Info.Console = Log.LOG_SYNC;
            MockConsole.Output = "";
            Log.Info (Log.LOG_SYNC, "Test no args.");
            CheckOutput (String.Format ("Info:{0}:: Test no args.", threadId));

            // Make sure multiple filters work
            settings.Info.Console |= Log.LOG_CALENDAR;
            Log.Info (Log.LOG_SYNC, "Test int 5 = {0}", 5);
            CheckOutput (String.Format ("Info:{0}:: Test int 5 = 5", threadId));
            Log.Info (Log.LOG_CALENDAR, "Test int 6 7 = {0} {1}", 6, 7);
            CheckOutput (String.Format ("Info:{0}:: Test int 6 7 = 6 7", threadId));

            // Make sure filters block too
            settings.Info.Console = Log.LOG_CALENDAR;
            MockConsole.Output = "";
            Log.Info (Log.LOG_SYNC, "You should not see this message.");
            CheckOutput ("");

            // Restore the original logger
            Log.SetLogger (save);
        }
    }
}