﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        private string Documents;

        public NcRateLimter RateLimiter { set; get; }

        public string FilesDir { set; get; }

        public string AttachmentsDir { set; get; }

        public string BodiesDir { set; get; }

        public string DbFileName { set; get; }

        public SQLiteConnection Db {
            get {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                SQLiteConnection db = null;
                if (!DbConns.TryGetValue (threadId, out db)) {
                    db = new SQLiteConnection (DbFileName, 
                        SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex, 
                        storeDateTimeAsTicks: true);
                    db.BusyTimeout = TimeSpan.FromSeconds (10.0);
                    DbConns.TryAdd (threadId, db);
                }
                return db;
            } 
        }

        private ConcurrentDictionary<int, SQLiteConnection> DbConns;
        private ConcurrentDictionary<int, int> TransDepth;

        private void Initialize ()
        {
            RateLimiter = new NcRateLimter (16, 0.250);
            FilesDir = Path.Combine (Documents, "files");
            Directory.CreateDirectory (Path.Combine (Documents, FilesDir));
            AttachmentsDir = Path.Combine (Documents, "attachments");
            Directory.CreateDirectory (Path.Combine (Documents, AttachmentsDir));
            BodiesDir = Path.Combine (Documents, "bodies");
            Directory.CreateDirectory (Path.Combine (Documents, BodiesDir));
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

        public bool IsInTransaction ()
        {
            int depth = 0;
            return (TransDepth.TryGetValue (Thread.CurrentThread.ManagedThreadId, out depth) && depth > 0);
        }

        public void RunInTransaction (Action action)
        {
            // DO NOT ADD LOGGING IN THE TRANSACTION, BECAUSE WE DON'T WANT LOGGING WRITES TO GET LUMPED IN.
            var threadId = Thread.CurrentThread.ManagedThreadId;
            int exitValue = 0;
            TransDepth.AddOrUpdate (threadId, 1, (key, oldValue) => {
                exitValue = oldValue;
                return oldValue + 1;
            });
            try {
                var whoa = DateTime.UtcNow.AddSeconds (5.0);
                do {
                    try {
                        Db.RunInTransaction (action);
                        break;
                    } catch (SQLiteException ex) {
                        if (ex.Message.Contains ("Busy")) {
                            Log.Warn (Log.LOG_SYS, "Caught a Busy");
                        } else {
                            Log.Error (Log.LOG_SYS, "Caught a non-Busy: {0}", ex);
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
            Log.Info (Log.LOG_SYS, "SQLite version number {0}", SQLite3.LibVersionNumber ());
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
            Initialize ();
        }
    }
}

