//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class NcMigration48 : NcMigration
    {
        List<McAttachment> Atts;
        public override int GetNumberOfObjects ()
        {
            Atts = NcModel.Instance.Db.Query<McAttachment> ("SELECT * FROM McAttachment WHERE ItemId > 0");
            return Atts.Count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            McAbstrItem item;
            foreach (var att in Atts) {
                token.ThrowIfCancellationRequested ();

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
                    att.ItemId = -1;
                    att.Update ();
                } else {
                    att.Delete ();
                }
                UpdateProgress (1);
            }
        }
    }
}

