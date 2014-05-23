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
    public class Log
    {
        public const int LOG_SYNC = 1;
        public const int LOG_CALENDAR = 2;
        public const int LOG_CONTACTS = 4;
        public const int LOG_UI = 8;
        public const int LOG_TIMER = 16;
        public const int LOG_HTTP = 32;
        public const int LOG_STATE = 64;
        public const int LOG_RENDER = 128;
        public const int LOG_EMAIL = 256;
        public const int LOG_AS = 512;
        public const int LOG_SYS = 1024;
        public const int LOG_XML = 2048;
        public const int LOG_LIFECYCLE = 4096;
        public static int logLevel = LOG_SYNC + LOG_TIMER + LOG_STATE + LOG_AS + LOG_XML + LOG_AS + LOG_HTTP + LOG_LIFECYCLE;
        // Determine if caller info (method, file name, line #) is included in log messages
        // Set it to false if it is slowing things down too much.
        public static Boolean CallerInfo = false;

        public Log ()
        {
        }

        private static string GetMethodShortName (string methodName)
        {
            int left = methodName.IndexOf ("(");
            string methodName2 = methodName.Remove (left);
            int space = methodName.LastIndexOf (" ");
            return methodName2.Substring (space + 1);
        }

        private static void _Log (int when, string fmt, string level, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }

            // Get the caller information
            StackTrace st = new StackTrace (true);
            StackFrame sf = st.GetFrame(2);
            MethodBase mb = sf.GetMethod ();
            string callInfo = "";
            if (CallerInfo) {
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
                level + ":" + Thread.CurrentThread.ManagedThreadId.ToString() + ":" + callInfo + ": " + fmt, list));
        }

        public static void Error (int when, string fmt, params object[] list)
        {
            _Log (when, fmt, "Error", list);
            Telemetry.RecordLogEvent (TelemetryEventType.ERROR, fmt, list);
        }

        public static void Warn (int when, string fmt, params object[] list)
        {
            _Log (when, fmt, "Warn", list);
            Telemetry.RecordLogEvent (TelemetryEventType.WARN, fmt, list);
        }

        public static void Info (int when, string fmt, params object[] list)
        {
            _Log (when, fmt, "Info", list);
            Telemetry.RecordLogEvent (TelemetryEventType.INFO, fmt, list);
        }

        public static void Error (string fmt, params object[] list)
        {
            _Log (logLevel, fmt, "Error", list);
            Telemetry.RecordLogEvent (TelemetryEventType.ERROR, fmt, list);
        }

        public static void Warn (string fmt, params object[] list)
        {
            _Log (logLevel, fmt, "Warn", list);
            Telemetry.RecordLogEvent (TelemetryEventType.WARN, fmt, list);
        }

        public static void Info (string fmt, params object[] list)
        {
            _Log (logLevel, fmt, "Info", list);
            Telemetry.RecordLogEvent (TelemetryEventType.INFO, fmt, list);
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
