//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public static partial class DateTime_Extension
    {
        public static string ToAsUtcString (this DateTime dateTime)
        {
            // From MS-ASDTYPE: "MSS = Number of milliseconds. This portion of the string is optional."
            // o365 will give you a status 6 if the MSS (fff) aren't there.
            return dateTime.ToString ("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fff'Z'");
        }
    }
}

