//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;

using MimeKit;

using NachoCore.Model;
using NachoCore.Utils;


namespace NachoCore.Brain
{
    /// <summary>
    /// We assume that each scorable object has a content stored in a file. So,
    /// paring each type of object involves getting a file path and returning 
    /// an object for each type. The caller knows the object type so it knows 
    /// which function to call.
    /// </summary>
    public class ObjectParser
    {
        private static void CheckPath (string path)
        {
            if (!File.Exists (path)) {
                throw new ArgumentException (String.Format ("Unknown object path {0}", path));
            }
        }

        public static MimeMessage ParseMimeMessage (string path)
        {
            CheckPath (path);
            using (var fileStream = new FileStream (path, FileMode.Open, FileAccess.Read)) {
                return MimeMessage.Load (fileStream);
            }
        }
    }
}

