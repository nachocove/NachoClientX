using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using NachoPlatform;
using System.Security.Cryptography;

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

        // This type is stored in the db; add to the end
        public enum NotificationConfigurationEnum : int
        {
            ALLOW_ALL_1 = 1,
            ALLOW_HOT_2 = 2,
            ALLOW_VIP_4 = 4,
            ALLOW_CUSTOM_8 = 8,
            ALLOW_INVITES_16 = 16,
            ALLOW_REMINDERS_32 = 32,
            ALLOW_INBOX_64 = 64,
        };

        public const NotificationConfigurationEnum DefaultNotificationConfiguration =
            NotificationConfigurationEnum.ALLOW_HOT_2 |
            NotificationConfigurationEnum.ALLOW_VIP_4 |
            NotificationConfigurationEnum.ALLOW_CUSTOM_8 |
            NotificationConfigurationEnum.ALLOW_INVITES_16 |
            NotificationConfigurationEnum.ALLOW_REMINDERS_32 |
            NotificationConfigurationEnum.ALLOW_INBOX_64;

        public McAccount ()
        {
            DaysToSyncEmail = Xml.Provision.MaxAgeFilterCode.OneMonth_5;
            NotificationConfiguration = DefaultNotificationConfiguration;
            FastNotificationEnabled = true;
        }

        public AccountTypeEnum AccountType { get; set; }

        public AccountServiceEnum AccountService { get; set; }

        /// <summary>
        /// DO NOT ACCESS. Use UpdateLogSalt/GetLogSalt.
        /// Property is here for SQLite.Net only!
        /// </summary>
        /// <value>The log salt.</value>
        public string LogSalt { get; set; }

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

        public NotificationConfigurationEnum NotificationConfiguration { get; set; }

        public bool FastNotificationEnabled { get; set; }

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

        public string GetLogSalt ()
        {
            if (Keychain.Instance.HasKeychain () && null == LogSalt) {
                return Keychain.Instance.GetLogSalt (Id);
            } else {
                return LogSalt;
            }
        }

        private void GenerateLogSalt ()
        { 
            RandomNumberGenerator rng = new RNGCryptoServiceProvider ();
            byte[] randData = new byte[32];
            rng.GetBytes (randData);
            string randString = Convert.ToBase64String (randData);
            LogSalt = randString;
        }

        public void GenerateAndUpdateLogSalt ()
        {
            GenerateLogSalt ();
            UpdateLogSalt ();
        }

        public void UpdateLogSalt ()
        {
            NcAssert.True (0 != Id);
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.SetLogSalt (Id, LogSalt);
                LogSalt = null;
            } 
            Update ();
        }

        public override int Delete ()
        {
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.DeleteLogSalt (Id);
                LogSalt = null;
                Update ();
            } 
            return base.Delete ();
        }
            
        public static string GetLoggablePassword (McAccount account, string password)
        {
            NcAssert.False (string.IsNullOrEmpty(account.GetLogSalt ()));
            string hash = HashHelper.Sha256 (account.GetLogSalt () + password);
            return hash.Substring(hash.Length-3); // e.g. "f47"
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

