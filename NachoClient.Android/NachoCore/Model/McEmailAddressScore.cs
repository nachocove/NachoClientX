﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using SQLite;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class McEmailAddressScore : McAbstrObjectPerAcc, IScoreStates
    {
        // DO NOT update these fields directly. Use IncrementXXX methods instead.
        // Otherwise, the delta will not be saved correctly. ORM does not allow
        // private property so there is no way to use a public property with
        // customized getters to read of a private property.
        [Indexed]
        public int ParentId { get; set; }

        ///////////////////// From address statistics /////////////////////
        // Number of emails receivied  as a From address
        public int EmailsReceived { get; set; }

        // Number of emails read as a From address
        public int EmailsRead { get; set; }

        // Number of emails replied as a From address
        public int EmailsReplied { get; set; }

        // Number of emails archived as a From address
        public int EmailsArchived { get; set; }

        // Number of emails deleted without being read as a From address
        public int EmailsDeleted { get; set; }

        ///////////////////// To address statistics /////////////////////
        // Number of emails receivied  as a To address
        public int ToEmailsReceived { get; set; }

        // Number of emails read as a To address
        public int ToEmailsRead { get; set; }

        // Number of emails replied as a To address
        public int ToEmailsReplied { get; set; }

        // Number of emails archived as a To address
        public int ToEmailsArchived { get; set; }

        // Number of emails deleted without being read as a To address
        public int ToEmailsDeleted { get; set; }

        ///////////////////// Cc address statistics /////////////////////
        // Number of emails receivied  as a Cc address
        public int CcEmailsReceived { get; set; }

        // Number of emails read as a Cc address
        public int CcEmailsRead { get; set; }

        // Number of emails replied as a cc address
        public int CcEmailsReplied { get; set; }

        // Number of emails archived as a Cc address
        public int CcEmailsArchived { get; set; }

        // Number of emails deleted without being read as a Cc address
        public int CcEmailsDeleted { get; set; }

        // Number of emails sent to this contact
        public int EmailsSent { get; set; }

        public static McEmailAddressScore QueryByParentId (int parentId)
        {
            return NcModel.Instance.Db.Query<McEmailAddressScore> (
                "SELECT a.* FROM McEmailAddressScore AS a WHERE likelihood(a.ParentId = ?, 0.1)",
                parentId).SingleOrDefault ();
        }

        public static void DeleteByParentId (int parentId)
        {
            NcModel.Instance.Db.Execute ("DELETE FROM McEmailAddressScore WHERE ParentId = ?", parentId);
        }
    }
}

