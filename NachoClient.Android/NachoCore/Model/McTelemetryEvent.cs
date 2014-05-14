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
    public class McTelemetryEvent : McObject
    {
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
            List<McTelemetryEvent> eventList = 
                NcModel.Instance.Db.Query<McTelemetryEvent> ("SELECT * FROM McTelemetryEvent LIMIT 1;");
            if (1 == eventList.Count) {
                return eventList [0];
            }
            return null;
        }
    }
}

