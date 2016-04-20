using System;
using System.Threading;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using DnDns.Enums;
using DnDns.Query;
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
using System.IO;

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

                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request);
                        return XMLForRobotType (request, robotType, step, xml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        if (MockSteps.S4 == step && MockSteps.S4 == DetermineRobotType (dnsType)) {
                            step = MockSteps.S1;
                            DnsQueryResponse response = new DnsQueryResponse ();
                            response.ParseResponse (dnsByteArray, dnsByteArray.Length);
                            return response;
                        } else {
                            return null;
                       }
                    },
                    (httpRequest, httpResponse) => {
                        // provide valid redirection headers if needed
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders();
                        if (ShouldRedirect (httpRequest, step) && !hasRedirected) {
                            status = HttpStatusCode.Found;
                            headers.Add ("Location", CommonMockData.RedirectionUrl);
                            hasRedirected = true; // disable second redirection
                            step = MockSteps.S1;
                        } else {
                            MockSteps robotType = DetermineRobotType (httpRequest);
                            status = AssignStatusCode (httpRequest, robotType, step);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );
            }
        }

        // Test that subdomain auth succeeds even if base domain auth fails - zach's scenario
        [TestFixture]
        public class SubDomainAuthSuccess : AsAutodiscoverCommandTest
        {
            [Test]
            public void TestS1SubDomainSuccess ()
            {
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1, isSubDomain : true);

            }

                
            private void TestAutodPingWithXmlResponse (string xml, MockSteps step, bool isSubDomain)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                bool hasRedirected = false;

                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request, isSubDomain: isSubDomain);
                        return XMLForRobotType (request, robotType, step, xml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        if (MockSteps.S4 == step && MockSteps.S4 == DetermineRobotType (dnsType)) {
                            step = MockSteps.S1;
                            DnsQueryResponse response = new DnsQueryResponse ();
                            response.ParseResponse (dnsByteArray, dnsByteArray.Length);
                            return response;
                        } else {
                            return null;
                        }
                    },
                    (httpRequest, httpResponse) => {
                        bool urisubdomain = isSubDomain;
                        if (httpRequest.RequestUri.Host.ToLower () == CommonMockData.Host) {
                            urisubdomain = false;
                        }
                        MockSteps robotType = DetermineRobotType (httpRequest, isSubDomain: urisubdomain);
                        // provide valid redirection headers if needed
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders();
                        if (ShouldRedirect (httpRequest, step, isSubDomain: isSubDomain) && !hasRedirected) {
                            status = HttpStatusCode.Found;
                            headers.Add ("Location", CommonMockData.RedirectionUrl);
                            hasRedirected = true; // disable second redirection
                            step = MockSteps.S1;
                        } else {
                            status = AssignStatusCode (httpRequest, robotType, step, isAuthFailFromBaseDomain: true);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );
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

                PerformAutoDiscoveryWithSettings (hasCert,
                    sm => {
                        provideSm (sm);
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request);
                        return XMLForRobotType (request, robotType, step, xml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        return null;
                    },
                    (httpRequest, httpResponse) => {
                        // provide valid redirection headers if needed
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders();
                        if (ShouldRedirect (httpRequest, step)) {
                            status = HttpStatusCode.Found;
                            headers.Add ("Location", redirUrl);
                        } else {
                            MockSteps robotType = DetermineRobotType (httpRequest);
                            status = AssignStatusCode (httpRequest, robotType, step);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );
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
                bool didReportError = false;
                MockOwner.StatusIndCallback += (result) => {
                    if (result.SubKind == errorKind) {
                        didReportError = true;
                    }
                };

                // header settings
                string mockResponseLength = xml.Length.ToString ();

                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                        sm.PostEvent ((uint)SmEvt.E.Launch, "TEST-FAIL");
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request);
                        return XMLForRobotType (request, robotType, step, xml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        return null;
                    },
                    (httpRequest, httpResponse) => {
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders ();
                        MockSteps robotType = DetermineRobotType (httpRequest);
                        status = AssignStatusCode (httpRequest, robotType, step);
                        headers.Add ("Content-Length", mockResponseLength);
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );

                Assert.True (didReportError, "Should report correct status ind result");
            }
        }

        [TestFixture]
        public class AutodTestFailure : AsAutodiscoverCommandTest
        {
            // Ensure that a server name test failure results in re-tries.
            [Test]
            public void TestFailureHasRetries ()
            {
                int retries = 5;
                McMutables.Set (1, "HTTPOP", "Retries", (retries).ToString ());
                int expectedRetries = (retries + 1) + 2; // n for step function plus 2 for the test re-try.

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

                bool didSetCreds = false;
                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                        sm.PostEvent ((uint)SmEvt.E.Launch, "TEST-FAIL");
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request);
                        return XMLForRobotType (request, robotType, step, xml, optionsXml: optionsXml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        if (MockSteps.S4 == step && MockSteps.S4 == DetermineRobotType (dnsType)) {
                            DnsQueryResponse response = new DnsQueryResponse ();
                            response.ParseResponse (dnsByteArray, dnsByteArray.Length);
                            return response;
                        } else {
                            return null;
                        }
                    },
                    (httpRequest, httpResponse) => {
                        // provide valid redirection headers if needed
                        HttpStatusCode xstatus;
                        NcHttpHeaders headers = new NcHttpHeaders ();
                        if (ShouldRedirect (httpRequest, step)) {
                            xstatus = HttpStatusCode.Found;
                            headers.Add ("Location", CommonMockData.RedirectionUrl);
                        } else if (IsOptionsRequest (httpRequest)) {
                            // pass after creds are set (don't do this for the BadGateway tests
                            if (didSetCreds && status != HttpStatusCode.BadGateway) {
                                xstatus = HttpStatusCode.OK;
                            } else {
                                // check for OPTIONS header and set status code to 404 to force hard fail
                                xstatus = status;
                                didSetCreds = true;
                            }
                        } else {
                            MockSteps robotType = DetermineRobotType (httpRequest);
                            xstatus = AssignStatusCode (httpRequest, robotType, step);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, xstatus, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );
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

                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                        sm.PostEvent ((uint)SmEvt.E.Launch, "TEST_FAIL");
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request);
                        return XMLForRobotType (request, robotType, step, xml, optionsXml: optionsXml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        return null;
                    },
                    (httpRequest, httpResponse) => {
                        // provide valid redirection headers if needed
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders ();
                        if (ShouldRedirect (httpRequest, step)) {
                            status = HttpStatusCode.Found;
                            headers.Add ("Location", CommonMockData.RedirectionUrl);
                        } else if (IsOptionsRequest (httpRequest) && !hasProvidedCreds) {
                            // if OPTIONS, set status code to Unauthorized to force auth failure
                            status = HttpStatusCode.Unauthorized;
                            hasProvidedCreds = true;
                        } else {
                            MockSteps robotType = DetermineRobotType (httpRequest);
                            status = AssignStatusCode (httpRequest, robotType, step);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    }
                );
            }
        }

        [TestFixture]
        public class SingleTimeoutSuccess : AsAutodiscoverCommandTest
        {
            /* The timeout flag is ASHTTPTTC */

            private void SetTimeoutConstants ()
            {
                McMutables.Set (1, "HTTPOP", "TimeoutSeconds", (TimeoutTime / 1000).ToString ());
                McMutables.Set (1, "AUTOD", "CertTimeoutSeconds", (TimeoutTime / 1000).ToString ());
                McMutables.Set (1, "DNSOP", "TimeoutSeconds", (TimeoutTime / 1000).ToString ());
            }

            [Test]
            public void TestS1 ()
            {
                TestSingleTimeout (MockSteps.S1, false);
            }

            [Test]
            public void TestS2 ()
            {
                TestSingleTimeout (MockSteps.S2, false);
            }

            // Ensure that the Owner is called-back when a valid cert is encountered in
            // the HTTPS access following a DNS SRV lookup
            [Test]
            public void TestS3 ()
            {
                TestSingleTimeout (MockSteps.S3, false);
            }

            [Test]
            public void TestSubS1 ()
            {
                TestSingleTimeout (MockSteps.S1, true);
            }

            [Test]
            public void TestSubS2 ()
            {
                TestSingleTimeout (MockSteps.S2, true);
            }

            [Test]
            public void TestSubS3 ()
            {
                TestSingleTimeout (MockSteps.S3, true);
            }

            private void TestSingleTimeout (MockSteps step, bool isSubDomain)
            {
                SetTimeoutConstants ();
                string xml = CommonMockData.AutodOffice365ResponseXml;
                TestAutodPingWithXmlResponse (xml, step, isSubDomain: isSubDomain);
            }

            private void TestAutodPingWithXmlResponse (string xml, MockSteps step, bool isSubDomain)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                bool hasRedirected = false;
                bool hasTimedOutOnce = false;

                PerformAutoDiscoveryWithSettings (true,
                    sm => {
                    },
                    request => {
                        MockSteps robotType = DetermineRobotType (request, isSubDomain: isSubDomain);
                        return XMLForRobotType (request, robotType, step, xml);
                    },
                    (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType) => {
                        if (MockSteps.S4 == step && MockSteps.S4 == DetermineRobotType (dnsType)) {
                            step = MockSteps.S1;
                            DnsQueryResponse response = new DnsQueryResponse ();
                            response.ParseResponse (dnsByteArray, dnsByteArray.Length);
                            return response;
                        } else {
                            return null;
                        }
                    },
                    (httpRequest, httpResponse) => {
                        HttpStatusCode status;
                        NcHttpHeaders headers = new NcHttpHeaders ();
                        MockSteps robotType = DetermineRobotType (httpRequest, isSubDomain: isSubDomain);
                        if (!hasTimedOutOnce && robotType == step) {
                            System.Threading.Thread.Sleep (TimeoutTime);
                            hasTimedOutOnce = true;
                            throw new WebException ("Timed out on purpose");
                        }
                        // provide valid redirection headers if needed
                        if (ShouldRedirect (httpRequest, step, isSubDomain: isSubDomain) && !hasRedirected) {
                            status = HttpStatusCode.Found;
                            headers.Add ("Location", CommonMockData.RedirectionUrl);
                            hasRedirected = true; // disable second redirection
                            step = MockSteps.S1;
                        } else {
                            status = AssignStatusCode (httpRequest, robotType, step);
                            headers.Add ("Content-Length", mockResponseLength);
                        }
                        return new NcHttpResponse (httpRequest.Method, status, httpResponse.Content, httpResponse.ContentType, headers);
                    });
            }
        }
    }

    public class AsAutodiscoverCommandTest : CommonTestOps
    {
        private static AsAutodiscoverCommand autodCommand { get; set; }
        private static MockContext mockContext { get; set; }

        public const int TimeoutTime = 1000;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();

            MockHttpClient.AsyncCalledCount = 0; // reset counter
            MockHttpClient.ExamineHttpRequestMessage = null;
            MockHttpClient.ProvideHttpResponseMessage = null;
            MockHttpClient.HasServerCertificate = null;
            MockNcCommStatus.Instance = null;

            MockOwner.Status = null;

            autodCommand = null;
            mockContext = null;

            mockContext = new MockContext (null);

            // insert phony server to db (this allows Auto-d 'DoAcceptServerConf' to update the record later)
            var phonyServer = new McServer ();
            phonyServer.AccountId = mockContext.Account.Id;
            phonyServer.Capabilities = McAccount.ActiveSyncCapabilities;
            phonyServer.Host = "/Phony-Server";
            phonyServer.Path = "/phonypath";
            phonyServer.Port = 500;
            phonyServer.Scheme = "/phonyscheme";
            phonyServer.UsedBefore = true;
            phonyServer.Insert ();

            mockContext.ProtoControl = ProtoOps.CreateProtoControl (mockContext.Account.Id);

            // flush the certificate cache so it doesn't interfere with future tests
            var instance = ServerCertificatePeek.Instance; // do this in case instance has not yet been created
            ServerCertificatePeek.TestOnlyFlushCache ();
        }

        public HttpStatusCode AssignStatusCode (NcHttpRequest request, MockSteps robotType, MockSteps step, bool isAuthFailFromBaseDomain = false)
        {
            string requestHost = request.RequestUri.Host.ToLower();

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
                if ((isAuthFailFromBaseDomain == true) && (requestHost == CommonMockData.Host)) {
                    Log.Info (Log.LOG_AS, "returning unauthorized for step {0} for base domain {1}", step, requestHost);
                    return HttpStatusCode.Unauthorized;
                }
                else{
                    return HttpStatusCode.OK;
                }
            default:
                return HttpStatusCode.NotFound;
            }
        }

        public string XMLForRobotType (NcHttpRequest request, MockSteps robotType, MockSteps step, string xml, string optionsXml = CommonMockData.BasicPhonyPingResponseXml)
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

        public bool HasBeenRedirected (NcHttpRequest request)
        {
            return request.RequestUri.ToString () == CommonMockData.RedirectionUrl;
        }

        public bool ShouldRedirect (NcHttpRequest request, MockSteps step, bool isSubDomain = false)
        {
            string getUri = "http://autodiscover.";
            if (isSubDomain) {
                getUri += CommonMockData.SubHost;
            } else {
                getUri += CommonMockData.Host;
            }
            string requestUri = request.RequestUri.ToString ();
            return request.Method.ToString () == "GET" && requestUri.Substring (0, getUri.Length) == getUri && step == MockSteps.S3;
        }

        public bool IsOptionsRequest (NcHttpRequest request)
        {
            return "OPTIONS" == request.Method.ToString ();
        }

        public void DoOptionsAsserts (NcHttpRequest request)
        {
            Assert.AreEqual (request.RequestUri.AbsolutePath, CommonMockData.PhonyAbsolutePath, "Options request absolute path should match phony path");

            string protocolVersion = request.Headers.GetValues ("MS-ASProtocolVersion").FirstOrDefault ();
            Assert.AreEqual ("12.0", protocolVersion, "MS-ASProtocolVersion should be set to the correct version by AsHttpOperation");
        }

        public MockSteps DetermineRobotType (NsType dnsType)
        {
            switch (dnsType) {
            case NsType.SRV:
                return MockSteps.S4;

            case NsType.MX:
                return MockSteps.S5;

            default:
                Assert.True (false, "Internal error - unexpected DNS request type.");
                return MockSteps.Other;
            }
        }

        public MockSteps DetermineRobotType (NcHttpRequest request, bool isSubDomain = false)
        {
            string requestUri = request.RequestUri.ToString ();
            string s1Uri = "https://";
            string s2Uri = "https://autodiscover.";
            string getUri = "http://autodiscover.";
            if (isSubDomain) {
                s1Uri += CommonMockData.SubHost;
                s2Uri += CommonMockData.SubHost;
                getUri += CommonMockData.SubHost;
            } else {
                s1Uri += CommonMockData.Host;
                s2Uri += CommonMockData.Host;
                getUri += CommonMockData.Host;
            }
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

        public delegate string ProvideXmlDelegate (NcHttpRequest request);
        public delegate NcHttpResponse ExposeHttpMessageDelegate (NcHttpRequest request, NcHttpResponse response);

        public void PerformAutoDiscoveryWithSettings (bool hasCert, Action<NcStateMachine> provideSm, ProvideXmlDelegate provideXml,
            AsDnsOperation.CallResQueryDelegate exposeDnsResponse, ExposeHttpMessageDelegate exposeHttpMessage)
        {
            var autoResetEvent = new AutoResetEvent(false);
            bool smFail = false;
            NcStateMachine sm = CreatePhonySM (
                () => {
                    autoResetEvent.Set ();
                },
                () => {
                    smFail = true; 
                }
            );

            provideSm (sm);

            AsDnsOperation.CallResQuery = exposeDnsResponse;

            MockHttpClient.ExamineHttpRequestMessage = (request) => {};

            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                string xml = provideXml (request);

                // create the response, then allow caller to set headers,
                // then return response and assign to mockResponse
                var mockResponse = new NcHttpResponse (request.Method, HttpStatusCode.OK, Encoding.UTF8.GetBytes (xml), "text/xml");
                // check for the type of request and respond with appropriate response (redir, error, pass)
                // allow the caller to modify the mockResponse object (esp. headers and StatusCode)
                return exposeHttpMessage (request, mockResponse);
            };

            MockHttpClient.HasServerCertificate = () => {
                return hasCert;
            };

            NcProtoControl.TestHttpClient = MockHttpClient.Instance;
            var autod = new AsAutodiscoverCommand (mockContext);

            autodCommand = autod;

            autod.Execute (sm);

            bool didFinish = autoResetEvent.WaitOne (8000);
            Assert.IsTrue (didFinish, "Operation did not finish");
            Assert.False (smFail);
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

        private NcStateMachine CreatePhonySM (Action action, Action failed)
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
                            new Trans {
                                Event = (uint)AsProtoControl.CtlEvt.E.GetServConf,
                                Act = delegate () {
                                    failed ();
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
            UITest = (AsProtoControl.Lst.QOpW + 1),
            Last = UITest,
        };

        public byte[] dnsByteArray = { 255, 134, 129, 0, 0, 1, 0, 1, 0, 0, 0, 0, 13, 95, 97, 117, 116, 111, 100, 105, 115, 99, 111, 118, 101, 114, 4, 95, 116, 99, 112, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 33, 0, 1, 13, 95, 97, 117, 116, 111, 100, 105, 115, 99, 111, 118, 101, 114, 4, 95, 116, 99, 112, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 33, 0, 1, 0, 0, 17, 150, 0, 30, 0, 0, 0, 0, 1, 187, 4, 109, 97, 105, 108, 13, 117, 116, 111, 112, 105, 97, 115, 121, 115, 116, 101, 109, 115, 3, 110, 101, 116, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    }
}