using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;

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

    [TestFixture]
    public class AsHttpOperationTest
    {
        [Test]
        public void BasicPhonyPing ()
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
                                }, 
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = delegate () {},
                                State = (uint)St.Start },
                        }
                    },
                }
            };
            sm.PostEvent ((uint)SmEvt.E.Launch, "BasicPhonyPing");
            var mockUri = new Uri ("https://contoso.com");
            var mockRequestXml = XDocument.Parse (BasicPhonyPingRequestXml);
            var mockResponseXml = XDocument.Parse (BasicPhonyPingResponseXml);
            var wbxml = mockResponseXml.ToWbxml ();
            var mockResponse = new HttpResponseMessage () {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new ByteArrayContent (wbxml),
            };
            // TODO Add appropriate headers.
            mockResponse.Content.Headers.Add ("Content-Length", wbxml.Length.ToString ());
            mockResponse.Content.Headers.Add ("Content-Type", "application/vnd.ms-sync.wbxml");

            // See correct process response with right data. return event.
            // See SM get right event.
            var owner = new BaseMockOwner (mockUri);
            owner.ProvideXDocument = () => {
                return mockRequestXml;
            };
            var context = new MockContext ();
            MockHttpClient.ExamineHttpRequestMessage = (request) => {
                Assert.AreEqual (request.RequestUri, mockUri);
                Assert.AreEqual (request.Method, HttpMethod.Post);
                // TODO Check appropriate headers.
                // TODO Check correct Query-parms.
                // TODO Check correct WBXML.
            };
            MockHttpClient.ProvideHttpResponseMessage = () => {
                return mockResponse;
            };
            var op = new AsHttpOperation ("Ping", owner, context);
            op.HttpClientType = typeof (MockHttpClient);
            owner.ProcessResponseStandin = (sender, response, doc) => {
                Assert.AreSame (op, sender);
                Assert.AreSame (mockResponse, response);
                return Event.Create ((uint)SmEvt.E.Success, "BasicPhonyPingSuccess");
            };
            op.Execute (sm);
            Assert.IsTrue (setTrueBySuccessEvent);
        }

        public const string BasicPhonyPingRequestXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"no\"?>\n<Ping xmlns=\"Ping\">\n  <HeartbeatInterval>600</HeartbeatInterval>\n  <Folders>\n    <Folder>\n      <Id>1</Id>\n      <Class>Calendar</Class>\n    </Folder>\n    <Folder>\n      <Id>3</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>4</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>5</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>7</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>9</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>10</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>2</Id>\n      <Class>Contacts</Class>\n    </Folder>\n  </Folders>\n</Ping>";
        public const string BasicPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Ping xmlns=\"Ping\">\n  <Status>2</Status>\n  <Folders>\n    <Folder>3</Folder>\n  </Folders>\n</Ping>";
    }
}
