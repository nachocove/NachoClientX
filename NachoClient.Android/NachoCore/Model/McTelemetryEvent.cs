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
            return (TelemetryEvent)serializer.Deserialize (binaryStream);
        }

        public static McTelemetryEvent QueryOne ()
        {
            try {
                return NcModel.Instance.TeleDb.Query<McTelemetryEvent> (
                    "SELECT * FROM McTelemetryEvent ORDER BY Id LIMIT 1;").SingleOrDefault ();
            }
            catch (SQLiteException e) {
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
                int rc =  NcModel.Instance.TeleDb.Insert (this);
                InsertCapture.Stop ();
                InsertCapture.Reset ();
                return rc;
            }
            catch (SQLiteException e) {
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
            }
            catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return 0;
                } else {
                    throw;
                }
            }
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

        public new static McTelemetryEvent QueryOne ()
        {
            try {
                return (McTelemetryEvent)NcModel.Instance.TeleDb.Query<McTelemetrySupportEvent> (
                    "SELECT * FROM McTelemetrySupportEvent ORDER BY Id ASC LIMIT 1;").SingleOrDefault ();
            }
            catch (SQLiteException e) {
                if (SQLite3.Result.Corrupt == e.Result) {
                    NcModel.Instance.ResetTeleDb ();
                    return null;
                } else {
                    throw;
                }
            }
        }
    }
}

