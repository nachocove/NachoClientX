using SQLite;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Reflection;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McObject
    {
        /// OperationCaptures holds a set of NcCapture for one type of operations 
        /// (Insert, Delete, or Update) for all tables. There is one NcCapture
        /// per thread per table. It is per thread because NcCapture is not
        /// thread-safe and I don't want to serialize all db operations.
        private class OperationCaptures
        {
            public struct CaptureKey
            {
                public int threadId;
                public string className;

                public CaptureKey (int threadId_, string className_)
                {
                    threadId = threadId_;
                    className = className_;
                }

                public static CaptureKey FromClassName (string className)
                {
                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    return new CaptureKey (threadId, className);
                }
            }

            ConcurrentDictionary<CaptureKey, NcCapture> Captures;
            string OpName;

            public OperationCaptures (string opName)
            {
                Captures = new ConcurrentDictionary<CaptureKey, NcCapture> ();
                OpName = opName;
            }

            private string CaptureName (string className)
            {
                return "McModel." + className + "." + OpName;
            }

            public void Add (string className)
            {
                CaptureKey key = CaptureKey.FromClassName (className);
                if (!Captures.ContainsKey (key)) {
                    string kind = CaptureName (className);
                    NcCapture.AddKind (kind);
                    Captures.TryAdd (key, NcCapture.Create (kind));
                }
            }

            public NcCapture Find (string className)
            {
                CaptureKey key = CaptureKey.FromClassName (className);
                Add (className); // Add() will check if the key already exists
                NcAssert.True (Captures.ContainsKey (key));
                return Captures [key];
            }
        }

        private static OperationCaptures InsertCaptures;
        private static OperationCaptures DeleteCaptures;
        private static OperationCaptures UpdateCaptures;

        [PrimaryKey, AutoIncrement, Unique]
        public virtual int Id { get; set; }
        // Optimistic concurrency control
        public DateTime LastModified { get; set; }

        public McObject ()
        {
            Id = 0;
            LastModified = DateTime.MinValue;

            string className = ClassName ();
            if (null == InsertCaptures) {
                InsertCaptures = new OperationCaptures ("Insert");
                InsertCaptures.Add (className);
            }
            if (null == DeleteCaptures) {
                DeleteCaptures = new OperationCaptures ("Delete");
                DeleteCaptures.Add (className);
            }
            if (null == UpdateCaptures) {
                UpdateCaptures = new OperationCaptures ("Update");
                UpdateCaptures.Add (className);
            }
        }

        public string ClassName ()
        {
            return GetType ().Name;
        }

        public virtual int Insert ()
        {
            NcAssert.True (0 == Id);
            NcCapture capture = InsertCaptures.Find (ClassName ());
            capture.Start ();
            int rc =  NcModel.Instance.Db.Insert (this);
            capture.Stop ();
            capture.Reset ();
            return rc;
        }

        public virtual int Delete ()
        {
            NcAssert.True (0 < Id);
            NcCapture capture = DeleteCaptures.Find (ClassName ());
            capture.Start ();
            int rc = NcModel.Instance.Db.Delete (this);
            capture.Stop ();
            capture.Reset ();
            return rc;
        }

        public virtual int Update ()
        {
            NcAssert.True (0 < Id);
            NcCapture capture = UpdateCaptures.Find (ClassName ());
            capture.Start ();
            int rc = NcModel.Instance.Db.Update (this);
            capture.Stop ();
            capture.Reset ();
            return rc;
        }

        public static T QueryById<T> (int id) where T : McObject, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " f.Id = ? ", 
                    typeof(T).Name), 
                id).SingleOrDefault ();
        }
    }
}

