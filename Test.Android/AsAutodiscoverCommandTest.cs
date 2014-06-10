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
using System.Net.Http;
using System.Text;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;


/*
 * uses dns response from _autodiscover._tcp.utopiasystems.net.
 * You can get another copy of the response by doing the following:
 *     DnsQueryRequest req = new DnsQueryRequest ();
 *     DnsQueryResponse resp = req.Resolve ("_autodiscover._tcp.utopiasystems.net", NsType.SRV, NsClass.INET, ProtocolType.Udp);
 */

/* Important: Auto-d uses xml, not wbxml */
/* Details about HTTP OPTIONS responses: http://msdn.microsoft.com/en-us/library/jj127441(v=exchg.140).aspx */
using System.Net;

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
        

    public class BaseAutoDiscoverTests : AsAutodiscoverCommandTest
    {
        // Test that each of the 8 Sx complete successfully
        // Ensure discovered server name is tested by auto-d
        [TestFixture]
        public class BasicSuccessfulResponses : AsAutodiscoverCommandTest
        {
            [Test]
            public void TestS1 ()
            {
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1);
            }

            [Test]
            public void TestS2 ()
            {
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S2);
            }

            // Ensure that the Owner is called-back when a valid cert is encountered in
            // the HTTPS access following a DNS SRV lookup
            [Test]
            public void TestS3 ()
            {
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S3);
            }

            // Ensure that the Owner is called-back when a valid cert is encountered in
            // the HTTPS access following a DNS SRV lookup
            [Test]
            public void TestS4 ()
            {
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S4);
            }

            private void TestAutodPingWithXmlResponse (string xml, MockSteps step)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                bool hasRedirected = false;

                PerformAutoDiscoveryWithSettings (true, sm => {}, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                        step = MockSteps.S1; // S4 resolves to POST after DNS lookup
                    }
                }, (httpRequest, httpResponse) => {
                    // provide valid redirection headers if needed
                    if (ShouldRedirect (httpRequest, step) && !hasRedirected) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                        hasRedirected = true; // disable second redirection
                        step = MockSteps.S1;
                    } else {
                        MockSteps robotType = DetermineRobotType (httpRequest);
                        httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
            }
        }

        [TestFixture]
        public class TestStep5Responses : AsAutodiscoverCommandTest
        {
            [Test]
            public void TestValidRedirectThenSuccess ()
            {
                string redirUrl = CommonMockData.RedirectionUrl;
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (true, xml, redirUrl, MockSteps.S3, sm => {});  
            }

            [Test]
            public void TestValidRedirectThenFailure ()
            {
                string redirUrl = CommonMockData.RedirectionUrl;
                string xml = CommonMockData.AutodPhonyErrorResponse;
                TestAutodPingWithXmlResponse (false, xml, redirUrl, MockSteps.S3, sm => {
                    sm.PostEvent ((uint)SmEvt.E.Launch, "Step5Tests");
                });
            }

            [Test]
            public void TestInvalidRedirect ()
            {
                string redirUrl = CommonMockData.InvalidRedirUrl;
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (false, xml, redirUrl, MockSteps.S3, sm => {
                    sm.PostEvent ((uint)SmEvt.E.Launch, "Step5Tests");
                });
            }

            private void TestAutodPingWithXmlResponse (bool hasCert, string xml, string redirUrl, MockSteps step, Action<NcStateMachine> provideSm)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                PerformAutoDiscoveryWithSettings (hasCert, sm => {
                    provideSm(sm);
                }, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    // provide valid redirection headers if needed
                    if (ShouldRedirect (httpRequest, step)) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", redirUrl);
                    } else {
                        MockSteps robotType = DetermineRobotType (httpRequest);
                        httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                }, testEndingDbState: false);
            }
        }

        [TestFixture]
        public class Test600XmlErrorCodes : AsAutodiscoverCommandTest
        {
            // 600 = Invalid Request
            [Test]
            public void Test600ErrorCode ()
            {
                var errorKind600 = NcResult.SubKindEnum.Error_AutoDError600;
                string xml = CommonMockData.AutodPhony600Response;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1, errorKind600);
            }

            // 601 = Requested schema version not supported
            [Test]
            public void Test601ErrorCode ()
            {
                var errorKind601 = NcResult.SubKindEnum.Error_AutoDError601;
                string xml = CommonMockData.AutodPhony601Response;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1, errorKind601);
            }

            private void TestAutodPingWithXmlResponse (string xml, MockSteps step, NcResult.SubKindEnum errorKind)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                PerformAutoDiscoveryWithSettings (true, sm => {
                    sm.PostEvent ((uint)SmEvt.E.Launch, "TEST-FAIL");
                }, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    MockSteps robotType = DetermineRobotType (httpRequest);
                    httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                    httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                }, resultKind: errorKind, testEndingDbState:false);
            }
        }

        [TestFixture]
        public class AutodTestFailure : AsAutodiscoverCommandTest
        {
            // Ensure that a server name test failure results in re-tries.
            [Test]
            public void TestFailureHasRetries ()
            {
                int expectedRetries = 15;

                string successXml = CommonMockData.AutodOffice365ResponseXml;
                string failureXml = CommonMockData.AutodPhonyErrorResponse;

                // bad gateway forces retries
                HttpStatusCode status = HttpStatusCode.BadGateway;

                // pass POST request, but fail OPTIONS
                TestAutodPingWithXmlResponse (successXml, failureXml, status, MockSteps.S1);
                Assert.AreEqual (expectedRetries, MockHttpClient.AsyncCalledCount, "Should match the expected number of Async calls");
            }

            // Ensure that a server name test failure results in the Owner being 
            // asked to supply a new server name.
            [Test]
            public void AsksOwnerForServerNameOnFailure ()
            {
                string successXml = CommonMockData.AutodOffice365ResponseXml;
                string failureXml = CommonMockData.AutodPhonyErrorResponse;

                // not found forces hard fail
                HttpStatusCode status = HttpStatusCode.NotFound;

                // pass POST request, but fail OPTIONS
                TestAutodPingWithXmlResponse (successXml, failureXml, status, MockSteps.S1);
            }

            private void TestAutodPingWithXmlResponse (string xml, string optionsXml, HttpStatusCode status, MockSteps step)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                PerformAutoDiscoveryWithSettings (true, sm => {
                    sm.PostEvent ((uint)SmEvt.E.Launch, "TEST-FAIL");
                }, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml, optionsXml: optionsXml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                    }
                }, (httpRequest, httpResponse) => {
                    // provide valid redirection headers if needed
                    if (ShouldRedirect (httpRequest, step)) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else if (IsOptionsRequest (httpRequest)) {
                        // check for OPTIONS header and set status code to 404 to force hard fail
                        httpResponse.StatusCode = status;
                    } else {
                        MockSteps robotType = DetermineRobotType (httpRequest);
                        httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                }, testEndingDbState: false);
            }
        }

        [TestFixture]
        public class AuthFailure : AsAutodiscoverCommandTest
        {
            // Ensure that an authentication failure during S1, S2, or S3 results in 
            // the Owner being asked to supply new credentials.
            [Test]
            public void NewCredsUponAuthFailureS1 ()
            {
                string successXml = CommonMockData.AutodOffice365ResponseXml;
                string failureXml = CommonMockData.AutodPhonyErrorResponse;

                TestAutodPingWithXmlResponse (successXml, failureXml, MockSteps.S1);
            }

            [Test]
            public void NewCredsUponAuthFailureS3 ()
            {
                string successXml = CommonMockData.AutodOffice365ResponseXml;

                TestAutodPingWithXmlResponse (successXml, successXml, MockSteps.S3);
            }

            private void TestAutodPingWithXmlResponse (string xml, string optionsXml, MockSteps step)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                // gets set to true when creds have been provided so that autod can proceed
                bool hasProvidedCreds = false;

                PerformAutoDiscoveryWithSettings (true, sm => {
                    sm.PostEvent ((uint)SmEvt.E.Launch, "TEST_FAIL");
                }, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml, optionsXml: optionsXml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    // provide valid redirection headers if needed
                    if (ShouldRedirect (httpRequest, step)) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else if (IsOptionsRequest (httpRequest) && !hasProvidedCreds) {
                        // if OPTIONS, set status code to Unauthorized to force auth failure
                        httpResponse.StatusCode = HttpStatusCode.Unauthorized;
                        hasProvidedCreds = true;
                    } else {
                        MockSteps robotType = DetermineRobotType (httpRequest);
                        httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
            }
        }

        [TestFixture]
        public class SingleTimeoutSuccess : AsAutodiscoverCommandTest
        {
            /* The timeout flag is ASHTTPTTC */

            private void SetTimeoutConstants ()
            {
                McMutables.Set ("HTTPOP", "TimeoutSeconds", "2");
            }

            [Test]
            public void TestS1 ()
            {
                SetTimeoutConstants ();
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1);
            }

            [Test]
            public void TestS2 ()
            {
                SetTimeoutConstants ();
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S2);
            }

            // Ensure that the Owner is called-back when a valid cert is encountered in
            // the HTTPS access following a DNS SRV lookup
            [Test]
            public void TestS3 ()
            {
                SetTimeoutConstants ();
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S3);
            }

            // Ensure that the Owner is called-back when a valid cert is encountered in
            // the HTTPS access following a DNS SRV lookup
            [Test]
            public void TestS4 ()
            {
                SetTimeoutConstants ();
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S4);
            }

            private void TestAutodPingWithXmlResponse (string xml, MockSteps step)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                bool hasRedirected = false;
                bool hasTimedOutOnce = false;

                PerformAutoDiscoveryWithSettings (true, sm => {}, request => {
                    MockSteps robotType = DetermineRobotType (request);
                    return XMLForRobotType (request, robotType, step, xml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                        step = MockSteps.S1; // S4 resolves to POST after DNS lookup
                    }
                }, (httpRequest, httpResponse) => {
                    MockSteps robotType = DetermineRobotType (httpRequest);
                    if (!hasTimedOutOnce && robotType == step) {
                        System.Threading.Thread.Sleep (2000);
                        hasTimedOutOnce = true;
                    }
                    // provide valid redirection headers if needed
                    if (ShouldRedirect (httpRequest, step) && !hasRedirected) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                        hasRedirected = true; // disable second redirection
                        step = MockSteps.S1;
                    } else {
                        httpResponse.StatusCode = AssignStatusCode (httpRequest, robotType, step);
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
            }
        }
    }

    public class AsAutodiscoverCommandTest
    {
        private static AsAutodiscoverCommand autodCommand { get; set; }
        private static MockContext mockContext { get; set; }

        [SetUp]
        public void Setup ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());

            MockDnsQueryRequest.ProvideDnsQueryResponseMessage = null;

            MockHttpClient.AsyncCalledCount = 0; // reset counter
            MockHttpClient.ExamineHttpRequestMessage = null;
            MockHttpClient.ProvideHttpResponseMessage = null;
            MockHttpClient.HasServerCertificate = null;
            MockNcCommStatus.Instance = null;

            MockOwner.Status = null;

            autodCommand = null;
            mockContext = null;

            // insert phony server to db (this allows Auto-d 'DoAcceptServerConf' to update the record later)
            var phonyServer = new McServer ();
            phonyServer.Host = "/Phony-Server";
            phonyServer.Path = "/phonypath";
            phonyServer.Port = 500;
            phonyServer.Scheme = "/phonyscheme";
            phonyServer.UsedBefore = true;

            NcModel.Instance.Db.Insert (phonyServer);

            mockContext = new MockContext ();
            // make a server for this context
            mockContext.Server = new McServer ();
            mockContext.Server.Host = "";
            mockContext.Server.UsedBefore = false;
            mockContext.Server.Id = 1;

            // flush the certificate cache so it doesn't interfere with future tests
            var instance = ServerCertificatePeek.Instance; // do this in case instance has not yet been created
            ServerCertificatePeek.TestOnlyFlushCache ();
        }

        public HttpStatusCode AssignStatusCode (HttpRequestMessage request, MockSteps robotType, MockSteps step)
        {
            if (HasBeenRedirected (request)) {
                return HttpStatusCode.OK;
            }

            if (IsOptionsRequest (request)) {
                DoOptionsAsserts (request);
                return HttpStatusCode.OK;
            }

            if (robotType != step) {
                return HttpStatusCode.NotFound;
            }

            switch (robotType) {
            case MockSteps.S1:
            case MockSteps.S2:
                return HttpStatusCode.OK;
            default:
                return HttpStatusCode.NotFound;
            }
        }

        public string XMLForRobotType (HttpRequestMessage request, MockSteps robotType, MockSteps step, string xml, string optionsXml = CommonMockData.BasicPhonyPingResponseXml)
        {
            // if a redirection has already occurred, that means we need to have the robot succeed
            if (HasBeenRedirected (request)) {
                return CommonMockData.AutodOffice365ResponseXml;
            }

            if (IsOptionsRequest (request)) {
                return optionsXml;
            }

            if (robotType != step) {
                return CommonMockData.AutodPhonyErrorResponse;
            }

            switch (robotType) {
            case MockSteps.S1:
            case MockSteps.S2:
                return xml;
            case MockSteps.S3:
                return CommonMockData.AutodPhonyRedirectResponse;
            default:
                Log.Info (Log.LOG_TEST, "A request occurred that was not recognized; this should not happen");
                return CommonMockData.AutodPhonyErrorResponse;
            }
        }

        public bool HasBeenRedirected (HttpRequestMessage request)
        {
            return request.RequestUri.ToString () == CommonMockData.RedirectionUrl;
        }

        public bool ShouldRedirect (HttpRequestMessage request, MockSteps step)
        {
            string getUri = "http://autodiscover." + CommonMockData.Host;
            string requestUri = request.RequestUri.ToString ();
            return request.Method.ToString () == "GET" && requestUri.Substring (0, getUri.Length) == getUri && step == MockSteps.S3;
        }

        public bool IsOptionsRequest (HttpRequestMessage request)
        {
            return "OPTIONS" == request.Method.ToString ();
        }

        public void DoOptionsAsserts (HttpRequestMessage request)
        {
            McServer serv = NcModel.Instance.Db.Table<McServer> ().First ();
            ServerFalseAssertions (serv, mockContext.Server);

            Assert.AreEqual (request.RequestUri.AbsolutePath, CommonMockData.PhonyAbsolutePath, "Options request absolute path should match phony path");

            string protocolVersion = request.Headers.GetValues ("MS-ASProtocolVersion").FirstOrDefault ();
            Assert.AreEqual ("12.0", protocolVersion, "MS-ASProtocolVersion should be set to the correct version by AsHttpOperation");
        }

        public MockSteps DetermineRobotType (HttpRequestMessage request)
        {
            string requestUri = request.RequestUri.ToString ();
            string s1Uri = "https://" + CommonMockData.Host;
            string s2Uri = "https://autodiscover." + CommonMockData.Host;
            string getUri = "http://autodiscover." + CommonMockData.Host;
            switch (request.Method.ToString ()) {
            case "POST":
                if (requestUri.Substring (0, s1Uri.Length) == s1Uri) {
                    return MockSteps.S1;
                } else if (requestUri.Substring (0, s2Uri.Length) == s2Uri) {
                    return MockSteps.S2;
                }
                break;
            case "GET":
                if (requestUri.Substring (0, getUri.Length) == getUri) {
                    return MockSteps.S3;
                }
                break;
            }

            // S4, OPTIONS, and robots that have already been redirected return "Other"
            return MockSteps.Other;
        }

        public void PerformAutoDiscoveryWithSettings (bool hasCert, Action<NcStateMachine> provideSm, Func<HttpRequestMessage, string> provideXml,
            Action<DnsQueryResponse> exposeDnsResponse, Action<HttpRequestMessage, HttpResponseMessage> exposeHttpMessage, 
            NcResult.SubKindEnum resultKind = NcResult.SubKindEnum.NotSpecified, bool testEndingDbState = true)
        {
            var autoResetEvent = new AutoResetEvent(false);

            NcStateMachine sm = CreatePhonySM (() => {
                autoResetEvent.Set ();
            });

            provideSm (sm);

            MockDnsQueryRequest.ProvideDnsQueryResponseMessage = () => {
                var mockDnsQueryResponse = new DnsQueryResponse () {};
                exposeDnsResponse (mockDnsQueryResponse);
                return mockDnsQueryResponse;
            };

            MockHttpClient.ExamineHttpRequestMessage = (request) => {};

            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                string xml = provideXml (request);

                // create the response, then allow caller to set headers,
                // then return response and assign to mockResponse
                var mockResponse = new HttpResponseMessage () {
                    Content = new StringContent(xml, Encoding.UTF8, "text/xml"),
                };
              
                // check for the type of request and respond with appropriate response (redir, error, pass)
                // allow the caller to modify the mockResponse object (esp. headers and StatusCode)
                exposeHttpMessage (request, mockResponse);


                return mockResponse;
            };

            MockHttpClient.HasServerCertificate = () => {
                return hasCert;
            };

            var autod = new AsAutodiscoverCommand (mockContext);
            autod.DnsQueryRequestType = typeof(MockDnsQueryRequest);
            autod.HttpClientType = typeof(MockHttpClient);

            autodCommand = autod;

            autod.Execute (sm);

            bool didFinish = autoResetEvent.WaitOne (8000);
            Assert.IsTrue (didFinish, "Operation did not finish");

            // if result kind was set by test (see 600/601 for example),
            // then test that code was set correctly
            if (resultKind != NcResult.SubKindEnum.NotSpecified) {
                Assert.AreEqual (resultKind, MockOwner.Status.SubKind, "StatusInd should set status code correctly");
            }

            if (testEndingDbState) {
                // Test that the server record was updated
                McServer serv = NcModel.Instance.Db.Table<McServer> ().Single (rec => rec.Id == mockContext.Account.ServerId);
                ServerTrueAssertions (mockContext.Server, serv);
            }

        }

        private void ServerTrueAssertions (McServer expected, McServer actual)
        {
            Assert.AreEqual (expected.Host, actual.Host, "Stored server host does not match expected");
            Assert.AreEqual (expected.Path, actual.Path, "Stored server path does not match expected");
            Assert.AreEqual (expected.Port, actual.Port, "Stored server port does not match expected");
            Assert.AreEqual (expected.Scheme, actual.Scheme, "Stored server scheme does not match expected");
            Assert.AreEqual (expected.UsedBefore, actual.UsedBefore, "Stored server used before flag does not match expected");
        }

        private void ServerFalseAssertions (McServer expected, McServer actual)
        {
            Assert.AreNotEqual (expected.Host, actual.Host, "Stored server host should not equal host in context");
            Assert.AreNotEqual (expected.Path, actual.Path, "Stored server path should not equal path in context");
            Assert.AreNotEqual (expected.Port, actual.Port, "Stored server port should not equal port in context");
            Assert.AreNotEqual (expected.Scheme, actual.Scheme, "Stored server scheme should not equal scheme in context");
            Assert.AreNotEqual (expected.UsedBefore, actual.UsedBefore, "Stored server used before flag should not equal flag in context");
        }

        private NcStateMachine CreatePhonySM (Action action)
        {
            var sm = new NcStateMachine ("PHONY") {
                Name = "BasicPhonyPing",
                LocalEventType = typeof(AsProtoControl.CtlEvt),
                LocalStateType = typeof(PhonySt),
                TransTable = new [] {
                    new Node {State = (uint)St.Start,
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = delegate () {},
                                State = (uint)PhonySt.UITest },
                            new Trans { 
                                Event = (uint)SmEvt.E.Success, 
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Success event was posted to Owner SM");
                                    action();
                                },
                                State = (uint)St.Start },
                            new Trans { 
                                Event = (uint)AsProtoControl.CtlEvt.E.GetCertOk, 
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Owner SM was asked to verify provided certificate with UI");
                                    Log.Info (Log.LOG_TEST, "Owner SM _verified_ provided certificate. Moving on.");
                                    PostAutodEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.SrvCertY, "TEST-ASPCDCOY");
                                },
                                State = (uint)St.Start },
                        }
                    },

                    new Node {State = (uint)PhonySt.UITest,
                        On = new [] {
                            new Trans {
                                // TestInvalidRedirect lands here. TestValidRedirectThenFailure should too
                                Event = (uint)AsProtoControl.CtlEvt.E.GetServConf, 
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Owner SM was asked to get server config from UI");
                                    action();
//                                    PostAutodEvent ((uint)AsAutodiscoverCommand.TlEvt.E.ServerSet, "TEST-ASPCDSSC");
                                },
                                State = (uint)St.Start },
                            new Trans {
                                // NewCredsUponAuthFailure test lands here
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Owner SM was asked to get new credentials from UI");
                                    Log.Info (Log.LOG_TEST, "Owner SM provided new credentials. Moving on.");
                                    PostAutodEvent ((uint)AsAutodiscoverCommand.TlEvt.E.CredSet, "TEST-ASPCDSC");
                                },
                                State = (uint)St.Start },
                            new Trans {
                                // NewCredsUponAuthFailure test lands here
                                Event = (uint)AsProtoControl.CtlEvt.E.GetCertOk,
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Owner SM was asked to verify provided certificate with UI");
                                    Log.Info (Log.LOG_TEST, "Owner SM _verified_ provided certificate. Moving on.");
                                    PostAutodEvent ((uint)AsAutodiscoverCommand.SharedEvt.E.SrvCertY, "TEST-ASPCDCOY");
                                },
                                State = (uint)PhonySt.UITest },
                        }
                    }
                }
            };
            return sm;
        }

        public void PostAutodEvent (uint evt, string mnemonic)
        {
            if (autodCommand != null) {
                autodCommand.Sm.PostEvent (evt, mnemonic);
            } else {
                Assert.Fail ("Autodiscover static property not set: Problem in test code.");
            }
        }
            
        public enum PhonySt : uint
        {
            UITest = (AsProtoControl.Lst.CalRespW + 1),
            Last = UITest,
        };

        public class PhonyEvt : AsProtoControl.CtlEvt
        {
            new public enum E : uint
            {
                MoveUITest = (AsProtoControl.CtlEvt.E.CalResp + 1),
                Last = MoveUITest,
            };
        }

        public byte[] dnsByteArray = { 255, 134, 129, 0, 0, 1, 0, 1, 0, 0, 0, 0, 13, 95, 97, 117, 116, 111, 100, 105, 115, 99, 111, 118, 101, 114, 4, 95, 116, 99, 112, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 33, 0, 1, 13, 95, 97, 117, 116, 111, 100, 105, 115, 99, 111, 118, 101, 114, 4, 95, 116, 99, 112, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 33, 0, 1, 0, 0, 17, 150, 0, 30, 0, 0, 0, 0, 1, 187, 4, 109, 97, 105, 108, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    }
        
}