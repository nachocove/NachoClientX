using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Reflection;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McAbstrObject
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

        [PrimaryKey, AutoIncrement, Unique]
        public virtual int Id { get; set; }
        // Optimistic concurrency control.
        public DateTime LastModified { get; set; }
        // Set only on Insert.
        public DateTime CreatedAt { get; set; }

        public int RowVersion { get; set; }

        public int MigrationVersion { get; set; }

        protected Boolean isDeleted;

        protected string CaptureName (string opName)
        {
            return "NcModel." + ClassName () + "." + opName;
        }

        protected NcCapture CaptureWithStart (string opName)
        {
            return NcCapture.CreateAndStart (CaptureName (opName));
        }

        public McAbstrObject ()
        {
            Id = 0;
            LastModified = DateTime.MinValue;
            isDeleted = false;
            MigrationVersion = NcMigration.CurrentVersion;
        }

        public string ClassName ()
        {
            return GetType ().Name;
        }

        public virtual int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                NcAssert.True (0 == Id);
                NcAssert.True (!isDeleted);
                NcModel.Instance.TakeTokenOrSleep ();
                LastModified = DateTime.UtcNow;
                CreatedAt = LastModified;
                int rc = NcModel.Instance.BusyProtect (() => {
                    return NcModel.Instance.Db.Insert (this);
                });
                return rc;
            }
        }

        public virtual int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                NcAssert.True (0 < Id);
                isDeleted = true;
                NcModel.Instance.TakeTokenOrSleep ();
                int rc = NcModel.Instance.BusyProtect (() => {
                    return NcModel.Instance.Db.Delete (this);
                });
                return rc;
            }
        }

        public delegate bool Mutator (McAbstrObject record);

        /// <summary>
        /// Update() with optimistic concurrency.
        /// </summary>
        /// <returns>The the value of the latest record we successfuly wrote or pulled from the DB, otherwise, this</returns>
        /// <param name="mutator">Mutator must return false if it can't apply change.</param>
        /// <param name="count">Count is the same as the retval from plain-old Update(). 0 indicates failure.</param>
        /// <param name="tries">Tries before giving up.</param>
        /// <typeparam name="T">T must match the type of the object.</typeparam>
        public virtual T UpdateWithOCApply<T> (Mutator mutator, out int count, int tries = 50) where T : McAbstrObject, new()
        {
            NcAssert.True (typeof(T) == this.GetType ());
            var record = this;
            var totalTries = tries;
            count = 0;
            while (0 == count && 0 < tries) {
                --tries;
                if (!mutator (record)) {
                    return (T)record;
                }
                int priorVersion = record.RowVersion;
                record.RowVersion = priorVersion + 1;
                try {
                    count = NcModel.Instance.Update (record, record.GetType (), true, priorVersion);
                } catch (SQLiteException ex) {
                    if (ex.Result == SQLite3.Result.Busy) {
                        Log.Warn (Log.LOG_DB, "UpdateWithOCApply: Busy");
                        Thread.Sleep (100);
                        count = 0;
                    } else {
                        throw;
                    }
                }
                if (0 < count) {
                    break;
                }
                try {
                    record = NcModel.Instance.Db.Get<T> (record.Id);
                } catch {
                    Log.Warn (Log.LOG_DB, "UpdateWithOCApply: unable to fetch record");
                    record = null;
                    break;
                }
            }
            if (10 < totalTries - tries) {
                Log.Warn (Log.LOG_DB, "UpdateWithOCApply: too many tries: {0}", totalTries - tries);
            }
            return (T)record;
        }

        /// <summary>
        /// Update() with optimistic concurrency.
        /// </summary>
        /// <returns>The the value of the latest record we successfuly wrote or pulled from the DB, otherwise, this</returns>
        /// <param name="mutator">Mutator must return false if it can't apply change.</param>
        /// <param name="tries">Tries before giving up.</param>
        /// <typeparam name="T">T must match the type of the object.</typeparam>
        public virtual T UpdateWithOCApply<T> (Mutator mutator, int tries = 100) where T : McAbstrObject, new()
        {
            int count = 0;
            var record = UpdateWithOCApply<T> (mutator, out count, tries);
            NcAssert.True (0 < count || null == record, string.Format ("UpdateWithOCApply count={0} record={1}", count, record));
            return record;
        }

        public virtual int Update ()
        {
            using (var capture = CaptureWithStart ("Update")) {
                NcAssert.True (0 < Id);
                NcAssert.True (!isDeleted);
                NcModel.Instance.TakeTokenOrSleep ();
                LastModified = DateTime.UtcNow;
                int rc = NcModel.Instance.BusyProtect (() => {
                    return NcModel.Instance.Db.Update (this);
                });
                return rc;
            }
        }

        public static T QueryById<T> (int id) where T : McAbstrObject, new()
        {
            return NcModel.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                " f.Id = ? ", 
                    typeof(T).Name), 
                id).SingleOrDefault ();
        }

        public static int DeleteById<T> (int id) where T : McAbstrObject, new()
        {
            return NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute (
                    string.Format ("DELETE FROM {0} WHERE " +
                    " Id = ? ", 
                        typeof(T).Name), 
                    id);
            });
        }
    }
}

