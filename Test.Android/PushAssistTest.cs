//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Test.iOS;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class MockPushAssistOwnwer : MockContext, IPushAssistOwner
    {
        public MockPushAssistOwnwer ()
        {
        }

        public PushAssistParameters PushAssistParameters ()
        {
            var request = new HttpRequestMessage ();
            request.Headers.Add ("Cookie", "123ABC");
            request.Content.Headers.Add ("Content-type", "wbxml");

            return new PushAssistParameters () {
                RequestUrl = "https://mail.company.com",
                RequestData = System.Text.Encoding.UTF8.GetBytes ("RequestData"),
                RequestHeaders = request.Headers,
                ContentHeaders = request.Content.Headers,
                NoChangeResponseData = System.Text.Encoding.UTF8.GetBytes ("ResponseData"),
                Protocol = PushAssistProtocol.ACTIVE_SYNC,
                ResponseTimeoutMsec = 600 * 1000,
                WaitBeforeUseMsec = 1000,
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

        public WrapPushAssist (IPushAssistOwner owner) : base (owner)
        {
        }
    }

    public class _PushAssistTest : NcTestBase
    {
        public byte[] DeviceToken = System.Text.Encoding.ASCII.GetBytes ("abcdef");
        const string ClientToken = "us-1-east:12345";

        private MockPushAssistOwnwer Owner;
        private WrapPushAssist Wpa;

        public _PushAssistTest ()
        {
        }

        [SetUp]
        public void Setup ()
        {
            var deviceAccount = new McAccount () {
                AccountType = McAccount.AccountTypeEnum.Device,
            };
            deviceAccount.Insert ();
            Owner = new MockPushAssistOwnwer ();
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

            // [got client token] -> SessTokW
            NcApplication.Instance.ClientId = ClientToken;
            WaitForState ((uint)PushAssist.Lst.SessTokW);
        }

        [Test]
        public void StartupWithTokens ()
        {
            // Set device and client tokens first
            Wpa.SetDeviceToken (DeviceToken);
            NcApplication.Instance.ClientId = ClientToken;

            // Start
            WaitForState ((uint)St.Start);

            // -> SessTokW
            Wpa.Execute ();
            WaitForState ((uint)PushAssist.Lst.SessTokW);
        }
    }
}

