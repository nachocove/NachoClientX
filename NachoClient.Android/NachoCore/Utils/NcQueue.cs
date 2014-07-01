//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;

namespace NachoCore.Utils
{
    public interface NcQueueElement
    {
        uint GetSize ();
    }

    public class NcQueue<T> where T : NcQueueElement
    {
        private Queue<T> _Queue;
        private object Lock;
        private SemaphoreSlim ProducedCount;

        private CancellationToken _Token;
        public CancellationToken Token {
            set {
                _Token = value;
            }
        }

        private ulong _NumEnqueue;
        public ulong NumEnqueue {
            get {
                return _NumEnqueue;
            }
        }

        private ulong _NumDequeue;
        public ulong NumDequeue {
            get {
                return _NumDequeue;
            }
        }

        private ulong _NumEnqueueBytes;
        public ulong NumEnqueueBytes {
            get {
                return _NumEnqueueBytes;
            }
        }

        private ulong _NumDequeueBytes;
        public ulong NumDequeueBytes {
            get {
                return _NumDequeueBytes;
            }
        }

        private ulong _MaxNumQueued;
        public ulong MaxNumQueued {
            get {
                return _MaxNumQueued;
            }
        }

        private ulong _MaxNumQueuedBytes;
        public ulong MaxNumQueuedBytes {
            get {
                return _MaxNumQueuedBytes;
            }
        }

        public NcQueue ()
        {
            _Queue = new Queue<T> ();
            Lock = new object ();
            ProducedCount = new SemaphoreSlim (0, Int32.MaxValue);
            _NumEnqueue = 0;
            _NumDequeue = 0;
            _NumEnqueueBytes = 0;
            _NumDequeueBytes = 0;
            _MaxNumQueued = 0;
            _MaxNumQueuedBytes = 0;
        }

        public void Enqueue (T obj)
        {
            lock (Lock) {
                _NumEnqueueBytes += obj.GetSize ();
                NcAssert.True (_NumEnqueueBytes >= _NumDequeueBytes);
                ulong numBytes = _NumEnqueueBytes - _NumDequeueBytes;
                if (numBytes > _MaxNumQueuedBytes) {
                    _MaxNumQueuedBytes = numBytes;
                }

                _NumEnqueue++;
                NcAssert.True (_NumEnqueue >= _NumDequeue);
                ulong numElements = _NumEnqueue - _NumDequeue;
                if (numElements > _MaxNumQueued) {
                    _MaxNumQueued = numElements;
                }

                _Queue.Enqueue (obj);
            }
            ProducedCount.Release ();
        }

        public T Dequeue ()
        {
            ProducedCount.Wait (_Token);
            lock (Lock) {
                T obj = (T)_Queue.Dequeue ();
                _NumDequeueBytes += obj.GetSize ();
                _NumDequeue++;
                return obj;
            }
        }
    }
}

