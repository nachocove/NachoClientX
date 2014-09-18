using System;
using System.Collections.Generic;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McAccount : McAbstrObject
    {
        public enum AccountTypeEnum { Exchange, Device };

        public AccountTypeEnum AccountType { get; set; }

        public string EmailAddr { get; set; }

        public string DisplayName { get; set; }

        public string Culture { get; set; }

        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncEmail { get; set; }

        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncCalendar { get; set; }

        public int PreferredConferenceId { get; set; }

        public string Signature { get; set; }

        public static IEnumerable<McAccount> QueryByAccountType (AccountTypeEnum accountType)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == accountType);
        }
    }

    public class ConstMcAccount : McAccount
    {
        // Constant handle to indicate that the object/message isn't account-specific.
        public static McAccount NotAccountSpecific = Create ();

        private bool IsLocked = false;

        public override int Id {
            get {
                return base.Id;
            }
            set {
                NcAssert.True (!IsLocked);
                base.Id = value;
            }
        }

        public static McAccount Create ()
        {
            var stone = new ConstMcAccount ();
            stone.IsLocked = true;
            return stone;
        }

        private ConstMcAccount ()
        {
            base.Id = -1;
        }

        public override int Insert ()
        {
            NcAssert.True (false);
            return -1;
        }
    }
}

