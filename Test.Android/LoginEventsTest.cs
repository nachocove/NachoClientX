//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.iOS
{
    
    [TestFixture]
    public class LoginEventsTest : CommonTestOps
    {
        private static int AccountIdA;
        private static int AccountIdB;

        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
            var accountA = new McAccount () {
                AccountCapability = McAccount.AccountCapabilityEnum.EmailSender,
            };
            accountA.Insert ();
            AccountIdA = accountA.Id;
            var accountB = new McAccount () {
                AccountCapability = McAccount.AccountCapabilityEnum.CalWriter,
            };
            accountB.Insert ();
            AccountIdB = accountB.Id;
        }

        [Test]
        // Static classes are ugly for testing. You only get one pass w/out a lot of reset logic.
        public void TestItAll ()
        {
            MockBE = new MockBackEnd ();
            MockSI = new MockStatusIndEvent ();
            MockCS = new MockCommStatus ();
            MockLE = new MockLoginEvents ();
            LoginEvents.Init (backEnd: MockBE, statusIndEvent: MockSI, commStatus: MockCS);
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockCS.Status = NachoPlatform.NetStatusStatusEnum.Down;
            MockCS.Speed = NachoPlatform.NetStatusSpeedEnum.CellSlow_2;
            LoginEvents.Owner = MockLE;
            // Ensure that we don't get callbacks when Owner is reset.
            MockLE.NetworkDownCalled = false;
            LoginEvents.Owner = null;
            SendNetDown ();
            Assert.IsFalse (MockLE.NetworkDownCalled);
            // Owner set w/no callback.
            MockCS.Status = NachoPlatform.NetStatusStatusEnum.Up;
            LoginEvents.Owner = MockLE;
            // network down.
            Assert.IsFalse (MockLE.NetworkDownCalled);
            //
            // Events triggering callbacks.
            //
            SendNetDown ();
            Assert.IsTrue (MockLE.NetworkDownCalled);
            // CredReq.
            SendCredReq (AccountIdA);
            Assert.AreEqual (AccountIdA, MockLE.CredReqAccountId);
            // ServerConfReq.
            SendServerConfReq (AccountIdB, McAccount.AccountCapabilityEnum.CalWriter, BackEnd.AutoDFailureReasonEnum.CannotConnectToServer);
            Assert.AreEqual (AccountIdB, MockLE.ServConfReqAccountId);
            Assert.AreEqual (McAccount.AccountCapabilityEnum.CalWriter, MockLE.ServConfReqCapabilities);
            Assert.AreEqual (BackEnd.AutoDFailureReasonEnum.CannotConnectToServer, MockLE.ServConfReqArg);
            // CertAsk.
            var myCert = new X509Certificate2 ();
            SendCertAsk (AccountIdA, McAccount.AccountCapabilityEnum.EmailSender, myCert);
            Assert.AreEqual (AccountIdA, MockLE.CertAskReqAccountId);
            Assert.AreEqual (McAccount.AccountCapabilityEnum.EmailSender, MockLE.CertAskReqCapabilities);
            Assert.AreSame (myCert, MockLE.CertAskReqCertificate);
            // PostPost
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendStateChange (AccountIdA);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPostInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendStateChange (AccountIdA);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            // PostPre
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendStateChange (AccountIdA);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendStateChange (AccountIdA);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            //
            // Polling triggering callbacks
            //
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.CalWriter),
            };
            // Network down.
            MockLE.ResetAll ();
            MockCS.Status = NachoPlatform.NetStatusStatusEnum.Down;
            LoginEvents.CheckBackendState ();
            Assert.True (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            MockCS.Status = NachoPlatform.NetStatusStatusEnum.Up;

            // NotYetStarted
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            // Running
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            // CertAskWait
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.CertAskWait, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            var match = new X509Certificate2 ();
            MockBE.ServerCertPreSet = match;
            MockBE.ServerCertCapabilities = McAccount.AccountCapabilityEnum.CalWriter;
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (AccountIdA, MockLE.CertAskReqAccountId);
            Assert.AreSame (match, MockLE.CertAskReqCertificate);
            Assert.AreEqual (McAccount.AccountCapabilityEnum.CalWriter, MockLE.CertAskReqCapabilities);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            MockBE.ServerCertPreSet = null;
            MockBE.ServerCertCapabilities = McAccount.AccountCapabilityEnum.TaskReader;

            // ServerConfWait
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.ServerConfWait, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            MockBE.AutoDFailureReasonPreSet = BackEnd.AutoDFailureReasonEnum.CannotConnectToServer;
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (AccountIdA, MockLE.ServConfReqAccountId);
            Assert.AreEqual (BackEnd.AutoDFailureReasonEnum.CannotConnectToServer, MockLE.ServConfReqArg);
            Assert.AreEqual (McAccount.AccountCapabilityEnum.EmailSender, MockLE.ServConfReqCapabilities);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            MockBE.AutoDFailureReasonPreSet = BackEnd.AutoDFailureReasonEnum.Unknown;

            // CredWait
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.CredWait, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.CredWait, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (AccountIdA, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.CredWait, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (AccountIdA, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            // PostAutoDPreInboxSync
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.CredWait, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (AccountIdA, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPreInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);

            // PostAutoDPostInboxSync
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.PostAutoDPostInboxSync, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            LoginEvents.CheckBackendState ();
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (AccountIdA, MockLE.PostAutoDPostInboxSyncAccountId);

            // ServerIndServerErrorRetryLater
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendServerInd (AccountIdB, (uint)Xml.StatusCode.ServerErrorRetryLater_111);
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            Assert.AreEqual (AccountIdB, MockLE.ServerIndServerErrorRetryLaterAccountId);
            Assert.AreEqual (-1, MockLE.ServerIndTooManyDevicesAccountId);

            // ServerIndTooManyDevices
            MockBE.BackEndStatesPreSet = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ()
            { 
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.EmailSender),
                new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.Running, McAccount.AccountCapabilityEnum.CalWriter),
            };
            MockLE.ResetAll ();
            SendServerInd (AccountIdA, (uint)Xml.StatusCode.MaximumDevicesReached_177);
            Assert.False (MockLE.NetworkDownCalled);
            Assert.AreEqual (-1, MockLE.CredReqAccountId);
            Assert.AreEqual (-1, MockLE.ServConfReqAccountId);
            Assert.AreEqual (-1, MockLE.CertAskReqAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPreInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.PostAutoDPostInboxSyncAccountId);
            Assert.AreEqual (-1, MockLE.ServerIndServerErrorRetryLaterAccountId);
            Assert.AreEqual (AccountIdA, MockLE.ServerIndTooManyDevicesAccountId);
        }

        private MockBackEnd MockBE;
        private MockStatusIndEvent MockSI;
        private MockCommStatus MockCS;
        private MockLoginEvents MockLE;

        private void SendNetDown ()
        {
            MockCS.SendEvent (new NachoPlatform.NetStatusEventArgs (NachoPlatform.NetStatusStatusEnum.Down, 
                NachoPlatform.NetStatusSpeedEnum.CellSlow_2));
        }
        private void SendCredReq (int accountId)
        {
            MockSI.SendEvent (new StatusIndEventArgs () {
                Status = NcResult.Info (NcResult.SubKindEnum.Info_CredReqCallback),
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }
        private void SendServerConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg)
        {
            var status = NcResult.Info (NcResult.SubKindEnum.Info_ServerConfReqCallback);
            status.Value = new Tuple<McAccount.AccountCapabilityEnum, BackEnd.AutoDFailureReasonEnum> (capabilities, arg);
            MockSI.SendEvent (new StatusIndEventArgs () {
                Status = status,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }
        private void SendCertAsk (int accountId, McAccount.AccountCapabilityEnum capabilities, X509Certificate2 certificate)
        {
            var status = NcResult.Info (NcResult.SubKindEnum.Info_CertAskReqCallback);
            status.Value = new Tuple<McAccount.AccountCapabilityEnum, X509Certificate2> (capabilities, certificate);
            MockSI.SendEvent (new StatusIndEventArgs () {
                Status = status,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }
        private void SendStateChange (int accountId)
        {
            var status = NcResult.Info (NcResult.SubKindEnum.Info_BackEndStateChanged);
            status.Value = accountId;
            MockSI.SendEvent (new StatusIndEventArgs () {
                Status = status,
                Account = McAccount.QueryById<McAccount> (accountId),
            });
        }
        private void SendServerInd (int accountId, uint code)
        {
            var status = NcResult.Info (NcResult.SubKindEnum.Info_ServerStatus);
            status.Value = code;
            MockSI.SendEvent (new StatusIndEventArgs () {
                Account = McAccount.QueryById<McAccount> (accountId),
                Status = status,
            });
        }
    }

    public class MockLoginEvents : ILoginEvents
    {
        public bool NetworkDownCalled;
        public int CredReqAccountId;
        public int ServConfReqAccountId;
        public McAccount.AccountCapabilityEnum ServConfReqCapabilities;
        public BackEnd.AutoDFailureReasonEnum ServConfReqArg;
        public int CertAskReqAccountId;
        public McAccount.AccountCapabilityEnum CertAskReqCapabilities;
        public X509Certificate2 CertAskReqCertificate;
        public int PostAutoDPreInboxSyncAccountId;
        public int PostAutoDPostInboxSyncAccountId;
        public int ServerIndServerErrorRetryLaterAccountId;
        public int ServerIndTooManyDevicesAccountId;

        public void ResetAll ()
        {
            NetworkDownCalled = false;
            CredReqAccountId = -1;
            ServConfReqAccountId = -1;
            CertAskReqAccountId = -1;
            PostAutoDPostInboxSyncAccountId = -1;
            PostAutoDPreInboxSyncAccountId = -1;
            ServerIndServerErrorRetryLaterAccountId = -1;
            ServerIndTooManyDevicesAccountId = -1;
        }

        public void CredReq (int accountId)
        {
            CredReqAccountId = accountId;
        }
        public void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg)
        {
            ServConfReqAccountId = accountId;
            ServConfReqCapabilities = capabilities;
            ServConfReqArg = arg;
        }
        public void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, X509Certificate2 certificate)
        {
            CertAskReqAccountId = accountId;
            CertAskReqCapabilities = capabilities;
            CertAskReqCertificate = certificate;
        }
        public void NetworkDown ()
        {
            NetworkDownCalled = true;
        }
        // Note that PostAutoDPreInboxSync may fire > 1 time for multi-controller accounts.
        public void PostAutoDPreInboxSync (int accountId)
        {
            PostAutoDPreInboxSyncAccountId = accountId;
        }
        public void PostAutoDPostInboxSync (int accountId)
        {
            PostAutoDPostInboxSyncAccountId = accountId;
        }
        public void ServerIndServerErrorRetryLater (int accountId)
        {
            ServerIndServerErrorRetryLaterAccountId = accountId;
        }
        public void ServerIndTooManyDevices (int accountId)
        {
            ServerIndTooManyDevicesAccountId = accountId;
        }
    }
    public class MockBackEnd : IBackEnd
    {
        public List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> BackEndStatesPreSet;
        public X509Certificate2 ServerCertPreSet;
        public McAccount.AccountCapabilityEnum ServerCertCapabilities;
        public BackEnd.AutoDFailureReasonEnum AutoDFailureReasonPreSet;

        public void Start ()
        {
        }
        public void Start (int accountId)
        {
        }
        public void Stop ()
        {
        }
        public void Stop (int accountId)
        {
        }
        public void Remove (int accountId)
        {
        }
        public NcProtoControl GetService (int accountId, McAccount.AccountCapabilityEnum capability)
        {
            return null;
        }
        public void CertAskResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool isOkay)
        {
        }
        public void ServerConfResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool forceAutodiscovery)
        {
        }
        public void CredResp (int accountId)
        {
        }
        public void PendQHotInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
        }
        public void PendQInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
        }
        public void HintInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
        }
        public NcResult StartSearchEmailReq (int accountId, string prefix, uint? maxResults)
        {
            return NcResult.OK ();
        }
        public NcResult SearchEmailReq (int accountId, string prefix, uint? maxResults, string token)
        {
            return NcResult.OK ();
        }
        public NcResult StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return NcResult.OK ();
        }
        public NcResult SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            return NcResult.OK ();
        }
        public NcResult SendEmailCmd (int accountId, int emailMessageId)
        {
            return NcResult.OK ();
        }
        public NcResult SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return NcResult.OK ();
        }
        public NcResult ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
            int folderId, bool originalEmailIsEmbedded)
        {
            return NcResult.OK ();
        }
        public NcResult ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
            int folderId, bool originalEmailIsEmbedded)
        {
            return NcResult.OK ();
        }
        public NcResult DeleteEmailCmd (int accountId, int emailMessageId, bool justDelete = false)
        {
            return NcResult.OK ();
        }
        public List<NcResult> DeleteEmailsCmd (int accountId, List<int> emailMessageIds, bool justDelete = false)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> MoveEmailsCmd (int accountId, List<int> emailMessageIds, int destFolderId)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult MarkEmailReadCmd (int accountId, int emailMessageId, bool read)
        {
            return NcResult.OK ();
        }
        public NcResult SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
            DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return NcResult.OK ();
        }
        public NcResult ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return NcResult.OK ();
        }
        public NcResult MarkEmailFlagDone (int accountId, int emailMessageId,
            DateTime completeTime, DateTime dateCompleted)
        {
            return NcResult.OK ();
        }
        public NcResult DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDelay = false)
        {
            return NcResult.OK ();
        }
        public NcResult DnldAttCmd (int accountId, int attId, bool doNotDelay = false)
        {
            return NcResult.OK ();
        }
        public NcResult CreateCalCmd (int accountId, int calId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult UpdateCalCmd (int accountId, int calId, bool sendBody)
        {
            return NcResult.OK ();
        }
        public NcResult DeleteCalCmd (int accountId, int calId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> DeleteCalsCmd (int accountId, List<int> calIds)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> MoveCalsCmd (int accountId, List<int> calIds, int destFolderId)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return NcResult.OK ();
        }
        public NcResult RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return NcResult.OK ();
        }
        public NcResult DnldCalBodyCmd (int accountId, int calId)
        {
            return NcResult.OK ();
        }
        public NcResult ForwardCalCmd (int accountId, int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult UpdateContactCmd (int accountId, int contactId)
        {
            return NcResult.OK ();
        }
        public NcResult DeleteContactCmd (int accountId, int contactId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> DeleteContactsCmd (int accountId, List<int> contactIds)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> MoveContactsCmd (int accountId, List<int> contactIds, int destFolderId)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult DnldContactBodyCmd (int accountId, int contactId)
        {
            return NcResult.OK ();
        }
        public NcResult CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult UpdateTaskCmd (int accountId, int taskId)
        {
            return NcResult.OK ();
        }
        public NcResult DeleteTaskCmd (int accountId, int taskId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> DeleteTasksCmd (int accountId, List<int> taskIds)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return NcResult.OK ();
        }
        public List<NcResult> MoveTasksCmd (int accountId, List<int> taskIds, int destFolderId)
        {
            return new List<NcResult> () { NcResult.OK () };
        }
        public NcResult DnldTaskBodyCmd (int accountId, int taskId)
        {
            return NcResult.OK ();
        }
        public NcResult CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return NcResult.OK ();
        }
        public NcResult CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return NcResult.OK ();
        }
        public NcResult DeleteFolderCmd (int accountId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return NcResult.OK ();
        }
        public NcResult RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return NcResult.OK ();
        }
        public NcResult SyncCmd (int accountId, int folderId)
        {
            return NcResult.OK ();
        }
        public NcResult ValidateConfig (int accountId, McServer server, McCred cred)
        {
            return NcResult.OK ();
        }
        public void CancelValidateConfig (int accountId)
        {
        }
        public List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> BackEndStates (int accountId)
        {
            if (2 != accountId) {
                return new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> () { 
                    new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (BackEndStateEnum.NotYetStarted, McAccount.AccountCapabilityEnum.TaskWriter),
                };
            }
            return BackEndStatesPreSet;
        }
        public BackEndStateEnum BackEndState (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return BackEndStateEnum.NotYetStarted;
        }
        public BackEnd.AutoDFailureReasonEnum AutoDFailureReason (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return AutoDFailureReasonPreSet;
        }
        public AutoDInfoEnum AutoDInfo (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return AutoDInfoEnum.Unknown;
        }
        public X509Certificate2 ServerCertToBeExamined (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            if (capabilities == ServerCertCapabilities) {
                return ServerCertPreSet;
            }
            return null;
        }
        public void SendEmailBodyFetchHint (int accountId, int emailMessageId)
        {
        }
        public NcResult SyncContactsCmd (int accountId)
        {
            return NcResult.OK ();
        }
    }

    public class MockStatusIndEvent : IStatusIndEvent
    {
        public event EventHandler StatusIndEvent;

        public void SendEvent (StatusIndEventArgs siea)
        {
            StatusIndEvent.Invoke (this, siea);
        }
    }

    public class MockCommStatus : INcCommStatus
    {
        public event NcCommStatusServerEventHandler CommStatusServerEvent;
        public event NachoPlatform.NetStatusEventHandler CommStatusNetEvent;

        public NachoPlatform.NetStatusStatusEnum Status { get; set; }

        public NachoPlatform.NetStatusSpeedEnum Speed { get; set; }

        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
        }
        public void ReportCommResult (int serverId, DateTime delayUntil)
        {
        }
        public void ReportCommResult (int accountId, McAccount.AccountCapabilityEnum capabilities, bool didFailGenerally)
        {
        }
        public void ReportCommResult (int accountId, string host, bool didFailGenerally)
        {
        }
        public void ReportCommResult (int accountId, string host, DateTime delayUntil)
        {
        }
        public void Reset (int serverId)
        {
        }
        public void Refresh ()
        {
        }
        public bool IsRateLimited (int serverId)
        {
            return false;
        }

        public void SendEvent (NachoPlatform.NetStatusEventArgs args)
        {
            CommStatusNetEvent.Invoke (this, args);
        }
    }
}

