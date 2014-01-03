using SQLite;
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public abstract class McEventable : McObject, ISQLiteEventable
    {
        public static event SQLiteEventHandler DbEvent;

        [Indexed]
        public int AccountId { get; set; }

        public void FireDbEvent (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, EventArgs e) {
            if (null != DbEvent) {
                DbEvent (dbActor, dbEvent, this, e);
            }
        }
    }
}

