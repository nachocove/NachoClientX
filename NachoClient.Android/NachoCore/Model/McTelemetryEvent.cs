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
        [PrimaryKey, AutoIncrement, Unique]
        public virtual int Id { get; set; }
        // Optimistic concurrency control
        public DateTime LastModified { get; set; }

        // This boolean serves a priority bit. Support events are 
        // process first out of order.
        [Indexed]
        public bool IsSupport { set; get; }

        public byte[] Data { set; get; }

        public McTelemetryEvent ()
        {
            Data = null;
            IsSupport = false;
        }

        public McTelemetryEvent (TelemetryEvent tEvent)
        {
            // Serialize to memory stream. The assumption is that there
            // never will be a large event since WBXML are redacted.
            MemoryStream binaryStream = new MemoryStream ();
            BinaryFormatter serializer = new BinaryFormatter ();
            serializer.Serialize (binaryStream, tEvent);
            Data = binaryStream.ToArray ();
            IsSupport = tEvent.IsSupportEvent ();
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
                    "SELECT * FROM McTelemetryEvent ORDER BY IsSupport DESC, Id LIMIT 1;").SingleOrDefault ();
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
                int rc =  NcModel.Instance.TeleDb.Insert (this);
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
                int rc = NcModel.Instance.TeleDb.Delete (this);
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
}

