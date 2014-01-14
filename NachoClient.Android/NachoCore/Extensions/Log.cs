using System;
using System.Globalization;
using System.Xml.Linq;
using System.Xml;

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
        public static int logLevel = 255;

        public Log ()
        {
        }

        public static void Error (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Error: " + fmt, list));
        }

        public static void Warn (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Warn: " + fmt, list));
        }

        public static void Info (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Info: " + fmt, list));
        }

        public static void Error (string fmt, params object[] list)
        {
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Error: " + fmt, list));
        }

        public static void Warn (string fmt, params object[] list)
        {
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Warn: " + fmt, list));
        }

        public static void Info (string fmt, params object[] list)
        {
            Console.WriteLine ("{0}", String.Format (new NachoFormatter (), "Info: " + fmt, list));
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
        public static string ToStringWithoutCharacterChecking (this XDocument xElement)
        {
            using (System.IO.StringWriter stringWriter = new System.IO.StringWriter ()) {
                using (System.Xml.XmlTextWriter xmlTextWriter = new XmlTextWriter (stringWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xElement.WriteTo (xmlTextWriter);
                }
                return stringWriter.ToString ();
            }
        }

        public static string ToStringWithoutCharacterChecking (this XElement xElement)
        {
            using (System.IO.StringWriter stringWriter = new System.IO.StringWriter ()) {
                using (System.Xml.XmlTextWriter xmlTextWriter = new XmlTextWriter (stringWriter)) {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xElement.WriteTo (xmlTextWriter);
                }
                return stringWriter.ToString ();
            }
        }
    }
}
