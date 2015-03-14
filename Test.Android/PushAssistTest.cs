//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using NUnit.Framework;
using Test.iOS;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class MockPushAssistOwnwer : MockContext, IPushAssistOwner
    {
        public const string ContentType = "application/vnd.ms-sync.wbxml";
        public const string Cookie = "123ABC";
        public const string ServerUrl = "https://mail.company.com";
        public static byte[] RequestData = System.Text.Encoding.UTF8.GetBytes ("RequestData");
        public static byte[] ResponseData = System.Text.Encoding.UTF8.GetBytes ("ResponseData");
        public const int ResponseTimeout = 600 * 1000;
        public const int WaitBeforeUse = 60 * 1000;

        public MockPushAssistOwnwer ()
        {
        }

        public PushAssistParameters PushAssistParameters ()
        {
            var request = new HttpRequestMessage ();
            request.Headers.Add ("Cookie", Cookie);
            request.Content = new StringContent ("abc");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse (ContentType);
            request.Content.Headers.ContentLength = ResponseData.Length;

            return new PushAssistParameters () {
                RequestUrl = ServerUrl,
                RequestData = RequestData,
                RequestHeaders = request.Headers,
                ContentHeaders = request.Content.Headers,
                NoChangeResponseData = ResponseData,
                Protocol = PushAssistProtocol.ACTIVE_SYNC,
                ResponseTimeoutMsec = ResponseTimeout,
                WaitBeforeUseMsec = WaitBeforeUse,
            };
        }
    }

    // A simple class that wraps PushAssist so it can expose some internal states
    // in unit tests
    public class WrapPushAssist : PushAssist
    {
        public uint State {
            get {
                return Sm.State;
            }
        }

        public new string StartSessionUrl {
            get {
                return base.StartSessionUrl;
            }
        }

        public new string DeferSessionUrl {
            get {
                return base.DeferSessionUrl;
            }
        }

        public new string StopSessionUrl {
            get {
                return base.StopSessionUrl;
            }
        }

        public string ClientContext {
            get {
                return base.GetClientContext (Owner.Account);
            }
        }

        public new string SessionToken {
            get {
                return base.SessionToken;
            }
        }

        public int EventQueueDepth {
            get {
                return Sm.EventQueueDepth ();
            }
        }

        public new string ProtocolToString (PushAssistProtocol protocol)
        {
            return base.ProtocolToString (protocol);
        }

        public WrapPushAssist (IPushAssistOwner owner) : base (owner)
        {
        }
    }

    public class PushAssistTest : NcTestBase
    {
        public string DeviceToken = Convert.ToBase64String (System.Text.Encoding.ASCII.GetBytes ("abcdef"));
        const string ClientToken = "us-1-east:12345";
        const string Email = "bob@company.net";
        const string Password = "Password";
        const string SessionToken = "xyz";

        private MockPushAssistOwnwer Owner;
        private WrapPushAssist Wpa;
        private int OriginalMinDelayMsec;
        private int OriginalIncrementalDelayMsec;

        public PushAssistTest ()
        {
        }

        [SetUp]
        public void Setup ()
        {
            Telemetry.ENABLED = false;

            // Set up credential
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
                AccountService = McAccount.AccountServiceEnum.Exchange,
                EmailAddr = Email,
            };
            account.Insert ();
            var cred = new McCred () {
                AccountId = account.Id,
                Username = Email,
            };
            cred.Insert ();
            cred.UpdatePassword (Password);

            Owner = new MockPushAssistOwnwer ();
            Owner.Account = account;
            PushAssist.HttpClientType = typeof(MockHttpClient);
            Wpa = new WrapPushAssist (Owner);
            NcApplication.Instance.TestOnlyInvokeUseCurrentThread = true;

            OriginalMinDelayMsec = PushAssist.MinDelayMsec;
            OriginalIncrementalDelayMsec = PushAssist.IncrementalDelayMsec;
            PushAssist.MinDelayMsec = 100;
            PushAssist.IncrementalDelayMsec = 100;
        }

        [TearDown]
        public void Teardown ()
        {
            Wpa.Dispose ();
            Wpa = null;
            NcApplication.Instance.TestOnlyInvokeUseCurrentThread = false;
            NcApplication.Instance.ClientId = null;
            PushAssist.MinDelayMsec = OriginalMinDelayMsec;
            PushAssist.IncrementalDelayMsec = OriginalIncrementalDelayMsec;
            PushAssist.SetDeviceToken (null);
            Telemetry.ENABLED = true;
        }

        private void WaitForState (uint expectedState)
        {
            DateTime now = DateTime.UtcNow;
            while ((0 < Wpa.EventQueueDepth) || (expectedState != Wpa.State)) {
                Thread.Sleep (100);
                if (3000 < (DateTime.UtcNow - now).TotalMilliseconds) {
                    Assert.AreEqual (expectedState, Wpa.State);
                }
            }
            Assert.AreEqual (expectedState, Wpa.State);
        }

        private void CheckHttpHeader<T> (HttpHeaders headers, string field, T value)
        {
            IEnumerable<string> values;
            Assert.True (headers.TryGetValues (field, out values));
            var valueList = values.ToList ();
            Assert.AreEqual (1, valueList.Count);
            Assert.AreEqual (value.ToString (), valueList [0]);
        }

        private void CheckContent (byte[] bytes, string b64)
        {
            Assert.AreEqual (Convert.ToBase64String (bytes), b64);
        }

        private async void CheckStartSessionRequest (HttpRequestMessage httpRequest)
        {
            Assert.AreEqual (Wpa.StartSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Content.Headers, "Content-Type", "application/json");

            var jsonContent = await httpRequest.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var jsonRequest = JsonConvert.DeserializeObject <StartSessionRequest> (jsonContent);

            Assert.AreEqual (NcApplication.Instance.ClientId, jsonRequest.ClientId);
            Assert.AreEqual (Wpa.ClientContext, jsonRequest.ClientContext);
            Assert.AreEqual (MockPushAssistOwnwer.ServerUrl, jsonRequest.MailServerUrl);
            Assert.AreEqual (Email, jsonRequest.MailServerCredentials.Username);
            Assert.AreEqual (Password, jsonRequest.MailServerCredentials.Password);
            Assert.AreEqual (Wpa.ProtocolToString (PushAssistProtocol.ACTIVE_SYNC), jsonRequest.Protocol);
            Assert.True (jsonRequest.HttpHeaders.ContainsKey ("Cookie"));
            Assert.AreEqual (MockPushAssistOwnwer.Cookie, jsonRequest.HttpHeaders ["Cookie"]);
            Assert.AreEqual (MockPushAssistOwnwer.ResponseTimeout, jsonRequest.ResponseTimeout);
            Assert.AreEqual (MockPushAssistOwnwer.WaitBeforeUse, jsonRequest.WaitBeforeUse);
            CheckContent (MockPushAssistOwnwer.RequestData, jsonRequest.RequestData);
            CheckContent (MockPushAssistOwnwer.ResponseData, jsonRequest.NoChangeReply);
        }

        private async void CheckDeferSessionRequest (HttpRequestMessage httpRequest)
        {
            Assert.AreEqual (Wpa.DeferSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Content.Headers, "Content-Type", "application/json");

            var jsonContent = await httpRequest.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var jsonRequest = JsonConvert.DeserializeObject <DeferSessionRequest> (jsonContent);

            Assert.AreEqual (NcApplication.Instance.ClientId, jsonRequest.ClientId);
            Assert.AreEqual (Wpa.ClientContext, jsonRequest.ClientContext);
            Assert.AreEqual (Wpa.SessionToken, jsonRequest.Token);
            Assert.AreEqual (MockPushAssistOwnwer.ResponseTimeout, jsonRequest.ResponseTimeout);
        }

        private async void CheckStopSessionRequest (HttpRequestMessage httpRequest)
        {
            Assert.AreEqual (Wpa.StopSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Content.Headers, "Content-Type", "application/json");

            var jsonContent = await httpRequest.Content.ReadAsStringAsync ().ConfigureAwait (false);
            var jsonRequest = JsonConvert.DeserializeObject <StopSessionRequest> (jsonContent);

            Assert.AreEqual (NcApplication.Instance.ClientId, jsonRequest.ClientId);
            Assert.AreEqual (Wpa.ClientContext, jsonRequest.ClientContext);
            Assert.AreEqual (Wpa.SessionToken, jsonRequest.Token);
        }

        private StringContent EncodeJsonResponse (PingerResponse jsonResponse)
        {
            var jsonContent = JsonConvert.SerializeObject (jsonResponse);
            var content = new StringContent (jsonContent);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse ("application/json");
            content.Headers.ContentLength = jsonContent.Length;
            return content;
        }

        private HttpResponseMessage PingerResponse (HttpRequestMessage httpRequest, string status, string message, string token = null)
        {
            var httpResponse = new HttpResponseMessage ();
            httpResponse.StatusCode = System.Net.HttpStatusCode.OK;

            var jsonResponse = new PingerResponse () {
                Status = status,
                Message = message,
                Token = token,
            };
            httpResponse.Content = EncodeJsonResponse (jsonResponse);

            return httpResponse;
        }

        private HttpResponseMessage StartSessionOkResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "", SessionToken);
        }

        private HttpResponseMessage StartSessionWarnResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "WARN", "Something is not right", SessionToken);
        }

        private HttpResponseMessage StartSessionErrorResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "ERROR", "Oh crap!");
        }

        private HttpResponseMessage DeferSessionOkResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "");
        }

        private HttpResponseMessage DeferSessionWarnResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "WARN", "Something is not right");
        }

        private HttpResponseMessage DeferSessionErrorResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "ERROR", "Oh crap!");
        }

        private HttpResponseMessage StopSessionOkResponse (HttpRequestMessage httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "");
        }

        private HttpResponseMessage HttpErrorPingerResponse (HttpRequestMessage httpRequest)
        {
            return new HttpResponseMessage () {
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
            };
        }

        private HttpResponseMessage NetworkErrorPingerResponse (HttpRequestMessage httpRequest)
        {
            throw new System.Net.WebException ("Network blows up");
        }

        // Start -> DevTokW -> CliTokW -> SessTokW -> Active
        // where both device and client tokens are not immediately available
        [Test]
        public void StartupWithoutTokens ()
        {
            // Start
            WaitForState ((uint)St.Start);

            // -> DevTokW
            Wpa.Execute ();
            WaitForState ((uint)PushAssist.Lst.DevTokW);

            // [got device token] -> CliTokW
            PushAssist.SetDeviceToken (DeviceToken);
            WaitForState ((uint)PushAssist.Lst.CliTokW);

            // [got client token] -> SessTokW -> Active
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StartSessionOkResponse;
            NcApplication.Instance.ClientId = ClientToken;
            WaitForState ((uint)PushAssist.Lst.Active);

            // [defer] -> Active
            MockHttpClient.ExamineHttpRequestMessage = CheckDeferSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = DeferSessionOkResponse;
            Wpa.Defer ();
            WaitForState ((uint)PushAssist.Lst.Active);

            // [stop] -> Start
            MockHttpClient.ExamineHttpRequestMessage = CheckStopSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StopSessionOkResponse;
            Wpa.Stop ();
            WaitForState ((uint)St.Start);
        }

        [Test]
        public void StartupWithTokens ()
        {
            // Set device and client tokens first
            PushAssist.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            // Start
            WaitForState ((uint)St.Start);

            // -> SessTokW -> Active
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StartSessionOkResponse;
            Wpa.Execute ();
            WaitForState ((uint)PushAssist.Lst.Active);

            // -> Active
            MockHttpClient.ExamineHttpRequestMessage = CheckDeferSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = DeferSessionOkResponse;
            Wpa.Defer ();
            WaitForState ((uint)PushAssist.Lst.Active);

            // -> Start
            MockHttpClient.ExamineHttpRequestMessage = CheckStopSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StopSessionOkResponse;
            Wpa.Stop ();
            WaitForState ((uint)St.Start);
        }

        [Test]
        public void StartSessionWithWarning ()
        {
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StartSessionWarnResponse;
            PushAssist.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            WaitForState ((uint)St.Start);
            Wpa.Execute ();

            WaitForState ((uint)PushAssist.Lst.Active);
        }

        private void StartSessionWithErrors (
            MockHttpClient.ExamineHttpRequestMessageDelegate checkDelegate,
            MockHttpClient.ProvideHttpResponseMessageDelegate respondDelegate)
        {
            MockHttpClient.ExamineHttpRequestMessage = checkDelegate;
            PushAssist.MinDelayMsec = 100;
            PushAssist.MaxDelayMsec = 100;
            int numErrors = 0;
            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                numErrors++;
                if (3 > numErrors) {
                    return respondDelegate (request);
                }
                return StartSessionOkResponse (request);
            };
            PushAssist.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            WaitForState ((uint)St.Start);
            Wpa.Execute ();
            WaitForState ((uint)PushAssist.Lst.Active);
            Assert.AreEqual (3, numErrors);

            MockHttpClient.ExamineHttpRequestMessage = CheckStopSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StopSessionOkResponse;
            Wpa.Stop ();
            WaitForState ((uint)St.Start);
        }

        [Test]
        public void StartSessionWithPingerError ()
        {
            StartSessionWithErrors (CheckStartSessionRequest, StartSessionErrorResponse);
        }

        [Test]
        public void StartSessionWithHttpError ()
        {
            StartSessionWithErrors (CheckStartSessionRequest, HttpErrorPingerResponse);
        }

        [Test]
        public void StartSessionWithNetworkError ()
        {
            StartSessionWithErrors (CheckStartSessionRequest, NetworkErrorPingerResponse);
        }

        private void DeferSessionWithErrors (
            MockHttpClient.ExamineHttpRequestMessageDelegate[] checkDelegate,
            MockHttpClient.ProvideHttpResponseMessageDelegate[] respondDelegate)
        {
            NcAssert.True (checkDelegate.Length == respondDelegate.Length);

            // Go from Start to Active 
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StartSessionOkResponse;
            PushAssist.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            WaitForState ((uint)St.Start);
            Wpa.Execute ();
            WaitForState ((uint)PushAssist.Lst.Active);

            // In Active, start a defer
            int numRequests = 0;
            MockHttpClient.ExamineHttpRequestMessage = checkDelegate [0];
            PushAssist.MinDelayMsec = 100;
            PushAssist.MaxDelayMsec = 100;
            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                numRequests++;
                if (checkDelegate.Length > numRequests) {
                    MockHttpClient.ExamineHttpRequestMessage = checkDelegate [numRequests];
                }
                return respondDelegate [numRequests - 1] (request);
            };
            Wpa.Defer ();
            DateTime now = DateTime.UtcNow;
            while (respondDelegate.Length > numRequests) {
                Thread.Sleep (100);
                if (3000 < (DateTime.UtcNow - now).TotalMilliseconds) {
                    break;
                }
            }
            Assert.AreEqual (respondDelegate.Length, numRequests);
            WaitForState ((uint)PushAssist.Lst.Active);

            // Go to Stop
            MockHttpClient.ExamineHttpRequestMessage = CheckStopSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StopSessionOkResponse;
            Wpa.Stop ();
            WaitForState ((uint)St.Start);
        }

        [Test]
        public void DeferSessionWithHttpErrors ()
        {
            DeferSessionWithErrors (
                new MockHttpClient.ExamineHttpRequestMessageDelegate [3] { 
                    CheckDeferSessionRequest, 
                    CheckDeferSessionRequest, 
                    CheckDeferSessionRequest, 
                },
                new MockHttpClient.ProvideHttpResponseMessageDelegate [3] {
                    HttpErrorPingerResponse,
                    HttpErrorPingerResponse,
                    HttpErrorPingerResponse,
                }
            );
        }

        [Test]
        public void DeferSessionWithNetworkError ()
        {
            DeferSessionWithErrors (
                new MockHttpClient.ExamineHttpRequestMessageDelegate [3] { 
                    CheckDeferSessionRequest,
                    CheckDeferSessionRequest, 
                    CheckDeferSessionRequest, 
                },
                new MockHttpClient.ProvideHttpResponseMessageDelegate [3] {
                    NetworkErrorPingerResponse,
                    NetworkErrorPingerResponse,
                    NetworkErrorPingerResponse,
                }
            );
        }

        [Test]
        public void DeferSessionWithPingerError ()
        {
            DeferSessionWithErrors (
                new MockHttpClient.ExamineHttpRequestMessageDelegate [2] { 
                    CheckDeferSessionRequest,
                    CheckStartSessionRequest, 
                },
                new MockHttpClient.ProvideHttpResponseMessageDelegate [2] {
                    DeferSessionErrorResponse,
                    StartSessionOkResponse,
                }
            );
        }
    }
}

