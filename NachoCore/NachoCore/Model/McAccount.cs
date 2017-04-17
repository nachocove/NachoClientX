using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using NachoPlatform;
using System.Security.Cryptography;
using NachoCore.SFDC;

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
            Unified,
            SalesForce,
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
            Device,
            Yahoo,
            iCloud,
            Office365Exchange,
            SalesForce,
        };

        [Flags]
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
            All = ~0,
        };

        public const AccountCapabilityEnum ActiveSyncCapabilities = (
                                                                        AccountCapabilityEnum.EmailReaderWriter |
                                                                        AccountCapabilityEnum.EmailSender |
                                                                        AccountCapabilityEnum.CalReader |
                                                                        AccountCapabilityEnum.CalWriter |
                                                                        AccountCapabilityEnum.ContactReader |
                                                                        AccountCapabilityEnum.ContactWriter |
                                                                        AccountCapabilityEnum.TaskReader |
                                                                        AccountCapabilityEnum.TaskWriter
                                                                    );

        public const AccountCapabilityEnum ImapCapabilities = (
                                                                  AccountCapabilityEnum.EmailReaderWriter
                                                              );

        public const AccountCapabilityEnum SmtpCapabilities = (
                                                                  AccountCapabilityEnum.EmailSender
                                                              );

        public const AccountCapabilityEnum DeviceCapabilities = (
                                                                    AccountCapabilityEnum.CalReader |
                                                                    AccountCapabilityEnum.CalWriter |
                                                                    AccountCapabilityEnum.ContactReader |
                                                                    AccountCapabilityEnum.ContactWriter
                                                                );

        public enum ConfigurationInProgressEnum : int
        {
            Done = 0,
            InProgress,
            GoogleCallback,
            CredentialsValidated,
        };

        // Flags an account that's being configured
        public ConfigurationInProgressEnum ConfigurationInProgress { get; set; }
        
        // This type is stored in the db; add to the end
        [Flags]
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

        /// <summary>
        /// Set the <see cref="AccountType"/> for the McAccount object.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="AccountCapability"/> as a side-effect.
        /// </remarks>
        /// <returns>Nothing</returns>
        /// <param name="value">The <see cref="AccountTypeEnum"/> to set.</param>
        public void SetAccountType (AccountTypeEnum value)
        {
            AccountType = value;
            switch (value) {
            case AccountTypeEnum.Exchange:
                AccountCapability = ActiveSyncCapabilities;
                break;
            case AccountTypeEnum.Device:
                AccountCapability = DeviceCapabilities;
                break;
            case AccountTypeEnum.IMAP_SMTP:
                AccountCapability = (ImapCapabilities | SmtpCapabilities);
                break;
            case AccountTypeEnum.Unified:
                AccountCapability = ActiveSyncCapabilities;
                break;
            case AccountTypeEnum.SalesForce:
                AccountCapability = SalesForceProtoControl.SalesForceCapabilities;
                break;
            default:
                NcAssert.CaseError (value.ToString ());
                break;
            }
        }

        // The service that the user picked when setting up the account
        public AccountServiceEnum AccountService { get; set; }

        /// <summary>
        /// Set the <see cref="AccountService"/> for the McAccount object.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="AccountType"/>, <see cref="Protocols"/>, and <see cref="AccountType"/> as a side-effect.
        /// </remarks>
        /// <returns>Nothing</returns>
        /// <param name="value">The <see cref="AccountServiceEnum"/> to set.</param>
        public void SetAccountService (AccountServiceEnum value)
        {
            AccountService = value;
            AccountType = GetAccountType (value);

            switch (AccountType) {
            case AccountTypeEnum.IMAP_SMTP:
                Protocols = (
                    McProtocolState.ProtocolEnum.IMAP |
                    McProtocolState.ProtocolEnum.SMTP);
                break;
            case AccountTypeEnum.Exchange:
                Protocols = McProtocolState.ProtocolEnum.ActiveSync;
                break;
            case AccountTypeEnum.Device:
                // No protocols.
                break;

            case AccountTypeEnum.SalesForce:
                Protocols = McProtocolState.ProtocolEnum.SalesForce;
                break;

            default:
                NcAssert.CaseError (value.ToString ());
                break;
            }
            SetAccountType (AccountType);
        }

        public static AccountTypeEnum GetAccountType (AccountServiceEnum value)
        {
            switch (value) {
            case AccountServiceEnum.GoogleDefault:
            case AccountServiceEnum.HotmailDefault:
            case AccountServiceEnum.Aol:
            case AccountServiceEnum.Yahoo:
            case AccountServiceEnum.iCloud:
            case AccountServiceEnum.IMAP_SMTP:
                return AccountTypeEnum.IMAP_SMTP;

            case AccountServiceEnum.Exchange:
            case AccountServiceEnum.GoogleExchange:
            case AccountServiceEnum.HotmailExchange:
            case AccountServiceEnum.OutlookExchange:
            case AccountServiceEnum.Office365Exchange:
                return AccountTypeEnum.Exchange;
            case AccountServiceEnum.Device:
                // FIXME: Do we need anything here?
                return AccountTypeEnum.Device;
            case AccountServiceEnum.SalesForce:
                return AccountTypeEnum.SalesForce;
            default:
                NcAssert.CaseError (value.ToString ());
                return AccountTypeEnum.Device;
            }
        }

        private static uint FudgeFactor = 1;

        public static TimeSpan SyncTimeSpan (ActiveSync.Xml.Provision.MaxAgeFilterCode code)
        {
            switch (code) {
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0:
                return TimeSpan.Zero;
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneDay_1:
                return TimeSpan.FromDays (1);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeDays_2:
                return TimeSpan.FromDays (3);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneWeek_3:
                return TimeSpan.FromDays (7);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.TwoWeeks_4:
                return TimeSpan.FromDays (14);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5:
                return TimeSpan.FromDays (30 + FudgeFactor);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.ThreeMonths_6:
                return TimeSpan.FromDays (90 + FudgeFactor * 2);
            case ActiveSync.Xml.Provision.MaxAgeFilterCode.SixMonths_7:
                return TimeSpan.FromDays (180 + FudgeFactor * 3);
            default:
                NcAssert.CaseError ();
                return TimeSpan.Zero;
            }
        }

        public TimeSpan DaysSyncEmailSpan ()
        {
            // Add 2 days just because we can.
            return SyncTimeSpan (DaysToSyncEmail);
        }

        public TimeSpan DaysSyncCalendar ()
        {
            return SyncTimeSpan (DaysToSyncCalendar);
        }

        // This is set as a side effect of setting AccountService.
        public AccountCapabilityEnum AccountCapability { get; set; }

        // The protocol(s) - possibly more than one - required by this account.
        // This is set as a side effect of setting AccountService.
        public McProtocolState.ProtocolEnum Protocols { get; set; }

        /// <summary>
        /// DO NOT ACCESS. Use UpdateLogSalt/GetLogSalt.
        /// Property is here for SQLite.Net only!
        /// </summary>
        /// <value>The log salt.</value>
        public string LogSalt { get; set; }

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

        public string HtmlSignature { get; set; }

        public NotificationConfigurationEnum NotificationConfiguration { get; set; }

        public bool FastNotificationEnabled { get; set; }

        public bool IsMdmBased { get; set; }

        public int ColorIndex { get; set; }

        /// <summary>
        /// Does this account have the given capability or capabilities?
        /// </summary>
        /// <param name="capability">A combination of capabilities from AccountCapabilityEnum</param>
        public bool HasCapability (McAccount.AccountCapabilityEnum capability)
        {
            return (AccountCapability & capability) == capability;
        }

        public static IEnumerable<McAccount> QueryByEmailAddr (string emailAddr)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.EmailAddr == emailAddr);
        }

        public static IEnumerable<McAccount> QueryByEmailAddrAndService (string emailAddr, AccountServiceEnum service)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.EmailAddr == emailAddr && x.AccountService == service);
        }

        public static IEnumerable<McAccount> QueryByAccountType (AccountTypeEnum accountType)
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == accountType);
        }

        public static IEnumerable<McAccount> QueryByAccountCapabilities (AccountCapabilityEnum accountCapabilities)
        {
            List<McAccount> result = new List<McAccount> ();
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (McAccount acc in accounts) {
                if (acc.ConfigurationInProgress == ConfigurationInProgressEnum.Done) {
                    if (acc.HasCapability (accountCapabilities)) {
                        result.Add (acc);
                    }
                }
            }
            return result;
        }

        // Create on first reference
        public static McAccount GetDeviceAccount ()
        {
            if (null == _deviceAccount) {
                _deviceAccount = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).SingleOrDefault ();
                if (null == _deviceAccount) {
                    NcModel.Instance.RunInTransaction (() => {
                        _deviceAccount = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).SingleOrDefault ();
                        if (null == _deviceAccount) {
                            _deviceAccount = new McAccount ();
                            _deviceAccount.DisplayName = "Device";
                            _deviceAccount.SetAccountType (McAccount.AccountTypeEnum.Device);
                            _deviceAccount.Insert ();
                        }
                    });
                }
            }
            return _deviceAccount;
        }
        // Cache it!
        static McAccount _deviceAccount;

        public static McAccount GetSalesForceAccount()
        {
            return McAccount.QueryByAccountType (McAccount.AccountTypeEnum.SalesForce).SingleOrDefault ();
        }

        // Create on first reference
        public static McAccount GetUnifiedAccount ()
        {
            if (null == _unifiedAccount) {
                _unifiedAccount = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Unified).SingleOrDefault ();
                if (null == _unifiedAccount) {
                    NcModel.Instance.RunInTransaction (() => {
                        _unifiedAccount = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Unified).SingleOrDefault ();
                        if (null == _unifiedAccount) {
                            _unifiedAccount = new McAccount ();
                            _unifiedAccount.DisplayName = "All Accounts";
                            _unifiedAccount.SetAccountType (McAccount.AccountTypeEnum.Unified);
                            _unifiedAccount.Insert ();
                        }
                    });
                }
            }
            return _unifiedAccount;
        }

        static McAccount _unifiedAccount;

        public static McAccount GetDefaultAccount (AccountCapabilityEnum capability)
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            var mutablesModule = "DefaultAccounts";
            var mutablesKey = String.Format ("Capability.{0}", (int)capability);
            var preferredId = McMutables.GetInt (deviceAccount.Id, mutablesModule, mutablesKey, 0);
            McAccount preferredAccount;
            if (preferredId != 0) {
                preferredAccount = McAccount.QueryById<McAccount> (preferredId);
                if (preferredAccount != null) {
                    return preferredAccount;
                }
            }
            var accounts = McAccount.GetAllAccounts ();
            foreach (var account in accounts) {
                if (account.AccountType != AccountTypeEnum.Device && account.AccountType != AccountTypeEnum.Unified) {
                    if (account.ConfigurationInProgress == ConfigurationInProgressEnum.Done) {
                        if (account.HasCapability (capability)) {
                            McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, account.Id);
                            return account;
                        }
                    }
                }
            }
            if (deviceAccount.HasCapability (capability)) {
                return deviceAccount;
            }
            return null;
        }

        public static void SetDefaultAccount (int accountId, AccountCapabilityEnum capability)
        {
            var deviceAccount = GetDeviceAccount ();
            var mutablesModule = "DefaultAccounts";
            var mutablesKey = String.Format ("Capability.{0}", (int)capability);
            McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, accountId);
        }

        public static McAccount EmailAccountForMessage (McEmailMessage message)
        {
            return EmailAccountForAccountId (message.AccountId);
        }

        public static McAccount EmailAccountForEvent (McEvent e)
        {
            return EmailAccountForAccountId (e.AccountId);
        }

        public static McAccount EmailAccountForCalendar (McCalendar calendar)
        {
            return EmailAccountForAccountId (calendar.AccountId);
        }

        public static McAccount EmailAccountForContact (McContact contact)
        {
            return EmailAccountForAccountId (contact.AccountId);
        }

        public static McAccount EmailAccountForAccount (McAccount account)
        {
            return EmailAccountForAccountId (account.Id);
        }

        private static McAccount EmailAccountForAccountId (int accountId)
        {
            var account = McAccount.QueryById<McAccount> (accountId);
            if (account.HasCapability (AccountCapabilityEnum.EmailSender) && account.AccountType != AccountTypeEnum.Unified) {
                return account;
            }
            return McAccount.GetDefaultAccount (AccountCapabilityEnum.EmailSender);
        }

        public static List<McAccount> GetAllAccounts ()
        {
            return NcModel.Instance.Db.Query<McAccount> ("SELECT * FROM McAccount");
        }

        public static List<McAccount> GetCanAddContactAccounts ()
        {
            return GetAllAccounts ().Where ((x) => x.CanAddContact ()).ToList ();
        }

        public static List<int> GetAllConfiguredNormalAccountIds ()
        {
            return (from account in McAccount.GetAllAccounts ()
                             where
                                 McAccount.AccountTypeEnum.Device != account.AccountType &&
                                 McAccount.AccountTypeEnum.Unified != account.AccountType &&
                                 McAccount.AccountTypeEnum.SalesForce != account.AccountType &&
                                 McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress
                             select account.Id).ToList ();
        }

        public static List<McAccount> GetAllConfiguredNormalAccounts ()
        {
            return (from account in McAccount.GetAllAccounts ()
                             where
                                 McAccount.AccountTypeEnum.Device != account.AccountType &&
                                 McAccount.AccountTypeEnum.Unified != account.AccountType &&
                                 McAccount.AccountTypeEnum.SalesForce != account.AccountType &&
                                 McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress
                             select account).ToList ();
        }

        public static McAccount GetAccountBeingConfigured ()
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => McAccount.ConfigurationInProgressEnum.Done != x.ConfigurationInProgress).SingleOrDefault ();
        }

        public static McAccount GetMDMAccount ()
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => true == x.IsMdmBased).SingleOrDefault ();
        }

        public static bool IsAccountBeingConfigured (int accountId)
        {
            var account = McAccount.QueryById<McAccount> (accountId);
            return (null != account) && (McAccount.ConfigurationInProgressEnum.Done != account.ConfigurationInProgress);
        }

        public bool CanAddContact ()
        {
            AccountCapabilityEnum capabilities = 0;
            switch (AccountType) {
            case AccountTypeEnum.Exchange:
                capabilities = ActiveSyncCapabilities;
                break;
            case AccountTypeEnum.IMAP_SMTP:
                capabilities = ImapCapabilities;
                break;
            default:
                break;
            }
            return capabilities.HasFlag (AccountCapabilityEnum.ContactWriter);
        }

        public string GetLogSalt ()
        {
            if (null == LogSalt) {
                if (0 < Id) {
                    return Keychain.Instance.GetLogSalt (Id);
                } else {
                    // It can't be in the Keychain if McAccount not in DB yet.
                    GenerateLogSalt ();
                }
            }
            return LogSalt;
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
            Keychain.Instance.SetLogSalt (Id, LogSalt);
            LogSalt = null;
            Update ();
        }

        public override int Insert ()
        {
            int retVal = base.Insert ();
            // need to do this after the insert since I need the account id for the keychain insert
            GenerateAndUpdateLogSalt ();
            return retVal;
        }

        public override int Delete ()
        {
            Keychain.Instance.DeleteLogSalt (Id);
            LogSalt = null;
            Update ();
            return base.Delete ();
        }

        public void LogHashedPassword (ulong service, string logComment, string password)
        {
            string salt = null;
            try {
                salt = GetLogSalt ();
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_SYS, "LoggablePasswordSaltedHash: Could not retrieve LogSalt for account {0}: KeychainItemNotFoundException {1}", Id, ex.Message);
                return;
            }
            if (string.IsNullOrEmpty (salt)) {
                Log.Error (Log.LOG_SYS, "LoggablePasswordSaltedHash: Could not retrieve LogSalt for account {0}", Id);
                return;
            }
            string hash = HashHelper.Sha256 (salt + password);
            var hashed = hash.Substring (hash.Length - 3); // e.g. "f47"
            if (string.IsNullOrEmpty (hashed)) {
                Log.Error (Log.LOG_SYS, "LoggablePasswordSaltedHash: Could not hash password for account {0}", Id);
                return;
            }
            Log.Info (service, "LoggablePasswordSaltedHash({0}): {1} passwordHash={2}", Id, logComment, hashed);
        }

        public void LogHashedPassword (ulong service, string logComment, McCred cred)
        {
            string credstring = null;
            try {
                switch (cred.CredType) {
                case McCred.CredTypeEnum.Password:
                    credstring = cred.GetPassword ();
                    break;

                case McCred.CredTypeEnum.OAuth2:
                    credstring = cred.GetAccessToken ();
                    break;

                default:
                    Log.Warn (Log.LOG_PUSH, "LogHashedPassword: Unhandled credential type {0}", cred.CredType);
                    break;
                }
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_UI, "LogHashedPassword({0}: {1}", logComment, ex.Message);
            }
            if (null != credstring) {
                try {
                    LogHashedPassword (service, logComment, credstring);
                } catch (KeychainItemNotFoundException ex) {
                    Log.Error (Log.LOG_UI, "LogHashedPassword({0}: {1}", logComment, ex.Message);
                }
            }
        }

        public async void PopulateProfilePhotoFromURL (Uri imageUrl)
        {
            try {
                var httpClient = new System.Net.Http.HttpClient ();
                byte[] contents = await httpClient.GetByteArrayAsync (imageUrl);
                var portrait = McPortrait.InsertFile (Id, contents);
                DisplayPortraitId = portrait.Id;
                Update ();
            } catch (Exception e) {
                Log.Info (Log.LOG_DB, "McAccount: PopulateProfilePhotoFromURL exception: {0}", e);
            }
        }

        public bool ContainsAccount (int accountId)
        {
            if (AccountType == AccountTypeEnum.Unified) {
                return true;
            }
            return Id == accountId;
        }

        public static byte[,] AccountColors = new byte[,] {
            { 0xE0, 0xE0, 0xE0 },
            { 0x01, 0x6B, 0x5E },
            { 0xFA, 0xBF, 0x20 },
            { 0xD2, 0x47, 0x47 },
            { 0xBE, 0xCA, 0x39 },
            { 0x4F, 0x64, 0x6D },
            { 0xF3, 0x68, 0x00 },
            { 0x2B, 0xD9, 0xB2 },
            { 0x3C, 0xB9, 0x6A }
        };

        public void AssignOpenColorIndex ()
        {
            if (AccountType == AccountTypeEnum.Device || AccountType == AccountTypeEnum.Unified || AccountType == AccountTypeEnum.SalesForce) {
                ColorIndex = 0;
            } else {
                var indexCounts = new Dictionary<int, int> ();
                var colorCount = AccountColors.Length / 3;
                for (int i = 0; i < colorCount; ++i) {
                    indexCounts [i] = 0;
                }
                foreach (var account in GetAllAccounts()) {
                    indexCounts [account.ColorIndex] += 1;
                }
                int targetCount = 0;
                bool didAssignColor = false;
                while (!didAssignColor) {
                    for (int i = 1; i < colorCount; ++i) {
                        if (indexCounts [i] == targetCount) {
                            ColorIndex = i;
                            didAssignColor = true;
                            break;
                        }
                    }
                    ++targetCount;
                }
            }
        }

        public enum AuthType
        {
            UserPass,
            GoogleOAuth
        }

        public McCred GetCred ()
        {
            return McCred.QueryByAccountId<McCred> (Id).SingleOrDefault ();
        }

        public AuthType GetAuthType() 
        {
            if (AccountService == McAccount.AccountServiceEnum.GoogleDefault) {
                var cred = GetCred ();
                if (cred != null) {
                    if (cred.CredType == McCred.CredTypeEnum.OAuth2) {
                        return AuthType.GoogleOAuth;
                    } else {
                        return AuthType.UserPass;
                    }
                } else {
                    return AuthType.UserPass;
                }
            } else {
                return AuthType.UserPass;
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

