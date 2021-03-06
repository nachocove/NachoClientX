﻿// Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.IO;
using NachoClient.Build;
using NachoCore.Utils;
using NachoPlatform;
using System.Linq;

namespace NachoCore.Model
{
    public class NcSQLiteConnection : SQLiteConnection
    {
        public const int KGCSeconds = 60;
        private object LockObj;
        private bool DidDispose;

        public NcSQLiteConnection (string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false) :
            base (databasePath, openFlags, storeDateTimeAsTicks)
        {
            LockObj = new object ();
            LastAccess = DateTime.UtcNow;
            GCSeconds = KGCSeconds;
        }

        public NcSQLiteConnection (string databasePath, bool storeDateTimeAsTicks = false) :
            base (databasePath, storeDateTimeAsTicks)
        {
            LockObj = new object ();
            LastAccess = DateTime.UtcNow;
            GCSeconds = KGCSeconds;
        }

        public override bool SetLastAccess ()
        {
            lock (LockObj) {
                if (DidDispose) {
                    Log.Error (Log.LOG_DB, "NcSQLiteConnection.SetLastAccess: found DidDispose");
                    return false;
                }
                return base.SetLastAccess ();
            }
        }

        public DateTime GetLastAccess ()
        {
            return LastAccess;
        }

        public void Eliminate ()
        {
            lock (LockObj) {
                if (!DidDispose) {
                    try {
                        Dispose ();
                        DidDispose = true;
                    } catch (SQLiteException ex) {
                        if (SQLite3.Result.Busy == ex.Result) {
                            // We tried to close a conn with 
                            // "unfinalized statements or unfinished backups".
                            Log.Error (Log.LOG_DB, "Eliminate: unfinalized statements or unfinished backups.");
                        } else {
                            throw;
                        }
                    }
                }
            }
        }

        public void EliminateIfStale (DateTime cutoff, Action action)
        {
            lock (LockObj) {
                if (LastAccess < cutoff) {
                    action ();
                    Eliminate ();
                }
            }
        }

        // To learn more about how the database plans and executes queries, define QUERY_DEBUG,
        // and then call NcModel.Instance.ExplainQuery(query) for the problematic queries.
        // The query string can have '?' in it without passing any actual arguments.
#if QUERY_DEBUG

        private class QueryPlan {
            public int selectid { get; set; }
            public int order { get; set; }
            public int from { get; set; }
            public string detail { get; set; }
        }

        public string ExplainQuery (string query)
        {
            var plan = this.Query<QueryPlan> (string.Format ("EXPLAIN QUERY PLAN {0}", query));
            StringBuilder result = new StringBuilder ();
            foreach (var row in plan) {
                result.AppendFormat ("|{0}|{1}|{2}| {3}\n", row.selectid, row.order, row.from, row.detail);
            }
            return result.ToString ();
        }

#endif
    }

    public sealed class NcModel : SQLiteConnectionDelegate
    {
        List<Type> AllTables {
            get {
                return new List<Type> () {
                    typeof(McAccount),
                    typeof(McConference),
                    typeof(McCred),
                    typeof(McMapFolderFolderEntry),
                    typeof(McFolder),
                    typeof(McEmailAddress),
                    typeof(McEmailMessage),
                    typeof(McEmailMessageCategory),
                    typeof(McMeetingRequest),
                    typeof(McAttachment),
                    typeof(McMapAttachmentItem),
                    typeof(McContact),
                    typeof(McContactDateAttribute),
                    typeof(McContactStringAttribute),
                    typeof(McContactAddressAttribute),
                    typeof(McContactEmailAddressAttribute),
                    typeof(McPolicy),
                    typeof(McProtocolState),
                    typeof(McServer),
                    typeof(McPending),
                    typeof(McPendDep),
                    typeof(McCalendar),
                    typeof(McException),
                    typeof(McAttendee),
                    typeof(McCalendarCategory),
                    typeof(McRecurrence),
                    typeof(McEvent),
                    typeof(McTask),
                    typeof(McBody),
                    typeof(McDocument),
                    typeof(McMutables),
                    typeof(McPath),
                    typeof(McNote),
                    typeof(McPortrait),
                    typeof(McMapEmailAddressEntry),
                    typeof(McMigration),
                    typeof(McLicenseInformation),
                    typeof(McBrainEvent),
                    typeof(McEmailAddressScore),
                    typeof(McEmailMessageScore),
                    typeof(McEmailMessageNeedsUpdate),
                    typeof(McChat),
                    typeof(McChatMessage),
                    typeof(McChatParticipant),
                    typeof(McAction),
                    typeof(McSearchUnindexQueueItem),
                };
            }
        }

        // RateLimiter PUBLIC FOR TEST ONLY.
        public NcRateLimter RateLimiter { set; get; }

        public bool FreshInstall { private set; get; }

        private const string KTmpPathSegment = "tmp";
        private const string KFilesPathSegment = "files";
        private const string KRemovingAccountLockFile = "removing_account_lockfile";
        public static string [] ExemptTables = new string [] {
            "McAccount", "sqlite_sequence", "McMigration", "McLicenseInformation", "McBuildInfo", "McEmailAddress"
        };

        public string DbFileName { set; get; }

        public object WriteNTransLockObj { private set; get; }

        public enum AutoVacuumEnum
        {
            NONE = 0,
            FULL = 1,
            INCREMENTAL = 2,
        };

        public AutoVacuumEnum AutoVacuum { set; get; }

        private ConcurrentQueue<NcSQLiteConnection> ConnectionPool;

        string ConnectionCounts ()
        {
            return string.Format ("Connections: Pool {0}, DbCons {1}", ConnectionPool.Count, DbConns.Count);
        }

        public SQLiteConnection Db {
            get {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                NcSQLiteConnection db = null;
                while (true) {
                    if (!DbConns.TryGetValue (threadId, out db)) {
                        if (!ConnectionPool.TryDequeue (out db)) {
                            Log.Info (Log.LOG_DB, "NcSQLiteConnection {0,3:###} + connection ({1})", threadId, ConnectionCounts ());
                            db = new NcSQLiteConnection (DbFileName,
                                SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex,
                                storeDateTimeAsTicks: true);
                            db.BusyTimeout = TimeSpan.FromSeconds (10.0);
                            db.TraceThreshold = 500;
                            db.Delegate = this;
                        } else {
                            Log.Info (Log.LOG_DB, "NcSQLiteConnection {0,3:###} > connection ({1})", threadId, ConnectionCounts ());
                        }
                        NcAssert.True (DbConns.TryAdd (threadId, db));
                    }
                    if (db.SetLastAccess ()) {
                        break;
                    }
                }
                return db;
            }

            set {
                NcAssert.True (null == value);
                var threadId = Thread.CurrentThread.ManagedThreadId;
                NcSQLiteConnection db = null;
                if (DbConns.TryRemove (threadId, out db)) {
                    ConnectionPool.Enqueue (db);
                    Log.Info (Log.LOG_DB, "NcSQLiteConnection {0,3:###} < connection ({1})", threadId, ConnectionCounts ());
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

        public void CleanupOldDbConnections (TimeSpan staleInterval, int maxCached)
        {
            Log.Info (Log.LOG_DB, "NcSQLiteConnection: Cleaning DB connections older than {0:n0} sec, caching at most {1} connections ({2}).", staleInterval.TotalSeconds, maxCached, ConnectionCounts ());
            DateTime staleCuttoff = DateTime.UtcNow - staleInterval;
            int uiThreadId = NcApplication.Instance.UiThreadId;
            foreach (var kvp in DbConns) {
                if (uiThreadId != kvp.Key && kvp.Value.GetLastAccess () < staleCuttoff) {
                    NcSQLiteConnection staleConnection;
                    if (DbConns.TryRemove (kvp.Key, out staleConnection)) {
                        ConnectionPool.Enqueue (staleConnection);
                        Log.Info (Log.LOG_DB, "NcSQLiteConnection {0,3:###} < recycling stale connection ({1:N0} sec) ({2})",
                            kvp.Key, (DateTime.UtcNow - staleConnection.GetLastAccess ()).TotalSeconds, ConnectionCounts ());
                    } else {
                        Log.Error (Log.LOG_DB, "NcSQLiteConnection: Internal error: Can't remove connection {0} from DbConns.", kvp.Key);
                    }
                }
            }
            int originalCacheCount = ConnectionPool.Count;
            while (ConnectionPool.Count > maxCached) {
                NcSQLiteConnection db;
                if (ConnectionPool.TryDequeue (out db)) {
                    Log.Info (Log.LOG_DB, "NcSQLiteConnection - eliminating connection");
                    db.Delegate = null;
                    db.Eliminate ();
                } else {
                    Log.Error (Log.LOG_DB, "NcSQLiteConnection: Internal error: Can't remove a connection from the cached connection pool with size {0}", ConnectionPool.Count);
                    break;
                }
            }
            int finalCacheCount = ConnectionPool.Count;
            if (originalCacheCount != finalCacheCount) {
                Log.Info (Log.LOG_DB, "NcSQLiteConnection: Removed {0} DB connections from the cache, leaving {1} connections.", originalCacheCount - finalCacheCount, finalCacheCount);
            }
            Log.Info (Log.LOG_DB, "NcSQLiteConnection: Cleanup done. {0}", ConnectionCounts ());
        }

        public void ConnectionDidExecuteLongQuery (SQLiteConnection conn, SQLiteCommand command, long ms, int rows)
        {
            if (NachoCore.NcApplication.Instance.UiThreadId == Thread.CurrentThread.ManagedThreadId) {
                Log.Error (Log.LOG_SYS, "SQLite-UI: {0}ms/{1} rows for: {2}", ms, rows, command.CommandText);
            } else {
                Log.Warn (Log.LOG_SYS, "SQLite: {0}ms/{1} rows for: {2}", ms, rows, command.CommandText);
            }
        }

        public Dictionary<string, long> AllTableRowCounts (bool includeZeroCounts = false)
        {
            Dictionary<string, long> tableCounts = new Dictionary<string, long> ();
            foreach (var tableType in AllTables) {
                string name = tableType.Name;
                var n = Db.ExecuteScalar<long> (string.Format ("SELECT COUNT(Id) FROM {0};", name));
                if (includeZeroCounts || n > 0) {
                    tableCounts [name] = n;
                }
            }
            return tableCounts;
        }

        public string GetDataDirPath ()
        {
            return NcApplication.GetDataDirPath ();
        }

        public string GetFileDirPath (int accountId, string segment)
        {
            return Path.Combine (GetDataDirPath (), KFilesPathSegment, accountId.ToString (), segment);
        }

        public string GetAccountDirPath (int accountId)
        {
            return Path.Combine (GetDataDirPath (), KFilesPathSegment, accountId.ToString ());
        }

        public string GetRemovingAccountLockFilePath ()
        {
            return Path.Combine (GetDataDirPath (), KRemovingAccountLockFile);
        }

        // Get the AccountId for the account being removed
        public int GetRemovingAccountIdFromFile ()
        {
            string AccountIdString;
            int AccountId = 0;
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccountLockFilePath ();
            if (File.Exists (RemovingAccountLockFile)) {
                // Get the account id from the file
                try {
                    using (var stream = new FileStream (RemovingAccountLockFile, FileMode.Open, FileAccess.Read)) {
                        using (var reader = new StreamReader (stream)) {
                            AccountIdString = reader.ReadLine ();
                            bool result = int.TryParse (AccountIdString, out AccountId);
                            if (!result) {
                                Log.Warn (Log.LOG_DB, "RemoveAccount: Unable to parse AccountId from file.");
                            }
                        }
                    }
                } catch (IOException e) {
                    Log.Warn (Log.LOG_DB, "RemoveAccount: Unable to read RemoveAccountLockFile.{0}", e.Message);
                }
            }
            return AccountId;
        }

        // write the removing AccountId to file
        public void WriteRemovingAccountIdToFile (int AccountId)
        {
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccountLockFilePath ();
            try {
                using (var stream = new FileStream (RemovingAccountLockFile, FileMode.Create, FileAccess.Write)) {
                    using (var writer = new StreamWriter (stream)) {
                        writer.WriteLine (AccountId);
                    }
                }
            } catch (IOException e) {
                Log.Warn (Log.LOG_DB, "RemoveAccount: Unable to write RemoveAccountLockFile.{0}", e.Message);
            }
        }

        // mark directories in Documents/Data for no backup
        public void MarkDataDirForSkipBackup ()
        {
            var dataDir = GetDataDirPath ();
            NcFileHandler.Instance.MarkFileForSkipBackup (dataDir);
        }

        public string GetIndexPath (int accountId)
        {
            return NcModel.Instance.GetFileDirPath (accountId, "index");
        }

        public void InitializeDirs (int accountId)
        {
            Directory.CreateDirectory (GetFileDirPath (accountId, KTmpPathSegment));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McDocument ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McAttachment ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McBody ().GetFilePathSegment ()));
            Directory.CreateDirectory (GetFileDirPath (accountId, new McPortrait ().GetFilePathSegment ()));
        }

        private void ConfigureDb (SQLiteConnection db)
        {
            // everything except synchronous seems to persist across re-launch.
            // TODO we can remove the checking to speed up launch.
            NcAssert.NotNull (db);
            var auto_vacuum = db.ExecuteScalar<int> ("PRAGMA auto_vacuum");
            Log.LOG_DB.Info ("PRAGMA auto_vacuum: {0}", auto_vacuum);
            if ((int)AutoVacuum != auto_vacuum) {
                var cmd = String.Format ("PRAGMA auto_vacuum = {0}", (int)AutoVacuum);
                db.Execute (cmd);
                auto_vacuum = db.ExecuteScalar<int> ("PRAGMA auto_vacuum");
                NcAssert.Equals (auto_vacuum, (int)AutoVacuum);
                Log.LOG_DB.Info ("PRAGMA auto_vacuum set to {0}", (int)AutoVacuum);
            }
            var cache_size = db.ExecuteScalar<int> ("PRAGMA cache_size");
            Log.LOG_DB.Info ("PRAGMA cache_size: {0}", cache_size);
            var journal_mode = db.ExecuteScalar<string> ("PRAGMA journal_mode");
            Log.LOG_DB.Info ("PRAGMA journal_mode: {0}", journal_mode);
            if ("wal" != journal_mode.ToLower ()) {
                journal_mode = db.ExecuteScalar<string> ("PRAGMA journal_mode = WAL");
                NcAssert.Equals ("wal", journal_mode.ToLower ());
                Log.LOG_DB.Info ("PRAGMA journal_mode set to {0}", journal_mode);
            }
            var wal_autocheckpoint = db.ExecuteScalar<int> ("PRAGMA wal_autocheckpoint");
            Log.LOG_DB.Info ("PRAGMA wal_autocheckpoint: {0}", wal_autocheckpoint);
            if (1000 != wal_autocheckpoint) {
                journal_mode = db.ExecuteScalar<string> ("PRAGMA wal_autocheckpoint = 1000");
                NcAssert.Equals (1000, wal_autocheckpoint);
                Log.LOG_DB.Info ("PRAGMA wal_autocheckpoint set to {0}", wal_autocheckpoint);
            }
            var synchronous = db.ExecuteScalar<int> ("PRAGMA synchronous");
            Log.LOG_DB.Info ("PRAGMA synchronous: {0}", synchronous);
            if (1 != synchronous) {
                db.Execute ("PRAGMA synchronous = 1");
                synchronous = db.ExecuteScalar<int> ("PRAGMA synchronous");
                NcAssert.Equals (synchronous, 1);
                Log.LOG_DB.Info ("PRAGMA synchronous set to: {0}", synchronous);
            }
        }

        private McBuildInfo _StoredBuildInfo = null;

        public McBuildInfo StoredBuildInfo {
            get {
                if (null == _StoredBuildInfo) {
                    Db.CreateTable<McBuildInfo> ();
                    _StoredBuildInfo = Db.Table<McBuildInfo> ().FirstOrDefault ();
                }
                return _StoredBuildInfo;
            }
        }

        private void InitializeDb ()
        {
            RateLimiter = new NcRateLimter (16, 0.250);
            DbConns = new ConcurrentDictionary<int, NcSQLiteConnection> ();
            ConnectionPool = new ConcurrentQueue<NcSQLiteConnection> ();
            Log.Info (Log.LOG_DB, "NcSQLiteConnection: Initialized connection pool.");
            TransDepth = new ConcurrentDictionary<int, int> ();
            AutoVacuum = AutoVacuumEnum.NONE;
            var watch = Stopwatch.StartNew ();
            // Use the SQLite.NET "raw" version of RunInTransaction while initializing NcModel.
            var storedBuildInfo = StoredBuildInfo;
            if (null == storedBuildInfo ||
                storedBuildInfo.BuildNumber != BuildInfo.BuildNumber ||
                storedBuildInfo.Time != BuildInfo.Time ||
                storedBuildInfo.Version != BuildInfo.Version) {
                Db.RunInTransaction (() => {
                    foreach (var tableType in AllTables) {
                        Db.CreateTable (tableType);
                    }
                });
                var current = new McBuildInfo () {
                    Version = BuildInfo.Version,
                    BuildNumber = BuildInfo.BuildNumber,
                    Time = BuildInfo.Time,
                };
                NcAssert.AreEqual (1, Db.InsertOrReplace (current));
            }
            watch.Stop ();
            Log.LOG_DB.Info ("NcModel: Db.CreateTables took {0}ms.", watch.ElapsedMilliseconds);
            ConfigureDb (Db);
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
                    foreach (var frame in PlatformProcess.Instance.GetStackTrace ()) {
                        messageWithStack += "\n" + frame;
                    }
                    Log.LOG_DB.Info (messageWithStack);
                }), (IntPtr)null);
            }
            DbFileName = Path.Combine (GetDataDirPath (), "db");
            FreshInstall = !File.Exists (DbFileName);
            InitializeDb ();
            NcApplicationMonitor.Instance.MonitorEvent += (sender, e) => Scrub ();
            //mark all the files for skip backup
            MarkDataDirForSkipBackup ();
        }

        private static volatile NcModel instance;
        private static object syncRoot = new Object ();

        public static NcModel Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            var newInstance = new NcModel ();
                            newInstance.WriteNTransLockObj = new object ();
                            instance = newInstance;
                        }
                    }
                }
                return instance;
            }
        }

        public static bool IsInitialized {
            get {
                return (null != instance);
            }
        }

#if QUERY_DEBUG

        public void ExplainQuery (string query)
        {
            Log.Info (Log.LOG_DB, "Query: {0}\n{1}", query, ((NcSQLiteConnection)Db).ExplainQuery (query));
        }

#endif

        public void Start ()
        {
            NcTask.Run (() => {
                McFolder.InitializeJunkFolders ();
            }, "InitializeJunkFolders");
        }

        public void Stop ()
        {
        }

        public bool IsInTransaction ()
        {
            int depth = 0;
            return (TransDepth.TryGetValue (Thread.CurrentThread.ManagedThreadId, out depth) && depth > 0);
        }

        void LogWriteFromUIThread (string tag)
        {
            if (Thread.CurrentThread.ManagedThreadId == NcApplication.Instance.UiThreadId) {
                // Log.Info (Log.LOG_DB, "NcModel: {0} on UI thread\n{1}", tag, PlatformProcess.Instance.GetStackTrace ());
            }
        }

        public int Update (object obj, Type objType, bool performOC = false, int priorVersion = 0)
        {
            lock (WriteNTransLockObj) {
                LogWriteFromUIThread ("Update");
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
                        LogWriteFromUIThread ("BusyProtect");
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
                LogWriteFromUIThread ("RunInLock");
                action ();
            }
        }

        public void RunInTransaction (Action action, bool shouldAlreadyBeInTransaction = false)
        {
            if (NcModel.Instance.IsInTransaction ()) {
                // If we are already in transaction, then no need to nest - just run the code.
                action ();
                return;
            } else if (shouldAlreadyBeInTransaction) {
                Log.Error (Log.LOG_DB, "RunInTransaction: Should already be in a transaction here: {0}", new StackTrace (true));
            }
            var threadId = Thread.CurrentThread.ManagedThreadId;
            bool onUiThread = NcApplication.Instance.UiThreadId == threadId;
            if (!onUiThread) {
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
            Stopwatch lockWatch = new Stopwatch ();
            Stopwatch workWatch = new Stopwatch ();
            try {
                var whoa = DateTime.UtcNow.AddSeconds (5.0);
                // It is okay to loop here, because a busy will have caused us to ROLLBACK and release
                // all locks. We can then run the action code again.
                do {
                    try {
                        lockWatch.Start ();
                        lock (WriteNTransLockObj) {
                            LogWriteFromUIThread ("RunInTransaction");
                            lockWatch.Stop ();
                            Db.CommandRecord = new List<string> ();
                            workWatch.Start ();
                            Db.RunInTransaction (action);
                            workWatch.Stop ();
                            if (1000 < workWatch.ElapsedMilliseconds || 1000 < Db.CommandRecord.Count) {
                                int dumpRemaining = 100;
                                Log.Error (Log.LOG_DB, "RunInTransaction: Commands/ms: {0}/{1}", Db.CommandRecord.Count, workWatch.ElapsedMilliseconds);
                                var sb = new StringBuilder ();
                                sb.AppendLine ();
                                foreach (var command in Db.CommandRecord) {
                                    if (0 > --dumpRemaining) {
                                        break;
                                    }
                                    sb.Append (command);
                                    sb.Append ('\n');
                                }
                                Log.Info (Log.LOG_DB, "RunInTransaction: {0}", sb);
                            }
                            Db.CommandRecord = null;
                        }
                        var lockSpan = lockWatch.ElapsedMilliseconds;
                        var workSpan = workWatch.ElapsedMilliseconds;
                        // Use different threshholds for reporting long transactions based on whether or not
                        // this is the UI thread.
                        if (onUiThread) {
                            if (100 < lockSpan) {
                                Log.Error (Log.LOG_DB, "RunInTransaction: UI thread spent {0}ms waiting to acquire the database write lock. {1}", lockSpan, new StackTrace (true));
                            }
                            if (100 < workSpan) {
                                Log.Error (Log.LOG_DB, "RunInTransaction: UI thread spent {0}ms running a transaction. {1}", workSpan, new StackTrace (true));
                            }
                        } else {
                            if (500 < lockSpan) {
                                Log.Warn (Log.LOG_DB, "RunInTransaction: Background thread spent {0}ms waiting to acquire the database write lock. {1}", lockSpan, new StackTrace (true));
                            }
                            if (1000 < workSpan) {
                                Log.Error (Log.LOG_DB, "RunInTransaction: Background thread spent {0}ms running a transaction. {1}", workSpan, new StackTrace (true));
                            } else if (500 < workSpan) {
                                Log.Warn (Log.LOG_DB, "RunInTransaction: Background thread spent {0}ms running a transaction. {1}", workSpan, new StackTrace (true));
                            }
                        }
                        break;
                    } catch (SQLiteException ex) {
                        if (SQLite3.Result.Busy == ex.Result) {
                            Log.Warn (Log.LOG_DB, "RunInTransaction: Caught a Busy");
                        } else {
                            Log.Error (Log.LOG_DB, "RunInTransaction: Caught a non-Busy: {0}", ex);
                            throw;
                        }
                    }
                    lockWatch.Reset ();
                    workWatch.Reset ();
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
            /*
             * This was inadvertently disabled, and it turns out we don't seem to need it. So leave it off.
             * 
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
            */
        }

        // Test use only.
        public void Reset (string dbFileName)
        {
            _StoredBuildInfo = null;
            DbFileName = dbFileName;
            InitializeDb ();
            GarbageCollectFiles (false);
        }

        public string TmpPath (int accountId, string suffix = null)
        {
            var guidString = Guid.NewGuid ().ToString ("N");
            if (!string.IsNullOrEmpty (suffix)) {
                guidString += suffix;
            }
            return Path.Combine (GetFileDirPath (accountId, KTmpPathSegment), guidString);
        }

        public void DumpLastAccess ()
        {
            Log.Info (Log.LOG_DB, "DbConn: Dumping LastAccess for open DB connections");
            foreach (var kvp in DbConns) {
                if (kvp.Key == NcApplication.Instance.UiThreadId) {
                    Log.Info (Log.LOG_DB, "DbConn: UiThread Key: {0} LastAccess at: {1} Seconds: {2:N0}", kvp.Key, kvp.Value.GetLastAccess (), (DateTime.UtcNow - kvp.Value.GetLastAccess ()).TotalSeconds);
                } else {
                    Log.Info (Log.LOG_DB, "DbConn: Key: {0} LastAccess at: {1} Seconds: {2:N0}", kvp.Key, kvp.Value.GetLastAccess (), (DateTime.UtcNow - kvp.Value.GetLastAccess ()).TotalSeconds);
                }
            }
        }

        // To be run synchronously only on app boot.
        public void GarbageCollectFiles (bool deleteGlobalTmp = true)
        {
            // Find any top-level file dir not backed by McAccount. Delete it.
            var acctLevelDirs = Directory.GetDirectories (GetDataDirPath ());
            // Foreach account...
            foreach (var acctDir in acctLevelDirs) {
                int accountId;
                var suffix = Path.GetFileName (acctDir);
                if (int.TryParse (suffix, out accountId)) {
                    if (null == McAccount.QueryById<McAccount> (accountId)) {
                        try {
                            Directory.Delete (acctDir, true);
                        } catch (Exception ex) {
                            Log.Error (Log.LOG_DB, "GarbageCollectFiles: Exception deleting account-level dir: {0}", ex);
                        }
                    } else {
                        // Remove any tmp files/dirs.
                        DeleteDirContent (GetFileDirPath (accountId, KTmpPathSegment));
                    }
                }
            }
            if (deleteGlobalTmp) {
                // Remove any global tmp files/dirs.
                DeleteDirContent (Path.GetTempPath ());
            }
        }

        static void DeleteDirContent (string dirname, bool logFiles = true)
        {
            try {
                // Remove any tmp files/dirs.
                foreach (var dir in Directory.GetDirectories (dirname)) {
                    Directory.Delete (dir, true);
                }
                foreach (var file in Directory.GetFiles (dirname)) {
                    if (logFiles) {
                        Log.Info (Log.LOG_SYS, "DeleteDirContent: Removing left-over file {0}", file);
                    }
                    File.Delete (file);
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_DB, "DeleteDirContent: Exception cleaning up files: {0}", ex);
            }
        }

        private static void Scrub ()
        {
            // The contents of this method change, depending on what we are scrubbing for.
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
