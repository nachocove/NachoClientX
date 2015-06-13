//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class McEmailMessageScore : McAbstrObjectPerAcc, IScoreStates
    {
        // Id of the corresponding McEmailMessage
        [Indexed]
        public int ParentId { get; set; }

        /// How many times the email is read
        public int TimesRead { get; set; }

        /// How long the user read the email
        public int SecondsRead { get; set; }

        public bool IsRead { get; set; }

        public bool IsReplied { get; set; }

        public static McEmailMessageScore QueryByParentId (int parentId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageScore> (
                "SELECT e.* FROM McEmailMessageScore AS e WHERE likelihood(e.ParentId = ?, 0.1)",
                parentId).SingleOrDefault ();
        }

        public static void DeleteByParentId (int parentId)
        {
            NcModel.Instance.Db.Execute ("DELETE FROM McEmailAddressScore WHERE ParentId = ?", parentId);
        }
    }
}

