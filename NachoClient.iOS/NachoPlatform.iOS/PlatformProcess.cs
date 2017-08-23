//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text;
using System.Runtime.InteropServices;

namespace NachoPlatform
{
    public class PlatformProcess : IPlatformProcess
    {
        public PlatformProcess ()
        {
        }

        public static readonly IPlatformProcess Instance = new PlatformProcess ();

        public string [] GetStackTrace ()
        {
            return nacho_get_stack_trace ();
        }

        public long GetUsedMemory ()
        {
            return nacho_get_used_memory ();
        }

        public int GetNumberOfSystemThreads ()
        {
            return nacho_get_number_of_system_threads ();
        }

        public int GetCurrentNumberOfFileDescriptors ()
        {
            return nacho_get_current_number_of_file_descriptors ();
        }

        public int GetCurrentNumberOfInUseFileDescriptors ()
        {
            return nacho_get_current_number_of_in_use_file_descriptors ();
        }

        public string [] GetCurrentInUseFileDescriptors ()
        {
            var fds = new int [GetCurrentNumberOfFileDescriptors ()];
            var count = nacho_get_current_in_use_file_descriptors (fds, fds.Length);
            var fdStrings = new string [count];
            for (var i = 0; i < count; ++i) {
                fdStrings [i] = fds [i].ToString ();
            }
            return fdStrings;
        }

        public string GetFileNameForDescriptor (int fd)
        {
            var builder = new StringBuilder (256);
            nacho_get_filename_for_descriptor (fd, builder, builder.Capacity);
            return builder.ToString ();
        }

        [DllImport ("__Internal")]
        static extern long nacho_get_used_memory ();

        [DllImport ("__Internal")]
		static extern int nacho_get_current_number_of_file_descriptors ();

        [DllImport ("__Internal")]
		static extern int nacho_get_current_number_of_in_use_file_descriptors ();

        [DllImport ("__Internal")]
		static extern int nacho_get_current_in_use_file_descriptors (int [] fds, int limit);

        [DllImport ("__Internal")]
		static extern void nacho_get_filename_for_descriptor (int fd, StringBuilder buf, int limit);

        [DllImport ("__Internal")]
		static extern int nacho_get_number_of_system_threads ();

        [DllImport ("__Internal")]
        static extern string[] nacho_get_stack_trace ();
    }
}
