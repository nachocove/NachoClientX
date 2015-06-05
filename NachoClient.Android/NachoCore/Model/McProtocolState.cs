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

        public bool HasSyncedInbox { get; set; }

        public bool DisableProvisionCommand { get; set; }

        public bool HasBeenRateLimited { get; set; }
        /*
         * "Imap" IMAP properties go here:
         */
        public uint ImapProtoControlState { get; set; }

        public ImapCapabilities ImapCapabilities { get; set; }

        /*
         * "Smtp" SMTP properties go here:
         */
        public uint SmtpProtoControlState { get; set; }

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

        /*
         * "Smtp" SMTP methods go here:
         */
    }
}

