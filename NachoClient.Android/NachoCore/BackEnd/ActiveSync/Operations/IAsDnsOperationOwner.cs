// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using DnDns.Enums;
using DnDns.Query;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsDnsOperationOwner
    {
        void CancelCleanup (AsDnsOperation Sender);
        string DnsHost (AsDnsOperation Sender);
        NsType DnsType (AsDnsOperation Sender);
        NsClass DnsClass (AsDnsOperation Sender);
        Event ProcessResponse (AsDnsOperation Sender, DnsQueryResponse response);
    }
}
