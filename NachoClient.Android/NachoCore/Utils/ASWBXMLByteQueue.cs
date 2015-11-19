using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NachoCore.Utils;
using System.IO.MemoryMappedFiles;

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
        protected byte[] buffer;
        protected long bufferPos;
        protected long bufferEnd;
        protected GatedMemoryStream RedactedCopy;
        protected FileStream dataStream;
        protected long Pos;

        private ASWBXMLByteQueue (FileStream data, byte[] bytes, GatedMemoryStream redactedCopy = null)
        {
            RedactedCopy = redactedCopy;
            dataStream = data;
            if (null == bytes) {
                buffer = new byte[bufferSize(data)];
                fillBuffer ();
            } else {
                buffer = bytes;
            }
        }

        /// <summary>
        /// Maximum size of the read buffer. 10K. Anything less than that will be read fully into the buffer/memory.
        /// </summary>
        const int MaxBufferSize = 1024*10;

        int bufferSize (FileStream data)
        {
            if (data.Length >= MaxBufferSize) {
                return MaxBufferSize;
            } else {
                return (int)data.Length;
            }
        }

        public ASWBXMLByteQueue (byte[] bytes, GatedMemoryStream redactedCopy = null) : this (null, bytes, redactedCopy)
        {
        }

        public ASWBXMLByteQueue (FileStream data, GatedMemoryStream redactedCopy = null) : this (data, null, redactedCopy)
        {
        }

        bool endOfBuffer {
            get {
                return bufferPos == bufferEnd;
            }
        }

        bool streamEOF {
            get {
                return Pos == dataStream.Length;
            }
        }

        bool EOF {
            get {
                if (null != dataStream) {
                    return streamEOF;
                } else {
                    return endOfBuffer;
                }
            }
        }

        void fillBuffer ()
        {
            if (null == dataStream) {
                return;
            }
            bufferEnd = dataStream.Read (buffer, 0, buffer.Length);
            Log.Info (Log.LOG_SYS, "WBXML buffer: read {0} into buffer", bufferEnd);
            bufferPos = 0;
        }

        public int Peek ()
        {
            if (EOF) {
                return -1;
            }
            if (endOfBuffer) {
                fillBuffer ();
            }
            return buffer [bufferPos];
        }

        public byte Dequeue ()
        {
            if (EOF) {
                throw new WBXMLReadPastEndException (Pos);
            }
            if (endOfBuffer) {
                fillBuffer ();
            }
            var retval = buffer [bufferPos++];
            Pos++;

            // We currently redact (telemetry) anything big, so stop copying bytes when something gets big.
            // The alternative is to rewrite the logic so we know what is redacted before we start keeping bytes.
            // We were ballooning attachment downloads in memory here.
            if (null != RedactedCopy && 1024 >= RedactedCopy.Length) {
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

        public void DequeueStringToStream (Stream stream, CancellationToken cToken, bool base64Decode = false)
        {
            var decoder = new NcBase64 ();
            byte currentByte = 0x00;
            do {
                if (cToken.IsCancellationRequested) {
                    throw new OperationCanceledException ();
                }
                currentByte = this.Dequeue ();
                if (currentByte != 0x00) {
                    if (base64Decode) {
                        var decoded = decoder.Next (currentByte);
                        if (0 <= decoded) {
                            stream.WriteByte ((byte)decoded);
                        }
                    } else {
                        stream.WriteByte (currentByte);
                    }
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
