//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Model;
using System.Threading;
using SQLite;

namespace Test.iOS
{
    public class Oauth2RefreshTest : INcProtoControlOwner
    {
        public Oauth2RefreshTest ()
        {
        }

        class OauthRefreshMockBE : BackEnd
        {
            public OauthRefreshMockBE () : base ()
            {
                Oauth2RefreshCancelSource = new CancellationTokenSource ();
            }

            public bool TokenRefreshSuccessCalled { get; set; }
            public bool TokenRefreshFailureCalled { get; set; }
            public bool RefreshOauth2Called { get; set; }
            public bool CredRespCalled { get; set; }
            public bool alertUiCalled { get; set; }
            public string alertUiCalledFrom { get; set; }

            public void Reset ()
            {
                TokenRefreshSuccessCalled = false;
                TokenRefreshFailureCalled = false;
                RefreshOauth2Called = false;
                CredRespCalled = false;
                alertUiCalled = false;
                alertUiCalledFrom = null;
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

            protected override void ResetOauthRefreshTimer ()
            {
            }

            protected override void ChangeOauthRefreshTimer (long nextUpdate)
            {
            }

            protected override void RefreshOauth2 (McCred cred)
            {
                RefreshOauth2Called = true;
            }

            public void FinishRequest (McCred cred, bool success)
            {
                if (!success) {
                    TokenRefreshFailure (cred);
                } else {
                    TokenRefreshSuccess (cred);
                }
            }
            public override void CredResp (int accountId)
            {
                CredRespCalled = true;
                lock (CredReqActive) {
                    CredReqActive.Remove (accountId);
                }
            }

            public void TestRefreshAllTokens ()
            {
                RefreshAllDueTokens ();
            }

            public bool RefreshStarted (int accountId)
            {
                return CredReqActive.ContainsKey (accountId);
            }

            public BackEnd.CredReqActiveState GetReqActiveState (int accountId)
            {
                CredReqActiveStatus status;
                Assert.True (CredReqActive.TryGetValue (accountId, out status));
                return status.State;
            }

            public uint GetReqActiveRefreshRetries (int accountId)
            {
                CredReqActiveStatus status;
                Assert.True (CredReqActive.TryGetValue (accountId, out status));
                return status.RefreshRetries;
            }

            public bool GetReqStatusNeedCredResp (int accountId)
            {
                CredReqActiveStatus status;
                Assert.True (CredReqActive.TryGetValue (accountId, out status));
                return status.NeedCredResp;
            }

            public bool HaveCredReqActive (int accountId)
            {
                return CredReqActive.ContainsKey (accountId);
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
        OauthRefreshMockBE MockBe { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount () {
            };
            Account.Insert ();
            ProtoControl = new NcProtoControl (this, Account.Id);
            MockBe = new OauthRefreshMockBE ();
            MockBe.Reset ();
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
            MockBe.CredReq (ProtoControl);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.False (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));
            Assert.AreEqual ("NotCanRefresh", MockBe.alertUiCalledFrom);
        }

        [Test]
        public void TestCredReqRefreshFail ()
        {
            Cred = MakeOauth2Credential ();

            bool credReqCalled = false;
            // loop until we decide to pass the request up to the UI, which happens
            // after KOauth2RefreshMaxFailure iterations.
            for (var i = 0; i <= BackEnd.KOauth2RefreshMaxFailure; i++) {
                if (i == 0) {
                    Assert.IsTrue (WaitForTimerRefresh (Account.Id));
                } else {
                    MockBe.TestRefreshAllTokens ();
                }
                if (i == 1) {
                    // ProtoController has also noticed the cred is bad. It calls CredReq.
                    MockBe.CredReq (ProtoControl);
                    credReqCalled = true;
                    // since we haven't yet exhausted the retries, UI should not be alerted.
                    Assert.IsFalse (MockBe.alertUiCalled);
                }
                Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
                Assert.AreEqual (credReqCalled, MockBe.GetReqStatusNeedCredResp (Account.Id));
                Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
                Assert.IsFalse (MockBe.TokenRefreshFailureCalled);
                if (i >= BackEnd.KOauth2RefreshMaxFailure) {
                    Assert.IsFalse (MockBe.RefreshOauth2Called);
                    Assert.AreEqual (BackEnd.CredReqActiveState.CredReqActive_NeedUI, MockBe.GetReqActiveState (Account.Id));
                } else {
                    Assert.IsTrue (MockBe.RefreshOauth2Called);
                    Assert.AreEqual (BackEnd.CredReqActiveState.CredReqActive_AwaitingRefresh, MockBe.GetReqActiveState (Account.Id));
                }

                MockBe.Reset ();

                MockBe.FinishRequest (Cred, false);
                Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
                Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
                var failedRetries = MockBe.GetReqActiveRefreshRetries (Account.Id);
                Assert.AreEqual (i+1, failedRetries);
                Assert.IsFalse (MockBe.CredRespCalled);

                if (failedRetries >= BackEnd.KOauth2RefreshMaxFailure) {
                    Assert.IsTrue (MockBe.alertUiCalled);
                    Assert.AreEqual ("TokenRefreshFailure1", MockBe.alertUiCalledFrom);
                } else {
                    Assert.IsFalse (MockBe.alertUiCalled);
                }
                MockBe.Reset ();
            }

            // After we've passed the request up, refreshToken should no longer be called.
            MockBe.TestRefreshAllTokens ();
            Assert.IsFalse (MockBe.RefreshOauth2Called);
        }

        private bool WaitForTimerRefresh (int accountId)
        {
            // wait for the refresh to figure out it needs to refresh the token. 
            // Should be pretty quick, since the credential expiry is set to 1 second
            // (see expirySecs).
            for (var i = 0; i < 6; i++) {
                MockBe.TestRefreshAllTokens ();
                if (MockBe.RefreshStarted (accountId)) {
                    break;
                }
                Thread.Sleep (500); // 500 milliseconds
            }
            return MockBe.RefreshStarted (accountId);
        }

        [Test]
        public void TestNoCredReqRefreshFail ()
        {
            Cred = MakeOauth2Credential ();

            // loop until we decide to pass the request up to the UI, which happens
            // after KOauth2RefreshMaxFailure iterations.
            for (var i = 0; i <= BackEnd.KOauth2RefreshMaxFailure; i++) {
                if (i == 0) {
                    Assert.IsTrue (WaitForTimerRefresh (Account.Id));
                } else {
                    MockBe.TestRefreshAllTokens ();
                }
                Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
                Assert.IsFalse (MockBe.GetReqStatusNeedCredResp(Account.Id));
                Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
                Assert.IsFalse (MockBe.TokenRefreshFailureCalled);
                if (i >= BackEnd.KOauth2RefreshMaxFailure) {
                    Assert.IsFalse (MockBe.RefreshOauth2Called);
                    Assert.AreEqual (BackEnd.CredReqActiveState.CredReqActive_NeedUI, MockBe.GetReqActiveState (Account.Id));
                } else {
                    Assert.IsTrue (MockBe.RefreshOauth2Called);
                    Assert.AreEqual (BackEnd.CredReqActiveState.CredReqActive_AwaitingRefresh, MockBe.GetReqActiveState (Account.Id));
                }

                MockBe.Reset ();

                MockBe.FinishRequest (Cred, false);
                Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
                Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
                var failedRetries = MockBe.GetReqActiveRefreshRetries (Account.Id);
                Assert.AreEqual (i+1, failedRetries);
                Assert.IsFalse (MockBe.CredRespCalled);

                if (failedRetries >= BackEnd.KOauth2RefreshMaxFailure) {
                    Assert.IsTrue (MockBe.alertUiCalled);
                    Assert.AreEqual ("TokenRefreshFailure1", MockBe.alertUiCalledFrom);
                } else {
                    Assert.IsFalse (MockBe.alertUiCalled);
                }
                MockBe.Reset ();
            }

            // After we've passed the request up, refreshToken should no longer be called.
            MockBe.TestRefreshAllTokens ();
            Assert.IsFalse (MockBe.RefreshOauth2Called);
        }

        [Test]
        public void TestNoCredReqRefreshSuccess ()
        {
            Cred = MakeOauth2Credential ();

            Assert.IsTrue (WaitForTimerRefresh (Account.Id));
            Assert.IsTrue (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockBe.GetReqStatusNeedCredResp (Account.Id));
            Assert.IsFalse (MockBe.alertUiCalled);

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsFalse (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
        }

        [Test]
        public void TestMultipleCredReq ()
        {
            Cred = MakeOauth2Credential ();

            MockBe.CredReq (ProtoControl);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.AreEqual (BackEnd.CredReqActiveState.CredReqActive_AwaitingRefresh, MockBe.GetReqActiveState (Account.Id));
            Assert.IsTrue (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));
            Assert.IsFalse (MockBe.alertUiCalled);
            MockBe.Reset ();

            // Subsequent CredReq gets ignored.
            MockBe.CredReq (ProtoControl);
            Assert.IsFalse (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsFalse (MockBe.alertUiCalled);
        }

        [Test]
        public void TestTimerAndCredReq ()
        {
            Cred = MakeOauth2Credential ();

            Assert.IsTrue (WaitForTimerRefresh (Account.Id));
            Assert.IsTrue (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsFalse (MockBe.GetReqStatusNeedCredResp(Account.Id));

            MockBe.Reset ();
            MockBe.CredReq (ProtoControl);
            Assert.IsFalse (MockBe.RefreshOauth2Called);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsFalse (MockBe.alertUiCalled);
        }
    }
}

