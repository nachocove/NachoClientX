﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
    public class NcSQLiteConnection : SQLiteConnection
    {
        private object LockObj;
        private DateTime LastAccess;
        private bool DidDispose;

        public NcSQLiteConnection (string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false) :
            base (databasePath, openFlags, storeDateTimeAsTicks)
        {
            LockObj = new object ();
            LastAccess = DateTime.UtcNow;
        }

        public NcSQLiteConnection (string databasePath, bool storeDateTimeAsTicks = false) :
            base (databasePath, storeDateTimeAsTicks)
        {
            LockObj = new object ();
            LastAccess = DateTime.UtcNow;
        }

        public bool SetLastAccess ()
        {
            lock (LockObj) {
                if (DidDispose) {
                    Log.Info (Log.LOG_DB, "NcSQLiteConnection.SetLastAccess: found DidDispose");
                    return false;
                }
                LastAccess = DateTime.UtcNow;
                return true;
            }
        }

        public void EliminateIfStale (Action action)
        {
            lock (LockObj) {
                var wayBack = DateTime.UtcNow.AddMinutes (-1);
                if (LastAccess < wayBack) {
                    action ();
                    Dispose ();
                    DidDispose = true;
                }
            }
        }
    }

    public sealed class NcModel
    {
        private string Documents;

        // RateLimiter PUBLIC FOR TEST ONLY.
        public NcRateLimter RateLimiter { set; get; }

        public bool FreshInstall { private set; get; }

        private const string KTmpPathSegment = "tmp";
        private const string KFilesPathSegment = "files";

        public string DbFileName { set; get; }

        public string TeleDbFileName { set; get; }

        public object WriteNTransLockObj { private set; get; }

        public enum AutoVacuumEnum
        {
            NONE = 0,
            FULL = 1,
            INCREMENTAL = 2,
        };

        public AutoVacuumEnum AutoVacuum { set; get; }

        public SQLiteConnection Db {
            get {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                NcSQLiteConnection db = null;
                while (true) {
                    if (!DbConns.TryGetValue (threadId, out db)) {
                        db = new NcSQLiteConnection (DbFileName, 
                            SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex, 
                            storeDateTimeAsTicks: true);
                        db.BusyTimeout = TimeSpan.FromSeconds (10.0);
                        db.TraceThreshold = 150;
                        NcAssert.True (DbConns.TryAdd (threadId, db));
                    }
                    if (db.SetLastAccess ()) {
                        break;
                    }
                }
                return db;
            } 
        }

        private object _TeleDbLock;
        private SQLiteConnection _TeleDb = null;

        public SQLiteConnection TeleDb {
            get {
                lock (_TeleDbLock) {
                    if (null == _TeleDb) {
                        _TeleDb = new SQLiteConnection (TeleDbFileName,
                            SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex,
                            storeDateTimeAsTicks: true);
                        _TeleDb.BusyTimeout = TimeSpan.FromSeconds (10.0);
                    }
                    return _TeleDb;
                }
            }
        }

        private ConcurrentDictionary<int, NcSQLiteConnection> DbConns;
        private ConcurrentDictionary<int, int> TransDepth;

        //private int walCheckpointCount = 0;

        public int NumberDbConnections {
            get {
                return DbConns.Count;
            }
        }

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
            var auto_vacuum = db.ExecuteScalar<int> ("PRAGMA auto_vacuum");
            QueueLogInfo (string.Format ("PRAGMA auto_vacuum: {0}", auto_vacuum));
            if ((int)AutoVacuum != auto_vacuum) {
                var cmd = String.Format ("PRAGMA auto_vacuum = {0}", (int)AutoVacuum);
                db.Execute (cmd);
                auto_vacuum = db.ExecuteScalar<int> ("PRAGMA auto_vacuum");
                NcAssert.Equals (auto_vacuum, (int)AutoVacuum);
                QueueLogInfo (string.Format ("PRAGMA auto_vacuum set to {0}", (int)AutoVacuum));
            }
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
            DbConns = new ConcurrentDictionary<int, NcSQLiteConnection> ();
            TransDepth = new ConcurrentDictionary<int, int> ();
            AutoVacuum = AutoVacuumEnum.NONE;
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
            Db.CreateTable<McMapEmailAddressEntry> ();
            Db.CreateTable<McMigration> ();
            ConfigureDb (Db);
        }

        private void InitializeTeleDb ()
        {
            if (null == _TeleDbLock) {
                _TeleDbLock = new object ();
            }
            AutoVacuum = AutoVacuumEnum.INCREMENTAL;
            ConfigureDb (TeleDb);
            // Auto-vacuum setting requires the table to be created after.
            TeleDb.CreateTable<McTelemetryEvent> ();
            TeleDb.CreateTable<McTelemetrySupportEvent> ();
        }

        private void QueueLogInfo (string message)
        {
            Log.IndirectQ.Enqueue (new LogElement () {
                Level = LogElement.LevelEnum.Info,
                Subsystem = Log.LOG_DB,
                Message = message,
                Occurred = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
            });
        }

        private NcModel ()
        {
            NcAssert.True (2 == SQLite3.Threadsafe () || 1 == SQLite3.Threadsafe ());
            if (4 == IntPtr.Size) {
                // bug qa-5: SQLite3.Config() causes a crash on 64-bit iOS devices.
                SQLite3.Config (SQLite3.ConfigOption.Log, Device.Instance.GetSQLite3ErrorCallback ((code, message) => {
                    if ((int)SQLite3.Result.OK == code ||
                        ((int)SQLite3.Result.Locked == code && message.Contains ("PRAGMA main.wal_checkpoint (PASSIVE)"))) {
                        return;
                    }
                    var messageWithStack = string.Format ("SQLite Error Log (code {0}): {1}", code, message);
                    foreach (var frame in NachoPlatformBinding.PlatformProcess.GetStackTrace ()) {
                        messageWithStack += "\n" + frame;
                    }
                    Log.IndirectQ.Enqueue (new LogElement () {
                        Level = LogElement.LevelEnum.Error,
                        Subsystem = Log.LOG_DB,
                        Message = messageWithStack,
                        Occurred = DateTime.UtcNow,
                        ThreadId = Thread.CurrentThread.ManagedThreadId,
                    });
                }), (IntPtr)null);
            }
            Documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            DbFileName = Path.Combine (Documents, "db");
            FreshInstall = !File.Exists (DbFileName);
            InitializeDb ();
            TeleDbFileName = Path.Combine (Documents, "teledb");
            InitializeTeleDb ();
            NcApplication.Instance.MonitorEvent += (object sender, EventArgs e) => {
                Scrub ();
            };
        }

        private static volatile NcModel instance;
        private static object syncRoot = new Object ();

        public static NcModel Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            var newInstnace = new NcModel ();
                            newInstnace.WriteNTransLockObj = new object ();
                            instance = newInstnace;
                        }
                    }
                }
                return instance; 
            }
        }

        private NcTimer CheckPointTimer;
        private NcTimer DbConnGCTimer;

        private class CheckpointResult
        {
            // Note: these property names can't be changed - they are hard-coded in the SQLite C code.
            public int busy { set; get; }

            public int log { set; get; }

            public int checkpointed { set; get; }
        }

        public void Start ()
        {
            DbConnGCTimer = new NcTimer ("NcModel.DbConnGCTimer", state => {
                foreach (var kvp in DbConns) {
                    NcSQLiteConnection dummy;
                    if (kvp.Key == NcApplication.Instance.UiThreadId) {
                        continue;
                    }
                    kvp.Value.EliminateIfStale (() => {
                        if (!DbConns.TryRemove (kvp.Key, out dummy)) {
                            Log.Error (Log.LOG_DB, "DbConnGCTimer: unable to remove DbConn for thread {0}", kvp.Key);
                        } else {
                            Log.Info (Log.LOG_DB, "DbConnGCTimer: removed DbConn for thread {0}", kvp.Key);
                        }
                    });
                }
            }, null, 120 * 1000, 120 * 1000);
            DbConnGCTimer.Stfu = true;

            CheckPointTimer = new NcTimer ("NcModel.CheckPointTimer", state => {
                var checkpointCmd = "PRAGMA main.wal_checkpoint (PASSIVE);";
                var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                foreach (var db in new List<SQLiteConnection> { Db, TeleDb }) {
                    var thisDb = db;
                    // Integrity check is slow but it was useful when we were tracking
                    // down integrity problem. Comment it out for future reuse
//                    if (0 == walCheckpointCount) {
//                        var ok = db.ExecuteScalar<string> ("PRAGMA integrity_check(1);");
//                        if ("ok" != ok) {
//                            Console.WriteLine ("Corrupted db detected. ({0})", db.DatabasePath);
//                            if (TeleDbFileName == db.DatabasePath) {
//                                NcModel.Instance.ResetTeleDb ();
//                                thisDb = TeleDb;
//                            }
//                        }
//                    }
//                    walCheckpointCount = (walCheckpointCount + 1) & 0xfff;

                    lock (WriteNTransLockObj) {
                        List<CheckpointResult> results = thisDb.Query<CheckpointResult> (checkpointCmd);
                        if ((0 < results.Count) && (0 != results [0].busy)) {
                            Log.Error (Log.LOG_DB, "Checkpoint busy of {0}", db.DatabasePath);
                        }
                    }
                }
            }, null, 10000, 2000);
            CheckPointTimer.Stfu = true;
        }

        public void Stop ()
        {
            if (null != CheckPointTimer) {
                CheckPointTimer.Dispose ();
                CheckPointTimer = null;
            }
            if (null != DbConnGCTimer) {
                DbConnGCTimer.Dispose ();
                DbConnGCTimer = null;
            }
        }

        public bool IsInTransaction ()
        {
            int depth = 0;
            return (TransDepth.TryGetValue (Thread.CurrentThread.ManagedThreadId, out depth) && depth > 0);
        }

        public int Update (object obj, Type objType, bool performOC = false, int priorVersion = 0)
        {
            lock (WriteNTransLockObj) {
                return Db.Update (obj, objType, performOC, priorVersion);
            }
        }

        public int BusyProtect (Func<int> action)
        {
            int rc = 0;
            if (IsInTransaction ()) {
                // Do not loop-retry within the transaction. 
                // If we are being given the busy, then rollback is needed to release a SQLite lock.
                rc = action ();
                return rc;
            }
            var whoa = DateTime.UtcNow.AddSeconds (5.0);
            do {
                try {
                    lock (WriteNTransLockObj) {
                        rc = action ();
                    }
                    return rc;
                } catch (SQLiteException ex) {
                    if (SQLite3.Result.Busy == ex.Result) {
                        if (DateTime.UtcNow > whoa) {
                            Log.Error (Log.LOG_DB, "BusyProtect: Caught a Busy");
                            throw;
                        } else {
                            Log.Warn (Log.LOG_DB, "BusyProtect: Caught a Busy");
                        }
                    } else {
                        Log.Error (Log.LOG_DB, "BusyProtect: Caught a non-Busy: {0}", ex);
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

        public void RunInLock (Action action)
        {
            lock (WriteNTransLockObj) {
                action ();
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
                        lock (WriteNTransLockObj) {
                            Db.RunInTransaction (action);
                        }
                        watch.Stop ();
                        var span = watch.ElapsedMilliseconds;
                        if (1000 < span) {
                            Log.Error (Log.LOG_DB, "RunInTransaction: {0}ms for {1}", span, 
                                new System.Diagnostics.StackTrace (true));
                        }
                        break;
                    } catch (SQLiteException ex) {
                        watch.Reset ();
                        if (SQLite3.Result.Busy == ex.Result) {
                            Log.Warn (Log.LOG_DB, "RunInTransaction: Caught a Busy");
                        } else {
                            Log.Error (Log.LOG_DB, "RunInTransaction: Caught a non-Busy: {0}", ex);
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
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "EngageRateLimiter received Info_BackgroundAbateStarted {0} seconds", deliveryTime.ToString ());
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_BackgroundAbateStopped) {
                    RateLimiter.Enabled = false;
                    var deliveryTime = NachoCore.Utils.NcAbate.DeliveryTime (siea);
                    NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "EngageRateLimiter received Info_BackgroundAbateStopped {0} seconds", deliveryTime.ToString ());
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

        private static void Scrub ()
        {
            // The contents of this method change, depending on what we are scrubbing for.
            // TODO: Make SQL this account-sensitive.
            var dupCals = Instance.Db.Query<McCalendar> (
                              "SELECT * FROM McCalendar WHERE UID IN " +
                              "(SELECT UID FROM McCalendar GROUP BY UID HAVING COUNT(*) > 1)"
                          );
            foreach (var dupCal in dupCals) {
                Log.Error (Log.LOG_DB, "Duplicate McCalendar Entry: Id={0}, ServerId={1}, UID={2}", 
                    dupCal.Id, dupCal.ServerId, dupCal.UID);
            }
        }

        public void ResetTeleDb ()
        {
            lock (_TeleDbLock) {
                // Close the connection
                _TeleDb.Close ();
                _TeleDb.Dispose ();
                _TeleDb = null; // next reference will re-initialize the connection

                // Rename the db file
                var timestamp = DateTime.Now.ToString ().Replace (' ', '_').Replace ('/', '-');
                File.Replace (TeleDbFileName, TeleDbFileName + "." + timestamp, null);
                File.Replace (TeleDbFileName + "-wal", TeleDbFileName + "-wal." + timestamp, null);
                File.Replace (TeleDbFileName + "-shm", TeleDbFileName + "-shm." + timestamp, null);

                // Recreate the db
                InitializeTeleDb ();

                Log.Error (Log.LOG_DB, "TeleDB corrupted. Reset.");
            }
        }

        public static void MayIncrementallyVacuum (SQLiteConnection db, int numberOfPages)
        {
            var freelistCount = db.ExecuteScalar<int> ("PRAGMA freelist_count");
            if (freelistCount <= numberOfPages) {
                return;
            }
            Log.Info (Log.LOG_DB, "Vacuuming free pages ({0})", freelistCount);
            var cmd = String.Format ("PRAGMA incremental_vacuum({0})", numberOfPages);
            db.Execute (cmd);
        }
    }
}
