using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using SQLite;

namespace NachoCore.Utils
{
	public class SQLiteConnectionWithEvents
	{
		private SQLiteConnection m_db;

		public SQLiteConnectionWithEvents (string databasePath, bool storeDateTimeAsTicks = false)
			: this (databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, storeDateTimeAsTicks) { 
		}

		public SQLiteConnectionWithEvents (string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false) {
			m_db = new SQLiteConnection (databasePath, openFlags, storeDateTimeAsTicks);
		}
		private void FireEvent (string MethodName, BackEnd.Actors actor, object obj) {
			var type = obj.GetType ();
			if (null != type.GetInterface ("ISQLiteEventable")) {
				var target = (ISQLiteEventable)obj;
				var method = type.GetMethod (MethodName);
				method.Invoke (target, new object[] {actor, target, EventArgs.Empty});
			}
		}
		private void DidWriteToDbEvent (BackEnd.Actors actor, object obj) {
			FireEvent ("Fire_DidWriteToDb", actor, obj);
		}
		private void DidWriteToDbEvent (BackEnd.Actors actor, IEnumerable objects) {
			foreach (var obj in objects) {
				DidWriteToDbEvent (actor, obj);
			}
		}
		private void WillDeleteFromDb (BackEnd.Actors actor, object obj) {
			FireEvent ("Fire_WillDeleteFromDb", actor, obj);
		}
		private void WillDeleteFromDb (BackEnd.Actors actor, IEnumerable objects) {
			foreach (var obj in objects) {
				WillDeleteFromDb (actor, obj);
			}
		}
		public int CreateTable<T>(CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable<T> (createFlags);
		}
		public int CreateTable(Type ty, CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable (ty, createFlags);
		}
		// The client-invoked method (that throw events) are below.
		public int InsertAll (BackEnd.Actors actor, System.Collections.IEnumerable objects) {
			var retval = m_db.Insert (objects);
			if (0 < retval) {
				DidWriteToDbEvent (actor, objects);
			}
			return retval;
		}
		public int InsertAll (BackEnd.Actors actor, System.Collections.IEnumerable objects, string extra) {
			var retval = m_db.InsertAll (objects, extra);
			if (0 < retval) {
				DidWriteToDbEvent (actor, objects);
			}
			return retval;
		}
		public int InsertAll (BackEnd.Actors actor, System.Collections.IEnumerable objects, Type objType) {
			var retval = m_db.InsertAll (objects, objType);
			if (0 < retval) {
				DidWriteToDbEvent (actor, objects);
			}
			return retval;
		}
		public int Insert (BackEnd.Actors actor, object obj) {
			var retval = m_db.Insert (obj);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int InsertOrReplace (BackEnd.Actors actor, object obj) {;
			var retval = m_db.InsertOrReplace (obj);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int Insert (BackEnd.Actors actor, object obj, Type objType) {
			var retval = m_db.Insert (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int InsertOrReplace (BackEnd.Actors actor, object obj, Type objType) {
			var retval = m_db.InsertOrReplace (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int Insert (BackEnd.Actors actor, object obj, string extra) {
			var retval = m_db.Insert (obj, extra);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int Insert (BackEnd.Actors actor, object obj, string extra, Type objType) {
			var retval = m_db.Insert (obj, extra, objType);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int Delete (BackEnd.Actors actor, object objectToDelete) {
			WillDeleteFromDb (actor, objectToDelete);
			var retval = m_db.Delete (objectToDelete);
			return retval;
		}
		public int Update (BackEnd.Actors actor, object obj) {
			var retval = m_db.Update (obj);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int Update (BackEnd.Actors actor, object obj, Type objType) {
			var retval = m_db.Update (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (actor, obj);
			}
			return retval;
		}
		public int UpdateAll (BackEnd.Actors actor, System.Collections.IEnumerable objects) {
			var retval = m_db.UpdateAll (objects);
			if (0 < retval) {
				DidWriteToDbEvent (actor, objects);
			}
			return retval;
		}
		public TableQuery<T> Table<T> () where T : new() {
			return m_db.Table<T>();
		}
		public T Find<T> (object pk) where T : new () {
			return m_db.Find<T> (pk);
		}
		public T Get<T> (Expression<Func<T, bool>> predicate) where T : new() {
			return m_db.Get<T> (predicate);
		}
	}
}

