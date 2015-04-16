//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class McBrainEvent : McAbstrObjectPerAcc
    {
        public byte[] Data { set; get; }

        public NcBrainEventType Type { set; get; }

        public McBrainEvent ()
        {
            Type = NcBrainEventType.UNKNOWN;
        }

        public McBrainEvent (NcBrainEvent brainEvent)
        {
            // Serialize to memory stream. The assumption is that there
            // never will be a large event.
            MemoryStream binaryStream = new MemoryStream ();
            BinaryFormatter serializer = new BinaryFormatter ();
            serializer.Serialize (binaryStream, brainEvent);
            Data = binaryStream.ToArray ();
            Type = brainEvent.Type;
        }

        public static McBrainEvent QueryNext ()
        {
            return NcModel.Instance.Db.Query<McBrainEvent> (
                "SELECT e.* FROM McBrainEvent AS e ORDER BY e.Id ASC LIMIT 1").SingleOrDefault ();
        }

        public NcBrainEvent BrainEvent ()
        {
            MemoryStream binaryStream = new MemoryStream (Data);
            BinaryFormatter serializer = new BinaryFormatter ();
            var brainEvent = (NcBrainEvent)serializer.Deserialize (binaryStream);
            return brainEvent;
        }
    }
}

