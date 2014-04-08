using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // NOTE: eventually this will be a base class, with an active-sync sub-class.
    public class McProtocolState : McObject
    {
        public const string AsSyncKey_Initial = "0";
        public const string AsPolicyKey_Initial = "0";

        public McProtocolState ()
        {
            AsProtocolVersion = "12.0";
            AsPolicyKey = AsPolicyKey_Initial;
            AsSyncKey = AsSyncKey_Initial;
            AsSyncLimit = uint.MaxValue;
            AsFolderSyncEpoch = 1; // So that just-created McFolders aren't presumed from current epoch.
            HeartbeatInterval = 600;
            MaxFolders = 200;
            KludgeSimulatorIdentity = Guid.NewGuid ().ToString ("N").Substring (0, 20);
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

        public uint HeartbeatInterval { get; set; }

        public uint MaxFolders { get; set; }

        public uint ProtoControlState { get; set; }

        public uint SyncStratEmailCalendarState { get; set; }

        public uint SyncStratContactsState { get; set; }

        public bool InitialProvisionCompleted { get; set; }

        public string KludgeSimulatorIdentity { get; set; }

        public void IncrementAsFolderSyncEpoch ()
        {
            ++AsFolderSyncEpoch;
            AsSyncKey = AsSyncKey_Initial;
            AsFolderSyncEpochScrubNeeded = true;
        }
    }
}

