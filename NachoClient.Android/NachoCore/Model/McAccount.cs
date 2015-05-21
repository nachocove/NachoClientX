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
            // Exchange ActiveSync.
            Exchange,
            Device,
            IMAP_SMTP,
        };

        public enum AccountServiceEnum
        {
            None,
            Exchange,
            HotmailExchange,
            OutlookExchange,
            GoogleExchange,
            GoogleDefault,
            HotmailDefault,
            Aol,
            IMAP_SMTP,
        };

        public enum AccountCapabilityEnum
        {
            EmailReaderWriter = (1 << 0),
            EmailSender = (1 << 1),
            CalReader = (1 << 2),
            CalWriter = (1 << 3),
            ContactReader = (1 << 4),
            ContactWriter = (1 << 5),
            TaskReader = (1 << 6),
            TaskWriter = (1 << 7),
        };

        public const AccountCapabilityEnum ActiveSyncCapabilities = (
            AccountCapabilityEnum.EmailReaderWriter |
            AccountCapabilityEnum.EmailSender |
            AccountCapabilityEnum.CalReader |
            AccountCapabilityEnum.CalWriter |
            AccountCapabilityEnum.ContactReader |
            AccountCapabilityEnum.ContactWriter |
            AccountCapabilityEnum.TaskReader |
            AccountCapabilityEnum.TaskWriter);

        public const AccountCapabilityEnum ImapCapabilities = (
            AccountCapabilityEnum.EmailReaderWriter);

        public const AccountCapabilityEnum SmtpCapabilities = (
            AccountCapabilityEnum.EmailSender);
        
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
        /// AccountType is set as a side effect of setting AccountService. 
        /// It is preferred to set it that way, rather than directly.
        public AccountTypeEnum AccountType { get; set; }

        public void SetAccountType (AccountTypeEnum value)
        {
            AccountType = value;
            switch (value) {
            case AccountTypeEnum.Exchange:
                AccountCapability = (
                    AccountCapabilityEnum.EmailReaderWriter |
                    AccountCapabilityEnum.EmailSender |
                    AccountCapabilityEnum.CalReader |
                    AccountCapabilityEnum.CalWriter |
                    AccountCapabilityEnum.ContactReader |
                    AccountCapabilityEnum.ContactWriter);
                break;
            case AccountTypeEnum.Device:
                    // FIXME - need to support full contact/cal access thru BE.
                AccountCapability = (
                    AccountCapabilityEnum.CalReader |
                    AccountCapabilityEnum.ContactReader);
                break;
            case AccountTypeEnum.IMAP_SMTP:
                AccountCapability = (
                    AccountCapabilityEnum.EmailReaderWriter |
                    AccountCapabilityEnum.EmailSender);
                break;
            default:
                NcAssert.CaseError (value.ToString ());
                break;
            }
        }

        // The service that the user picked when setting up the account
        public AccountServiceEnum AccountService { get; set; }

        public void SetAccountService (AccountServiceEnum value)
        {
            AccountService = value;

            switch (value) {
            case AccountServiceEnum.GoogleDefault:
            case AccountServiceEnum.HotmailDefault:
            case AccountServiceEnum.Aol:
            case AccountServiceEnum.IMAP_SMTP:
                AccountType = AccountTypeEnum.IMAP_SMTP;
                Protocols = (
                    McProtocolState.ProtocolEnum.IMAP |
                    McProtocolState.ProtocolEnum.SMTP);
                break;
            case AccountServiceEnum.Exchange:
            case AccountServiceEnum.GoogleExchange:
            case AccountServiceEnum.HotmailExchange:
            case AccountServiceEnum.OutlookExchange:
                AccountType = AccountTypeEnum.Exchange;
                Protocols = McProtocolState.ProtocolEnum.ActiveSync;
                break;
            default:
                NcAssert.CaseError (value.ToString ());
                break;
            }
        }
         
        // This is set as a side effect of setting AccountService. 
        public AccountCapabilityEnum AccountCapability { get; private set; }

        // The protocol(s) - possibly more than one - required by this account.
        // This is set as a side effect of setting AccountService. 
        public McProtocolState.ProtocolEnum Protocols { get; private set; }

        public string EmailAddr { get; set; }

        // This is the nickname of the account, not the user's name
        public string DisplayName { get; set; }

        // This is the image associated with the account, not the user's initials
        public int DisplayPortraitId { get; set; }

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

        public static IEnumerable<McAccount> QueryByEmailAddr (string emailAddr)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.EmailAddr == emailAddr);
        }

        public static IEnumerable<McAccount> QueryByAccountType (AccountTypeEnum accountType)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == accountType);
        }

        public static McAccount GetDeviceAccount ()
        {
            return McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).SingleOrDefault ();
        }

        public static List<McAccount> GetAllAccounts ()
        {
            return NcModel.Instance.Db.Query<McAccount> ("SELECT * FROM McAccount");
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
            case AccountServiceEnum.GoogleDefault:
                return "GMail";
            case AccountServiceEnum.HotmailDefault:
                return "Hotmail";
            case AccountServiceEnum.Aol:
                return "Aol";
            case AccountServiceEnum.IMAP_SMTP:
                return "IMAP";
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

