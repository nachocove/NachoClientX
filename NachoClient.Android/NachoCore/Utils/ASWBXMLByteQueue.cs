using System;
using System.IO;
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

    class WBXMLWritePastEndException : Exception
    {
    }

    class ASWBXMLByteQueue : IDisposable
    {
        protected byte[] buffer;
        protected long bufferPos;
        protected long bufferEnd;
        protected GatedMemoryStream RedactedCopy;
        protected byte[]RedactedCopyBuffer;
        protected long RedactedCopyLen;
        protected long RedactedCopyBufferPos;
        protected FileStream dataStream;
        protected long Pos;
        protected long dataStreamLen;

        private ASWBXMLByteQueue (FileStream data, byte[] bytes, GatedMemoryStream redactedCopy = null)
        {
            RedactedCopy = redactedCopy;
            if (null != RedactedCopy) {
                RedactedCopyLen = 4096;
                RedactedCopyBuffer = new byte[RedactedCopyLen];
                RedactedCopyBufferPos = 0;
            }
            dataStream = data;
            if (dataStream != null) {
                dataStreamLen = dataStream.Length;
            }
            if (null == bytes) {
                buffer = new byte[bufferSize(data)];
                fillBuffer ();
            } else {
                buffer = bytes;
                bufferPos = 0;
                bufferEnd = bytes.Length;
            }
        }

        #region IDisposable implementation

        public void Dispose ()
        {
            if (RedactedCopyBufferPos > 0) {
                RedactedCopy.Write (RedactedCopyBuffer, 0, (int)RedactedCopyBufferPos);
                RedactedCopyBufferPos = 0;
            }
            RedactedCopy.Flush ();
        }

        #endregion

        /// <summary>
        /// Maximum size of the read buffer. 1M. Anything less than that will be read fully into the buffer/memory.
        /// </summary>
        const int MaxBufferSize = 1024*1024;

        int bufferSize (FileStream data)
        {
            dataStreamLen = data.Length;
            if (dataStreamLen >= MaxBufferSize) {
                return MaxBufferSize;
            } else {
                return (int)dataStreamLen;
            }
        }

        public ASWBXMLByteQueue (byte[] bytes, GatedMemoryStream redactedCopy = null) : this (null, bytes, redactedCopy)
        {
        }

        public ASWBXMLByteQueue (FileStream data, GatedMemoryStream redactedCopy = null) : this (data, null, redactedCopy)
        {
        }

        void fillBuffer ()
        {
            NcAssert.NotNull (dataStream);
            int toRead = buffer.Length;
            int pos = 0;
            while (toRead > 0) {
                int n = dataStream.Read (buffer, pos, toRead);
                if (n == 0) {
                    break;
                }
                toRead -= n;
                pos += n;
            }
            bufferPos = 0;
            bufferEnd = pos;
        }

        public int Peek ()
        {
            if ((null != dataStream && Pos >= dataStreamLen) || (null == dataStream && bufferPos >= bufferEnd)) {
                return -1;
            }
            if (null != dataStream && bufferPos >= bufferEnd) {
                fillBuffer ();
            }
            return buffer [bufferPos];
        }

        public byte Dequeue ()
        {
            if ((null != dataStream && Pos >= dataStreamLen) || (null == dataStream && bufferPos >= bufferEnd)) {
                throw new WBXMLReadPastEndException (Pos);
            }
            if (null != dataStream && bufferPos >= bufferEnd) {
                fillBuffer ();
            }
            var retval = buffer [bufferPos++];
            Pos++;

            // We currently redact (telemetry) anything big, so stop copying bytes when something gets big.
            // The alternative is to rewrite the logic so we know what is redacted before we start keeping bytes.
            // We were ballooning attachment downloads in memory here.
            if (null != RedactedCopy && 1024 >= RedactedCopyLen) {
                RedactedCopyBuffer [RedactedCopyBufferPos++] = retval;
                if (RedactedCopyBufferPos == RedactedCopyLen) {
                    RedactedCopy.Write (RedactedCopyBuffer, 0, (int)RedactedCopyBufferPos);
                    RedactedCopyBufferPos = 0;
                }
            }
            return retval;
        }

        public int DequeueMultibyteInt ()
        {
            int iReturn = 0;
            byte singleByte = 0xFF;

            do {
                iReturn <<= 7;

                singleByte = Dequeue ();
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
            byte[] lbuffer = new byte[1024*10]; // 10K buffer
            int lbufferPos = 0;

            do {
                if (cToken.IsCancellationRequested) {
                    throw new OperationCanceledException ();
                }
                currentByte = Dequeue ();
                if (currentByte != 0x00) {
                    if (base64Decode) {
                        var decoded = decoder.Next (currentByte);
                        if (0 <= decoded) {
                            lbuffer[lbufferPos++] = (byte)decoded;
                        }
                    } else {
                        lbuffer[lbufferPos++] = currentByte;
                    }
                    if (lbufferPos == lbuffer.Length) {
                        stream.Write (lbuffer, 0, lbuffer.Length);
                        lbufferPos = 0;
                    }
                }
            } while (currentByte != 0x00);
            if (lbufferPos > 0) {
                stream.Write (lbuffer, 0, lbufferPos);
            }
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
                    byte head = Dequeue ();
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
            byte[] lbuffer = new byte[length];
            int lbufferPos = 0;
            for (int i = 0; i < length; i++) {
                // TODO: Improve this handling. We are technically UTF-8, meaning
                // that characters could be more than one byte long. This will fail if we have
                // characters outside of the US-ASCII range
                lbuffer[lbufferPos++] = Dequeue ();
                if (lbufferPos > length) {
                    throw new WBXMLWritePastEndException ();
                }
                if (cToken.IsCancellationRequested) {
                    throw new OperationCanceledException ();
                }
            }
            return lbuffer;
        }
    }
}
