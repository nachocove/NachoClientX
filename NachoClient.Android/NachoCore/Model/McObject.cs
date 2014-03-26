using SQLite;
using System;
using System.Linq;
using System.Reflection;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McObject
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
            NachoAssert.True (0 == Id);
            return BackEnd.Instance.Db.Insert (this);
        }

        public virtual int Delete ()
        {
            NachoAssert.True (0 != Id);
            return BackEnd.Instance.Db.Delete (this);
        }

        public virtual int Update ()
        {
            NachoAssert.True (0 != Id);
            return BackEnd.Instance.Db.Update (this);
        }

        public static T QueryById<T> (int id) where T : McObject, new()
        {
            return BackEnd.Instance.Db.Query<T> (
                string.Format ("SELECT f.* FROM {0} AS f WHERE " +
                    " f.Id = ? ", 
                    typeof(T).Name), 
                id).SingleOrDefault ();
        }
    }
}

