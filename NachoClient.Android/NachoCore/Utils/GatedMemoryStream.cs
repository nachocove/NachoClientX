//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Security.Cryptography;

namespace NachoCore.Utils
{
    public class GatedMemoryStream : MemoryStream {
        public enum WriteMode {
            NORMAL = 0,
            REDACTED,
            SHA1
        };

        private WriteMode _Mode;
        public WriteMode Mode {
            get {
                return _Mode;
            }
            set {
                // We only allow mode change when there is no data in the stream buffer
                NcAssert.True (0 == Length);
                _Mode = value;

                if (WriteMode.SHA1 == _Mode) {
                    Sha1 = SHA1.Create ();
                }
            }
        }

        SHA1 Sha1;

        public byte[] Sha1Hash {
            get {
                NcAssert.True (WriteMode.SHA1 == Mode);
                byte[] hash = Sha1.ComputeHash (this);
                SetLength (0);
                return hash;
            }
        }

        public GatedMemoryStream () : base ()
        {
            Mode = WriteMode.NORMAL;
        }

        public override void Write (byte[] buffer, int offset, int count)
        {
            switch (Mode) {
            case WriteMode.REDACTED:
                return;
            case WriteMode.NORMAL:
            case WriteMode.SHA1:
                base.Write (buffer, offset, count);
                break;
            }
        }

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

