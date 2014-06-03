﻿using System;
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

                PerformAutoDiscoveryWithSettings (true, sm => {}, request => {
                    return PassRobotForStep (step, request, xml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                    }
                }, (httpRequest, httpResponse) => {
                    // check for redirection and set the response to 302 (Found) if true
                    bool isRedirection = httpRequest.Method.ToString () == "GET" && step == MockSteps.S3;

                    // provide valid redirection headers if needed
                    if (isRedirection) {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
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
                    return PassRobotForStep (step, request, xml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    // check for redirection and set the response to 302 (Found) if true
                    bool isRedirection = httpRequest.Method.ToString () == "GET" && step == MockSteps.S3;

                    // provide valid redirection headers if needed
                    if (isRedirection) {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", redirUrl);
                    } else {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
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
                var errorKind601 = NcResult.SubKindEnum.Error_AutoDError600;
                string xml = CommonMockData.AutodPhony601Response;
                TestAutodPingWithXmlResponse (xml, MockSteps.S1, errorKind601);
            }

            private void TestAutodPingWithXmlResponse (string xml, MockSteps step, NcResult.SubKindEnum errorKind)
            {
                // header settings
                string mockResponseLength = xml.Length.ToString ();

                PerformAutoDiscoveryWithSettings (true, sm => {}, request => {
                    return PassRobotForStep (step, request, xml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
                    httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                }, resultKind: errorKind);
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
                    return PassRobotForStep (step, request, xml, optionsXml: optionsXml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                    }
                }, (httpRequest, httpResponse) => {
                    // check for OPTIONS header and set status code to 404 to force hard fail
                    bool isOptions = httpRequest.Method.ToString () == "OPTIONS";
                    // check for redirection and set the response to 302 (Found) if true
                    bool isRedirection = httpRequest.Method.ToString () == "GET" && step == MockSteps.S3;

                    // provide valid redirection headers if needed
                    if (isRedirection) {
                        httpResponse.StatusCode = HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else if (isOptions) {
                        httpResponse.StatusCode = status;
                    } else {
                        httpResponse.StatusCode = HttpStatusCode.OK;
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
            }
        }

        [TestFixture]
        public class AuthFailure : AsAutodiscoverCommandTest
        {
            // Ensure that an authentication failure during S1, S2, or S3 results in 
            // the Owner being asked to supply new credentials.
//            [Test]
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
                    return PassRobotForStep (step, request, xml, optionsXml: optionsXml);
                }, provideDnsResponse => {
                }, (httpRequest, httpResponse) => {
                    // check for OPTIONS header and set status code to Unauthorized to force auth failure
                    bool isOptions = httpRequest.Method.ToString () == "OPTIONS";
                    // check for redirection and set the response to 302 (Found) if true
                    bool isRedirection = httpRequest.Method.ToString () == "GET" && step == MockSteps.S3;

                    // provide valid redirection headers if needed
                    if (isRedirection) {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else if (isOptions && !hasProvidedCreds) {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.Unauthorized;
                        hasProvidedCreds = true;
                    } else {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
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
                McMutables.Set ("HTTPOP", "TimeoutSeconds", "3");
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

                PerformAutoDiscoveryWithSettings (true, sm => {}, request => {
                    return PassRobotForStep (step, request, xml);
                }, provideDnsResponse => {
                    if (step == MockSteps.S4) {
                        provideDnsResponse.ParseResponse (dnsByteArray);
                    }
                }, (httpRequest, httpResponse) => {
                    // check for redirection and set the response to 302 (Found) if true
                    bool isRedirection = httpRequest.Method.ToString () == "GET" && step == MockSteps.S3;

                    // provide valid redirection headers if needed
                    if (isRedirection) {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.Found;
                        httpResponse.Headers.Add ("Location", CommonMockData.RedirectionUrl);
                    } else {
                        httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
                        httpResponse.Content.Headers.Add ("Content-Length", mockResponseLength);
                    }
                });
            }
        }
    }

    public class AsAutodiscoverCommandTest
    {
        public static AsAutodiscoverCommand autodCommand { get; set; }
        public static MockContext mockContext { get; set; }

        [SetUp]
        public void Setup ()
        {
            Log.Info (Log.LOG_TEST, "Setup began");

            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());

            MockDnsQueryRequest.ProvideDnsQueryResponseMessage = null;

            MockHttpClient.AsyncCalledCount = 0; // reset counter
            MockHttpClient.ExamineHttpRequestMessage = null;
            MockHttpClient.ProvideHttpResponseMessage = null;
            MockHttpClient.HasServerCertificate = null;
            MockNcCommStatus.Instance = null;

            autodCommand = null;
            mockContext = null;

            // insert phony server to db (this allows Auto-d 'DoAcceptServerConf' to update the record later)
            var phonyServer = new McServer ();
            phonyServer.Host = "/Phony-Server";
            phonyServer.Path = "/phonypath";
            phonyServer.Port = 500;
            phonyServer.Scheme = "/phonyscheme";
            phonyServer.UsedBefore = true;

//            phonyServer.Host = "";
//            phonyServer.UsedBefore = false;
//            phonyServer.Id = 5;
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

        [TearDown]
        public void Teardown ()
        {
            Log.Info (Log.LOG_TEST, "Teardown began");
        }

        // return good xml if the robot should pass, bad otherwise
        public string PassRobotForStep (MockSteps step, HttpRequestMessage request, string xml, string optionsXml = CommonMockData.BasicPhonyPingResponseXml)
        {
            string redirUrl = CommonMockData.RedirectionUrl;
            string requestUri = request.RequestUri.ToString ();
            string s1Uri = "https://" + CommonMockData.Host;
            string s2Uri = "https://autodiscover." + CommonMockData.Host;
            string getUri = "http://autodiscover." + CommonMockData.Host;
            switch (request.Method.ToString ()) {
            case "POST":
                if (step == MockSteps.S1 && requestUri.Substring (0, s1Uri.Length) == s1Uri) {
                    return xml;
                } else if (requestUri == redirUrl) {
                    return CommonMockData.AutodOffice365ResponseXml;
                } else if (step == MockSteps.S2 && requestUri.Substring (0, s2Uri.Length) == s2Uri) {
                    return xml;
                }
                break;
            case "GET":
                if (step == MockSteps.S3 && requestUri.Substring (0, getUri.Length) == getUri) {
                    return CommonMockData.AutodPhonyRedirectResponse;
                }
                break;
            case "OPTIONS":
                McServer serv = NcModel.Instance.Db.Table<McServer> ().First ();

                ServerFalseAssertions (serv, mockContext.Server);
                Assert.AreEqual (request.RequestUri.AbsolutePath, CommonMockData.PhonyAbsolutePath, "Options request absolute path should match phony path");

                string protocolVersion = request.Headers.GetValues ("MS-ASProtocolVersion").FirstOrDefault ();
                Assert.AreEqual ("12.0", protocolVersion, "MS-ASProtocolVersion should be set to the correct version by AsHttpOperation");
                return optionsXml;
            }
            return CommonMockData.AutodPhonyErrorResponse;
        }

        public void PerformAutoDiscoveryWithSettings (bool hasCert, Action<NcStateMachine> provideSm, Func<HttpRequestMessage, string> provideXml,
            Action<DnsQueryResponse> exposeDnsResponse, Action<HttpRequestMessage, HttpResponseMessage> exposeHttpMessage, 
            NcResult.SubKindEnum resultKind = NcResult.SubKindEnum.NotSpecified)
        {
            var interlock = new BlockingCollection<bool> ();

            bool setTrueBySuccessEvent = false;
            NcStateMachine sm = CreatePhonySM (val => {
                setTrueBySuccessEvent = val;
                interlock.Add(true);
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

            bool didFinish = false;

            if (!interlock.TryTake (out didFinish, 8000)) {
                if (resultKind == MockOwner.Status.SubKind) {
                    return;
                }

                Assert.Fail ("Failed in TryTake clause");
            }
            Assert.IsTrue (didFinish, "Autodiscovery operation should finish");
            Assert.IsTrue (setTrueBySuccessEvent, "State machine should set setTrueBySuccessEvent value to true");

            // Test that the server record was updated
//            McServer serv = NcModel.Instance.Db.Table<McServer> ().Single (rec => rec.Id == mockContext.Account.ServerId);
//            ServerTrueAssertions (mockContext.Server, serv);
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

        private NcStateMachine CreatePhonySM (Action<bool> action)
        {
            bool setTrueBySuccessEvent = false;
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
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
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
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
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