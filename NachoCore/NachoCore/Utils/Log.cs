using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;

namespace NachoCore.Utils
{

    public partial class LogSettings
    {

        public class Levels
        {
            public Log.Level Console;
            public Log.Level Telemetry;
        }

    }

    public class Log
    {

        [Flags]
        public enum Level : byte
        {
            Off = 0,
            Debug = 1 << 0,
            Info = 1 << 1,
            Warn = 1 << 2,
            Error = 1 << 3
        }

        public static LogFormatter DefaultFormatter = new LogFormatter ();

        private static HashSet<string> TelemetryBlacklist = new HashSet<string> () {
            "XML"
        };

        public static bool TelemetryDisabled;

        #region Nacho Subsystems

        public static Log LOG_SYNC = new Log ("SYNC");
        public static Log LOG_CALENDAR = new Log ("CALENDAR");
        public static Log LOG_CONTACTS = new Log ("CONTACTS");
        public static Log LOG_UI = new Log ("UI");
        public static Log LOG_TIMER = new Log ("TIMER");
        public static Log LOG_HTTP = new Log ("HTTP");
        public static Log LOG_STATE = new Log ("STATE");
        public static Log LOG_RENDER = new Log ("RENDER");
        public static Log LOG_EMAIL = new Log ("EMAIL");
        public static Log LOG_AS = new Log ("AS");
        public static Log LOG_SYS = new Log ("SYS");
        public static Log LOG_LIFECYCLE = new Log ("LIFECYCLE");
        public static Log LOG_BRAIN = new Log ("BRAIN");
        public static Log LOG_XML_FILTER = new Log ("XML_FILTER");
        public static Log LOG_UTILS = new Log ("UTILS");
        public static Log LOG_INIT = new Log ("INIT");
        public static Log LOG_TEST = new Log ("TEST");
        public static Log LOG_DNS = new Log ("DNS");
        public static Log LOG_ASSERT = new Log ("ASSERT");
        public static Log LOG_DB = new Log ("DB");
        public static Log LOG_PUSH = new Log ("PUSH");
        public static Log LOG_BACKEND = new Log ("BACKEND");
        public static Log LOG_SMTP = new Log ("SMTP");
        public static Log LOG_IMAP = new Log ("IMAP");
        public static Log LOG_SEARCH = new Log ("SEARCH");
        public static Log LOG_SFDC = new Log ("SFDC");
        public static Log LOG_CHAT = new Log ("CHAT");
        public static Log LOG_OAUTH = new Log ("OAUTH");

        // Note: used only for displaying XML. Do not use for anything else because
        // it does not get sent to telemetry.
        public static Log LOG_XML = new Log ("XML");

        #endregion

        public string Subsystem { get; private set; }
        private Level ConsoleLevel;
        private Level TelemetryLevel;
        NachoPlatform.IConsoleLog ConsoleLog;

        #region Creating a Log

        public Log (string subsystem)
        {
            Subsystem = subsystem;
            AssignLevels ();
            ConsoleLog = NachoPlatform.ConsoleLog.Create (Subsystem);
        }

        private void AssignLevels ()
        {
            if (LogSettings.Subsystems.TryGetValue (Subsystem, out LogSettings.Levels levels)) {
                ConsoleLevel = levels.Console;
                TelemetryLevel = levels.Telemetry;
            } else {
                ConsoleLevel = Level.Info | Level.Warn | Level.Error;
                TelemetryLevel = Level.Info | Level.Warn | Level.Error;
            }
            if (TelemetryBlacklist.Contains (Subsystem)) {
                TelemetryLevel = Level.Off;
            }
        }

        #endregion

        #region Controlling Levels

        public void SetEnabled (bool enabled)
        {
            if (enabled) {
                AssignLevels ();
            } else {
                ConsoleLevel = Level.Off;
                TelemetryLevel = Level.Off;
            }
        }

        #endregion

        #region Legacy Static-style calls

        public static void Debug (Log log, string fmt, params object [] list)
        {
            log.Debug (fmt, list);
        }

        public static void Info (Log log, string fmt, params object [] list)
        {
            log.Info (fmt, list);
        }

        public static void Warn (Log log, string fmt, params object [] list)
        {
            log.Warn (fmt, list);
        }

        public static void Error (Log log, string fmt, params object [] list)
        {
            log.Error (fmt, list);
        }

        #endregion

        #region Log Messages

        public void Debug (string fmt, params object [] list)
        {
            if (ConsoleLevel.HasFlag (Level.Debug)) {
                ConsoleLog.Debug (fmt, list);
            }
            if (TelemetryLevel.HasFlag (Level.Debug)) {
                CrashReporter.Instance.ReceiveLog (Level.Debug, this.Subsystem, fmt, list);
                if (!TelemetryDisabled) {
                    NcApplication.Instance.TelemetryService.RecordLogEvent (this, Level.Debug, fmt, list);
                }
            }
        }

        public void Info (string fmt, params object [] list)
        {
            if (ConsoleLevel.HasFlag (Level.Info)) {
                ConsoleLog.Info (fmt, list);
            }
            if (TelemetryLevel.HasFlag (Level.Info)) {
                CrashReporter.Instance.ReceiveLog (Level.Info, this.Subsystem, fmt, list);
                if (!TelemetryDisabled) {
                    NcApplication.Instance.TelemetryService.RecordLogEvent (this, Level.Info, fmt, list);
                }
            }
        }

        public void Warn (string fmt, params object [] list)
        {
            if (ConsoleLevel.HasFlag (Level.Warn)) {
                ConsoleLog.Warn (fmt, list);
            }
            if (TelemetryLevel.HasFlag (Level.Warn)) {
                CrashReporter.Instance.ReceiveLog (Level.Warn, this.Subsystem, fmt, list);
                if (!TelemetryDisabled) {
                    NcApplication.Instance.TelemetryService.RecordLogEvent (this, Level.Warn, fmt, list);
                }
            }
        }

        public void Error (string fmt, params object [] list)
        {
            if (ConsoleLevel.HasFlag (Level.Error)) {
                ConsoleLog.Error (fmt, list);
            }
            if (TelemetryLevel.HasFlag (Level.Error)) {
                CrashReporter.Instance.ReceiveLog (Level.Error, this.Subsystem, fmt, list);
                if (!TelemetryDisabled) {
                    NcApplication.Instance.TelemetryService.RecordLogEvent (this, Level.Error, fmt, list);
                }
            }
        }

        #endregion

        public static String ReplaceFormatting (String s)
        {
            return s.Replace ("{", "[").Replace ("}", "]");
        }

    }

    #region Formatting

    public class LogFormatter : IFormatProvider, ICustomFormatter
    {
        // IFormatProvider.GetFormat implementation.
        public object GetFormat (Type formatType)
        {
            // Determine whether custom formatting object is requested. 
            if (formatType == typeof (ICustomFormatter)) {
                return this;
            } else {
                return null;
            }
        }

        public string Format (string format, object arg, IFormatProvider formatProvider)
        {
            if (null == arg) {
                return "<null>";
            }
            if (arg.GetType () == typeof (XElement)) {
                var xelement = (XElement)arg;
                return xelement.ToStringWithoutCharacterChecking ();
            }
            if (arg.GetType () == typeof (XDocument)) {
                var xdocument = (XDocument)arg;
                return xdocument.ToStringWithoutCharacterChecking ();
            }
            if (arg is IFormattable) {
                return ((IFormattable)arg).ToString (format, CultureInfo.CurrentCulture);
            }
            return arg.ToString ();
        }
    }

    public static class LogHelpers
    {
        public class TruncatingXmlTextWriter : XmlTextWriter
        {
            public TruncatingXmlTextWriter (System.IO.TextWriter textWriter) : base (textWriter)
            {
            }

            public override void WriteString (string text)
            {
                if (text.Length > 1024) {
                    base.WriteString (text.Substring (0, 80) + "...");
                } else {
                    base.WriteString (text);
                }
            }
        }

        public static string ToStringWithoutCharacterChecking (this XDocument xElement)
        {
            using (System.IO.StringWriter stringWriter = new System.IO.StringWriter ()) {
                using (System.Xml.XmlTextWriter xmlTextWriter = new TruncatingXmlTextWriter (stringWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xElement.WriteTo (xmlTextWriter);
                }
                return stringWriter.ToString ();
            }
        }

        public static string ToStringWithoutCharacterChecking (this XElement xElement)
        {
            using (System.IO.StringWriter stringWriter = new System.IO.StringWriter ()) {
                using (System.Xml.XmlTextWriter xmlTextWriter = new TruncatingXmlTextWriter (stringWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xElement.WriteTo (xmlTextWriter);
                }
                return stringWriter.ToString ();
            }
        }

        public static string BytesDump (byte [] bytes, int bytesPerLine = 16, int bytesPerExtraSpace = 8)
        {
            string output = "";
            int n = 0;

            for (n = 0; n < (bytes.Length - bytesPerLine); n += bytesPerLine) {
                output += String.Format ("{0:D8}:", n);
                for (int m = 0; m < bytesPerLine; m++) {
                    if ((0 != m) && (0 == (m % bytesPerExtraSpace))) {
                        output += " ";
                    }
                    output += String.Format (" {0:X2}", (int)bytes [n + m]);
                }
                output += "\n";
            }

            // Handle the last line
            if (n < bytes.Length) {
                output += String.Format ("{0:D8}:", n);
                for (int m = 0; n < bytes.Length; n++, m++) {
                    if ((0 != m) && (0 == (m % bytesPerExtraSpace))) {
                        output += " ";
                    }
                    output += String.Format (" {0:X2}", (int)bytes [n]);
                }
                output += "\n";
            }

            return output;
        }
    }

    #endregion
}
