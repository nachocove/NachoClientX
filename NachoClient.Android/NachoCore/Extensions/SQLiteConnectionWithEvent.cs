using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using SQLite;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class SQLiteConnectionWithEvents
    {
        private SQLiteConnection m_db;

        public SQLiteConnectionWithEvents (string databasePath, bool storeDateTimeAsTicks = false)
: this (databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, storeDateTimeAsTicks)
        { 
        }

        public SQLiteConnectionWithEvents (string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false)
        {
            m_db = new SQLiteConnection (databasePath, openFlags, storeDateTimeAsTicks);
        }

        private void DidWriteToDbEvent (BackEnd.DbActors actor, object obj)
        {
            var type = obj.GetType ();
            if (null != type.GetInterface ("ISQLiteEventable")) {
                var target = (ISQLiteEventable)obj;
                target.FireDbEvent (actor, BackEnd.DbEvents.DidWrite, EventArgs.Empty);
            }
        }

        private void DidWriteToDbEvent (BackEnd.DbActors actor, IEnumerable objects)
        {
            foreach (var obj in objects) {
                DidWriteToDbEvent (actor, obj);
            }
        }

        private void WillDeleteFromDb (BackEnd.DbActors actor, object obj)
        {
            var type = obj.GetType ();
            if (null != type.GetInterface ("ISQLiteEventable")) {
                var target = (ISQLiteEventable)obj;
                target.FireDbEvent (actor, BackEnd.DbEvents.WillDelete, EventArgs.Empty);
            }
        }

        private void WillDeleteFromDb (BackEnd.DbActors actor, IEnumerable objects)
        {
            foreach (var obj in objects) {
                WillDeleteFromDb (actor, obj);
            }
        }

        public int CreateTable<T> (CreateFlags createFlags = CreateFlags.None)
        {
            return m_db.CreateTable<T> (createFlags);
        }

        public int CreateTable (Type ty, CreateFlags createFlags = CreateFlags.None)
        {
            return m_db.CreateTable (ty, createFlags);
        }

        // For unit tests
        public int DropTable<T>()
        {
            return m_db.DropTable<T> ();
        }

        // The client-invoked method (that throw events) are below.
        public int InsertAll (BackEnd.DbActors actor, System.Collections.IEnumerable objects)
        {
            var retval = m_db.Insert (objects);
            if (0 < retval) {
                DidWriteToDbEvent (actor, objects);
            }
            return retval;
        }

        public int InsertAll (BackEnd.DbActors actor, System.Collections.IEnumerable objects, string extra)
        {
            var retval = m_db.InsertAll (objects, extra);
            if (0 < retval) {
                DidWriteToDbEvent (actor, objects);
            }
            return retval;
        }

        public int InsertAll (BackEnd.DbActors actor, System.Collections.IEnumerable objects, Type objType)
        {
            var retval = m_db.InsertAll (objects, objType);
            if (0 < retval) {
                DidWriteToDbEvent (actor, objects);
            }
            return retval;
        }

        public int Insert (BackEnd.DbActors actor, object obj)
        {
            var retval = m_db.Insert (obj);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int InsertOrReplace (BackEnd.DbActors actor, object obj)
        {
            ;
            var retval = m_db.InsertOrReplace (obj);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int Insert (BackEnd.DbActors actor, object obj, Type objType)
        {
            var retval = m_db.Insert (obj, objType);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int InsertOrReplace (BackEnd.DbActors actor, object obj, Type objType)
        {
            var retval = m_db.InsertOrReplace (obj, objType);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int Insert (BackEnd.DbActors actor, object obj, string extra)
        {
            var retval = m_db.Insert (obj, extra);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int Insert (BackEnd.DbActors actor, object obj, string extra, Type objType)
        {
            var retval = m_db.Insert (obj, extra, objType);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int Delete (BackEnd.DbActors actor, object objectToDelete)
        {
            WillDeleteFromDb (actor, objectToDelete);
            var retval = m_db.Delete (objectToDelete);
            return retval;
        }

        public int Update (BackEnd.DbActors actor, object obj)
        {
            var retval = m_db.Update (obj);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int Update (BackEnd.DbActors actor, object obj, Type objType)
        {
            var retval = m_db.Update (obj, objType);
            if (0 < retval) {
                DidWriteToDbEvent (actor, obj);
            }
            return retval;
        }

        public int UpdateAll (BackEnd.DbActors actor, System.Collections.IEnumerable objects)
        {
            var retval = m_db.UpdateAll (objects);
            if (0 < retval) {
                DidWriteToDbEvent (actor, objects);
            }
            return retval;
        }

        public TableQuery<T> Table<T> () where T : new()
        {
            return m_db.Table<T> ();
        }

        public T Find<T> (object pk) where T : new()
        {
            return m_db.Find<T> (pk);
        }

        public T Get<T> (Expression<Func<T, bool>> predicate) where T : new()
        {
            return m_db.Get<T> (predicate);
        }

        // Return the newly inserted row's id.
        public Int64 LastId()
        {
            string sql = @"select last_insert_rowid()";
            return (Int64)m_db.ExecuteScalar<Int64> (sql); 
        }

        // Insert & return last row id.
        // TODO: Add event support?
        public NcResult Insert (McObject obj)
        {
            System.Diagnostics.Trace.Assert (obj.Id == 0);

            Int64 lastId = 0;
            obj.LastModified = DateTime.UtcNow;
            m_db.RunInTransaction(() => {
                m_db.Insert(obj);
                lastId = LastId();
            });
            // TODO: Handled errors
            return NcResult.OK (lastId);
        }


        // Update, no event.  Temporary?
        // Lots of TODOs in this code.
        public NcResult Update (McObject obj)
        {
            System.Diagnostics.Trace.Assert (obj.Id > 0);

            DateTime lastModified = obj.LastModified;
            obj.LastModified = DateTime.UtcNow;

            m_db.RunInTransaction(() => {
                // TODO: Check that lastModified is unchanged
                m_db.Update(obj);
            });
            // TODO: Handled errors
            return NcResult.OK (obj.Id);
        }


        // Delete, no event.  Temporary?
        // Lots of TODOs in this code.
        public NcResult Delete (McObject obj)
        {
            System.Diagnostics.Trace.Assert (obj.Id > 0);
            m_db.Delete(obj);
            // TODO: Handled errors
            return NcResult.OK (obj.Id);
        }

        public List<T> Query<T> (string query, params object[] args) where T : new()
        {
            return m_db.Query<T> (query, args);
        }

    }
}

