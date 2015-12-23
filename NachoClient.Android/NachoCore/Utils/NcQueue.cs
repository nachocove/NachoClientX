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
        private List<T> _Queue;
        private object Lock;
        private SemaphoreSlim ProducedCount;

        public CancellationToken Token { get; set; }

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
            _Queue = new List<T> ();
            Lock = new object ();
            ProducedCount = new SemaphoreSlim (0, Int32.MaxValue);
            _NumEnqueue = 0;
            _NumDequeue = 0;
            _NumEnqueueBytes = 0;
            _NumDequeueBytes = 0;
            _MaxNumQueued = 0;
            _MaxNumQueuedBytes = 0;
        }

        private void UpdateEnqueueStats (T obj)
        {
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
        }

        public void Enqueue (T obj)
        {
            lock (Lock) {
                UpdateEnqueueStats (obj);
                _Queue.Add (obj);
            }
            ProducedCount.Release ();
        }

        public void Undequeue (T obj)
        {
            lock (Lock) {
                UpdateEnqueueStats (obj);
                _Queue.Insert (0, obj);
            }
            ProducedCount.Release ();
        }

        public T Dequeue ()
        {
            Token.ThrowIfCancellationRequested ();
            ProducedCount.Wait (Token);
            lock (Lock) {
                NcAssert.True (0 < _Queue.Count);
                T obj = _Queue [0];
                _Queue.RemoveAt (0);
                _NumDequeueBytes += obj.GetSize ();
                _NumDequeue++;
                return obj;
            }
        }

        public bool IsEmpty ()
        {
            lock (Lock) {
                return (0 == _Queue.Count);
            }
        }

        public delegate bool QueueItemMatchFunction (T obj1);

        public void UndequeueIfNot (T obj, QueueItemMatchFunction match)
        {
            lock (Lock) {
                T objAlreadyThere = Peek ();
                if (obj == default(T) && !match (objAlreadyThere)) {
                    Undequeue (obj);
                }
            }
        }

        public T DequeueIf (QueueItemMatchFunction match)
        {
            lock (Lock) {
                T obj = Peek ();
                if (obj != default(T) && match (obj)) {
                    Dequeue ();
                }
                return obj;
            }
        }

        public T Peek ()
        {
            Token.ThrowIfCancellationRequested ();
            T obj = default(T);
            lock (Lock) {
                if (_Queue.Count > 0) {
                    obj = _Queue [0];
                }
            }
            return obj;
        }
    }
}

