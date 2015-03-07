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

        public new string ProtocolToString (PushAssistProtocol protocol)
        {
            return base.ProtocolToString (protocol);
        }

        public WrapPushAssist (IPushAssistOwner owner) : base (owner)
        {
        }
    }

    public class _PushAssistTest : NcTestBase
    {
        public byte[] DeviceToken = System.Text.Encoding.ASCII.GetBytes ("abcdef");
        const string ClientToken = "us-1-east:12345";
        const string Email = "bob@company.net";
        const string Password = "Password";
        const string SessionToken = "xyz";

        private MockPushAssistOwnwer Owner;
        private WrapPushAssist Wpa;

        public _PushAssistTest ()
        {
        }

        [SetUp]
        public void Setup ()
        {
            // Set up a device account for device token
            var deviceAccount = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Device,
            };
            deviceAccount.Insert ();

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
        }

        [TearDown]
        public void Teardown ()
        {
            NcApplication.Instance.TestOnlyInvokeUseCurrentThread = false;
            NcApplication.Instance.ClientId = null;
        }

        private void WaitForState (uint expectedState)
        {
            DateTime now = DateTime.UtcNow;
            while (expectedState != Wpa.State) {
                Thread.Sleep (100);
                if (1000 < (DateTime.UtcNow - now).TotalMilliseconds) {
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
            CheckContent (MockPushAssistOwnwer.RequestData, jsonRequest.HttpRequestData);
            CheckContent (MockPushAssistOwnwer.ResponseData, jsonRequest.HttpNoChangeReply);
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
            Wpa.SetDeviceToken (DeviceToken);
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
            Wpa.SetDeviceToken (DeviceToken);
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
            Wpa.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            WaitForState ((uint)St.Start);
            Wpa.Execute ();

            WaitForState ((uint)PushAssist.Lst.Active);
        }

        [Test]
        public void StartSessionWithError ()
        {
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            PushAssist.MinDelayMsec = 100;
            PushAssist.MaxDelayMsec = 100;
            int numErrors = 0;
            MockHttpClient.ProvideHttpResponseMessage = (request) => {
                numErrors++;
                if (3 > numErrors) {
                    return StartSessionErrorResponse (request);
                }
                return StartSessionOkResponse (request);
            };
            Wpa.SetDeviceToken (DeviceToken);
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
    }
}

