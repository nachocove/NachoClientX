//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;

namespace NachoCore.Model
{
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

