using System;
using System.Collections.Concurrent;
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
using System.Xml.Linq;


namespace Test.iOS
{
    public enum Lst : uint
    {
        RobotW = (St.Last + 1),
        AskW,
        CredW1,
        CredW2,
        SrvConfW,
        TestW,
    };

    public class MockDnsQueryRequest : IDnsQueryRequest
    {
        public delegate DnsQueryResponse ProvideDnsQueryResponseMessageDelegate ();
        public static ProvideDnsQueryResponseMessageDelegate ProvideDnsQueryResponseMessage { set; get; }

        public UdpClient UdpClient { set; get; }

        public Task<DnsQueryResponse> ResolveAsync (string host, NsType dnsType, NsClass dnsClass, ProtocolType pType)
        {
            return Task.Run<DnsQueryResponse> (delegate {
                return ProvideDnsQueryResponseMessage ();
            });
        }
    }

    [TestFixture]
    public class AsAutodiscoverCommandTest
    {
        [Test]
        public void ServerNotConfigured ()
        {
            PerformAutoDiscoveryWithSettings (mockContext => {
                mockContext.Server = McServer.Create (CommonMockData.MockUri);
                mockContext.Server.UsedBefore = false;
            });
        }

        [Test]
        public void ServerConfigured ()
        {
            PerformAutoDiscoveryWithSettings (mockContext => {
                mockContext.Server = null;
            });
        }

        public void PerformAutoDiscoveryWithSettings (Action<MockContext> provideContext)
        {
            var interlock = new BlockingCollection<bool> ();

            bool setTrueBySuccessEvent = false;
            NcStateMachine sm = CreatePhonySM (val => {
                setTrueBySuccessEvent = val;
                interlock.Add(true);
            });

            MockDnsQueryRequest.ProvideDnsQueryResponseMessage = () => {
                var mockDnsQueryResponse = new DnsQueryResponse () {
                    // from StepRobot L. 848 --> Do I need to be able to pass in a mock owner?
                    RCode = RCode.NoError,
                    AnswerRRs = 5,
                    NsType = NsType.SRV
                };

                return mockDnsQueryResponse;
            };

            var mockContext = new MockContext ();
            provideContext (mockContext);

            var autod = new AsAutodiscoverCommand (mockContext);
            autod.DnsQueryRequestType = typeof(MockDnsQueryRequest);
            autod.HttpClientType = typeof(MockHttpClient);

            autod.Execute (sm);

            bool didFinish = false;
            if (!interlock.TryTake (out didFinish, 6000)) {
                Assert.Inconclusive ("Failed in TryTake clause");
            }
            Assert.IsTrue (didFinish);
            Assert.IsTrue (setTrueBySuccessEvent);
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
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                }, 
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)Lst.TestW,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },

                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                        }
                    }
                }
            };

            return sm;
        }
    }
}
