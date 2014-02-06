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

        public McObject ()
        {
            Id = 0;
            LastModified = DateTime.MinValue;
        }

        public string ClassName ()
        {
            return GetType ().Name;
        }

        public virtual int Insert ()
        {
            return BackEnd.Instance.Db.Insert (this);
        }

        public virtual int Delete ()
        {
            return BackEnd.Instance.Db.Delete (this);
        }

        public virtual int Update ()
        {
            return BackEnd.Instance.Db.Update (this);
        }
    }
}

