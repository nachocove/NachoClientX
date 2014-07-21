using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // NOTE: eventually this will be a base class, with an active-sync sub-class.
    public class McProtocolState : McAbstrObject
    {
        public const string AsSyncKey_Initial = "0";
        public const string AsPolicyKey_Initial = "0";

        public enum AsThrottleReasons {
            Unknown,
            CommandFrequency,
            RecentCommands,
        };

        public McProtocolState ()
        {
            AsProtocolVersion = "12.0";
            AsPolicyKey = AsPolicyKey_Initial;
            AsSyncKey = AsSyncKey_Initial;
            AsSyncLimit = uint.MaxValue;
            AsFolderSyncEpoch = 1; // So that just-created McFolders aren't presumed from current epoch.
            HeartbeatInterval = 600;
            MaxFolders = 200;
            ProtoControlState = (uint)St.Start;
            SyncStratEmailCalendarState = (uint)St.Start;
            SyncStratContactsState = (uint)St.Start;
        }

        public string AsProtocolVersion { get; set; }

        public string AsPolicyKey { get; set; }

        public string AsSyncKey { get; set; }

        public uint AsSyncLimit { get; set; }

        public uint AsFolderSyncEpoch { get; set; }

        public bool AsFolderSyncEpochScrubNeeded { get; set; }

        public AsThrottleReasons AsThrottleReason { get; set; }

        public uint HeartbeatInterval { get; set; }

        public uint MaxFolders { get; set; }

        public uint ProtoControlState { get; set; }

        public uint SyncStratEmailCalendarState { get; set; }

        public uint SyncStratContactsState { get; set; }

        public bool IsWipeRequired { get; set; }

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
            }
            if ("RecentCommands" == fromServer) {
                AsThrottleReason = AsThrottleReasons.RecentCommands;
            }
            Log.Error (Log.LOG_AS, "Unknown X-MS-ASThrottle value: {0}", fromServer);
            AsThrottleReason = AsThrottleReasons.Unknown;
        }
    }
}

