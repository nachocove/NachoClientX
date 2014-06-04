using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    class WBXMLReadPastEndException : Exception
    {
    }

    class ASWBXMLByteQueue
    {
        private Stream ByteStream;
        private int? PeekHolder;
        private GatedMemoryStream RedactedCopy;

        public ASWBXMLByteQueue (Stream bytes, GatedMemoryStream redactedCopy = null)
        {
            PeekHolder = null;
            ByteStream = bytes;
            RedactedCopy = redactedCopy;
        }

        public int Peek ()
        {
            if (null == PeekHolder) {
                var value = ByteStream.ReadByte ();
                PeekHolder = value;
            }
            return (int)PeekHolder;
        }

        public byte Dequeue ()
        {
            int value;
            if (null == PeekHolder) {
                value = ByteStream.ReadByte ();
            } else {
                value = (int)PeekHolder;
                PeekHolder = null;
            }
            if (-1 == value) {
                throw new WBXMLReadPastEndException ();
            }
            byte aByte = Convert.ToByte (value);
            if (null != RedactedCopy) {
                RedactedCopy.WriteByte (aByte);
            }
            return aByte;
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

        public bool DequeueStringToStream (Stream stream, CancellationToken cToken)
        {
            try {
                byte currentByte = 0x00;
                do {
                    currentByte = this.Dequeue ();
                    if (currentByte != 0x00) {
                        stream.WriteByte (currentByte);
                    }
                } while (currentByte != 0x00 && !cToken.IsCancellationRequested);
                stream.Flush();
                return true;
            } catch (Exception ex) {
                Log.Error (Log.LOG_AS, "Exception in DequeueStringToStream {0}", ex.ToString ());
                return false;
            }
        }

        public string DequeueString (CancellationToken cToken)
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
            } while (currentByte != 0x00 && !cToken.IsCancellationRequested);

            return strReturn.ToString ();
        }

        public byte[] DequeueOpaque (int length, CancellationToken cToken)
        {
            MemoryStream bStream = new MemoryStream ();

            byte currentByte;
            for (int i = 0; i < length; i++) {
                // TODO: Improve this handling. We are technically UTF-8, meaning
                // that characters could be more than one byte long. This will fail if we have
                // characters outside of the US-ASCII range
                currentByte = this.Dequeue ();
                bStream.WriteByte (currentByte);
                if (cToken.IsCancellationRequested) {
                    break;
                }
            }
            return bStream.ToArray ();
        }
    }
}
