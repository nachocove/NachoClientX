//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public sealed class NcModel
    {
        public const bool DbTimingMode = false;

        private string Documents;

        public NcRateLimter RateLimiter { set; get; }

        public string FilesDir { set; get; }

        public string AttachmentsDir { set; get; }

        public string BodiesDir { set; get; }

        public string DbFileName { set; get; }

        public SQLiteConnection Db {
            get {
                #pragma warning disable 162
                int threadId;
                if (DbTimingMode) {
                    threadId = 0;
                } else {
                    threadId = Thread.CurrentThread.ManagedThreadId;
                }
                SQLiteConnection db = null;
                if (!DbConns.TryGetValue (threadId, out db)) {
                    var dbFlags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
                    if (DbTimingMode) {
                        dbFlags |= SQLiteOpenFlags.FullMutex;
                    } else {
                        dbFlags |= SQLiteOpenFlags.NoMutex;
                    }
                    db = new SQLiteConnection (DbFileName, dbFlags, storeDateTimeAsTicks: true);
                    if (DbTimingMode) {
                        db.Trace = true;
                        db.TimeExecution = true;
                        db.SerializeOperations = true;
                    }
                    db.BusyTimeout = TimeSpan.FromSeconds (10.0);
                    DbConns.TryAdd (threadId, db);
                }
                #pragma warning restore 162
                return db;
            } 
        }

        private ConcurrentDictionary<int, SQLiteConnection> DbConns;

        private void Initialize ()
        {
            RateLimiter = new NcRateLimter (4, 0.250);
            FilesDir = Path.Combine (Documents, "files");
            Directory.CreateDirectory (Path.Combine (Documents, FilesDir));
            AttachmentsDir = Path.Combine (Documents, "attachments");
            Directory.CreateDirectory (Path.Combine (Documents, AttachmentsDir));
            BodiesDir = Path.Combine (Documents, "bodies");
            Directory.CreateDirectory (Path.Combine (Documents, BodiesDir));
            DbConns = new ConcurrentDictionary<int, SQLiteConnection> ();
            Db.CreateTable<McAccount> ();
            Db.CreateTable<McCred> ();
            Db.CreateTable<McMapFolderFolderEntry> ();
            Db.CreateTable<McFolder> ();
            Db.CreateTable<McEmailMessage> ();
            Db.CreateTable<McEmailMessageCategory> ();
            Db.CreateTable<McAttachment> ();
            Db.CreateTable<McContact> ();
            Db.CreateTable<McContactDateAttribute> ();
            Db.CreateTable<McContactStringAttribute> ();
            Db.CreateTable<McContactAddressAttribute> ();
            Db.CreateTable<McPolicy> ();
            Db.CreateTable<McProtocolState> ();
            Db.CreateTable<McServer> ();
            Db.CreateTable<McPending> ();
            Db.CreateTable<McPendDep> ();
            Db.CreateTable<McPendingPath> ();
            Db.CreateTable<McCalendar> ();
            Db.CreateTable<McException> ();
            Db.CreateTable<McAttendee> ();
            Db.CreateTable<McCalendarCategory> ();
            Db.CreateTable<McRecurrence> ();
            Db.CreateTable<McTimeZone> ();
            Db.CreateTable<McTask> ();
            Db.CreateTable<McBody> ();
            Db.CreateTable<McFile> ();
            Db.CreateTable<McTelemetryEvent> ();
            Db.CreateTable<McMutables> ();
            Db.CreateTable<McPath> ();
        }

        private NcModel ()
        {
            NcAssert.True (2 == SQLite3.Threadsafe () || 1 == SQLite3.Threadsafe ());
            Documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            DbFileName = Path.Combine (Documents, "db");
            Initialize ();
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

        public void Nop ()
        {
        }

        public void Info ()
        {
            Log.Info (Log.LOG_SYS, "SQLite version number {0}", SQLite3.LibVersionNumber ());
        }

        public void EngageRateLimiter ()
        {
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs ea) => {
                var siea = (StatusIndEventArgs)ea;
                if (siea.Status.SubKind == NcResult.SubKindEnum.Info_ViewScrollingStarted) {
                    #pragma warning disable 162
                    if (DbTimingMode) {
                        RateLimiter.Enabled = true;
                    }
                    #pragma warning restore 162
                } else if (siea.Status.SubKind == NcResult.SubKindEnum.Info_ViewScrollingStopped) {
                    RateLimiter.Enabled = false;
                }
            };
        }

        public void Reset (string dbFileName)
        {
            DbFileName = dbFileName;
            Initialize ();
        }
    }
}

