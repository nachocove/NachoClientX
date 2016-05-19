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
            throw new NotImplementedException ();
        }

        public static int GetCurrentNumberOfFileDescriptors ()
        {
            throw new NotImplementedException ();
        }

        public static int GetCurrentNumberOfInUseFileDescriptors ()
        {
            throw new NotImplementedException ();
        }

        public static string[] GetCurrentInUseFileDescriptors ()
        {
            throw new NotImplementedException ();
        }


        public static string GetFileNameForDescriptor (int fd)
        {
            throw new NotImplementedException ();
        }

        public static int GetNumberOfSystemThreads ()
        {
            throw new NotImplementedException ();
        }

        public static string[] GetStackTrace ()
        {
            var stacktrace = System.Environment.StackTrace;
            return stacktrace.Split (new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}

