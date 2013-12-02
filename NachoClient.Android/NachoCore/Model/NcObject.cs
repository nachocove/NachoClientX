using SQLite;
using System;

namespace NachoCore.Model
{
    public abstract class NcObject
    {
        [PrimaryKey, AutoIncrement, Unique]
        public int Id { get; set; }

        // Optimistic concurrency control
        public DateTime LastModified { get; set; }

        public NcObject()
        {
            Id = -1;
            LastModified = DateTime.MinValue;
        }
    }
}

