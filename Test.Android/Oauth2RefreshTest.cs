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
            public bool RefreshMcCredCalled { get; set; }
            public bool CredRespCalled { get; set; }

            public void Reset ()
            {
                TokenRefreshSuccessCalled = false;
                TokenRefreshFailureCalled = false;
                RefreshMcCredCalled = false;
                CredRespCalled = false;
            }

            protected override void TokenRefreshFailure (McCred cred)
            {
                TokenRefreshFailureCalled = true;
                // don't call base method. It's not needed.
            }

            protected override void TokenRefreshSuccess (McCred cred)
            {
                TokenRefreshSuccessCalled = true;
                base.TokenRefreshSuccess (cred);
            }

            protected override void RefreshMcCred (McCred cred)
            {
                RefreshMcCredCalled = true;
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

            public bool NeedToPassUp (int accountId)
            {
                return NeedToPassReqToUi (accountId);
            }

            public void FakeRefreshToken (McCred cred)
            {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (cred.AccountId, out status)) {
                    status.RefreshRetries++;
                }
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
        #endregion

        uint expirySecs = 1;
        OauthRefreshMockBE MockBe { get; set; }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount () {
            };
            Account.Insert ();
            Cred = new McCred () {
                AccountId = Account.Id,
                CredType = McCred.CredTypeEnum.OAuth2,
                ExpirySecs = expirySecs,
                Expiry = DateTime.UtcNow.AddSeconds (expirySecs),
            };
            Cred.Insert ();
            ProtoControl = new NcProtoControl (this, Account.Id);
            MockBe = new OauthRefreshMockBE ();
            MockBe.Reset ();
        }

        [TearDown]
        public void Teardown ()
        {
            Account.Delete ();
            Cred.Delete ();
        }

        [Test]
        public void TestPasswordNoOauth ()
        {
            var cred = Cred;
            cred = cred.UpdateWithOCApply<McCred> ((record) => {
                var target = (McCred)record;
                target.CredType = McCred.CredTypeEnum.Password;
                return true;
            });
            Assert.AreEqual (McCred.CredTypeEnum.Password, Cred.CredType);
            MockBe.CredReq (ProtoControl);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.False (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));
            // finish the request
            MockBe.FinishRequest (Cred, false);
            Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
            Assert.IsFalse (MockBe.CredRespCalled);
            Assert.IsTrue (MockBe.NeedToPassUp (Account.Id));
        }

        [Test]
        public void TestCredReqRefreshFail ()
        {
            MockBe.CredReq (ProtoControl);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));
            // finish the request
            MockBe.FinishRequest (Cred, false);
            Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
            Assert.IsFalse (MockBe.CredRespCalled);
            Assert.IsTrue (MockBe.NeedToPassUp (Account.Id));
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
        }

        [Test]
        public void TestNoCredReqRefreshFail ()
        {
            // wait for the refresh to figure out it needs to refresh the token. 
            // Should be pretty quick, since the credential expiry is set to 1 second
            // (see expirySecs).
            for (var i = 0; i < 6; i++) {
                MockBe.TestRefreshAllTokens ();
                if (MockBe.RefreshStarted (Account.Id)) {
                    break;
                }
                Thread.Sleep (500); // 500 milliseconds
            }
            Assert.IsTrue (MockBe.RefreshStarted (Account.Id));
            Assert.IsTrue (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsFalse (MockBe.GetReqStatusNeedCredResp(Account.Id));
            MockBe.FakeRefreshToken (Cred);
            MockBe.FinishRequest (Cred, false);

            // fail the request several times.
            for (var i = 1; i < BackEnd.KOauth2RefreshMaxFailure-1; i++) {
                Console.WriteLine ("JAN: {0}", i);
                Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
                Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
                Assert.IsFalse (MockBe.CredRespCalled);
                Assert.IsFalse (MockBe.NeedToPassUp (Account.Id));

                MockBe.FinishRequest (Cred, false);
                MockBe.FakeRefreshToken (Cred);
            }

            Assert.IsFalse (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.TokenRefreshFailureCalled);
            Assert.IsFalse (MockBe.CredRespCalled);
            Assert.IsTrue (MockBe.NeedToPassUp (Account.Id));
        }

        [Test]
        public void TestNoCredReqRefreshSuccess ()
        {
            // wait for the refresh to figure out it needs to refresh the token. 
            // Should be pretty quick, since the credential expiry is set to 1 second
            // (see expirySecs).
            for (var i = 0; i < 6; i++) {
                MockBe.TestRefreshAllTokens ();
                if (MockBe.RefreshStarted (Account.Id)) {
                    break;
                }
                Thread.Sleep (500); // 500 milliseconds
            }
            Assert.IsTrue (MockBe.RefreshStarted (Account.Id));
            Assert.IsTrue (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.HaveCredReqActive (Account.Id));
            Assert.IsFalse (MockBe.GetReqStatusNeedCredResp (Account.Id));

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsFalse (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
        }

        [Test]
        public void TestMultipleCredReq ()
        {
            MockBe.CredReq (ProtoControl);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));
            MockBe.Reset ();

            // Subsequent CredReq gets ignored.
            MockBe.CredReq (ProtoControl);
            Assert.IsFalse (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
        }

        [Test]
        public void TestTimerAndCredReq ()
        {
            // wait for the refresh to figure out it needs to refresh the token. Should be pretty quick,
            // since the credential expiry is set to 1 second.
            for (var i = 0; i < 6; i++) {
                MockBe.TestRefreshAllTokens ();
                if (MockBe.RefreshStarted (Account.Id)) {
                    break;
                }
                Thread.Sleep (500);
            }
            Assert.IsTrue (MockBe.RefreshStarted (Account.Id));
            Assert.IsTrue (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsFalse (MockBe.GetReqStatusNeedCredResp(Account.Id));

            MockBe.Reset ();
            MockBe.CredReq (ProtoControl);
            Assert.IsFalse (MockBe.RefreshMcCredCalled);
            Assert.IsTrue (MockBe.HaveCredReqActive(Account.Id));
            Assert.IsTrue (MockBe.GetReqStatusNeedCredResp(Account.Id));

            // finish the request
            MockBe.FinishRequest (Cred, true);
            Assert.IsTrue (MockBe.TokenRefreshSuccessCalled);
            Assert.IsTrue (MockBe.CredRespCalled);
            Assert.IsFalse (MockBe.HaveCredReqActive(Account.Id));
        }
    }
}

