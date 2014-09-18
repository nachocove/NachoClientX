//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.Model
{
    public sealed class NcModel
    {
        private string Documents;

        // RateLimiter PUBLIC FOR TEST ONLY.
        public NcRateLimter RateLimiter { set; get; }

        private const string KTmpPathSegment = "tmp";
        private const string KFilesPathSegment = "files";

        public string DbFileName { set; get; }

        public string TeleDbFileName { set; get; }

        public SQLiteConnection Db {
            get {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                SQLiteConnection db = null;
                if (!DbConns.TryGetValue (threadId, out db)) {
                    db = new SQLiteConnection (DbFileName, 
                        SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex, 
                        storeDateTimeAsTicks: true);
                    db.BusyTimeout = TimeSpan.FromSeconds (10.0);
                    db.TraceThreshold = 100;
                    NcAssert.True (DbConns.TryAdd (threadId, db));
                }
                return db;
            } 
        }

        private SQLiteConnection _TeleDb = null;

        public SQLiteConnection TeleDb {
            get {
                if (null == _TeleDb) {
                    _TeleDb = new SQLiteConnection (TeleDbFileName,
                        SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex,
                        storeDateTimeAsTicks: true);
                    _TeleDb.BusyTimeout = TimeSpan.FromSeconds (10.0);
                }
                return _TeleDb;
            }
        }

        private ConcurrentDictionary<int, SQLiteConnection> DbConns;
        private ConcurrentDictionary<int, int> TransDepth;

        public string GetFileDirPath (int accountId, string segment)
        {
            return Path.Combine (Documents, KFilesPathSegment, accountId.ToString (), segment);
        }

        public void InitalizeDirs (int accountId)
        {
            Directory.CreateDirectory (GetFileDirPath (accountId, KTmpPathSegment));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McDocument ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McAttachment ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McBody ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McPortrait ().GetFilePathSegment ()));
        }

        private void ConfigureDb (SQLiteConnection db)
        {
            NcAssert.NotNull (db);
            var cache_size = db.ExecuteScalar<int> ("PRAGMA cache_size");
            QueueLogInfo (string.Format ("PRAGMA cache_size: {0}", cache_size));
            var journal_mode = db.ExecuteScalar<string> ("PRAGMA journal_mode");
            QueueLogInfo (string.Format ("PRAGMA journal_mode: {0}", journal_mode));
            if ("wal" != journal_mode.ToLower ()) {
                journal_mode = db.ExecuteScalar<string> ("PRAGMA journal_mode = WAL");
                NcAssert.Equals ("wal", journal_mode.ToLower ());
                QueueLogInfo (string.Format ("PRAGMA journal_mode set to {0}", journal_mode));
            }
            var wal_autocheckpoint = db.ExecuteScalar<int> ("PRAGMA wal_autocheckpoint");
            QueueLogInfo (string.Format ("PRAGMA wal_autocheckpoint: {0}", wal_autocheckpoint));
            if (1000 != wal_autocheckpoint) {
                journal_mode = db.ExecuteScalar<string> ("PRAGMA wal_autocheckpoint = 1000");
                NcAssert.Equals (1000, wal_autocheckpoint);
                QueueLogInfo (string.Format ("PRAGMA wal_autocheckpoint set to {0}", wal_autocheckpoint));
            }
            var synchronous = db.ExecuteScalar<int> ("PRAGMA synchronous");
            QueueLogInfo (string.Format ("PRAGMA synchronous: {0}", synchronous));
            if (1 != synchronous) {
                db.Execute ("PRAGMA synchronous = 1");
                synchronous = db.ExecuteScalar<int> ("PRAGMA synchronous");
                NcAssert.Equals (synchronous, 1);
                QueueLogInfo (string.Format ("PRAGMA synchronous set to: {0}", synchronous));
            }
        }

        private void InitializeDb ()
        {
            RateLimiter = new NcRateLimter (16, 0.250);
            DbConns = new ConcurrentDictionary<int, SQLiteConnection> ();
            TransDepth = new ConcurrentDictionary<int, int> ();
            Db.CreateTable<McAccount> ();
            Db.CreateTable<McConference> ();
            Db.CreateTable<McCred> ();
            Db.CreateTable<McMapFolderFolderEntry> ();
            Db.CreateTable<McFolder> ();
            Db.CreateTable<McEmailAddress> ();
            Db.CreateTable<McEmailMessage> ();
            Db.CreateTable<McEmailMessageCategory> ();
            Db.CreateTable<McEmailMessageScoreSyncInfo> ();
            Db.CreateTable<McEmailMessageDependency> ();
            Db.CreateTable<McMeetingRequest> ();
            Db.CreateTable<McAttachment> ();
            Db.CreateTable<McContact> ();
            Db.CreateTable<McContactDateAttribute> ();
            Db.CreateTable<McContactStringAttribute> ();
            Db.CreateTable<McContactAddressAttribute> ();
            Db.CreateTable<McContactEmailAddressAttribute> ();
            Db.CreateTable<McEmailAddressScoreSyncInfo> ();
            Db.CreateTable<McPolicy> ();
            Db.CreateTable<McProtocolState> ();
            Db.CreateTable<McServer> ();
            Db.CreateTable<McPending> ();
            Db.CreateTable<McPendDep> ();
            Db.CreateTable<McCalendar> ();
            Db.CreateTable<McException> ();
            Db.CreateTable<McAttendee> ();
            Db.CreateTable<McCalendarCategory> ();
            Db.CreateTable<McRecurrence> ();
            Db.CreateTable<McEvent> ();
            Db.CreateTable<McTask> ();
            Db.CreateTable<McBody> ();
            Db.CreateTable<McDocument> ();
            Db.CreateTable<McMutables> ();
            Db.CreateTable<McPath> ();
            Db.CreateTable<McNote> ();
            Db.CreateTable<McPortrait> ();
            ConfigureDb (Db);
        }

        private void InitializeTeleDb ()
        {
            TeleDb.CreateTable<McTelemetryEvent> ();
            ConfigureDb (TeleDb);
        }

        private void QueueLogInfo (string message)
        {
            Log.IndirectQ.Enqueue (new LogElement () {
                Level = LogElement.LevelEnum.Info,
                Subsystem = Log.LOG_DB,
                Message = message,
                Occurred = DateTime.UtcNow,
            });
        }

        private NcModel ()
        {
            NcAssert.True (2 == SQLite3.Threadsafe () || 1 == SQLite3.Threadsafe ());
            SQLite3.Config (SQLite3.ConfigOption.Log, Device.Instance.GetSQLite3ErrorCallback ((code, message) => {
                Log.IndirectQ.Enqueue (new LogElement () {
                    Level = LogElement.LevelEnum.Error,
                    Subsystem = Log.LOG_DB,
                    Message = string.Format ("SQLite Error Log (code {0}): {1}", code, message),
                    Occurred = DateTime.UtcNow,
                });
            }), 
                (IntPtr)null);
            Documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            DbFileName = Path.Combine (Documents, "db");
            InitializeDb ();
            TeleDbFileName = Path.Combine (Documents, "teledb");
            InitializeTeleDb ();
        }

        private static volatile NcModel instance;
        private static object syncRoot = new Object ();

        public static NcModel Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcModel ();
                    }
                }
                return instance; 
            }
        }

        private NcTimer CheckPointTimer;

        private class CheckpointResult
        {
            // Note: these property names can't be changed - they are hard-coded in the SQLite C code.
            public int busy { set; get; }

            public int log { set; get; }

            public int checkpointed { set; get; }
        }

        public void Start ()
        {

            CheckPointTimer = new NcTimer ("NcModel.CheckPointTimer", state => {
                var checkpointCmd = "PRAGMA main.wal_checkpoint (PASSIVE);";
                foreach (var db in new List<SQLiteConnection> { Db, TeleDb }) {
                    db.Query<CheckpointResult> (checkpointCmd);
                    /*
                     * TODO: Try using the C interface. It doesn't seem that the log/checkpointed
                     * values always make sense as they don't float down to zero. This is the case 
                     * no matter the mode.
                    if (0 != results.Count && (0 != results[0].busy || 0 < results[0].checkpointed)) {
                        Log.Info (Log.LOG_DB, "Checkpoint of {0}: {1}, {2}, {3}", db.DatabasePath, 
                            results[0].busy, results[0].log, results[0].checkpointed);
                    }
                     */
                }
            }, null, 1000, 1000);
            CheckPointTimer.Stfu = true;
        }

        public void Stop ()
        {
            if (null != CheckPointTimer) {
                CheckPointTimer.Dispose ();
                CheckPointTimer = null;
            }
        }

        public bool IsInTransaction ()
        {
            int depth = 0;
            return (TransDepth.TryGetValue (Thread.CurrentThread.ManagedThreadId, out depth) && depth > 0);
        }

        public int BusyProtect (Func<int> action)
        {
            int rc = 0;
            if (IsInTransaction ()) {
                // Do not loop-retry within the transaction. 
                // If we are being given the busy, then rollback may be needed to release a SQLite lock.
                rc = action ();
                return rc;
            }
            var whoa = DateTime.UtcNow.AddSeconds (5.0);
            do {
                try {
                    rc = action ();
                    return rc;
                } catch (SQLiteException ex) {
                    if (ex.Message.Contains ("Busy")) {
                        if (DateTime.UtcNow > whoa) {
                            Log.Error (Log.LOG_DB, "Caught a Busy");
                            throw;
                        } else {
                            Log.Warn (Log.LOG_DB, "Caught a Busy");
                        }
                    } else {
                        Log.Error (Log.LOG_DB, "Caught a non-Busy: {0}", ex);
                        throw;
                    }
                }
            } while (true);
        }

        public void TakeTokenOrSleep ()
        {
            if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId &&
                !IsInTransaction ()) {
                RateLimiter.TakeTokenOrSleep ();
            }
        }

        public void RunInTransaction (Action action)
        {
            if (NcModel.Instance.IsInTransaction ()) {
                // If we are already in transaction, then no need to nest - just run the code.
                action ();
                return;
            }
            var threadId = Thread.CurrentThread.ManagedThreadId;
            if (NcApplication.Instance.UiThreadId != threadId) {
                // We aren't in a transaction yet. If not UI thread, adhere to rate limiting.
                RateLimiter.TakeTokenOrSleep ();
            }
            int exitValue = 0;
            // TODO: We use a concurrent dict for now, but we can move to a var-per-conn, 
            // since each conn is single threaded.
            TransDepth.AddOrUpdate (threadId, 1, (key, oldValue) => {
                exitValue = oldValue;
                return oldValue + 1;
            });
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch ();
            try {
                var whoa = DateTime.UtcNow.AddSeconds (5.0);
                // It is okay to loop here, because a busy will have caused us to ROLLBACK and release
                // all locks. We can then run the action code again.
                do {
                    try {
                        watch.Start ();
                        Db.RunInTransaction (action);
                        watch.Stop ();
                        var span = watch.ElapsedMilliseconds;
                        if (1000 < span) {
                            Log.Error (Log.LOG_DB, "RunInTransaction: {0}ms for {1}", span, 
                                new System.Diagnostics.StackTrace (true));
                        }
                        break;
                    } catch (SQLiteException ex) {
                        watch.Reset ();
                        if (ex.Message.Contains ("Busy")) {
                            Log.Warn (Log.LOG_DB, "Caught a Busy");
                        } else {
                            Log.Error (Log.LOG_DB, "Caught a non-Busy: {0}", ex);
                            throw;
                        }
                    }
                } while (DateTime.UtcNow < whoa);
            } finally {
                NcAssert.True (TransDepth.TryUpdate (threadId, exitValue, exitValue + 1));
            }
        }

        public void Info ()
        {
            Log.Info (Log.LOG_DB, "SQLite version number {0}", SQLite3.LibVersionNumber ());
        }

        public void EngageRateLimiter ()
        {
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs ea) => {
                var siea = (StatusIndEventArgs)ea;
                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStarted) {
                    RateLimiter.Enabled = true;
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStopped) {
                    RateLimiter.Enabled = false;
                }
            };
        }

        public void Reset (string dbFileName)
        {
            DbFileName = dbFileName;
            InitializeDb ();
            GarbageCollectFiles ();
        }

        public string TmpPath (int accountId)
        {
            var guidString = Guid.NewGuid ().ToString ("N");
            return Path.Combine (GetFileDirPath (accountId, KTmpPathSegment), guidString);
        }

        // To be run synchronously only on app boot.
        public void GarbageCollectFiles ()
        {
            // Find any top-level file dir not backed by McAccount. Delete it.
            var acctLevelDirs = Directory.GetDirectories (Documents);
            // Foreach account...
            foreach (var acctDir in acctLevelDirs) {
                int accountId;
                var suffix = Path.GetFileName (acctDir);
                try {
                    accountId = (int)uint.Parse (suffix);
                    if (null == McAccount.QueryById<McAccount> (accountId)) {
                        try {
                            Directory.Delete (acctDir, true);
                        } catch (Exception ex) {
                            Log.Error (Log.LOG_DB, "GarbageCollectFiles: Exception deleting account-level dir: {0}", ex);
                        }
                        continue;
                    }
                } catch {
                    // Must not be a number, so we're not interested in it. Loop.
                    continue;
                }
                var tmpTop = GetFileDirPath (accountId, KTmpPathSegment);
                try {
                    // Remove any tmp files/dirs.
                    foreach (var dir in Directory.GetDirectories (tmpTop)) {
                        Directory.Delete (dir, true);
                    }
                    foreach (var file in Directory.GetFiles (tmpTop)) {
                        File.Delete (file);
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_DB, "GarbageCollectFiles: Exception cleaning up tmp files: {0}", ex);
                }
            }
        }
    }
}
