using System;
using System.IO;
using Android.OS;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace NachoPlatformBinding
{
    public class PlatformProcess
    {
        public PlatformProcess ()
        {
        }

        protected static string ProcPathSelf = "/proc/self";
        protected static string ProcPathSelfFd = ProcPathSelf + "/fd";
        protected static string ProcPathSelfStatus = ProcPathSelf + "/status";

        protected static string SearchProcStatus (string target)
        {
            string line;
            using (System.IO.StreamReader file = new System.IO.StreamReader (ProcPathSelfStatus)) {
                while ((line = file.ReadLine ()) != null) {
                    if (line.StartsWith (target, StringComparison.OrdinalIgnoreCase)) {
                        if (target.Length < line.Length) {
                            var value = line.Substring (target.Length);
                            return value.Trim ();
                        } else {
                            return "";
                        }
                    }
                }
            }
            return null;
        }

        public static long GetUsedMemory ()
        {
            return Java.Lang.Runtime.GetRuntime ().TotalMemory ();
        }

        public static int GetCurrentNumberOfFileDescriptors ()
        {
            var value = SearchProcStatus ("FDSize:");
            if (String.IsNullOrEmpty (value)) {
                Console.WriteLine ("GetCurrentNumberOfFileDescriptors: cannot find number of file descriptors.");
                return 0;
            }
            int n;
            if (int.TryParse (value, out n)) {
                return n;
            }
            return 0;
        }

        public static int GetCurrentNumberOfInUseFileDescriptors ()
        {
            var dir = new DirectoryInfo (ProcPathSelfFd);
            try {
                return dir.GetFileSystemInfos ().Length;
            } catch (Exception e) {
                Console.WriteLine ("GetCurrentNumberOfInUseFileDescriptors: error accessing file descriptors.");
                return 0;
            }
        }

        class FdComparer : IComparer<string>
        {
            public int Compare (string x, string y)
            {
                return int.Parse (x) - int.Parse (y);
            }
        }

        public static string[] GetCurrentInUseFileDescriptors ()
        {
            var list = new List<string> ();
            var dir = new DirectoryInfo (ProcPathSelfFd);
            try {
                foreach (var fileInfo in dir.GetFiles()) {
                    int result;
                    if (int.TryParse (fileInfo.Name, out result)) {
                        list.Add (fileInfo.Name);
                    }
                }
            } catch (Exception e) {
                Console.WriteLine ("GetCurrentInUseFileDescriptors: error accessing file descriptors.");
            }
            var arr = list.ToArray ();
            Array.Sort (arr, new FdComparer ());
            return arr;
        }

        [DllImport ("libc")]
        private static extern int readlink (string path, byte[] buffer, int buflen);

        public static string readlink (string path)
        {
            byte[] buf = new byte[512];
            int ret = readlink (path, buf, buf.Length);
            if (ret == -1)
                return null;
            char[] cbuf = new char[512];
            int chars = System.Text.Encoding.Default.GetChars (buf, 0, ret, cbuf, 0);
            return new String (cbuf, 0, chars);
        }

        public static string GetFileNameForDescriptor (int fd)
        {
            var path = String.Format ("{0}/{1}", ProcPathSelfFd, fd);
            try {
                var filename = readlink (path);
                return filename;
            } catch (Exception e) {
                Console.WriteLine ("GetFileNameForDescriptor: error reading symbolic link {0}", e);
                return "";
            }
        }

        public static int GetNumberOfSystemThreads ()
        {
            var value = SearchProcStatus ("Threads:");
            if (String.IsNullOrEmpty (value)) {
                Console.WriteLine ("GetNumberOfSystemThreads: cannot find number of threads.");
                return 0;
            }
            int n;
            if (int.TryParse (value, out n)) {
                return n;
            }
            return 0;
        }

        public static string[] GetStackTrace ()
        {
            var stacktrace = System.Environment.StackTrace;
            return stacktrace.Split (new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}

