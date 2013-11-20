using SQLite;
using System;

namespace NachoCore.Model
{
    public abstract class NcObject
    {
        [PrimaryKey, AutoIncrement, Unique]
        public int Id { get; set; }
    }
}

