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
        public long BytesRead;
        public WBXMLReadPastEndException (long bytesRead)
        {
            BytesRead = bytesRead;
        }
    }

    class ASWBXMLByteQueue
    {
        private byte[] Bytes;
        private GatedMemoryStream RedactedCopy;
        private long Pos;

        public ASWBXMLByteQueue (byte[] bytes, GatedMemoryStream redactedCopy = null)
        {
            Bytes = bytes;
            RedactedCopy = redactedCopy;
        }

        public int Peek ()
        {
            if (Pos == Bytes.Length) {
                return -1;
            }
            return Bytes [Pos];
        }

        public byte Dequeue ()
        {
            if (Pos == Bytes.Length) {
                throw new WBXMLReadPastEndException (Pos);
            }
            var retval = Bytes [Pos++];
            if (null != RedactedCopy) {
                RedactedCopy.WriteByte (retval);
            }
            return retval;
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

        public void DequeueStringToStream (Stream stream, CancellationToken cToken)
        {
            byte currentByte = 0x00;
            do {
                if (cToken.IsCancellationRequested) {
                    throw new OperationCanceledException ();
                }
                currentByte = this.Dequeue ();
                if (currentByte != 0x00) {
                    stream.WriteByte (currentByte);
                }
            } while (currentByte != 0x00);
            stream.Flush ();
        }

        public string DequeueString (CancellationToken cToken)
        {
            bool terminated = false;
            const int blockSize = 256;
            int extraBlocks = 0;
            int i;
            byte[] buff = new byte[blockSize];
            do {
                for (i = 0; i < blockSize; ++i) {
                    byte head = this.Dequeue ();
                    if (0 == head) {
                        terminated = true;
                        break;
                    }
                    buff [extraBlocks * blockSize + i] = head;
                }
                if (!terminated) {
                    ++extraBlocks;
                    Array.Resize (ref buff, (extraBlocks + 1) * blockSize);
                }
            } while (!terminated);
            Array.Resize (ref buff, extraBlocks * blockSize + i);
            var retval = Encoding.UTF8.GetString (buff);
            return retval;
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
                    throw new OperationCanceledException ();
                }
            }
            return bStream.ToArray ();
        }
    }
}
