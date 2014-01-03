using System;
using SQLite;

namespace NachoCore.Model
{
    public abstract class McItem : McEventable
    {

        // The ServerId represents a unique identifier that is assigned by the server
        // to each object that can be synchronized. The client MUST store the server
        // ID for each object as an opaque string of up to 64 characters and MUST
        // be able to locate an object given a server ID.
        [Indexed]
        public string ServerId { get; set; }

        // The FolderId is the index of an NcFolder object.
        // It's a foreign key.
        [Indexed]
        public int FolderId { get; set; }
    }
}

