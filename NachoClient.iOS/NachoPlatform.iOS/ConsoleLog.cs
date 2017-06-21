//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;
using System.Threading;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class ConsoleLog
    {

        public static IConsoleLog Create (string subsystem)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion (10, 0)){
                return new OSConsoleLog (subsystem);
            }else{
                return new LegacyConsoleLog (subsystem);
            }
        }
    }

    public class OSConsoleLog : IConsoleLog
    {

        IntPtr NativeLog;

        [DllImport ("__Internal")]
        static extern IntPtr nacho_os_log_create (string subsystem, string category);

        [DllImport ("__Internal")]
        static extern void nacho_os_log_debug (IntPtr log, string message);

        [DllImport ("__Internal")]
        static extern void nacho_os_log_info (IntPtr log, string message);

        [DllImport ("__Internal")]
        static extern void nacho_os_log_warn (IntPtr log, string message);

        [DllImport ("__Internal")]
        static extern void nacho_os_log_error (IntPtr log, string message);

        public OSConsoleLog (string subsystem)
        {
            NativeLog = nacho_os_log_create ("com.nachocove.mail", subsystem);
        }

        public void Info (string message, params object [] args)
        {
            nacho_os_log_info (NativeLog, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Debug (string message, params object [] args)
        {
            nacho_os_log_debug (NativeLog, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Warn (string message, params object [] args)
        {
            nacho_os_log_warn (NativeLog, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Error (string message, params object [] args)
        {
            nacho_os_log_error (NativeLog, string.Format (Log.DefaultFormatter, message, args));
        }
    }

    public class LegacyConsoleLog : IConsoleLog
    {
        public string Subsystem;
        
        public LegacyConsoleLog (string subsystem)
        {
            Subsystem = subsystem;
        }

        public void Debug (string message, params object [] args)
        {
            _Log ("DEBUG", message, args);
        }

        public void Info (string message, params object [] args)
        {
            _Log ("INFO", message, args);
        }

        public void Warn (string message, params object [] args)
        {
            _Log ("WARN", message, args);
        }

        public void Error (string message, params object [] args)
        {
            _Log ("ERROR", message, args);
        }

        private void _Log (string level, string message, params object [] args)
        {
            Console.WriteLine (string.Format (Log.DefaultFormatter, Subsystem + ":" + level + ":" + Thread.CurrentThread.ManagedThreadId.ToString () + ": " + message, args));

        }
    }
}
