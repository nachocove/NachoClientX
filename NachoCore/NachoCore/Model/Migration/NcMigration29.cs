//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class TableColumns
    {
        protected HashSet<string> Columns;

        public TableColumns (string tableName)
        {
            Columns = new HashSet<string> ();
            var colInfo = NcModel.Instance.Db.GetTableInfo (tableName);
            foreach (var col in colInfo) {
                Columns.Add (col.Name);
            }
        }

        public bool ColumnExists (string columnName)
        {
            return Columns.Contains (columnName);
        }
    }

    // This migration copies the columns from McEmailAddress to McEmailAddressScore and McEmailMessage to McEmailMessageScore
    public class NcMigration29 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            int count = Db.Table<McEmailMessage> ().Count () +
                        Db.Table<McEmailAddress> ().Count ();
            return count;
        }

        public override void Run (CancellationToken token)
        {
            var emailMessageSrcCols = new List<string> () {
                "Id",
                "AccountId",
                "TimesRead",
                "SecondsRead",
                "ScoreIsRead",
                "ScoreIsReplied",
            };
            var emailMessageDstCols = new List<string> () {
                "ParentId",
                "AccountId",
                "TimesRead",
                "SecondsRead",
                "IsRead",
                "IsReplied",
            };

            // May not have all the columns. Remove all the ones that do not exist
            var emailMessageColumns = new TableColumns ("McEmailMessage");
            NcAssert.True (emailMessageSrcCols.Count == emailMessageDstCols.Count);
            for (int n = emailMessageSrcCols.Count - 1; 0 <= n; n--) {
                if (emailMessageColumns.ColumnExists (emailMessageSrcCols [n])) {
                    continue;
                }
                emailMessageSrcCols.RemoveAt (n);
                emailMessageDstCols.RemoveAt (n);
            }

            // Copy columns from McEmailMessage to McEmailMessageScore
            var cmd = String.Format ("INSERT INTO McEmailMessageScore ({0}) SELECT {1} FROM McEmailMessage",
                          String.Join (",", emailMessageDstCols), String.Join (",", emailMessageSrcCols));
            var rows = Db.Execute (cmd);
            UpdateProgress (rows);

            var emailAddressSrcCols = new List<string> () {
                "Id",
                "AccountId",
                "EmailsReceived",
                "EmailsRead",
                "EmailsReplied",
                "EmailsArchived",
                "EmailsDeleted",
                "ToEmailsReceived",
                "ToEmailsRead",
                "ToEmailsReplied",
                "ToEmailsArchived",
                "ToEmailsDeleted",
                "CcEmailsReceived",
                "CcEmailsRead",
                "CcEmailsReplied",
                "CcEmailsArchived",
                "CcEmailsDeleted",
                "EmailsSent",
            };
            var emailAddressDstCols = new List<string> () {
                "ParentId",
                "AccountId",
                "EmailsReceived",
                "EmailsRead",
                "EmailsReplied",
                "EmailsArchived",
                "EmailsDeleted",
                "ToEmailsReceived",
                "ToEmailsRead",
                "ToEmailsReplied",
                "ToEmailsArchived",
                "ToEmailsDeleted",
                "CcEmailsReceived",
                "CcEmailsRead",
                "CcEmailsReplied",
                "CcEmailsArchived",
                "CcEmailsDeleted",
                "EmailsSent",
            };

            // May not have all the columns. Remove all the ones that do not exist
            var emailAddressColumns = new TableColumns ("McEmailAddress");
            NcAssert.True (emailAddressSrcCols.Count == emailAddressDstCols.Count);
            for (int n = emailAddressSrcCols.Count - 1; 0 <= n; n--) {
                if (emailAddressColumns.ColumnExists (emailAddressSrcCols [n])) {
                    continue;
                }
                emailAddressSrcCols.RemoveAt (n);
                emailAddressDstCols.RemoveAt (n);
            }

            // Copy McEmailAddress to McEmailAddressScore
            cmd = String.Format ("INSERT INTO McEmailAddressScore ({0}) SELECT {1} FROM McEmailAddress",
                String.Join (",", emailAddressDstCols), String.Join (",", emailAddressSrcCols));
            rows = Db.Execute (cmd);
            UpdateProgress (rows);
        }
    }
}

