﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public enum PickActionEnum { Sync, Ping, QOop, Fetch, Wait };

    public class PingKit
    {
        public List<McFolder> Folders { get; set; }
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
        public int OverallWindowSize { get; set; }
        public List<PerFolder> PerFolders { get; set; }
    }

    public class FetchKit
    {
        public class FetchBody
        {
            public string ParentId { get; set; }
            public string ServerId { get; set; }
        }
        public List<FetchBody> FetchBodies { get; set; }
        public List<McAttachment> FetchAttachments { get; set; }
        public List<McPending> Pendings { get; set; }
    }

    public interface IAsStrategy
    {
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, bool cantBeEmpty);
        Tuple<PickActionEnum, AsCommand> Pick ();
    }
}
