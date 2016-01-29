//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Model;
using System.Threading;

namespace Test.iOS
{
    public class Oauth2RefreshTest : INcProtoControlOwner
    {
        class TestOauthRefresh : Oauth2Refresh
        {
            public TestOauthRefresh () : base ()
            {
                Be.CredReqActive.Clear ();
                Be.Oauth2RefreshInstance = this;
            }

            public bool TokenRefreshSuccessCalled { get; set; }

            public bool TokenRefreshFailureCalled { get; set; }

            public bool RefreshOauth2Called { get; set; }

            public bool CredRespCalled { get; set; }

            public bool alertUiCalled { get; set; }

            public string alertUiCalledFrom { get; set; }

            public bool ChangeOauthRefreshTimerCalled { get; set; }

            public void Reset ()
            {
                TokenRefreshSuccessCalled = false;
                TokenRefreshFailureCalled = false;
                RefreshOauth2Called = false;
                alertUiCalled = false;
                alertUiCalledFrom = null;
                CredRespCalled = false;
                ChangeOauthRefreshTimerCalled = false;
            }

            protected override void alertUi (int accountId, string message)
            {
                alertUiCalled = true;
                alertUiCalledFrom = message;
            }

            protected override void TokenRefreshFailure (McCred cred)
            {
                TokenRefreshFailureCalled = true;
                base.TokenRefreshFailure (cred);
            }

            protected override void TokenRefreshSuccess (McCred cred)
            {
                TokenRefreshSuccessCalled = true;
                base.TokenRefreshSuccess (cred);
            }

            protected override void RefreshOauth2 (McCred cred)
            {
                RefreshOauth2Called = true;
            }

            public void CallCredReq (NcProtoControl sender)
            {
                Be.CredReq (sender);
            }

            protected override void CredResp (int accountId)
            {
                CredRespCalled = true;
                lock (Be.CredReqActive) {
                    Be.CredReqActive.Remove (accountId);
                }
            }

            public void FinishRequest (McCred cred, bool success)
            {
                if (!success) {
                    TokenRefreshFailure (cred);
                } else {
                    TokenRefreshSuccess (cred);
                }
            }

            public void TestRefreshAllTokens ()
            {
                RefreshAllDueTokens ();
            }

            public bool RefreshStarted (int accountId)
            {
                CredReqActiveState.CredReqActiveStatus status;
                return Be.CredReqActive.TryGetStatus (accountId, out status);
            }

            public CredReqActiveState.State GetReqActiveState (int accountId)
            {
                CredReqActiveState.CredReqActiveStatus status;
                Assert.True (Be.CredReqActive.TryGetStatus (accountId, out status));
                return status.State;
            }

            public uint GetReqActiveRefreshRetries (int accountId)
            {
                CredReqActiveState.CredReqActiveStatus status;
                Assert.True (Be.CredReqActive.TryGetStatus (accountId, out status));
                return status.PostExpiryRefreshRetries;
            }

            public bool GetReqStatusNeedCredResp (int accountId)
            {
                CredReqActiveState.CredReqActiveStatus status;
                Assert.True (Be.CredReqActive.TryGetStatus (accountId, out status));
                return status.NeedCredResp;
            }

            public bool HaveCredReqActive (int accountId)
            {
                CredReqActiveState.CredReqActiveStatus status;
                return Be.CredReqActive.TryGetStatus (accountId, out status);
            }

            protected override void ChangeOauthRefreshTimer (int nextUpdate)
            {
                ChangeOauthRefreshTimerCalled = true;
            }
        }

        #region INcProtoControlOwner implementation

        public McAccount Account { get; set; }

        public McCred Cred { get; set; }

        NcProtoControl ProtoControl { get; set; }

        public void StatusInd (NcProtoControl sender, NachoCore.Utils.NcResult status)
        {
            throw new NotImplementedException ();
        }

        public void StatusInd (NcProtoControl sender, NachoCore.Utils.NcResult status, string[] tokens)
        {
            throw new NotImplementedException ();
        }

        public void CredReq (NcProtoControl sender)
        {
            throw new NotImplementedException ();
        }

        public void ServConfReq (NcProtoControl sender, BackEnd.AutoDFailureReasonEnum arg)
        {
            throw new NotImplementedException ();
        }

        public void CertAskReq (NcProtoControl sender, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
        {
            throw new NotImplementedException ();
        }

        public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
        {
            throw new NotImplementedException ();
        }

        public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
        {
            throw new NotImplementedException ();
        }

        public void BackendAbateStart ()
        {
            throw new NotImplementedException ();
        }


        public void BackendAbateStop ()
        {
            throw new NotImplementedException ();
        }

        #endregion

        uint expirySecs = 1;

        TestOauthRefresh MockRefresh { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount ();
            Account.Insert ();
            ProtoControl = new NcProtoControl (this, Account.Id);

            MockRefresh = new TestOauthRefresh ();
            MockRefresh.Reset ();
        }

        [TearDown]
        public void Teardown ()
        {
            Account.Delete ();
            NcModel.Instance.Db.DeleteAll<McCred> ();
        }

        McCred MakeOauth2Credential ()
        {
            var cred = new McCred () {
                AccountId = Account.Id,
                CredType = McCred.CredTypeEnum.OAuth2,
                ExpirySecs = expirySecs,
                Expiry = DateTime.UtcNow.AddSeconds (expirySecs),
            };
            cred.Insert ();
            return cred;
        }

        McCred MakePasswordCredential ()
        {
            var cred = new McCred () {
                AccountId = Account.Id,
                CredType = McCred.CredTypeEnum.Password,
            };
            cred.Insert ();
            return cred;
        }

        [Test]
        public void TestPasswordNoOauth ()
        {
            Cred = MakePasswordCredential ();
            Assert.AreEqual (McCred.CredTypeEnum.Password, Cred.CredType);
            MockRefresh.CallCredReq (ProtoControl);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.False (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.GetReqStatusNeedCredResp (Account.Id));
            Assert.AreEqual ("NotCanRefresh", MockRefresh.alertUiCalledFrom);
        }

        [Test]
        public void TestCredReqRefreshFail ()
        {
            Cred = MakeOauth2Credential ();

            bool credReqCalled = false;
            // loop until we decide to pass the request up to the UI, which happens
            // after KOauth2RefreshMaxFailure iterations.
            for (var i = 0; i <= TestOauthRefresh.KOauth2RefreshMaxFailure; i++) {
                if (i == 0) {
                    Assert.IsTrue (WaitForTimerRefresh (Account.Id));
                } else {
                    MockRefresh.TestRefreshAllTokens ();
                }
                if (i == 1) {
                    // ProtoController has also noticed the cred is bad. It calls CredReq.
                    MockRefresh.CallCredReq (ProtoControl);
                    credReqCalled = true;
                    // since we haven't yet exhausted the retries, UI should not be alerted.
                    Assert.IsFalse (MockRefresh.alertUiCalled);
                }
                Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
                Assert.AreEqual (credReqCalled, MockRefresh.GetReqStatusNeedCredResp (Account.Id));
                Assert.IsFalse (MockRefresh.TokenRefreshSuccessCalled);
                Assert.IsFalse (MockRefresh.TokenRefreshFailureCalled);
                if (i >= TestOauthRefresh.KOauth2RefreshMaxFailure) {
                    Assert.IsFalse (MockRefresh.RefreshOauth2Called);
                    Assert.AreEqual (CredReqActiveState.State.NeedUI, MockRefresh.GetReqActiveState (Account.Id));
                } else {
                    Assert.IsTrue (MockRefresh.RefreshOauth2Called);
                    Assert.AreEqual (CredReqActiveState.State.AwaitingRefresh, MockRefresh.GetReqActiveState (Account.Id));
                }

                MockRefresh.Reset ();

                MockRefresh.FinishRequest (Cred, false);
                Assert.IsFalse (MockRefresh.TokenRefreshSuccessCalled);
                Assert.IsTrue (MockRefresh.TokenRefreshFailureCalled);
                var failedRetries = MockRefresh.GetReqActiveRefreshRetries (Account.Id);
                Assert.AreEqual (i + 1, failedRetries);
                Assert.IsFalse (MockRefresh.CredRespCalled);

                if (failedRetries >= TestOauthRefresh.KOauth2RefreshMaxFailure) {
                    Assert.IsTrue (MockRefresh.alertUiCalled);
                    Assert.AreEqual ("TokenRefreshFailure1", MockRefresh.alertUiCalledFrom);
                } else {
                    Assert.IsFalse (MockRefresh.alertUiCalled);
                }
                MockRefresh.Reset ();
            }

            // After we've passed the request up, refreshToken should no longer be called.
            MockRefresh.TestRefreshAllTokens ();
            Assert.IsFalse (MockRefresh.RefreshOauth2Called);
        }

        private bool WaitForTimerRefresh (int accountId)
        {
            // wait for the refresh to figure out it needs to refresh the token. 
            // Should be pretty quick, since the credential expiry is set to 1 second
            // (see expirySecs).
            for (var i = 0; i < 6; i++) {
                MockRefresh.TestRefreshAllTokens ();
                if (MockRefresh.RefreshStarted (accountId)) {
                    break;
                }
                Thread.Sleep (500); // 500 milliseconds
            }
            return MockRefresh.RefreshStarted (accountId);
        }

        [Test]
        public void TestNoCredReqRefreshFail ()
        {
            Cred = MakeOauth2Credential ();

            // loop until we decide to pass the request up to the UI, which happens
            // after KOauth2RefreshMaxFailure iterations.
            for (var i = 0; i <= TestOauthRefresh.KOauth2RefreshMaxFailure; i++) {
                if (i == 0) {
                    Assert.IsTrue (WaitForTimerRefresh (Account.Id));
                } else {
                    MockRefresh.TestRefreshAllTokens ();
                }
                Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
                Assert.IsFalse (MockRefresh.GetReqStatusNeedCredResp (Account.Id));
                Assert.IsFalse (MockRefresh.TokenRefreshSuccessCalled);
                Assert.IsFalse (MockRefresh.TokenRefreshFailureCalled);
                if (i >= TestOauthRefresh.KOauth2RefreshMaxFailure) {
                    Assert.IsFalse (MockRefresh.RefreshOauth2Called);
                    Assert.AreEqual (CredReqActiveState.State.NeedUI, MockRefresh.GetReqActiveState (Account.Id));
                } else {
                    Assert.IsTrue (MockRefresh.RefreshOauth2Called);
                    Assert.AreEqual (CredReqActiveState.State.AwaitingRefresh, MockRefresh.GetReqActiveState (Account.Id));
                }

                MockRefresh.Reset ();

                MockRefresh.FinishRequest (Cred, false);
                Assert.IsFalse (MockRefresh.TokenRefreshSuccessCalled);
                Assert.IsTrue (MockRefresh.TokenRefreshFailureCalled);
                var failedRetries = MockRefresh.GetReqActiveRefreshRetries (Account.Id);
                Assert.AreEqual (i + 1, failedRetries);
                Assert.IsFalse (MockRefresh.CredRespCalled);

                if (failedRetries >= TestOauthRefresh.KOauth2RefreshMaxFailure) {
                    Assert.IsTrue (MockRefresh.alertUiCalled);
                    Assert.AreEqual ("TokenRefreshFailure1", MockRefresh.alertUiCalledFrom);
                } else {
                    Assert.IsFalse (MockRefresh.alertUiCalled);
                }
                MockRefresh.Reset ();
            }

            // After we've passed the request up, refreshToken should no longer be called.
            MockRefresh.TestRefreshAllTokens ();
            Assert.IsFalse (MockRefresh.RefreshOauth2Called);
        }

        [Test]
        public void TestNoCredReqRefreshSuccess ()
        {
            Cred = MakeOauth2Credential ();

            Assert.IsTrue (WaitForTimerRefresh (Account.Id));
            Assert.IsTrue (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockRefresh.GetReqStatusNeedCredResp (Account.Id));
            Assert.IsFalse (MockRefresh.alertUiCalled);

            // finish the request
            MockRefresh.FinishRequest (Cred, true);
            Assert.IsTrue (MockRefresh.TokenRefreshSuccessCalled);
            Assert.IsFalse (MockRefresh.CredRespCalled);
            Assert.IsFalse (MockRefresh.HaveCredReqActive (Account.Id));
        }

        [Test]
        public void TestMultipleCredReq ()
        {
            Cred = MakeOauth2Credential ();

            MockRefresh.CallCredReq (ProtoControl);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.AreEqual (CredReqActiveState.State.AwaitingRefresh, MockRefresh.GetReqActiveState (Account.Id));
            Assert.IsTrue (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.GetReqStatusNeedCredResp (Account.Id));
            Assert.IsFalse (MockRefresh.alertUiCalled);
            MockRefresh.Reset ();

            // Subsequent CredReq gets ignored.
            MockRefresh.CallCredReq (ProtoControl);
            Assert.IsFalse (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsTrue (MockRefresh.GetReqStatusNeedCredResp (Account.Id));

            // finish the request
            MockRefresh.FinishRequest (Cred, true);
            Assert.IsTrue (MockRefresh.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockRefresh.CredRespCalled);
            Assert.IsFalse (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockRefresh.alertUiCalled);
        }

        [Test]
        public void TestTimerAndCredReq ()
        {
            Cred = MakeOauth2Credential ();

            Assert.IsTrue (WaitForTimerRefresh (Account.Id));
            Assert.IsTrue (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockRefresh.GetReqStatusNeedCredResp (Account.Id));

            MockRefresh.Reset ();
            MockRefresh.CallCredReq (ProtoControl);
            Assert.IsFalse (MockRefresh.RefreshOauth2Called);
            Assert.IsTrue (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsTrue (MockRefresh.GetReqStatusNeedCredResp (Account.Id));

            // finish the request
            MockRefresh.FinishRequest (Cred, true);
            Assert.IsTrue (MockRefresh.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockRefresh.CredRespCalled);
            Assert.IsFalse (MockRefresh.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockRefresh.alertUiCalled);
        }
    }
}

