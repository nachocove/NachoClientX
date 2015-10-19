//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;

namespace NachoCore.Model
{
    /// <summary>
    /// Support the proper synching of multiple device calendars.  This requires two changes to existing objects.
    /// (1) The device account should have a name, because that name is displayed in the calendar field of the event
    /// detail view.  (2) The folder where device events have been kept now needs to be hidden, so it doesn't show
    /// up in the list of calendars that the user can select. (The folder will remain, and will be used as a backstop
    /// if there are problems synching the real device calendars. But it should remain invisible and mostly unused.)
    /// </summary>
    public class NcMigration48 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAttachment> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var atts = Db.Table<McAttachment> ().Where (x => 0 < x.ItemId);
            McAbstrItem item;
            foreach (var att in atts) {
                switch (att.ClassCode) {
                case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                    item = McCalendar.QueryById<McCalendar> (att.ItemId);
                    break;
                case McAbstrFolderEntry.ClassCodeEnum.Email:
                    item = McEmailMessage.QueryById<McEmailMessage> (att.ItemId);
                    break;
                default:
                    item = null;
                    continue;
                }
                if (null != item) {
                    att.Link (item);
                }
                att.ItemId = -1;
                att.Update ();
                UpdateProgress (1);
            }
        }
    }
}

