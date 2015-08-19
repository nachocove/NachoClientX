//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McTelemetryEvent
    {
        private static NcCapture _InsertCapture;

        private static NcCapture InsertCapture {
            get {
                if (null == _InsertCapture) {
                    var kind = "NcModel.McTelemetryEvent.Insert";
                    NcCapture.AddKind (kind);
                    _InsertCapture = NcCapture.Create (kind);
                }
                return _InsertCapture;
            }
        }

        private static NcCapture _DeleteCapture;

        private static NcCapture DeleteCapture {
            get {
                if (null == _DeleteCapture) {
                    var kind = "NcModel.McTelemetryEvent.Delete";
                    NcCapture.AddKind (kind);
                    _DeleteCapture = NcCapture.Create (kind);
                }
                return _DeleteCapture;
            }
        }

        [PrimaryKey, AutoIncrement, Unique]
        public virtual int Id { get; set; }
        // Optimistic concurrency control
        public DateTime LastModified { get; set; }

        public byte[] Data { set; get; }

        public McTelemetryEvent ()
        {
            Data = null;
        }

        public McTelemetryEvent (TelemetryEvent tEvent)
        {
            // Serialize to memory stream. The assumption is that there
            // never will be a large event since WBXML are redacted.
            MemoryStream binaryStream = new MemoryStream ();
            BinaryFormatter serializer = new BinaryFormatter ();
            serializer.Serialize (binaryStream, tEvent);
            Data = binaryStream.ToArray ();
        }

        public TelemetryEvent GetTelemetryEvent ()
        {
            MemoryStream binaryStream = new MemoryStream (Data);
            BinaryFormatter serializer = new BinaryFormatter ();
            var tEvent = (TelemetryEvent)serializer.Deserialize (binaryStream);
            tEvent.dbId = Id;
            return tEvent;
        }

        public static List<McTelemetryEvent> QueryMultiple (int numItems)
        {
            try {
                return NcModel.Instance.TeleDb.Query<McTelemetryEvent> (
                    "SELECT * FROM McTelemetryEvent ORDER BY Id LIMIT ?;", numItems);
            } catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return null;
                } else {
                    throw;
                }
            }
        }

        public int Insert ()
        {
            NcAssert.True (0 == Id);
            try {
                InsertCapture.Start ();
                int rc = NcModel.Instance.TeleDb.Insert (this);
                InsertCapture.Stop ();
                InsertCapture.Reset ();
                return rc;
            } catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return 0;
                } else {
                    throw;
                }
            }
        }

        public int Delete ()
        {
            NcAssert.True (0 < Id);
            try {
                DeleteCapture.Start ();
                int rc = NcModel.Instance.TeleDb.Delete (this);
                DeleteCapture.Stop ();
                DeleteCapture.Reset ();
                return rc;
            } catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return 0;
                } else {
                    throw;
                }
            }
        }

        public static int QueryCount ()
        {
            return QueryCount<McTelemetryEvent> ();
        }

        protected static int QueryCount<T> () where T : McTelemetryEvent, new()
        {
            try {
                return NcModel.Instance.TeleDb.Table<T> ().Count ();
            } catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return 0;
                } else {
                    throw;
                }
            }
        }

        public static void Purge<T> (int limit) where T : McTelemetryEvent, new()
        {
            int count = QueryCount ();
            if (limit < count) {
                // SQLITE_ENABLE_UPDATE_DELETE_LIMIT is not enabled on a lot of 
                // platforms. Cannot count its avail.
                var tableName = typeof(T).Name;
                var dbEventList =
                    NcModel.Instance.TeleDb.Query<McTelemetryEvent> (
                        String.Format ("SELECT * FROM {0} ORDER BY Id DESC LIMIT ?", tableName), count - limit
                    );
                var dbEvent = dbEventList.LastOrDefault ();
                if (null == dbEvent) {
                    return;
                }
                NcModel.Instance.TeleDb.Query<T> (
                    String.Format ("DELETE FROM {0} WHERE Id >= ?", tableName), dbEvent.Id
                );
            }
        }

        public static T QueryById<T> (int id) where T : McTelemetryEvent, new()
        {
            return NcModel.Instance.TeleDb.Table<T> ().Where (x => x.Id == id).SingleOrDefault ();
        }
    }

    public class McTelemetrySupportEvent : McTelemetryEvent
    {
        public McTelemetrySupportEvent () : base ()
        {
        }

        public McTelemetrySupportEvent (TelemetryEvent tEvent) : base (tEvent)
        {
        }

        public static List<McTelemetryEvent> QueryOne ()
        {
            try {
                var dbEvent = (McTelemetryEvent)NcModel.Instance.TeleDb.Query<McTelemetrySupportEvent> (
                                  "SELECT * FROM McTelemetrySupportEvent ORDER BY Id ASC LIMIT 1;").SingleOrDefault ();
                var dbEventList = new List<McTelemetryEvent> ();
                if (null != dbEvent) {
                    dbEventList.Add (dbEvent);
                }
                return dbEventList;
            } catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return null;
                } else {
                    throw;
                }
            }
        }

        public new static int QueryCount ()
        {
            return QueryCount<McTelemetrySupportEvent> ();
        }
    }
}

