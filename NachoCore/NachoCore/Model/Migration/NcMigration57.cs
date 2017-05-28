//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;
using SQLite;

namespace NachoCore.Model
{
	public class NcMigration57 : NcMigration
	{

        IntPtr UpdateStatement;
        IntPtr ConnectionHandle;
        static IntPtr NegativePointer = new IntPtr (-1);

		public NcMigration57 ()
		{
		}

		public override int GetNumberOfObjects ()
		{
			return 1;
		}

        public override void Run (System.Threading.CancellationToken token)
        {
            ConnectionHandle = NcModel.Instance.Db.Handle;
            UpdateStatement = SQLite3.Prepare2 (ConnectionHandle, "UPDATE McContact SET CachedSortFirstName = ?, CachedSortLastName = ?, CachedGroupFirstName = ?, CachedGroupLastName = ? WHERE Id = ?");
            var selectStatement = SQLite3.Prepare2 (ConnectionHandle, "SELECT Id, DisplayName, FirstName, MiddleName, LastName, Suffix, CompanyName FROM McContact");
            SQLite3.Result result;
            McContact contact;
            do {
                result = SQLite3.Step (selectStatement);
                contact = new McContact ();
                contact.Id = SQLite3.ColumnInt (selectStatement, 0);
                contact.DisplayName = SQLite3.ColumnString (selectStatement, 1);
                contact.FirstName = SQLite3.ColumnString (selectStatement, 2);
                contact.MiddleName = SQLite3.ColumnString (selectStatement, 3);
                contact.LastName = SQLite3.ColumnString (selectStatement, 4);
                contact.Suffix = SQLite3.ColumnString (selectStatement, 5);
                contact.CompanyName = SQLite3.ColumnString (selectStatement, 6);
                UpdateContact (contact);
            } while (result == SQLite3.Result.Row);
            if (result != SQLite3.Result.Done) {
            }
            SQLite3.Finalize (UpdateStatement);
            SQLite3.Finalize (selectStatement);
		}

        void UpdateContact (McContact contact)
        {
            contact.UpdateCachedSortNames ();
            SQLite3.Reset (UpdateStatement);
            SQLite3.BindText (UpdateStatement, 1, contact.CachedSortFirstName, -1, NegativePointer);
            SQLite3.BindText (UpdateStatement, 2, contact.CachedSortLastName, -1, NegativePointer);
            SQLite3.BindText (UpdateStatement, 3, contact.CachedGroupFirstName, -1, NegativePointer);
            SQLite3.BindText (UpdateStatement, 4, contact.CachedGroupLastName, -1, NegativePointer);
            SQLite3.BindInt (UpdateStatement, 5, contact.Id);
            SQLite3.Result result;
            do {
                result = SQLite3.Step (UpdateStatement);
            } while (result == SQLite3.Result.Row);
            if (result != SQLite3.Result.Done) {
                string msg = SQLite3.GetErrmsg (ConnectionHandle);
                throw SQLiteException.New (result, msg);
            }
        }
	}
}

