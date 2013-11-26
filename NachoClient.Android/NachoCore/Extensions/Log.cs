using System;

namespace NachoCore.Utils
{
    public class Log
    {
        public enum Filter {
            LOG_SYNC = 2,
            LOG_CALENDAR = 4,
        }

        public static Filter logLevel = 0;
       
        public Log ()
        {
        }

        public static void Error(Filter when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine("Error: " + fmt, list);
        }

        public static void Warn(Filter when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine("Warn: " + fmt, list);
        }

        public static void Info(Filter when, string fmt, params object[] list)
        {
            if ((when & logLevel) == 0) {
                return;
            }
            Console.WriteLine("Info: " + fmt, list);
        }

    }
}

