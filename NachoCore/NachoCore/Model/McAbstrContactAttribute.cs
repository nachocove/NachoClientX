//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    /// Used to create lists of name/value pairs
    public class McAbstrContactAttribute : McAbstrObjectPerAcc
    {
        [Indexed]
        public Int64 ContactId { get; set; }

        /// Values are created & displayed in a certain order
        public int Order { get; set; }

        // User-defined default contact attribute
        public bool IsDefault { get; set; }

        /// Field name
        public string Name { get; set; }

        /// User-defined label if one exists
        public string Label { get; set; }

        public McContact GetContact ()
        {
            return McContact.QueryById<McContact> ((int)ContactId);
        }

        private McContact _CachedContact;
        public McContact CachedContact {
            get {
                if (_CachedContact == null) {
                    _CachedContact = GetContact ();
                }
                return _CachedContact;
            }
        }

        public static List<T> QueryByContactId<T> (int contactId) where T : McAbstrContactAttribute, new()
        {
            return NcModel.Instance.Db.Table<T> ().Where (x => contactId == x.ContactId).ToList ();
        }

        public McAbstrContactAttribute ()
        {
        }

        public virtual void ChangeName (string name)
        {
            Name = name;
            Label = ContactsHelper.ExchangeNameToLabel (Name);
        }

        public virtual string GetDisplayLabel (string fallback = null)
        {
            if (!String.IsNullOrWhiteSpace (Label)) {
                return Label;
            }
            var label = ContactsHelper.ExchangeNameToLabel (Name);
            if (!String.IsNullOrWhiteSpace (label)) {
                return label;
            }
            if (!String.IsNullOrWhiteSpace (fallback)) {
                return fallback;
            }
            return Name;
        }
    }
}

