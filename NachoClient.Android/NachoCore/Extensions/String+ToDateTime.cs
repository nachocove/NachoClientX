//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Globalization;

namespace NachoCore.Utils
{
    public static partial class String_Extension
    {
        public static DateTime ToDateTime (this string value)
        {
            if (value == null) {
                return DateTime.MinValue;
            }
            DateTime convertedDate = DateTime.MinValue;
            try {
                string pattern = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fff'Z'";

                DateTimeOffset dto = DateTimeOffset.ParseExact
                    (value, pattern, CultureInfo.InvariantCulture);
                convertedDate = dto.DateTime;
            } catch (FormatException) {
                convertedDate = DateTime.MinValue;
            }
            return convertedDate;
        }
    }
}

