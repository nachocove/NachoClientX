using System;
using SQLite;
using NachoCore.Utils;
using MailKit.Net.Imap;

namespace NachoCore.Model
{
    // We choose to have a single table rather than a table per-protocol. So we use a prefix to 
    // differentiate between variables that belong to each protocol. We assume that there will be 
    // only one ProtoControl active for a given protocol per account.
    // "As" is ActiveSync.
    // "Imap" is IMAP.
    // "Smtp" is SMTP.
    public class McProtocolState : McAbstrObjectPerAcc
    {
        // Supported protocols. Bitfield for McAccount's benefit.
        [Flags]
        public enum ProtocolEnum {
            ActiveSync = (1 << 0),
            IMAP = (1 << 1),
            SMTP = (1 << 2),
            SalesForce = (1 << 3),
        };
        // The protocol for this instance. Only one!
        public ProtocolEnum Protocol { get; set; }

        public const string AsSyncKey_Initial = "0";
        public const string AsPolicyKey_Initial = "0";
        public const uint AsSyncLimit_Default = 10;

        public enum AsThrottleReasons {
            Unknown,
            CommandFrequency,
            RecentCommands,
        };

        [Flags]
        public enum NcImapCapabilities {
            /// <summary>
            /// The server does not support any additional extensions.
            /// </summary>
            None             = 0,
            /// <summary>
            /// The server supports the IDLE extension defined in rfc2177.
            /// </summary>
            Idle             = 1 << 0,
            /// <summary>
            /// The server supports the UIDPLUS extension defined in rfc4315.
            /// </summary>
            UidPlus          = 1 << 1,
            /// <summary>
            /// The server supports the CONDSTORE extension defined in rfc4551.
            /// </summary>
            CondStore        = 1 << 2,
            /// <summary>
            /// The server supports the <a href="https://tools.ietf.org/html/rfc4959">SASL-IR</a> extension.
            /// </summary>
            SaslIR           = 1 << 3,
            /// <summary>
            /// The server supports the <a href="https://developers.google.com/gmail/imap_extensions">X-GM-EXT1</a> extension (GMail).
            /// </summary>
            GMailExt1        = 1 << 4,

        }

        public McProtocolState ()
        {
            /*
             * common ctor inits here:
             */

            /*
             * "As" ActiveSync ctor inits here:
             */
            AsProtocolVersion = "12.0";
            AsPolicyKey = AsPolicyKey_Initial;
            AsSyncKey = AsSyncKey_Initial;
            AsSyncLimit = AsSyncLimit_Default;
            AsFolderSyncEpoch = 1; // So that just-created McFolders aren't presumed from current epoch.
            HeartbeatInterval = 600;
            MaxFolders = 200;
            ProtoControlState = (uint)St.Start;
            /*
             * "Imap" IMAP ctor inits here:
             */
            ImapServerCapabilities = ImapServerCapabilities;
            /*
             * "Smtp" SMTP ctor inits here:
             */
        }

        /*
         * common properties go here:
         */

        /*
         * "As" ActiveSync properties go here:
         */
        public string AsProtocolVersion { get; set; }

        public string AsPolicyKey { get; set; }

        public string AsSyncKey { get; set; }

        public uint AsSyncLimit { get; set; }

        public uint AsFolderSyncEpoch { get; set; }

        public bool AsFolderSyncEpochScrubNeeded { get; set; }

        public DateTime AsLastFolderSync { get; set; }

        public AsThrottleReasons AsThrottleReason { get; set; }

        public uint HeartbeatInterval { get; set; }

        public uint MaxFolders { get; set; }

        public uint ProtoControlState { get; set; }

        public int StrategyRung { get; set; }

        public DateTime LastNarrowSync { get; set; }

        public DateTime LastPing { get; set; }

        public bool IsWipeRequired { get; set; }

        public bool LastAutoDSucceeded { get; set; }

        public int Consec401Count { get; set; }

        public bool DisableProvisionCommand { get; set; }

        public bool HasBeenRateLimited { get; set; }

        /*
         * General properties
         */
        public bool HasSyncedInbox { get; set; }

        /*
         * "Imap" IMAP properties go here:
         */
        public uint ImapProtoControlState { get; set; }

        public NcImapCapabilities ImapServerCapabilities { get; set; }
        // servers can send different capabilities, depending on whether we're auth'd or not.
        // We need to know both, because in some cases the auth'd capabilities no longer include
        // the authentication capabilities, which we need to know.
        public NcImapCapabilities ImapServerCapabilitiesUnAuth { get; set; }

        // Same principle as McAccount.AccountService which is set by which account-type the
        // user picks. This one is auto-discovered, so doesn't rely on the user's input.
        // Reason: We can't rely on McAccount.AccountService for anything functional, since
        // a user could just as easily configure a GMail account using the generic 'IMAP' button,
        // which would break some internal functionality that relies on knowing whether a server
        // is gmail, or yahoo, or aol, etc.
        public McAccount.AccountServiceEnum ImapServiceType { get; set; }

        // The current sync type
        public uint ImapSyncRung { get; set; }

        public bool ImapDiscoveryDone { get; set; }

        /*
         * "Smtp" SMTP properties go here:
         */
        public uint SmtpProtoControlState { get; set; }

        public bool SmtpDiscoveryDone { get; set; }

        /*
         * SalesForce Properties go here
         */
        public DateTime SFDCLastContactsSynced { get; set; }

        /*
         * common methods go here:
         */

        public override int Update ()
        {
            NcAssert.True (false, "Must use UpdateWithOCApply.");
            return 0;
        }

        /*
         * "As" ActiveSync methods go here:
         */
        public void IncrementAsFolderSyncEpoch ()
        {
            ++AsFolderSyncEpoch;
            AsSyncKey = AsSyncKey_Initial;
            AsFolderSyncEpochScrubNeeded = true;
        }

        public void SetAsThrottleReason (string fromServer)
        {
            fromServer = fromServer.Trim ();
            if ("CommandFrequency" == fromServer) {
                AsThrottleReason = AsThrottleReasons.CommandFrequency;
            } else if ("RecentCommands" == fromServer) {
                AsThrottleReason = AsThrottleReasons.RecentCommands;
            } else {
                Log.Error (Log.LOG_AS, "Unknown X-MS-ASThrottle value: {0}", fromServer);
                AsThrottleReason = AsThrottleReasons.Unknown;
                return;
            }
            Log.Info (Log.LOG_AS, "X-MS-ASThrottle value: {0}", AsThrottleReason);
        }

        /*
         * "Imap" IMAP methods go here:
         */
        public static NcImapCapabilities FromImapCapabilities(ImapCapabilities capabilities)
        {
            NcImapCapabilities cap = NcImapCapabilities.None;
            if (capabilities.HasFlag (ImapCapabilities.Idle)) {
                cap |= NcImapCapabilities.Idle;
            }
                if (capabilities.HasFlag (ImapCapabilities.UidPlus)) {
                cap |= NcImapCapabilities.UidPlus;
            }
            if (capabilities.HasFlag (ImapCapabilities.CondStore)) {
                cap |= NcImapCapabilities.CondStore;
            }
            if (capabilities.HasFlag (ImapCapabilities.SaslIR)) {
                cap |= NcImapCapabilities.SaslIR;
            }
            if (capabilities.HasFlag (ImapCapabilities.GMailExt1)) {
                cap |= NcImapCapabilities.GMailExt1;
            }

            return cap;
        }

        /*
         * "Smtp" SMTP methods go here:
         */
    }
}

