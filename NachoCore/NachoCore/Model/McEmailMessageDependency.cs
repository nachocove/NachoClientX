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
    public class McEmailMessageDependency : McAbstrObjectPerAcc
    {
        public enum AddressType
        {
            UNKNOWN = 0,
            SENDER = 1,
            TO = 2,
            CC = 3,
        };

        [Indexed]
        public Int64 EmailAddressId { get; set; }

        // Type of contacts. Currently, support SENDER only.
        public int EmailAddressType { get; set; }

        [Indexed]
        public Int64 EmailMessageId { get; set; }

        public McEmailMessageDependency ()
        {
            EmailAddressId = 0;
            EmailMessageId = 0;
            EmailAddressType = (int)AddressType.UNKNOWN;
        }

        public McEmailMessageDependency (int accountId) : this ()
        {
            AccountId = accountId;
        }

        private bool ValidType ()
        {
            return ((int)AddressType.SENDER == EmailAddressType);
        }

        public static List<McEmailMessageDependency> QueryByEmailMessageId (Int64 emailMessagegId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageDependency> ("SELECT * FROM McEmailMessageDependency WHERE EmailMessageId == ?", emailMessagegId);
        }

        public static List<McEmailMessageDependency> QueryByEmailAddressId (Int64 emailAddressId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageDependency> ("SELECT * FROM McEmailMessageDependency WHERE EmailAddressId == ?", emailAddressId);
        }

        public static void DeleteByEmailMessageId (Int64 emailMessageid)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            NcModel.Instance.Db.Query<McEmailMessageDependency> ("DELETE FROM McEmailMessageDependency WHERE EmailMessageId == ?", emailMessageid);
        }

        public static void DeleteByEmailAddressId (Int64 emailAddressId)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            NcModel.Instance.Db.Query<McEmailMessageDependency> ("DELETE FROM McEmailMessageDependency WHERE EmailAddressId == ?", emailAddressId);
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
            Insert ();
        }

        public void UpdateByBrain ()
        {
            Update ();
        }

        public void DeleteByBrain ()
        {
            Delete ();
        }
    }

    // TOOD - Add McMeetingDependency
}

