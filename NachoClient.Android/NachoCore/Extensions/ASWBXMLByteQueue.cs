using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NachoCore.Wbxml
{
    class ASWBXMLByteQueue
    {
        public long Count { set; get; }

        private Stream ByteStream;

        public ASWBXMLByteQueue (Stream bytes)
        {
            ByteStream = bytes;
            Count = ByteStream.Length;
        }

        public byte Dequeue ()
        {
            var value = ByteStream.ReadByte ();
            if (-1 == value) {
                throw new Exception ("ASWBXMLByteQueue: attempted read past end.");
            }
            --Count;
            return Convert.ToByte (value);
        }

        public int DequeueMultibyteInt ()
        {
            int iReturn = 0;
            byte singleByte = 0xFF;

            do {
                iReturn <<= 7;

                singleByte = this.Dequeue ();
                iReturn += (int)(singleByte & 0x7F);
            } while (CheckContinuationBit (singleByte));

            return iReturn;
        }

        private bool CheckContinuationBit (byte byteval)
        {
            byte continuationBitmask = 0x80;
            return (continuationBitmask & byteval) != 0;
        }

        public string DequeueString ()
        {
            StringBuilder strReturn = new StringBuilder ();
            byte currentByte = 0x00;
            do {
                // TODO: Improve this handling. We are technically UTF-8, meaning
                // that characters could be more than one byte long. This will fail if we have
                // characters outside of the US-ASCII range
                currentByte = this.Dequeue ();
                if (currentByte != 0x00) {
                    strReturn.Append ((char)currentByte);
                }
            } while (currentByte != 0x00);

            return strReturn.ToString ();
        }

        public string DequeueString (int length)
        {
            StringBuilder strReturn = new StringBuilder ();

            byte currentByte = 0x00;
            for (int i = 0; i < length; i++) {
                // TODO: Improve this handling. We are technically UTF-8, meaning
                // that characters could be more than one byte long. This will fail if we have
                // characters outside of the US-ASCII range
                currentByte = this.Dequeue ();
                strReturn.Append ((char)currentByte);
            }

            return strReturn.ToString ();
        }
    }
}
