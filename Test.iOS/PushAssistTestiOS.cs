//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using NUnit.Framework;
using Test.iOS;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoPlatform;
using System.IO;
using System.Text;
using System.Net;

namespace Test.Common
{
    public class MockPushAssistOwnwer : IBEContext, IPushAssistOwner
    {
        public const string ContentType = "application/vnd.ms-sync.wbxml";
        public const string Cookie = "123ABC";
        public const string ServerUrl = "https://mail.company.com";
        public static byte[] RequestData = System.Text.Encoding.UTF8.GetBytes ("RequestData");
        public static byte[] ResponseData = System.Text.Encoding.UTF8.GetBytes ("ResponseData");
        public const int ResponseTimeout = 600 * 1000;
        public const int WaitBeforeUse = 60 * 1000;

        // IBEContext
        public INcProtoControlOwner Owner { set; get; }

        public NcProtoControl ProtoControl { set; get; }

        public McProtocolState ProtocolState { get; set; }

        public McServer Server { get; set; }

        public McAccount Account { get; set; }

        public McCred Cred { get; set; }

        public MockPushAssistOwnwer ()
        {
            Owner = new MockOwner ();
        }

        public PushAssistParameters PushAssistParameters ()
        {
            //request.Content = new StringContent ("abc");

            var headers = new NcHttpHeaders ();
            headers.Add ("Cookie", Cookie);
            headers.Add ("Content-Type", MediaTypeHeaderValue.Parse (ContentType).ToString ());
            headers.Add ("Content-Length", ResponseData.Length.ToString ());

            return new PushAssistParameters () {
                MailServerCredentials = new Credentials () {
                    Username = PushAssistTest.Email,
                    Password = PushAssistTest.Password,
                },
                RequestUrl = ServerUrl,
                RequestData = RequestData,
                RequestHeaders = headers,
                NoChangeResponseData = ResponseData,
                Protocol = PushAssistProtocol.ACTIVE_SYNC,
                ResponseTimeoutMsec = ResponseTimeout,
                WaitBeforeUseMsec = WaitBeforeUse,
            };
        }
    }

    // A simple class that wraps PushAssist so it can expose some internal states
    // in unit tests
    public class WrapPushAssist : PushAssistCommon
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

        public new static void SetDeviceToken (string token)
        {
            DeviceToken = token;
        }

        public WrapPushAssist (IPushAssistOwner owner) : base (owner)
        {
        }

        protected override INcHttpClient HttpClient {
            get {
                return MockHttpClient.Instance;
            }
        }
    }

    public class PushAssistTest : NcTestBase
    {
        public string DeviceToken = Convert.ToBase64String (System.Text.Encoding.ASCII.GetBytes ("abcdef"));
        const string ClientToken = "us-1-east:12345";
        public const string Email = "bob@company.net";
        public const string Password = "Password";
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
            Assert.IsFalse (Telemetry.ENABLED, "Telemetry needs to be disabled");
            NcTask.StartService ();

            // Set up credential
            var account = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Exchange,
                AccountService = McAccount.AccountServiceEnum.Exchange,
                EmailAddr = Email,
            };
            account.Insert ();
            var cred = new McCred () {
                CredType = McCred.CredTypeEnum.Password,
                AccountId = account.Id,
                Username = Email,
            };
            cred.Insert ();
            cred.UpdatePassword (Password);

            Owner = new MockPushAssistOwnwer ();
            Owner.Account = account;
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
            PushAssist.MinDelayMsec = OriginalMinDelayMsec;
            PushAssist.IncrementalDelayMsec = OriginalIncrementalDelayMsec;
            WrapPushAssist.SetDeviceToken (null); // do not use PushAssist.SetDeviceToken() as it creates a new task.
        }

        private void WaitForState (uint expectedState)
        {
            DateTime now = DateTime.UtcNow;
            while ((0 < Wpa.EventQueueDepth) || (expectedState != Wpa.State) || (0 < NcTask.TaskCount)) {
                Thread.Sleep (100);
                if (3000 < (DateTime.UtcNow - now).TotalMilliseconds) {
                    Assert.AreEqual (expectedState, Wpa.State);
                    Assert.AreEqual (0, NcTask.TaskCount);
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

        private void CheckStartSessionRequest (NcHttpRequest httpRequest)
        {
            Assert.AreEqual (Wpa.StartSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Headers, "Content-Type", "application/json");

            var jsonContent = httpRequest.GetContent ();
            var jsonRequest = JsonConvert.DeserializeObject <StartSessionRequest> (Encoding.UTF8.GetString (jsonContent));

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

        private void CheckDeferSessionRequest (NcHttpRequest httpRequest)
        {
            Assert.AreEqual (Wpa.DeferSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Headers, "Content-Type", "application/json");

            var jsonContent = httpRequest.GetContent ();
            var jsonRequest = JsonConvert.DeserializeObject <DeferSessionRequest> (Encoding.UTF8.GetString (jsonContent));

            Assert.AreEqual (NcApplication.Instance.ClientId, jsonRequest.ClientId);
            Assert.AreEqual (Wpa.ClientContext, jsonRequest.ClientContext);
            Assert.AreEqual (Wpa.SessionToken, jsonRequest.Token);
            Assert.AreEqual (MockPushAssistOwnwer.ResponseTimeout, jsonRequest.ResponseTimeout);
        }

        private void CheckStopSessionRequest (NcHttpRequest httpRequest)
        {
            Assert.AreEqual (Wpa.StopSessionUrl, httpRequest.RequestUri.AbsoluteUri);
            CheckHttpHeader<string> (httpRequest.Headers, "Content-Type", "application/json");

            var jsonContent = httpRequest.GetContent ();
            var jsonRequest = JsonConvert.DeserializeObject <StopSessionRequest> (Encoding.UTF8.GetString (jsonContent));

            Assert.AreEqual (NcApplication.Instance.ClientId, jsonRequest.ClientId);
            Assert.AreEqual (Wpa.ClientContext, jsonRequest.ClientContext);
            Assert.AreEqual (Wpa.SessionToken, jsonRequest.Token);
        }

        private NcHttpResponse PingerResponse (NcHttpRequest httpRequest, string status, string message, string token = null)
        {
            var jsonResponse = new PingerResponse () {
                Status = status,
                Message = message,
                Token = token,
            };
            var jsonContent = Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (jsonResponse));
            return new NcHttpResponse (httpRequest.Method, HttpStatusCode.OK, jsonContent, "application/json");
        }

        private NcHttpResponse StartSessionOkResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "", SessionToken);
        }

        private NcHttpResponse StartSessionWarnResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "WARN", "Something is not right", SessionToken);
        }

        private NcHttpResponse StartSessionErrorResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "ERROR", "Oh crap!");
        }

        private NcHttpResponse DeferSessionOkResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "");
        }

        private NcHttpResponse DeferSessionWarnResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "WARN", "Something is not right");
        }

        private NcHttpResponse DeferSessionErrorResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "ERROR", "Oh crap!");
        }

        private NcHttpResponse StopSessionOkResponse (NcHttpRequest httpRequest)
        {
            return PingerResponse (httpRequest, "OK", "");
        }

        private NcHttpResponse HttpErrorPingerResponse (NcHttpRequest httpRequest)
        {
            return new NcHttpResponse (httpRequest.Method, HttpStatusCode.InternalServerError);
        }

        private NcHttpResponse NetworkErrorPingerResponse (NcHttpRequest httpRequest)
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

            // [got device token] -> CliTokW -> SessTokW -> Active
            MockHttpClient.ExamineHttpRequestMessage = CheckStartSessionRequest;
            MockHttpClient.ProvideHttpResponseMessage = StartSessionOkResponse;
            PushAssist.SetDeviceToken (DeviceToken);
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

        [Test]
        public void NotificationPayloadTest ()
        {
            string payload = @"{""aps"":{""alert"":""Nacho says: Reregister!"",""content-available"":1,""sound"":""silent.wav""},""pinger"":{""ctxs"":{""d15902b3"":{""cmd"":""reg""}},""meta"":{""time"":""1428685273""}}}";
            var json = JsonConvert.DeserializeObject<Notification> (payload);
            // Verify the pinger section exists
            Assert.True (json.HasPingerSection ());
            var pinger = json.pinger;
            Assert.NotNull (pinger);

            // Verify timestamp
            Assert.NotNull (pinger.meta);
            var timestamp = DateTime.MaxValue;
            Assert.True (pinger.meta.HasTimestamp (out timestamp));
            Assert.AreEqual (2015, timestamp.Year);
            Assert.AreEqual (4, timestamp.Month);
            Assert.AreEqual (10, timestamp.Day);
            Assert.AreEqual (17, timestamp.Hour);
            Assert.AreEqual (1, timestamp.Minute);
            Assert.AreEqual (13, timestamp.Second);

            // Verify context
            Assert.AreEqual (1, pinger.ctxs.Count);
            var context = "d15902b3";
            Assert.True (pinger.ctxs.ContainsKey (context));
            var contextObj = pinger.ctxs [context];
            Assert.AreEqual (PingerContext.REGISTER, contextObj.cmd);
            Assert.Null (contextObj.ses);
        }

        private void CheckShouldNotify (bool[] expected, McEmailMessage[] emails, McAccount account)
        {
            Assert.AreEqual (expected.Length, emails.Length);
            for (int n = 0; n < expected.Length; n++) {
                Assert.AreEqual (expected [n], NotificationHelper.ShouldNotifyEmailMessage (emails [n]));
            }
        }

        [Test]
        public void NotificationConfigurationTest ()
        {
            // Create 4 contacts
            var names = new string [4, 2] {
                { "Bob", "Smith" },
                { "Mary", "Jane" }, // hot address
                { "John", "Doe" }, // VIP
                { "Tom", "Jones" }, // VIP & hot address
            };

            var contacts = new McContact[names.GetLength (0)];
            for (int n = 0; n < names.GetLength (0); n++) {
                contacts [n] = new McContact () {
                    AccountId = Owner.Account.Id,
                    FirstName = names [n, 0],
                    LastName = names [n, 1],
                };
                contacts [n].AddEmailAddressAttribute (Owner.Account.Id, "Email1Address", "Email",
                    contacts [n].FirstName.ToLower () + "@company.net");
                contacts [n].Insert ();
            }
            contacts [2].IsVip = true;
            contacts [2].Update ();
            contacts [3].IsVip = true;
            contacts [3].Update ();

            // Create 4 emails from the four contacts
            var emails = new McEmailMessage[names.GetLength (0)];
            for (int n = 0; n < names.GetLength (0); n++) {
                emails [n] = new McEmailMessage () {
                    AccountId = Owner.Account.Id,
                    From = contacts [n].EmailAddresses [0].Value,
                };
                emails [n].Insert ();
            }

            var inbox = McFolder.Create (Owner.Account.Id, false, false, true, "0", "made-up", "Inbox", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            inbox.Insert ();
            inbox.Link (emails [0]);
            inbox.Link (emails [1]);

            emails [1] = emails [1].UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.Score = 1.0;
                return true;
            });

            emails [3].UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.Score = 1.0;
                return true;
            });

            // Configuration #1 - Inbox
            var account = Owner.Account;
            account.NotificationConfiguration = McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64;
            account.Update ();
            CheckShouldNotify (new bool[4] { true, true, false, false }, emails, account);

            // Configuration #2 - Hot only
            account.NotificationConfiguration = McAccount.NotificationConfigurationEnum.ALLOW_HOT_2;
            account.Update ();
            CheckShouldNotify (new bool[4] { false, true, false, true }, emails, account);

            // Configuration #3 - VIP only
            account.NotificationConfiguration = McAccount.NotificationConfigurationEnum.ALLOW_VIP_4;
            account.Update ();
            CheckShouldNotify (new bool[4] { false, false, true, true }, emails, account);

            // Configuration #4 - VIP
            account.NotificationConfiguration =
                McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 | McAccount.NotificationConfigurationEnum.ALLOW_HOT_2;
            account.Update ();
            CheckShouldNotify (new bool[4] { false, true, true, true }, emails, account);
        }
    }
}

