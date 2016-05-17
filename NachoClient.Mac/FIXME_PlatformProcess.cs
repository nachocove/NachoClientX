using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using Foundation;

namespace NachoPlatformBinding
{
    public class PlatformProcess
    {
        public PlatformProcess ()
        {
        }

        public static long GetUsedMemory ()
        {
            return 0;
        }

        public static int GetCurrentNumberOfFileDescriptors ()
        {
            return 0;
        }

        public static int GetCurrentNumberOfInUseFileDescriptors ()
        {
            return 0;
        }

        public static string[] GetCurrentInUseFileDescriptors ()
        {
            return new string[]{ };
        }


        public static string GetFileNameForDescriptor (int fd)
        {
            return "";
        }

        public static int GetNumberOfSystemThreads ()
        {
            return 0;
        }

        public static string[] GetStackTrace ()
        {
            var stacktrace = System.Environment.StackTrace;
            return stacktrace.Split (new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}

