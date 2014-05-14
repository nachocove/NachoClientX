using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;


/*
 * Use a mock HttpClient.
 * BEContext?
 */
namespace Test.iOS
{
    class BaseMockOwner : IAsHttpOperationOwner 
    {
        public delegate Event ProcessResponseStandinDelegate (AsHttpOperation sender, HttpResponseMessage response, XDocument doc);
        public ProcessResponseStandinDelegate ProcessResponseStandin { set; get; }

        public delegate XDocument ProvideXDocumentDelegate ();
        public ProvideXDocumentDelegate ProvideXDocument { set; get; }

        public Uri ProvidedUri { set; get; }
        public AsHttpOperationTest Tester { set; get; }

        public BaseMockOwner (Uri providedUri)
        {
            ProvidedUri = providedUri;
        }

        AsHttpOperation Op { set; get; }

        public virtual Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation sender)
        {
            return null;
        }

        public virtual Event PreProcessResponse (AsHttpOperation sender, HttpResponseMessage response)
        {
            return null;
        }

        public virtual Event ProcessResponse (AsHttpOperation sender, HttpResponseMessage response)
        {
            return null;
        }

        public virtual Event ProcessResponse (AsHttpOperation sender, HttpResponseMessage response, XDocument doc)
        {
            if (null != ProcessResponseStandin) {
                return ProcessResponseStandin (sender, response, doc);
            }
            return null;
        }

        public virtual void PostProcessEvent (Event evt)
        {
        }

        public virtual Event ProcessTopLevelStatus (AsHttpOperation sender, uint status)
        {
            return null;
        }

        public virtual XDocument ToXDocument (AsHttpOperation sender)
        {
            if (null != ProvideXDocument) {
                return ProvideXDocument ();
            }
            return null;
        }

        public virtual string ToMime (AsHttpOperation sender)
        {
            return null;
        }

        public virtual Uri ServerUri (AsHttpOperation sender)
        {
            return ProvidedUri;
        }

        public virtual void ServerUriChanged (Uri ServerUri, AsHttpOperation sender)
        {
            // Dummy.
        }

        public virtual HttpMethod Method (AsHttpOperation sender)
        {
            return HttpMethod.Post;
        }

        public virtual bool UseWbxml (AsHttpOperation sender)
        {
            return true;
        }

        public virtual bool DoSendPolicyKey (AsHttpOperation sender)
        {
            return true;
        }

        public virtual void StatusInd (NcResult result)
        {
            // Dummy.
        }

        public virtual void StatusInd (bool didSucceed)
        {
            // Dummy.
        }

        public virtual bool WasAbleToRephrase ()
        {
            return false;
        }

        public virtual void ResoveAllFailed (NcResult.WhyEnum why)
        {
            // Dummy.
        }

        public virtual void ResoveAllDeferred ()
        {
            // Dummy.
        }
    }
        

    // reusable request/response data
    class MockData
    {
        public static Uri MockUri = new Uri ("https://contoso.com");
        public static XDocument MockRequestXml = XDocument.Parse (BasicPhonyPingRequestXml);
        public static XDocument MockResponseXml = XDocument.Parse (BasicPhonyPingResponseXml);
        public static byte[] Wbxml = MockResponseXml.ToWbxml ();

        public const string BasicPhonyPingRequestXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"no\"?>\n<Ping xmlns=\"Ping\">\n  <HeartbeatInterval>600</HeartbeatInterval>\n  <Folders>\n    <Folder>\n      <Id>1</Id>\n      <Class>Calendar</Class>\n    </Folder>\n    <Folder>\n      <Id>3</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>4</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>5</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>7</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>9</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>10</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>2</Id>\n      <Class>Contacts</Class>\n    </Folder>\n  </Folders>\n</Ping>";
        public const string BasicPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Ping xmlns=\"Ping\">\n  <Status>2</Status>\n  <Folders>\n    <Folder>3</Folder>\n  </Folders>\n</Ping>";
    }


    [TestFixture]
    public class AsHttpOperationTest
    {
        private class HttpOpEvt : SmEvt
        {
            new public enum E : uint
            {
                Cancel = (SmEvt.E.Last + 1),
                Delay,
                Timeout,
                Rephrase,
                Final,
            };
        }

        [Test]
        public void BasicPhonyPing ()
        {
            // header settings
            string contentType = "application/vnd.ms-sync.wbxml";
            string mockRequestLength = MockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = MockData.Wbxml.Length.ToString ();

            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (contentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        [Test]
        public void NegativeContentLength ()
        {
            // use this to test timeout values once they can be set
//            string contentType = "application/vnd.ms-sync.wbxml";
//            string mockResponseLength = -15.ToString ();
//
//            PerformHttpOperationWithSettings (response => {
//                response.StatusCode = System.Net.HttpStatusCode.OK;
//                response.Content.Headers.Add ("Content-Length", mockResponseLength);
//                response.Content.Headers.Add ("Content-Type", contentType);
//            }, request => {
//            });
        }

        // TODO finish this test -- not sure where the commresult method should be called in the exceptions
        [Test]
        public void BadWbxmlShouldFailCommResult ()
        {
            // use this to test timeout values once they can be set
            string contentType = "application/vnd.ms-sync.wbxml";
            string mockResponseLength = 10.ToString ();

            PerformHttpOperationWithSettings (response => {
                string badWbxml = "wbxml bad wbxml";
                byte[] bytes = new byte[badWbxml.Length * sizeof(char)];
                System.Buffer.BlockCopy(badWbxml.ToCharArray(), 0, bytes, 0, bytes.Length);
                response.Content = new ByteArrayContent(bytes);  

                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
            });

            DoReportCommResultWithFailureType (() => {
                return true;
            });
        }

        [Test]
        public void BadXmlShouldFailCommResult ()
        {
            // use this to test timeout values once they can be set
            string contentType = "text/xml";
            string mockResponseLength = 10.ToString ();

            PerformHttpOperationWithSettings (response => {
                string badXml = "xml bad xml";
                byte[] bytes = new byte[badXml.Length * sizeof(char)];
                System.Buffer.BlockCopy(badXml.ToCharArray(), 0, bytes, 0, bytes.Length);
                response.Content = new ByteArrayContent(bytes);  

                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
            });

            DoReportCommResultWithFailureType (() => {
                return true;
            });
        }

        [Test]
        public void GoodXmlShouldReportSuccessfulCommResult ()
        {
            // use this to test timeout values once they can be set
            string contentType = "text/xml";
            string mockResponseLength = 10.ToString ();

            PerformHttpOperationWithSettings (response => {
                string badXml = MockData.BasicPhonyPingRequestXml;
                byte[] bytes = new byte[badXml.Length * sizeof(char)];
                System.Buffer.BlockCopy(badXml.ToCharArray(), 0, bytes, 0, bytes.Length);
                response.Content = new ByteArrayContent(bytes);  

                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
            });

            DoReportCommResultWithFailureType (() => {
                return false;
            });
        }

        [Test]
        public void MismatchHeaderSizeValues ()
        {
            /* Response Content-Length header does not match actual content length.
               Should not crash on bad or unexpected values */

            // content length is smaller than header
            int halfLength = MockData.Wbxml.Length / 2;  // make the test length < actual length
            string responseLengthHalf = halfLength.ToString ();
            PerformHttpOperationWithResponseLength (responseLengthHalf);

            // content length is 0
            string responseLengthZero = 0.ToString ();
            PerformHttpOperationWithResponseLength (responseLengthZero);

            // content length is a large number
            string responseLengthLarge = 9000.ToString ();
            PerformHttpOperationWithResponseLength (responseLengthLarge);
        }

        // Helper function for MismatchHeaderSizeValues Test
        private void PerformHttpOperationWithResponseLength (string responseLength)
        {
            string contentType = "application/vnd.ms-sync.wbxml";
            string mockRequestLength = MockData.MockRequestXml.ToWbxml ().Length.ToString ();

            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", responseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (contentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        // Content-Type is not required if Content-Length is missing or zero
        /* TODO: Both of these tests currently fail. An exception is thrown in AsHttpOperation.cs
         * Need to inspect */
        [Test]
        public void ContentTypeNotRequired ()
        {
            /* Content-Length is zero --> must not require content type */
            // header settings (get passed into CreateMockResponseWithHeaders ())
            string mockRequestLength = MockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = 0.ToString ();

            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
            });

            /* Content-Length is missing --> must not require content type */
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
            });
        }

        [Test]
        public void StatusCodeFound ()
        {
            // Status Code -- Found (200)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.Found;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeBadRequest ()
        {
            // Status Code -- Bad Request (400)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeUnauthorized ()
        {
            // Status Code -- Unauthorized (401)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.Unauthorized;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeForbidden ()
        {
            // Status Code -- Forbidden (403)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.Forbidden;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeNotFound ()
        {
            // Status Code -- NotFound (404)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCode449 ()
        {
            // Status Code -- Retry With Status Code (449)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = (System.Net.HttpStatusCode)449;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeInternalServerError ()
        {
            // Status Code -- Internal Server Error (500)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCode501 ()
        {
            // Status Code -- Command Not Implemented (501)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = (System.Net.HttpStatusCode)501;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCode507 ()
        {
            // Status Code -- Server out of Space (507)
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = (System.Net.HttpStatusCode)507;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeUnknown ()
        {
            // Unknown status code
            PerformHttpOperationWithSettings (response => {
                response.StatusCode = (System.Net.HttpStatusCode)8035;
            }, request => {
            });

            DoReportCommResultWithFailureType (() => {
                return true;
            });
        }

        private void DoReportCommResultWithNonGeneralFailure ()
        {
            DoReportCommResultWithFailureType (() => {
                return false;
            });
        }

        // Test that comm status' are reported correctly by each status code method
        // Allow the type of failure (general/non-general) to be set by the caller
        private void DoReportCommResultWithFailureType (Func <bool> failureAction)
        {
            var mockCommStatus = MockNcCommStatus.Instance;

            bool didFailGenerally = failureAction ();

            Assert.AreEqual (didFailGenerally, mockCommStatus.DidFailGenerally, "Should set MockNcCommStatus Correctly");
            Assert.AreEqual ("contoso.com", mockCommStatus.Host);

            // teardown -- reset the comm status singleton before each test
            // TODO move this into a teardown method
            MockNcCommStatus.Instance = null;
        }

        private void PerformHttpOperationWithSettings (Action<HttpResponseMessage> provideResponse, Action<HttpRequestMessage> provideRequest)
        {
            var interlock = new BlockingCollection<bool> ();
            // setup
            bool setTrueBySuccessEvent = false;
            NcStateMachine sm = CreatePhonySM (val => {
                setTrueBySuccessEvent = val;
                interlock.Add(true);
            });

            sm.PostEvent ((uint)SmEvt.E.Launch, "BasicPhonyPing");

            // create the response, then allow caller to set headers,
            // then return response and assign to mockResponse
            var mockResponse = CreateMockResponse (MockData.Wbxml, response => {
                provideResponse (response);   
            });

            // do some common assertions
            ExamineRequestMessageOnMockClient (MockData.MockUri, request => {
                provideRequest (request);
            });

            // provides the mock response
            MockHttpClient.ProvideHttpResponseMessage = () => {
                return mockResponse;
            };

            var context = new MockContext ();

            // provides the mock owner
            BaseMockOwner owner = CreateMockOwner (MockData.MockUri, MockData.MockRequestXml);

            var op = new AsHttpOperation ("Ping", owner, context);

            var mockCommStatusInstance = MockNcCommStatus.Instance;
            op.NcCommStatusSingleton = mockCommStatusInstance;
            op.HttpClientType = typeof (MockHttpClient);
            owner.ProcessResponseStandin = (sender, response, doc) => {
                Assert.AreSame (op, sender, "Owner's sender and AsHttpOperation should match when response is processed");
                Assert.AreSame (mockResponse, response, "Response should match mock response");
                return Event.Create ((uint)SmEvt.E.Success, "BasicPhonyPingSuccess");
            };

            op.Execute (sm);

            bool didFinish = false;
            if (!interlock.TryTake (out didFinish, 2000)) {
                Assert.Inconclusive ("Failed in TryTake clause");
            }
            Assert.IsTrue (didFinish);
            Assert.IsTrue (setTrueBySuccessEvent);
        }

        // Action Delegate for creating a state machine
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
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                }, 
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)HttpOpEvt.E.Rephrase,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = delegate () {
                                    setTrueBySuccessEvent = true;
                                    action(setTrueBySuccessEvent);
                                },
                                State = (uint)St.Start },
                        }
                    },
                }
            };

            return sm;
        }
           
        private BaseMockOwner CreateMockOwner (Uri mockUri, XDocument mockRequestXml)
        {
            // See correct process response with right data. return event.
            // See SM get right event.
            var owner = new BaseMockOwner (mockUri);
            owner.ProvideXDocument = () => {
                return mockRequestXml;
            };

            return owner;
        }

        private HttpResponseMessage CreateMockResponse (byte[] wbxml, Action<HttpResponseMessage> provideResponse)
        {
            var mockResponse = new HttpResponseMessage () {
                Content = new ByteArrayContent (wbxml),
            };
     
            // allow the caller to modify the mockResponse object (esp. headers and StatusCode)
            provideResponse (mockResponse);

            return mockResponse;
        }

        private void ExamineRequestMessageOnMockClient (Uri mockUri, Action<HttpRequestMessage> requestAction)
        {
            MockHttpClient.ExamineHttpRequestMessage = (request) => {
                Assert.AreEqual (mockUri, request.RequestUri, "Uri's should match");
                Assert.AreEqual (HttpMethod.Post, request.Method, "HttpMethod's should match");

                /* TODO Check appropriate headers. */

                // test that user agent is correct
                Assert.AreEqual (Device.Instance.UserAgent ().ToString (), request.Headers.UserAgent.ToString (), 
                    "request User-Agent should be set to correct UserAgent");

                // test that the protocol version is correctly set
                McProtocolState protocol = new McProtocolState ();
                var expectedVersion = protocol.AsProtocolVersion;
                string protocolVersion = request.Headers.GetValues ("MS-ASProtocolVersion").FirstOrDefault ();
                Assert.AreEqual (expectedVersion, protocolVersion, "MS-ASProtocolVersion should be set to the correct version by AsHttpOperation");

                // perform any checks that are specific to a test case
                requestAction(request);
        
                // TODO Check correct Query-params.
                // TODO Check correct WBXML.
            };
        }
    
    }
}
