using System;
using NachoCore.Model;

// FIXME - decouple from Model.
namespace NachoCore.Utils
{
    public delegate void SQLiteEventHandler (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, McEventable target, EventArgs e);
    public interface ISQLiteEventable
    {
        int Id { get; set; }

        int AccountId { get; set; }

        void FireDbEvent (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, EventArgs e);
    }
}

