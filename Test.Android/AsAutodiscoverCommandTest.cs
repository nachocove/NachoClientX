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
            bool setTrueBySuccess = false;
            var sm = CreatePhonySM (val => {
                setTrueBySuccess = val;
            });

            var mockContext = new MockContext ();
            var autod = new AsAutodiscoverCommand (mockContext);
            autod.DnsQueryRequestType = typeof(MockDnsQueryRequest);
            autod.HttpClientType = typeof(MockHttpClient);

            autod.Execute (sm);
        }

        private NcStateMachine CreatePhonySM (Action<bool> action)
        {
            bool setTrueBySuccessEvent = false;
            var sm = new NcStateMachine ("PHONY") {
                Name = "BasicPhonyPing",
                LocalEventType = typeof(AsProtoControl.CtlEvt),
                LocalStateType = typeof(AsProtoControl.Lst),
                TransTable = new [] {
                    new Node {State = (uint)St.Start,
                        On = new [] {
                            new Trans { 
                                Event = (uint)SmEvt.E.Launch, 
                                Act = delegate () {},
                                State = (uint)St.Start },
                        }
                    },
                }
            };

            return sm;
        }
    }
}
