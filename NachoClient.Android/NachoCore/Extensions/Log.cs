using System;

namespace NachoCore.Utils
{
    public class Log
    {

        public const int LOG_SYNC = 1;
        public const int LOG_CALENDAR = 2;
        public const int LOG_UI = 4;
        public static int logLevel = LOG_CALENDAR;


        public Log ()
        {
        }

        public static void Error (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("Error: " + fmt, list);
        }

        public static void Warn (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("Warn: " + fmt, list);
        }

        public static void Info (int when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine ("Info: " + fmt, list);
        }

        public static void Error (string fmt, params object[] list)
        {
            Console.WriteLine ("Error: " + fmt, list);
        }

        public static void Warn (string fmt, params object[] list)
        {
            Console.WriteLine ("Warn: " + fmt, list);
        }

        public static void Info (string fmt, params object[] list)
        {
            Console.WriteLine ("Info: " + fmt, list);
        }
    }
}

