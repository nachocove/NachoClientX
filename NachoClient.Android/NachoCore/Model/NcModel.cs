//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Threading;
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

        public string FilesDir { set; get; }

        public string AttachmentsDir { set; get; }

        public string BodiesDir { set; get; }

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

        private void InitalizeDirs ()
        {
            FilesDir = Path.Combine (Documents, "files");
            Directory.CreateDirectory (Path.Combine (Documents, FilesDir));
            AttachmentsDir = Path.Combine (Documents, "attachments");
            Directory.CreateDirectory (Path.Combine (Documents, AttachmentsDir));
            BodiesDir = Path.Combine (Documents, "bodies");
            Directory.CreateDirectory (Path.Combine (Documents, BodiesDir));
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
            Db.CreateTable<McTimeZone> ();
            Db.CreateTable<McTask> ();
            Db.CreateTable<McBody> ();
            Db.CreateTable<McFile> ();
            Db.CreateTable<McMutables> ();
            Db.CreateTable<McPath> ();
            Db.CreateTable<McNote> ();
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
            InitalizeDirs ();
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

        public bool IsInTransaction ()
        {
            int depth = 0;
            return (TransDepth.TryGetValue (Thread.CurrentThread.ManagedThreadId, out depth) && depth > 0);
        }

        public int BusyProtect (Func<int> action)
        {
            int rc = 0;
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
            // DO NOT ADD LOGGING IN THE TRANSACTION, BECAUSE WE DON'T WANT LOGGING WRITES TO GET LUMPED IN.
            var threadId = Thread.CurrentThread.ManagedThreadId;
            if (NcApplication.Instance.UiThreadId != threadId && !NcModel.Instance.IsInTransaction ()) {
                NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
            }
            int exitValue = 0;
            TransDepth.AddOrUpdate (threadId, 1, (key, oldValue) => {
                exitValue = oldValue;
                return oldValue + 1;
            });
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch ();
            try {
                var whoa = DateTime.UtcNow.AddSeconds (5.0);
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

        public void Nop ()
        {
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
        }
    }
}

