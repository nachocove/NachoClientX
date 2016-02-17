//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class PingKit
    {
        public uint MaxHeartbeatInterval { get; set; }
        public List<McFolder> Folders { get; set; }
        public bool IsNarrow { get; set; }
    }

    public class SyncKit
    {
        public class PerFolder
        {
            public McFolder Folder { get; set; }
            public List<McPending> Commands { get; set; }
            public int WindowSize { get; set; }
            public Xml.Provision.MaxAgeFilterCode FilterCode { get; set; }
            public bool GetChanges { get; set; }
        }
        public TimeSpan WaitInterval { get; set; }
        public bool IsNarrow { get; set; }
        public bool IsPinging { get; set; }
        public int OverallWindowSize { get; set; }
        public List<PerFolder> PerFolders { get; set; }
    }

    public class FetchKit
    {
        public class FetchBody
        {
            public McPending Pending { get; set; }
            public Xml.AirSync.TypeCode BodyPref { get; set; }
        }

        public class FetchAttachment
        {
            public McPending Pending { get; set; }
            public McAttachment Attachment { get; set; }
        }

        public List<FetchAttachment> FetchAttachments { get; protected set; }
        public List<FetchBody> FetchBodies { get; protected set; }

        public FetchKit (List<FetchBody> fetchBodies, List<FetchAttachment> fetchAttachments = null)
        {
            FetchBodies = fetchBodies;
            FetchAttachments = fetchAttachments ?? new List<FetchAttachment> ();
        }
    }

    public class MoveKit
    {
        public List<McAbstrFolderEntry.ClassCodeEnum> ClassCodes { get; set; }
        public List<McPending> Pendings { get; set; }
    }

    // This interface is here for mocking, unlikely to be useful beyond that.
    public interface IAsStrategy
    {
        MoveKit GenMoveKit ();
        FetchKit GenFetchKit ();
        FetchKit GenFetchKitHints ();
        SyncKit GenSyncKit (McProtocolState protocolState);
        PingKit GenPingKit (McProtocolState protocolState, bool isNarrow, bool stillHaveUnsyncedFolders, bool ignoreToClientExpected);
        SyncKit GenSyncKitFromPingKit (McProtocolState protocolState, PingKit pingKit);
        Tuple<PickActionEnum, AsCommand> Pick ();
        Tuple<PickActionEnum, AsCommand> PickUserDemand ();
        int UploadTimeoutSecs (long length);
        int DownloadTimeoutSecs (long length);
        int DefaultTimeoutSecs { get; }
    }
}
