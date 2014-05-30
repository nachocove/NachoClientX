//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.IO;

namespace NachoCore.Model
{
    public sealed class NcModel
    {
        public string FilesDir { set; get; }
        public string AttachmentsDir { set; get; }
        public string BodiesDir { set; get; }
        public SQLiteConnection Db { set; get; }

        private string DbFileName;

        private NcModel ()
        {
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            FilesDir = Path.Combine (documents, "files");
            Directory.CreateDirectory (Path.Combine (documents, FilesDir));
            AttachmentsDir = Path.Combine (documents, "attachments");
            Directory.CreateDirectory (Path.Combine (documents, AttachmentsDir));
            BodiesDir = Path.Combine (documents, "bodies");
            Directory.CreateDirectory (Path.Combine (documents, BodiesDir));

            DbFileName = Path.Combine (documents, "db");
            Db = new SQLiteConnection (DbFileName, 
                SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex, 
                storeDateTimeAsTicks: true);
            Db.CreateTable<McAccount> ();
            Db.CreateTable<McCred> ();
            Db.CreateTable<McMapFolderFolderEntry> ();
            Db.CreateTable<McFolder> ();
            Db.CreateTable<McEmailMessage> ();
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
    }
}

