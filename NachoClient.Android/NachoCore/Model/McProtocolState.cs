using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    // NOTE: eventually this will be a base class, with an active-sync sub-class.
    public class McProtocolState : McObject
    {
        public McProtocolState ()
        {
            AsProtocolVersion = "12.0";
            AsPolicyKey = "0";
            AsSyncKey = "0";
            HeartbeatInterval = 24;
            MaxFolders = 200;
            State = (uint)St.Start;
        }

        public string AsProtocolVersion { get; set; }

        public string AsPolicyKey { get; set; }

        public string AsSyncKey { get; set; }

        public uint HeartbeatInterval { get; set; }

        public uint MaxFolders { get; set; }

        public uint State { get; set; }

        public bool InitialProvisionCompleted { get; set; }
    }
}

