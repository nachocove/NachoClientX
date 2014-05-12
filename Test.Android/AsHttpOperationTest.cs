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

    class MockHttpClient : IHttpClient
    {
        // TODO: do we need to go the factory route and get rid of the statics?
        public delegate void ExamineHttpRequestMessageDelegate (HttpRequestMessage request);
        public static ExamineHttpRequestMessageDelegate ExamineHttpRequestMessage { set; get; }

        public delegate HttpResponseMessage ProvideHttpResponseMessageDelegate ();
        public static ProvideHttpResponseMessageDelegate ProvideHttpResponseMessage { set; get; }

        public TimeSpan Timeout { get; set; }

        public MockHttpClient (HttpClientHandler handler)
        {
        }

        public Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, 
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            if (null != ExamineHttpRequestMessage) {
                ExamineHttpRequestMessage (request);
            }

            return Task.Run<HttpResponseMessage> (delegate {
                return ProvideHttpResponseMessage ();
            });
        }
    }

    class MockContext : IBEContext
    {
        public IProtoControlOwner Owner { set; get; }
        public AsProtoControl ProtoControl { set; get; }
        public McProtocolState ProtocolState { get; set; }
        public McServer Server { get; set; }
        public McAccount Account { set; get; }
        public McCred Cred { set; get; }

        public MockContext ()
        {
            Owner = null; // Should not be accessed.
            ProtoControl = null; // Should not be accessed.
            ProtocolState = new McProtocolState ();
            // READ AsPolicyKey
            // R/W AsProtocolVersion
            // READ InitialProvisionCompleted
            Server = null; // Should not be accessed.
            Account = new McAccount () {
                Id = 1,
            };
            Cred = new McCred () {
                Username = "dummy",
                Password = "password",
            };
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
        [Test]
        public void BasicPhonyPing ()
        {
            // header settings (get passed into CreateMockResponseWithHeaders ())
            string contentType = "application/vnd.ms-sync.wbxml";
            string mockRequestLength = MockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = MockData.Wbxml.Length.ToString ();

            PerformHttpOperationWithSettings (contentType, mockRequestLength, mockResponseLength, response => {
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (contentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        [Test]
        public void BadTextHeaderValues ()
        {
            // Expected behavior is not crashing
        }

        [Test]
        public void LargeNumberHeaderValues ()
        {
            // Expected behavior is not crashing
        }

        [Test]
        public void MismatchHeaderSizeValues ()
        {
            /* Content-Length header does not match actual content length */

            // Setup
            bool setTrueBySuccessEvent = false;
            NcStateMachine sm = CreatePhonySM (val => {
                setTrueBySuccessEvent = val;
            });
            Assert.False (setTrueBySuccessEvent);
            sm.PostEvent ((uint)SmEvt.E.Launch, "BasicPhonyPing");

            // content length is smaller than header

            // content length is 0

            // content length is gigantic number

            // actual content length is 0 and header says it is longer than that

        }

        // Content-Type is not required if Content-Length is missing or zero
        /* TODO: Both of these tests currently fail. An exception is thrown in AsHttpOperation.cs
         * Need to inspect */
        [Test]
        public void ContentTypeNotRequired ()
        {
            /* Content-Length is zero --> must not require content type */
            // header settings (get passed into CreateMockResponseWithHeaders ())
            string contentType = "application/vnd.ms-sync.wbxml";
            string mockRequestLength = MockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = 0.ToString ();

            PerformHttpOperationWithSettings (contentType, mockRequestLength, mockResponseLength, response => {
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
            });

            /* Content-Length is missing --> must not require content type */
            PerformHttpOperationWithSettings (contentType, mockRequestLength, mockResponseLength, response => {}, request => {});
        }

        public void PerformHttpOperationWithSettings (string contentType, string mockRequestLength, string mockResponseLength, 
            Action<HttpResponseMessage> provideResponse, Action<HttpRequestMessage> provideRequest)
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
            var mockResponse = CreateMockResponse (MockData.Wbxml, mockResponseLength, response => {
                provideResponse (response);
            });

            // do some common assertions
            ExamineRequestMessageOnMockClient (MockData.MockUri, contentType, mockRequestLength, request => {
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
            op.HttpClientType = typeof (MockHttpClient);
            owner.ProcessResponseStandin = (sender, response, doc) => {
                Assert.AreSame (op, sender, "Owner's sender and AsHttpOperation should match when response is processed");
                Assert.AreSame (mockResponse, response, "Response should match mock response");
                return Event.Create ((uint)SmEvt.E.Success, "BasicPhonyPingSuccess");
            };

            op.Execute (sm);

            bool didFinish = false;
            Assert.IsTrue (interlock.TryTake (out didFinish, 2000));
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

        private HttpResponseMessage CreateMockResponse (byte[] wbxml, string mockResponseLength, Action<HttpResponseMessage> provideResponse)
        {
            var mockResponse = new HttpResponseMessage () {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new ByteArrayContent (wbxml),
            };
     
            // allow the caller to modify the mockResponse object (esp. headers)
            provideResponse (mockResponse);

            return mockResponse;
        }

        private void ExamineRequestMessageOnMockClient (Uri mockUri, string contentType, string mockRequestLength, 
            Action<HttpRequestMessage> requestAction)
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
