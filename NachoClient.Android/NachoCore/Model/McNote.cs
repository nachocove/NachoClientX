//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    public class McNote : McAbstrObjectPerAcc, IFilesViewItem
    {
        public enum NoteType
        {
            Event,
            Contact,
        };
            
        [Indexed]
        public int TypeId { get; set; }

        [Indexed]
        public string DisplayName { get; set; }

        public NoteType noteType { get; set; }

        public string noteContent { get; set; }

        public static List<McNote> QueryByTypeId (int typeId, NoteType noteType)
        {
            return NcModel.Instance.Db.Query<McNote> ("SELECT n.* FROM McNote AS n WHERE " +
                " n.TypeId = ? AND " +
                " n.noteType = ?",
                typeId, noteType);
        }

        public static List<McNote> QueryByType (NoteType noteType)
        {
            return NcModel.Instance.Db.Query<McNote> ("SELECT n.* FROM McNote AS n WHERE " +
                " n.noteType = ?", noteType);
        }
    }
}
