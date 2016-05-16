//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // Set the DisplayColor field for existing calendar folders.  Each calendar folder needs to have a unique
    // value, and the values should be continuous.

    public class NcMigration32 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            // The number of calendar folders whose DisplayColor field is not set.
            return Db.Table<McFolder> ()
                .Where (x => (x.Type == Xml.FolderHierarchy.TypeCode.DefaultCal_8 ||
                    x.Type == Xml.FolderHierarchy.TypeCode.UserCreatedCal_13) &&
                    x.DisplayColor != 0)
                .Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            // Handle the case where this migration was interrupted during a previous run.  Look for any
            // existing folders where DisplayColor is set, and then make sure any newly set folders have
            // a value greater than that.
            var maxIndexFolder = Db.Query<McFolder> (
                "SELECT f.* FROM McFolder AS f " +
                " WHERE f.Type IN " + Folder_Helpers.TypesToCommaDelimitedString (NachoFolders.FilterForCalendars) +
                " AND f.DisplayColor <> 0 " +
                " ORDER BY f.DisplayColor DESC").FirstOrDefault ();
            int colorIndex = (null == maxIndexFolder) ? 1 : maxIndexFolder.DisplayColor + 1;

            var calFoldersNeedingUpdate = Db.Query<McFolder> (
                "SELECT f.* FROM McFolder AS f " +
                " WHERE f.Type IN " + Folder_Helpers.TypesToCommaDelimitedString (NachoFolders.FilterForCalendars) +
                " AND f.DisplayColor = 0");
            foreach (var folder in calFoldersNeedingUpdate) {
                token.ThrowIfCancellationRequested ();
                folder.UpdateWithOCApply<McFolder> ((record) => {
                    ((McFolder)record).DisplayColor = colorIndex++;
                    return true;
                });
                UpdateProgress (1);
            }
        }
    }
}
