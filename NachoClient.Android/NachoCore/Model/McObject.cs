using SQLite;
using System;

namespace NachoCore.Model
{
    public abstract class McObject
    {
        [PrimaryKey, AutoIncrement, Unique]
        public int Id { get; set; }

        // Optimistic concurrency control
        public DateTime LastModified { get; set; }

        public McObject()
        {
            Id = 0;
            LastModified = DateTime.MinValue;
        }
    }
}

