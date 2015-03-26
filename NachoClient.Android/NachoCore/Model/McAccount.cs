using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McAccount : McAbstrObject
    {
        public enum AccountTypeEnum
        {
            Exchange,
            Device,
        };

        public enum AccountServiceEnum
        {
            None,
            Exchange,
            HotmailExchange,
            OutlookExchange,
            GoogleExchange,
        };

        public McAccount ()
        {
            DaysToSyncEmail = Xml.Provision.MaxAgeFilterCode.OneMonth_5;
        }

        public AccountTypeEnum AccountType { get; set; }

        public AccountServiceEnum AccountService { get; set; }

        public string EmailAddr { get; set; }

        // This is the nickname of the account, not the user's name
        public string DisplayName { get; set; }

        // This is the user's display name, it should be null.
        // Exchange servers do a good job of converting the email
        // address on outgoing messages to including the user's name.
        public string DisplayUserName { get; set; }

        public string Culture { get; set; }

        // Default is OneMonth_5. The only other supported valued is SyncAll_0.
        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncEmail { get; set; }

        // DaysToSyncCalendar NYI.
        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncCalendar { get; set; }

        public int PreferredConferenceId { get; set; }

        public string Signature { get; set; }

        public static IEnumerable<McAccount> QueryByAccountType (AccountTypeEnum accountType)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == accountType);
        }

        public static McAccount GetDeviceAccount ()
        {
            return McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).SingleOrDefault ();
        }

        public static string AccountServiceName (AccountServiceEnum service)
        {
            switch (service) {
            case AccountServiceEnum.None:
                return "";
            case AccountServiceEnum.Exchange:
                return "Exchange";
            case AccountServiceEnum.HotmailExchange:
                return "Hotmail";
            case AccountServiceEnum.OutlookExchange:
                return "Outlook.com";
            case AccountServiceEnum.GoogleExchange:
                return "Google Apps for Work";
            default:
                NcAssert.CaseError (String.Format ("AccountServiceName: unknown {0}", service));
                return "";
            }
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

