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
		private void FireEvent (string MethodName, object obj) {
			var type = obj.GetType ();
			if (null != type.GetInterface ("ISQLiteEventable")) {
				var target = (ISQLiteEventable)obj;
				var method = type.GetMethod (MethodName);
				method.Invoke (target, new object[] {type, target.Id, EventArgs.Empty});
			}
		}
		private void DidWriteToDbEvent (object obj) {
			FireEvent ("Fire_DidWriteToDb", obj);
		}
		private void DidWriteToDbEvent (IEnumerable objects) {
			foreach (var obj in objects) {
				DidWriteToDbEvent (obj);
			}
		}
		private void WillDeleteFromDb (object obj) {
			FireEvent ("Fire_WillDeleteFromDb", obj);
		}
		private void WillDeleteFromDb (IEnumerable objects) {
			foreach (var obj in objects) {
				WillDeleteFromDb (obj);
			}
		}
		private void WillDeleteFromDb<T> (object primarykey) where T : new() {
			if (primarykey is System.Int32) {
				T dummy = new T ();
				if (null != dummy.GetType().GetInterface("ISQLiteEventable")) {
					((ISQLiteEventable)dummy).Id = (int)primarykey;
					WillDeleteFromDb (dummy);
				}
			}
		}
		public int CreateTable<T>(CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable<T> (createFlags);
		}
		public int CreateTable(Type ty, CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable (ty, createFlags);
		}
		public int InsertAll (System.Collections.IEnumerable objects) {
			var retval = m_db.Insert (objects);
			if (0 < retval) {
				DidWriteToDbEvent (objects);
			}
			return retval;
		}
		public int InsertAll (System.Collections.IEnumerable objects, string extra) {
			var retval = m_db.InsertAll (objects, extra);
			if (0 < retval) {
				DidWriteToDbEvent (objects);
			}
			return retval;
		}
		public int InsertAll (System.Collections.IEnumerable objects, Type objType) {
			var retval = m_db.InsertAll (objects, objType);
			if (0 < retval) {
				DidWriteToDbEvent (objects);
			}
			return retval;
		}
		public int Insert (object obj) {
			var retval = m_db.Insert (obj);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int InsertOrReplace (object obj) {;
			var retval = m_db.InsertOrReplace (obj);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int Insert (object obj, Type objType) {
			var retval = m_db.Insert (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int InsertOrReplace (object obj, Type objType) {
			var retval = m_db.InsertOrReplace (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int Insert (object obj, string extra) {
			var retval = m_db.Insert (obj, extra);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int Insert (object obj, string extra, Type objType) {
			var retval = m_db.Insert (obj, extra, objType);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int Delete (object objectToDelete) {
			WillDeleteFromDb (objectToDelete);
			var retval = m_db.Delete (objectToDelete);
			return retval;
		}
		public int Delete<T> (object primaryKey) where T : new() {
			WillDeleteFromDb<T> (primaryKey);
			return m_db.Delete<T> (primaryKey);
		}
		public int Update (object obj) {
			var retval = m_db.Update (obj);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int Update (object obj, Type objType) {
			var retval = m_db.Update (obj, objType);
			if (0 < retval) {
				DidWriteToDbEvent (obj);
			}
			return retval;
		}
		public int UpdateAll (System.Collections.IEnumerable objects) {
			var retval = m_db.UpdateAll (objects);
			if (0 < retval) {
				DidWriteToDbEvent (objects);
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

