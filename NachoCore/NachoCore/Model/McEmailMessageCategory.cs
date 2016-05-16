//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

namespace NachoCore.Model
{
    public partial class McEmailMessageCategory : McAbstrObjectPerAcc
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        /// Name of category
        [MaxLength (256)]
        public string Name { get; set; }

        public McEmailMessageCategory ()
        {
            Id = 0;
            ParentId = 0;
            Name = null;
        }

        public McEmailMessageCategory (int accountId, string name, int parentId) : this (accountId, name)
        {
            ParentId = parentId;
        }

        public McEmailMessageCategory (int accountId, string name) : this ()
        {
            AccountId = accountId;
            Name = name;
        }

        public void SetParent (McEmailMessage r)
        {
            ParentId = r.Id;
        }
    }
}
