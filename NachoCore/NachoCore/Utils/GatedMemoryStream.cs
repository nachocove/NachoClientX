//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Security.Cryptography;

namespace NachoCore.Utils
{
    public class GatedMemoryStream : MemoryStream
    {
        public byte[] ReadAll ()
        {
            byte[] bytes = new byte[Length];
            Seek (0, SeekOrigin.Begin);
            Read (bytes, 0, (int)Length);
            SetLength (0); // clear the stream
            return bytes;
        }
    }
}
