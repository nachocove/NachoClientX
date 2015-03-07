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

        private HttpResponseMessage StartSessionOkResponse (HttpRequestMessage httpRequest)
        {
            var httpResponse = new HttpResponseMessage ();
            httpResponse.StatusCode = System.Net.HttpStatusCode.OK;
            var jsonResponse = new PingerResponse () {
                Status = "OK",
                Message = "",
            };
            var jsonContent = JsonConvert.SerializeObject (jsonResponse);
            var content = new StringContent (jsonContent);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse ("application/json");
            content.Headers.ContentLength = jsonContent.Length;
            httpResponse.Content = content;

            return httpResponse;
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
        }
    }
}

