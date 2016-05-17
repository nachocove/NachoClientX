//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using AppKit;

namespace NachoClient.Mac
{
    static class MainClass
    {
        static void Main (string[] args)
        {
            NSApplication.Init ();
            NSApplication.Main (args);
        }
    }
}
