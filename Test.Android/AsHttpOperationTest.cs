using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        public static NcResult Status { get; set; } // for checking StatusInd posts work

        public delegate Event ProcessResponseStandinDelegate (AsHttpOperation sender, HttpResponseMessage response, XDocument doc);
        public ProcessResponseStandinDelegate ProcessResponseStandin { set; get; }

        public delegate XDocument ProvideXDocumentDelegate ();
        public ProvideXDocumentDelegate ProvideXDocument { set; get; }
       
        public delegate void ViewStatusIndMessageDelegate (NcResult result);
        public static event ViewStatusIndMessageDelegate StatusIndCallback;

        public Uri ProvidedUri { set; get; }
        public AsHttpOperationTest Tester { set; get; }

        public BaseMockOwner (Uri providedUri)
        {
            Status = null;
            ProvidedUri = providedUri;
        }

        AsHttpOperation Op { set; get; }

        public virtual double TimeoutInSeconds
        {
            get { return 0.0; }
        }

        public virtual Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation sender)
        {
            return null;
        }

        public virtual Event PreProcessResponse (AsHttpOperation sender, HttpResponseMessage response)
        {
            return null;
        }

        public virtual Event ProcessResponse (AsHttpOperation sender, HttpResponseMessage response, CancellationToken cToken)
        {
            return Event.Create ((uint)SmEvt.E.Success, "MOCKSUCCESS");
        }

        public virtual Event ProcessResponse (AsHttpOperation sender, HttpResponseMessage response, XDocument doc, CancellationToken cToken)
        {
            if (null != ProcessResponseStandin) {
                return ProcessResponseStandin (sender, response, doc);
            }
            return null;
        }

        public virtual void PostProcessEvent (Event evt)
        {
        }

        public virtual Event ProcessTopLevelStatus (AsHttpOperation sender, uint status, XDocument doc)
        {
            return null;
        }

        public virtual bool SafeToXDocument (AsHttpOperation sender, out XDocument doc)
        {
            if (null != ProvideXDocument) {
                doc = ProvideXDocument ();
                return true;
            }
            doc = null;
            return true;
        }

        public virtual bool SafeToMime (AsHttpOperation sender, out Stream mime)
        {
            mime = null;
            return true;
        }

        public virtual Uri ServerUri (AsHttpOperation sender, bool isEmailRedacted = false)
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

        public virtual bool IgnoreBody (AsHttpOperation Sender)
        {
            return false;
        }

        public virtual bool IsContentLarge (AsHttpOperation Sender)
        {
            return false;
        }

        public virtual bool DoSendPolicyKey (AsHttpOperation sender)
        {
            return true;
        }

        public virtual void StatusInd (NcResult result)
        {
            if (StatusIndCallback != null) {
                StatusIndCallback (result);
            }
            Status = result;
        }

        public virtual void StatusInd (bool didSucceed)
        {
            // Dummy.
        }

        public virtual bool WasAbleToRephrase ()
        {
            return false;
        }

        public virtual void ResolveAllFailed (NcResult.WhyEnum why)
        {
            // Dummy.
        }

        public virtual void ResolveAllDeferred ()
        {
            // Dummy.
        }
    }


    [TestFixture]
    public class AsHttpOperationTest : CommonTestOps
    {
        private MockContext Context;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            Context = null;

            // reset the comm status singleton before each test
            MockNcCommStatus.Instance = null;
        }

        static string WBXMLContentType = "application/vnd.ms-sync.wbxml";

        [Test]
        public void BasicPhonyPingOK ()
        {
            // header settings
            string contentType = WBXMLContentType;
            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = CommonMockData.Wbxml.Length.ToString ();

            PerformHttpOperationWithSettings (sm => {

            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", contentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (contentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        [Test]
        public void BasicPhonyPingAccept ()
        {
            // header settings
            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = CommonMockData.Wbxml.Length.ToString ();

            PerformHttpOperationWithSettings (sm => {

            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.Accepted;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (WBXMLContentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        private const string HeaderXMsCredentialsExpire = "X-MS-Credentials-Expire";
        private const string HeaderXMsCredentialServiceUrl = "X-MS-Credential-Service-Url";

        [Test]
        public void StatusCodeOkWithCredExpUri ()
        {
            bool isExpiryNotified = false;
            Tuple<int,Uri> value = null;
            const string match = "http://nacho.com/";

            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = CommonMockData.Wbxml.Length.ToString ();

            BaseMockOwner.StatusIndCallback += (result) => {
                if (result.SubKind == NcResult.SubKindEnum.Error_PasswordWillExpire) {
                    isExpiryNotified = true;
                    value = (Tuple<int,Uri>)result.Value;
                }
            };

            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
                response.Headers.Add (HeaderXMsCredentialServiceUrl, match);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (WBXMLContentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
            Context.Cred = McCred.QueryById<McCred> (Context.Cred.Id);
            Assert.True (isExpiryNotified);
            Assert.NotNull (value);
            Assert.AreEqual (new Uri(match), value.Item2);
            Assert.AreEqual (-1, value.Item1);
            Assert.IsNotNull (Context.Cred.RectificationUrl);
            Assert.AreEqual (match, Context.Cred.RectificationUrl);
            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeOkWithCredExpDaysLeft ()
        {
            bool isExpiryNotified = false;
            Tuple<int,Uri> value = null;

            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = CommonMockData.Wbxml.Length.ToString ();

            BaseMockOwner.StatusIndCallback += (result) => {
                if (result.SubKind == NcResult.SubKindEnum.Error_PasswordWillExpire) {
                    isExpiryNotified = true;
                    value = (Tuple<int,Uri>)result.Value;
                }
            };

            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
                response.Headers.Add (HeaderXMsCredentialsExpire, "2");
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (WBXMLContentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
            
            Context.Cred = McCred.QueryById<McCred> (Context.Cred.Id);
            Assert.True (isExpiryNotified);
            Assert.NotNull (value);
            Assert.IsNull (value.Item2);
            Assert.AreEqual (2, value.Item1);
            Assert.AreNotEqual (DateTime.MaxValue, Context.Cred.Expiry);
            DoReportCommResultWithNonGeneralFailure ();
        }

        // TODO Set timeout values to fix this test
//        [Test]
        public void NegativeContentLength ()
        {
            // use this to test timeout values once they can be set
            string mockResponseLength = "-15";

            PerformHttpOperationWithSettings (sm => {

            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
            }, request => {
            });
        }

        // TODO finish this test -- not sure where the commresult method should be called in the exceptions
//        [Test]
        public void BadWbxmlShouldFailCommResult ()
        {
            // use this to test timeout values once they can be set
            string mockResponseLength = 10.ToString ();

            PerformHttpOperationWithSettings (sm => {

            }, response => {
                string badWbxml = "wbxml bad wbxml";
                byte[] bytes = new byte[badWbxml.Length * sizeof(char)];
                System.Buffer.BlockCopy(badWbxml.ToCharArray(), 0, bytes, 0, bytes.Length);
                response.Content = new ByteArrayContent(bytes);  

                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
            }, request => {
            });

            DoReportCommResultWithFailureType (() => {
                return true;
            });
        }

//        [Test]
        // TODO Ask Jeff about this
        public void BadXmlShouldFailCommResult ()
        {
            // use this to test timeout values once they can be set
            string contentType = "text/xml";
            string mockResponseLength = 10.ToString ();

            PerformHttpOperationWithSettings (sm => {

            }, response => {
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

            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                string goodXml = CommonMockData.BasicPhonyPingRequestXml;
                byte[] bytes = new byte[goodXml.Length * sizeof(char)];
                System.Buffer.BlockCopy(goodXml.ToCharArray(), 0, bytes, 0, bytes.Length);
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
            int halfLength = CommonMockData.Wbxml.Length / 2;  // make the test length < actual length
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
            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();

            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", responseLength);
                response.Content.Headers.Add ("Content-Type", WBXMLContentType);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
                Assert.AreEqual (WBXMLContentType, request.Content.Headers.ContentType.ToString (), "request Content-Type should match expected");
            });
        }

        // Content-Type is not required if Content-Length is missing or zero
        [Test]
        public void ContentTypeNotRequired ()
        {
            /* Content-Length is zero --> must not require content type */
            // header settings (get passed into CreateMockResponseWithHeaders ())
            string mockRequestLength = CommonMockData.MockRequestXml.ToWbxml ().Length.ToString ();
            string mockResponseLength = 0.ToString ();

            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content.Headers.Add ("Content-Length", mockResponseLength);
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
            });

            /* Content-Length is missing --> must not require content type */
            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.OK;
            }, request => {
                Assert.AreEqual (mockRequestLength, request.Content.Headers.ContentLength.ToString (), "request Content-Length should match expected");
            });
        }

        [Test]
        public void StatusCodeFound ()
        {
            // Status Code -- Found (302)
            PerformHttpOperationWithSettings (sm => {

            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.Found;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeBadRequest ()
        {
            // Status Code -- Bad Request (400)
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        // I don't see any reason this test should be failing. TODO Investigate further.
//        [Test]
        public void StatusCodeUnauthorized ()
        {
            // Status Code -- Unauthorized (401)
            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.Unauthorized;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeForbidden ()
        {
            // Status Code -- Forbidden (403)
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.Forbidden;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeNotFound ()
        {
            // Status Code -- NotFound (404)
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCode449 ()
        {
            // Status Code -- Retry With Status Code (449)
            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = (System.Net.HttpStatusCode)449;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeInternalServerError ()
        {
            // Status Code -- Internal Server Error (500)
            PerformHttpOperationWithSettings (sm => {
            }, response => {
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCode501 ()
        {
            // Status Code -- Command Not Implemented (501)
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                response.StatusCode = (System.Net.HttpStatusCode)501;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }


        private uint AssertRetry (uint retryCount)
        {
            if (retryCount > 0) {
                Assert.NotNull (BaseMockOwner.Status);
                Assert.AreEqual (NcResult.SubKindEnum.Info_ServiceUnavailable, BaseMockOwner.Status.SubKind, "Should set post StatusInd after 503");
            }
            Assert.False (retryCount > 2, "Retry count should not exceed required number of retries");
            return retryCount++;
        }

        [Test]
        public void StatusCode503 ()
        {
            // A 503 with no retry-after and no X-MS-ASThrottle.
            McMutables.Set (2, "HTTP", "DelaySeconds", (1).ToString ());
            McMutables.Set (2, "HTTP", "MaxDelaySeconds", (3).ToString ());

            uint retryCount = 0;
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                retryCount = AssertRetry (retryCount);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }, request => {});

            DoReportCommResultWithDateTime ();
        }

        [Test]
        public void StatusCode503RetryAfter ()
        {
            // A 503 with retry-after.
            McMutables.Set (2, "HTTP", "ThrottleDelaySeconds", (1).ToString ());
            McMutables.Set (2, "HTTP", "MaxDelaySeconds", (3).ToString ());
            string retryAfterSecs = (1).ToString ();

            string HeaderRetryAfter = "Retry-After";
            string HeaderXMsThrottle = "X-MS-ASThrottle";

            bool isThrottlingSet = false;
            BaseMockOwner.StatusIndCallback += (result) => {
                if (result.SubKind == NcResult.SubKindEnum.Info_ExplicitThrottling) {
                    isThrottlingSet = true;
                }
            };

            bool hasBeenThrottled = false;
            uint retryCount = 0;
            var stopwatch = new System.Diagnostics.Stopwatch ();
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                if (!hasBeenThrottled) {
                    response.Headers.Add (HeaderRetryAfter, retryAfterSecs);
                    response.Headers.Add (HeaderXMsThrottle, "UnknownReason");
                    hasBeenThrottled = true;
                    stopwatch.Start ();
                } else {
                    stopwatch.Stop ();
                    Assert.True (stopwatch.ElapsedMilliseconds >= 1000, "Should not retry until at least retry after time");
                    Assert.AreEqual (McProtocolState.AsThrottleReasons.Unknown, Context.ProtocolState.AsThrottleReason, "Should set throttle reason");
                    Assert.True (isThrottlingSet, "Should send throttling message to StatusInd");
                    retryCount = AssertRetry (retryCount);
                }
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }, request => {});

            DoReportCommResultWithDateTime ();
        }
            
        [Test]
        public void StatusCode503Throttle ()
        {
            // A 503 with no retry-after and X-MS-ASThrottle.
            McMutables.Set (2, "HTTP", "ThrottleDelaySeconds", (1).ToString ());
            McMutables.Set (2, "HTTP", "MaxDelaySeconds", (3).ToString ());

            string HeaderXMsThrottle = "X-MS-ASThrottle";

            bool isThrottlingSet = false;
            BaseMockOwner.StatusIndCallback += (result) => {
                if (result.SubKind == NcResult.SubKindEnum.Info_ExplicitThrottling) {
                    isThrottlingSet = true;
                }
            };

            uint retryCount = 0;
            bool hasBeenThrottled = false;
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                if (!hasBeenThrottled) {
                    response.Headers.Add (HeaderXMsThrottle, "UnknownReason");
                    hasBeenThrottled = true;
                } else {
                    Assert.AreEqual (McProtocolState.AsThrottleReasons.Unknown, Context.ProtocolState.AsThrottleReason, "Should set throttle reason");
                    Assert.True (isThrottlingSet, "Should send throttling message to StatusInd");
                    retryCount = AssertRetry (retryCount);
                }
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }, request => {});

            DoReportCommResultWithDateTime ();
        }

        [Test]
        public void StatusCode507 ()
        {
            // Status Code -- Server out of Space (507)
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
                response.StatusCode = (System.Net.HttpStatusCode)507;
            }, request => {
            });

            DoReportCommResultWithNonGeneralFailure ();
        }

        [Test]
        public void StatusCodeUnknown ()
        {
            // Unknown status code
            PerformHttpOperationWithSettings (sm => {
                sm.PostEvent ((uint)SmEvt.E.Launch, "MoveToFailureMachine");
            }, response => {
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

        private void DoReportCommResultWithDateTime ()
        {
            var mockCommStatus = MockNcCommStatus.Instance;
            Assert.True (mockCommStatus.DelayUntil > DateTime.UtcNow, "Should delay until a time later than the present");
        }

        // Test that comm status' are reported correctly by each status code method
        // Allow the type of failure (general/non-general) to be set by the caller
        private void DoReportCommResultWithFailureType (Func <bool> failureAction)
        {
            var mockCommStatus = MockNcCommStatus.Instance;

            bool didFailGenerally = failureAction ();

            Assert.AreEqual (didFailGenerally, mockCommStatus.DidFailGenerally, "Should set MockNcCommStatus Correctly");
            Assert.AreEqual (CommonMockData.Host, mockCommStatus.Host);
        }

        private void PerformHttpOperationWithSettings (Action<NcStateMachine> provideSm, Action<HttpResponseMessage> provideResponse, Action<HttpRequestMessage> provideRequest)
        {
            var autoResetEvent = new AutoResetEvent(false);
            string errorString = null;
            // setup
            NcStateMachine sm = CreatePhonySM (
                () => {
                    autoResetEvent.Set ();
                },
                (message) => {
                    errorString = message;
                }
            );

            provideSm (sm);

            // do some common assertions
            ExamineRequestMessageOnMockClient (CommonMockData.MockUri, request => {
                provideRequest (request);
            });

            // provides the mock response
            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                // create the response, then allow caller to set headers,
                // then return response and assign to mockResponse
                return CreateMockResponse (CommonMockData.Wbxml, response => {
                    provideResponse (response);   
                });
            };

            McServer server = new McServer () {
                Capabilities = McAccount.ActiveSyncCapabilities,
                Host = "foo.utopiasystems.net",
            };
            Context = new MockContext (null, protoControl : null, server : server);

            // provides the mock owner
            BaseMockOwner owner = CreateMockOwner (CommonMockData.MockUri, CommonMockData.MockRequestXml);

            var op = new AsHttpOperation ("Ping", owner, Context);

            var mockCommStatusInstance = MockNcCommStatus.Instance;
            op.NcCommStatusSingleton = mockCommStatusInstance;
            AsHttpOperation.HttpClientType = typeof (MockHttpClient);
            owner.ProcessResponseStandin = (sender, response, doc) => {
                Assert.AreSame (op, sender, "Owner's sender and AsHttpOperation should match when response is processed");
                return Event.Create ((uint)SmEvt.E.Success, "BasicPhonyPingSuccess");
            };

            op.Execute (sm);

            bool didFinish = autoResetEvent.WaitOne (6000);
            Assert.IsNull (errorString, errorString);
            Assert.IsTrue (didFinish, "Operation did not finish");
        }

        // Action Delegate for creating a state machine
        private NcStateMachine CreatePhonySM (Action action, Action<string> errorIndicator)
        {
            var sm = new NcStateMachine ("PHONY") {
                Name = "BasicPhonyPing",
                LocalEventType = typeof(AsProtoControl.CtlEvt),
                LocalStateType = typeof(PhonySt),
                TransTable = new [] {
                    // the "start" state is used for tests where we expect not to fail.
                    new Node {State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.CtlEvt.E.UiSetCred,
                            (uint)AsProtoControl.CtlEvt.E.GetServConf,
                            (uint)AsProtoControl.CtlEvt.E.UiSetServConf,
                            (uint)AsProtoControl.CtlEvt.E.GetCertOk,
                            (uint)AsProtoControl.CtlEvt.E.UiCertOkYes,
                            (uint)AsProtoControl.CtlEvt.E.UiCertOkNo,
                            (uint)AsProtoControl.CtlEvt.E.ReFSync,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { 
                                Event = (uint)SmEvt.E.Launch, 
                                Act = delegate () {},
                                State = (uint)PhonySt.FailureTests },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = delegate () {
                                    action();
                                }, 
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = delegate () {
                                    errorIndicator ("Unexpected HardFail event");
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = delegate () {
                                    action();
                                },
                                State = (uint)St.Start },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = delegate () {
                                    action();
                                },
                                State = (uint)St.Start },
                        }
                    },
                    // The "FailureTests" state is used for tests where the HTTP operation fails in some manner.
                    new Node {State = (uint)PhonySt.FailureTests,
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                            (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.CtlEvt.E.UiSetCred,
                            (uint)AsProtoControl.CtlEvt.E.GetServConf,
                            (uint)AsProtoControl.CtlEvt.E.UiSetServConf,
                            (uint)AsProtoControl.CtlEvt.E.GetCertOk,
                            (uint)AsProtoControl.CtlEvt.E.UiCertOkYes,
                            (uint)AsProtoControl.CtlEvt.E.UiCertOkNo,
                            (uint)AsProtoControl.CtlEvt.E.ReFSync,
                            (uint)AsProtoControl.AsEvt.E.ReProv,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = delegate () {
                                    action();
                                },
                                State = (uint)St.Start,
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = delegate () {
                                    errorIndicator ("Unexpected Success event");
                                },
                                State = (uint)St.Start,
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = delegate () {
                                    action();
                                },
                                State = (uint)St.Start,
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = delegate () {
                                    action ();
                                },
                                State = (uint)St.Start,
                            },
                        }
                    },
                }
            };
            sm.Validate ();
            return sm;
        }

        public enum PhonySt : uint
        {
            FailureTests = (St.Last + 1),
            Last = FailureTests,
        };
           
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
