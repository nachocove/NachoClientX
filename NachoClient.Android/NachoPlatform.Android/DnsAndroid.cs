//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using DnDns.Query;
using DnDns.Enums;

namespace NachoPlatform
{
    public class PlatformDns : IPlatformDns
    {
        public DnsQueryResponse ResQuery (IDnsLockObject op, string host, NsClass dnsClass, NsType dnsType)
        {
            var DnsQuery = new DnsQueryRequest ();
            DnsQueryResponse response = DnsQuery.Resolve (host, dnsType, dnsClass, System.Net.Sockets.ProtocolType.Udp);
            lock (op.lockObject) {
                if (op.complete) {
                    // The operation timed out or was canceled
                    return null;
                }

                if (null == response || response.Answers.Length == 0) {
                    return null;
                }
                return response;
            }
        }
    }
}

