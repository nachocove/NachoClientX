using SQLite;
using System;

namespace NachoCore.Model
{
    public abstract class NcObject
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
    }
}

