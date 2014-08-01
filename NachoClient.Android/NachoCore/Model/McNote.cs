//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McNote : McAbstrObject
    {

        [Indexed]
        public int TypeId { get; set; }

        [Indexed]
        public string DisplayName { get; set; }

        public string noteType { get; set; }

        public string noteContent { get; set; }


        public static List<McNote> QueryByTypeId (int typeId, string noteType)
        {
            return NcModel.Instance.Db.Query<McNote> ("SELECT n.* FROM McNote AS n WHERE " +
                " n.TypeId = ? AND " +
                " n.noteType = ?",
                typeId, noteType);
        }
       
    }
}
