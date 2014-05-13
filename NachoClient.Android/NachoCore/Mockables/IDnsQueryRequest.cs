//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnDns.Query;
using DnDns.Enums;

namespace NachoCore.Utils
{
    public interface IDnsQueryRequest
    {
        UdpClient UdpClient { get; }
        Task<DnsQueryResponse> ResolveAsync (string host, NsType dnsType, NsClass dnsClass, ProtocolType pType);
    }
}

