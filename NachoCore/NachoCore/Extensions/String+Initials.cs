//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
namespace NachoCore
{
    public static class StringInitialsExtensions
    {

        public static string GetFirstLetterOrDigit (this string str)
        {
            string initial = "";
            foreach (char c in str) {
                if (Char.IsLetterOrDigit (c)) {
                    initial += Char.ToUpperInvariant (c);
                    break;
                }
            }
            return initial;
        }
    }
}
