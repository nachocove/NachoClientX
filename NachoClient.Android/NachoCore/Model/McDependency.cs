//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    /// Contact dependency lists objects that depend on certain contacts.
    /// So, if a contact score changes due to the contact score states being
    /// updated, this table can quickly list all objects (of a certain type)
    /// that needs to be updated. 
    /// 
    public class McEmailMessageDependency : McObject
    {
        [Indexed]
        public Int64 ContactId { get; set; }

        // Type of contacts. Currently, support "Sender".
        public string ContactType { get; set; }

        [Indexed]
        public Int64 EmailMessageId { get; set; }

        public McEmailMessageDependency ()
        {
            ContactId = 0;
            EmailMessageId = 0;
            ContactType = "";
        }

        // Get all McEmailMessage given a contact id
        public static List<McEmailMessage> QueryDependenciesByContactId (Int64 contactId)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> ("SELECT m.* FROM McEmailMessage AS m " +
            "INNER JOIN McEmailMessageDependency AS d " +
            "ON m.Id = d.EmailMessageId " +
            "WHERE d.ContactId == ?", contactId);
        }

        // Get all McContact given an email message id
        public static List<McContact> QueryDependenciesByEmailMessageId (Int64 emailMessageId)
        {
            return NcModel.Instance.Db.Query<McContact> ("SELECT c.* FROM McContact AS c " +
            "INNER JOIN McEmailMessageDependency AS d " +
            "ON c.Id = d.ContactId " +
            "WHERE d.EmailMessageId == ?", emailMessageId);
        }

        private bool ValidType ()
        {
            return ("Sender" == ContactType);
        }

        public static List<McEmailMessageDependency> QueryByEmailMessageId (Int64 emailMessagegId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageDependency> ("SELECT * FROM McEmailMessageDependency WHERE EmailMessageId == ?", emailMessagegId);
        }

        public static List<McEmailMessageDependency> QueryByContactId (Int64 contactId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageDependency> ("SELECT * FROM McEmailMessageDependency WHERE ContactId == ?", contactId);
        }

        public static void DeleteByEmailMessageId (Int64 emailMessageid)
        {
            NcModel.Instance.Db.Query<McEmailMessageDependency> ("DELETE FROM McEmailMessageDependency WHERE EmailMessageId == ?", emailMessageid);
        }

        public static void DeleteByContactId (Int64 contactId)
        {
            NcModel.Instance.Db.Query<McEmailMessageDependency> ("DELETE FROM McEmailMessageDependency WHERE ContactId == ?", contactId);
        }

        public override int Insert ()
        {
            NcAssert.True (ValidType ());
            int retval = base.Insert ();
            return retval;
        }

        public override int Update ()
        {
            NcAssert.True (ValidType ());
            int retval = base.Update ();
            return retval;
        }

        public void InsertByBrain ()
        {
            int rc = Insert ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageDependencyCounters.Insert.Click ();
            }
        }

        public void UpdateByBrain ()
        {
            int rc = Update ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageDependencyCounters.Update.Click ();
            }
        }

        public void DeleteByBrain ()
        {
            int rc = Delete ();
            if (0 < rc) {
                NcBrain.SharedInstance.McEmailMessageDependencyCounters.Delete.Click ();
            }
        }
    }

    // TOOD - Add McMeetingDependency
}

