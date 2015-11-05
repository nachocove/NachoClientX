//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using DnDns.Query;
using DnDns.Enums;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Net;

namespace NachoPlatform
{
    public class PlatformDns : IPlatformDns
    {
        public DnsQueryResponse ResQuery (IDnsLockObject op, string host, NsClass dnsClass, NsType dnsType)
        {
            List<NsType> validTypes = new List<NsType> ();
            validTypes.Add (NsType.MX);
            validTypes.Add (NsType.SRV);
            NcAssert.True (!string.IsNullOrEmpty (host));
            NcAssert.True (dnsClass == NsClass.INET);
            NcAssert.True (validTypes.Contains (dnsType));

            IPAddress[] ipas = Dns.GetHostAddresses (DnDns.Tools.DiscoverUnixDnsServerAddress());
            foreach (var addr in ipas) {
                Log.Info (Log.LOG_DNS, "ResQuery DNS server addr: Family={0} addr_len={1} addr={2}", addr.AddressFamily, addr.GetAddressBytes ().Length, addr.ToString ());
            }

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

