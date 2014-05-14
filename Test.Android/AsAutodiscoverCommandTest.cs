using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using DnDns.Enums;
using DnDns.Query;


namespace Test.iOS
{
    public class MockDnsQueryRequest : IDnsQueryRequest
    {
        public UdpClient UdpClient { set; get; }

        public Task<DnsQueryResponse> ResolveAsync (string host, NsType dnsType, NsClass dnsClass, ProtocolType pType)
        {
            Host = host;
            DnsType = dnsType;
            DnsClass = dnsClass;
            PType = pType;

            return Task.Run<DnsQueryResponse> (delegate {
                return new DnsQueryResponse ();
            });
        }

        public string Host { get; set; }
        public NsType DnsType { get; set; }
        public NsClass DnsClass { get; set; }
        public ProtocolType PType { get; set; }
    }

    [TestFixture]
    public class AsAutodiscoverCommandTest
    {
        [Test]
        public void Pass ()
        {

        }
    }
}
