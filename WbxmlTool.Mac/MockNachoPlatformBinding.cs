//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

// TODO - Need to cleanup the NachoPlatformBinding soon. Should move this into
// NachoPlatformBinding with a new .Mac project
using System;

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

        public static string GetFileNameForDescriptor (int fd)
        {
            throw new NotImplementedException ();
        }
    }
}
