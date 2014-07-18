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
                    DbConns.TryAdd (threadId, db);
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

        private void InitializeDb ()
        {
            RateLimiter = new NcRateLimter (16, 0.250);
            DbConns = new ConcurrentDictionary<int, SQLiteConnection> ();
            TransDepth = new ConcurrentDictionary<int, int> ();
            Db.CreateTable<McAccount> ();
            Db.CreateTable<McCred> ();
            Db.CreateTable<McMapFolderFolderEntry> ();
            Db.CreateTable<McFolder> ();
            Db.CreateTable<McEmailMessage> ();
            Db.CreateTable<McEmailMessageCategory> ();
            Db.CreateTable<McEmailMessageScoreSyncInfo> ();
            Db.CreateTable<McEmailMessageDependency> ();
            Db.CreateTable<McAttachment> ();
            Db.CreateTable<McContact> ();
            Db.CreateTable<McContactDateAttribute> ();
            Db.CreateTable<McContactStringAttribute> ();
            Db.CreateTable<McContactAddressAttribute> ();
            Db.CreateTable<McContactScoreSyncInfo> ();
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
        }

        private void InitializeTeleDb ()
        {
            TeleDb.CreateTable<McTelemetryEvent> ();
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
                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_ViewScrollingStarted) {
                    RateLimiter.Enabled = true;
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_ViewScrollingStopped) {
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

