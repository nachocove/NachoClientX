using System;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using System.Reflection;
using System.Threading;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    // LogLevelSettings represents the configuration for one log level (debug, 
    // info, warn, error). Each level has configuration for two destinations - 
    // console and telemetry. Each level has multiple (up to 64) subsystem. Each
    // subsystem can decide to go to console or telemetry or both.
    public class LogLevelSettings
    {
        public ulong Console;
        public ulong Telemetry;
        public bool CallerInfo;

        public LogLevelSettings (ulong consoleFlags = ulong.MaxValue, ulong telemetryFlags = ulong.MaxValue, bool callerInfo=false)
        {
            Console = consoleFlags;
            Telemetry = telemetryFlags;
            CallerInfo = callerInfo;
        }

        // Copy Constructor
        public LogLevelSettings (LogLevelSettings settings)
        {
            Console = settings.Console;
            Telemetry = settings.Telemetry;
        }

        public void DisableConsole (ulong subsystem = ulong.MaxValue)
        {
            Console &= ~subsystem;
        }

        public void DisableTelemetry (ulong subsystem = ulong.MaxValue)
        {
            Telemetry &= ~subsystem;
        }

        public bool ToConsole (ulong subsystem)
        {
            return (0 != (Console & subsystem));
        }

        public bool ToTelemetry (ulong subsystem)
        {
            return (0 != (Telemetry & subsystem));
        }

        public override string ToString ()
        {
            return String.Format ("console=0x{0:X16}, telemetry=0x{1:X16}, callerinfo={2}", Console, Telemetry, CallerInfo);
        }
    }

    public partial class LogSettings
    {
        public LogLevelSettings Debug;
        public LogLevelSettings Info;
        public LogLevelSettings Warn;
        public LogLevelSettings Error;

        public LogSettings ()
        {
            // Log is used everywhere. So, we want this to be self-contained. Just
            // including this file should be all anyone needs to do to make logging
            // to work.
            //
            // However, we want to provide a convenient way to customize logging settings
            // for developers. The solution is to create a LogSettings.cs that is never
            // committed to source repo. It contains the default settings and developers
            // can customize it without worrying about accidentally committing private
            // settings.
            //
            // LogSetting.cs is partial class definition of LogSettings that defines 
            // constants to be used for constructing LogSetting objects. If Log.cs
            // is used standalone, it will fail to detect the existence of those contants
            // and fall back to the default.
            ulong debugConsoleSettings = GetOptionalUlong ("DEBUG_CONSOLE_SETTINGS");
            ulong debugTelemetrySettings = GetOptionalUlong ("DEBUG_TELEMETRY_SETTINGS");
            bool debugCallInfo = GetOptionalBool ("DEBUG_CALLERINFO");

            ulong infoConsoleSettings = GetOptionalUlong ("INFO_CONSOLE_SETTINGS");
            ulong infoTelemetrySettings = GetOptionalUlong ("INFO_TELEMETRY_SETTINGS");
            bool infoCallInfo = GetOptionalBool ("INFO_CALLERINFO");

            ulong warnConsoleSettings = GetOptionalUlong ("WARN_CONSOLE_SETTINGS");
            ulong warnTelemetrySettings = GetOptionalUlong ("WARN_TELEMETRY_SETTINGS");
            bool warnCallInfo = GetOptionalBool ("WARN_CALLERINFO");

            ulong errorConsoleSettings = GetOptionalUlong ("ERROR_CONSOLE_SETTINGS");
            ulong errorTelemetrySettings = GetOptionalUlong ("ERROR_TELEMETRY_SETTINGS");
            bool errorCallInfo = GetOptionalBool ("ERROR_CALLERINFO");

            Debug = new LogLevelSettings (debugConsoleSettings, debugTelemetrySettings, debugCallInfo);
            Info = new LogLevelSettings (infoConsoleSettings, infoTelemetrySettings, infoCallInfo);
            Warn = new LogLevelSettings (warnConsoleSettings, warnTelemetrySettings, warnCallInfo);
            Error = new LogLevelSettings (errorConsoleSettings, errorTelemetrySettings, errorCallInfo);

            FixUp ();
        }

        // Copy constructor
        public LogSettings (LogSettings settings)
        {
            Debug = new LogLevelSettings (settings.Debug);
            Info = new LogLevelSettings (settings.Info);
            Warn = new LogLevelSettings (settings.Warn);
            Error = new LogLevelSettings (settings.Error);

            FixUp ();
        }

        public LogSettings (LogLevelSettings debug, LogLevelSettings info, 
            LogLevelSettings warn, LogLevelSettings error)
        {
            Debug = new LogLevelSettings(debug);
            Info = new LogLevelSettings(info);
            Warn = new LogLevelSettings (warn);
            Error = new LogLevelSettings (error);

            FixUp ();
        }

        private ulong GetOptionalUlong(string fieldName, ulong defaultValue = ulong.MaxValue)
        {
            FieldInfo field = typeof(LogSettings).GetField (fieldName);
            if (null == field) {
                return defaultValue;
            }
            return (ulong)field.GetValue (this);
        }

        private bool GetOptionalBool(string fieldName, bool defaultValue = false)
        {
            FieldInfo field = typeof(LogSettings).GetField (fieldName);
            if (null == field) {
                return defaultValue;
            }
            return (bool)field.GetValue (this);
        }

        private void FixUp ()
        {
            // We'll never send XML and state machine logs to telemetry because
            // they have specialized telemetry API.
            Debug.DisableTelemetry (Log.LOG_XML/* | Log.LOG_STATE */);
            Warn.DisableTelemetry (Log.LOG_XML/* | Log.LOG_STATE */);
            Info.DisableTelemetry (Log.LOG_XML/* | Log.LOG_STATE */);
            Error.DisableTelemetry (Log.LOG_XML/* | Log.LOG_STATE */);
        }

        public override string ToString ()
        {
            string retval = "";
            retval += "Error: " + Error.ToString () + "\n";
            retval += "Warn: " + Warn.ToString () + "\n";
            retval += "Info: " + Info.ToString () + "\n";
            retval += "Debug: " + Debug.ToString () + "\n";
            return retval;
        }
    }

    public class Logger
    {
        public LogSettings Settings;

        public Logger ()
        {
            Settings = new LogSettings ();
        }

        private static string GetMethodShortName (string methodName)
        {
            int left = methodName.IndexOf ("(");
            string methodName2 = methodName.Remove (left);
            int space = methodName.LastIndexOf (" ");
            return methodName2.Substring (space + 1);
        }

        private static void _Log (ulong subsystem,  LogLevelSettings settings, TelemetryEventType teleType,
            string fmt, string level, params object[] list)
        {
            if (settings.ToConsole (subsystem)) {
                // Get the caller information
                StackTrace st = new StackTrace (true);
                StackFrame sf = st.GetFrame (2);
                MethodBase mb = sf.GetMethod ();
                string callInfo = "";
                if (settings.CallerInfo) {
                    if (0 == sf.GetFileLineNumber ()) {
                        // No line # info. Must be a release build
                        callInfo = String.Format (" [{0}.{1}()]", mb.DeclaringType.Name, mb.Name);
                    } else {
                        callInfo = String.Format (" [{0}:{1}, {2}.{3}()]",
                            Path.GetFileName (sf.GetFileName ()), sf.GetFileLineNumber (),
                            mb.DeclaringType.Name, mb.Name);
                    }
                }
                Console.WriteLine ("{0}", String.Format (new NachoFormatter (), 
                    level + ":" + Thread.CurrentThread.ManagedThreadId.ToString () + ":" + callInfo + ": " + fmt, list));
            }
            if (settings.ToTelemetry (subsystem)) {
                Telemetry.RecordLogEvent (teleType, fmt, list);
            }
        }

        public void Error (ulong subsystem, string fmt, params object[] list)
        {
            _Log (subsystem, Settings.Error, TelemetryEventType.ERROR, fmt, "Error", list);
        }

        public void Warn (ulong subsystem, string fmt, params object[] list)
        {
            _Log (subsystem, Settings.Warn, TelemetryEventType.WARN, fmt, "Warn", list);
        }

        public void Info (ulong subsystem, string fmt, params object[] list)
        {
            _Log (subsystem, Settings.Info, TelemetryEventType.INFO, fmt, "Info", list);
        }

        public void Debug (ulong subsystem, string fmt, params object[] list)
        {
            _Log (subsystem, Settings.Debug, TelemetryEventType.DEBUG, fmt, "Debug", list);
        }

        public class NachoFormatter : IFormatProvider, ICustomFormatter
        {
            // IFormatProvider.GetFormat implementation.
            public object GetFormat (Type formatType)
            {
                // Determine whether custom formatting object is requested. 
                if (formatType == typeof(ICustomFormatter)) {
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
                if (arg.GetType () == typeof(XElement)) {
                    var xelement = (XElement)arg;
                    return xelement.ToStringWithoutCharacterChecking ();
                }
                if (arg.GetType () == typeof(XDocument)) {
                    var xdocument = (XDocument)arg;
                    return xdocument.ToStringWithoutCharacterChecking ();
                }
                if (arg is IFormattable) {
                    return ((IFormattable)arg).ToString (format, CultureInfo.CurrentCulture);
                }
                return arg.ToString ();
            }
        }
    }

    public class Log
    {
        // Subsystem denotes a functional area of the app. Ulong supports 64 subsystems.
        // 
        public const ulong LOG_SYNC = (1 << 0);
        public const ulong LOG_CALENDAR = (1 << 1);
        public const ulong LOG_CONTACTS = (1 << 2);
        public const ulong LOG_UI = (1 << 3);
        public const ulong LOG_TIMER = (1 << 4);
        public const ulong LOG_HTTP = (1 << 5);
        public const ulong LOG_STATE = (1 << 6);
        public const ulong LOG_RENDER = (1 << 7);
        public const ulong LOG_EMAIL = (1 << 8);
        public const ulong LOG_AS = (1 << 9);
        public const ulong LOG_SYS = (1 << 10);
        // Note: used only for displaying XML. Do not use for anything else because
        // it does not get sent to telemetry.
        public const ulong LOG_XML = (1 << 11);
        public const ulong LOG_LIFECYCLE = (1 << 12);
        public const ulong LOG_BRAIN = (1 << 13);
        public const ulong LOG_XML_FILTER = (1 << 14);
        public const ulong LOG_UTILS = (1 << 15);
        public const ulong LOG_INIT = (1 << 16);

        private static Logger DefaultLogger;
        public static Logger SharedInstance {
            get {
                if (null == DefaultLogger) {
                    DefaultLogger = new Logger ();
                }
                return DefaultLogger;
            }
        }

        public Log ()
        {
        }

        public static void Deubg (ulong subsystem, string fmt, params object[] list)
        {
            Log.SharedInstance.Debug (subsystem, fmt, list);
        }

        public static void Info (ulong subsystem, string fmt, params object[] list)
        {
            Log.SharedInstance.Info (subsystem, fmt, list);
        }

        public static void Warn (ulong subsystem, string fmt, params object[] list)
        {
            Log.SharedInstance.Warn (subsystem, fmt, list);
        }

        public static void Error (ulong subsystem, string fmt, params object[] list)
        {
            Log.SharedInstance.Error (subsystem, fmt, list);
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

        public static string BytesDump (byte[] bytes, int bytesPerLine = 16, int bytesPerExtraSpace = 8)
        {
            string output = "";
            int n = 0;

            for (n = 0; n < (bytes.Length - bytesPerLine); n += bytesPerLine) {
                output += String.Format ("{0:D8}:", n);
                for (int m = 0; m < bytesPerLine; m++) {
                    if ((0 != m) && (0 == (m % bytesPerExtraSpace))) {
                        output += " ";
                    }
                    output += String.Format (" {0:X2}", (int)bytes [n+m]);
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
}
