//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace NachoCore.Utils
{
    public static partial class String_Extension
    {
        public static string SantizeFileName (this string fileName)
        {
            var badOnes = Path.GetInvalidFileNameChars ().ToList ();
            badOnes.Add ('/');
            return badOnes.Aggregate (fileName, (current, c) => current.Replace (c.ToString (), "_"));
        }
    }
}

