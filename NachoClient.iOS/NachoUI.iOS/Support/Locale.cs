//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;

namespace NachoCore
{
    public static class Locale
    {
        static NSBundle main = NSBundle.MainBundle;

        public static string GetText (string str)
        {
            return main.LocalizedString (str, "", "");
        }

        public static string Format (string fmt, params object [] args)
        {
            var msg = main.LocalizedString (fmt, "", "");

            return String.Format (msg, args);
        }
    }
}

