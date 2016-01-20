//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace Test.Common
{
    public class McPendDepTest : NcTestBase
    {
        [Test]
        public void CrossAccount ()
        {
            McPending._BackEnd = new MockBackEnd ();

            var acct1 = new McAccount () {
            };
            acct1.Insert ();
            var acct2 = new McAccount () {
            };
            acct2.Insert ();
            var email = new McEmailMessage () {
                AccountId = acct1.Id,
                ServerId = "email",
            };
            email.Insert ();

            var host = new McEmailMessage () {
                AccountId = acct2.Id,
                ServerId = "host",
            };
            host.Insert ();

            var att = new McAttachment () {
                AccountId = acct2.Id,
            };
            att.Insert ();
            att.Link (host);
            att.Link (email);
            var sendPending = new McPending (acct1.Id, McAccount.AccountCapabilityEnum.EmailSender, email) {
                Operation = McPending.Operations.EmailSend,
            };
            sendPending.Insert ();
            var attPending = McPending.QueryByAttachmentId (att.AccountId, att.Id);
            attPending.UnblockSuccessors (null, McPending.StateEnum.Eligible);
            sendPending = McPending.QueryById<McPending> (sendPending.Id);
            Assert.AreEqual (sendPending.State, McPending.StateEnum.Eligible);

            McPending._BackEnd = BackEnd.Instance;
        }

        [Test]
        public void DepThenIndep ()
        {
            List<int> depPendIdList = new List<int> ();
            var createFolder = new McPending (1, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                Operation = McPending.Operations.FolderCreate,
                ServerId = "parent",
                ParentId = "0",
                DisplayName = "Folder",
                Folder_Type = Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14,
            };
            createFolder.Insert ();
            for (int iter = 0; iter < 10; ++iter) {
                var pending = new McPending (1, McAccount.AccountCapabilityEnum.ContactWriter) {
                    Operation = McPending.Operations.ContactCreate,
                    ItemId = iter,
                    ParentId = "guid",
                    ClientId = iter.ToString(),
                };
                pending.Insert ();
                pending = pending.MarkPredBlocked (createFolder.Id);
                depPendIdList.Add (pending.Id);
            }
            // Verify all are blocked.
            var suc = McPending.QuerySuccessors (createFolder.Id);
            Assert.IsNotNull (suc);
            Assert.True (10 == suc.Count);
            foreach (var pid in depPendIdList) {
                var pending = NcModel.Instance.Db.Get<McPending> (pid);
                Assert.True (pending.State == McPending.StateEnum.PredBlocked);
                var dep = McPending.QueryPredecessors (1, pending.Id);
                Assert.IsNotNull (dep);
                Assert.True (1 == dep.Count);
                var pred = dep.First ();
                Assert.True (createFolder.Id == pred.Id);
            }
            createFolder.UnblockSuccessors (null, McPending.StateEnum.Eligible);
            // Verify all aren't blocked.
            suc = McPending.QuerySuccessors (createFolder.Id);
            Assert.IsNotNull (suc);
            Assert.True (0 == suc.Count);
            foreach (var pid in depPendIdList) {
                var pending = NcModel.Instance.Db.Get<McPending> (pid);
                Assert.True (pending.State == McPending.StateEnum.Eligible);
                var dep = McPending.QueryPredecessors (1, pending.Id);
                Assert.IsNotNull (dep);
                Assert.True (0 == dep.Count);
            }
        }

        public class MockProtoControl : NcProtoControl
        {
            public MockProtoControl (INcProtoControlOwner owner, int accountId) : base (owner, accountId)
            {
            }

            public override NcResult DnldAttCmd (int attId, bool doNotDelay = false)
            {
                var att = McAttachment.QueryById<McAttachment> (attId);
                Assert.NotNull (att);
                Assert.AreEqual (McAbstrFileDesc.FilePresenceEnum.None, att.FilePresence);
                var emailMessage = McAttachment.QueryItems (att.AccountId, att.Id)
                    .Where (x => x is McEmailMessage && !x.IsAwaitingCreate).FirstOrDefault ();
                Assert.NotNull (emailMessage);
                var pending = new McPending (att.AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.AttachmentDownload,
                    ServerId = emailMessage.ServerId,
                    AttachmentId = attId,
                };
                pending.Insert ();
                var result = NcResult.OK (pending.Token);
                att.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Partial);
                att.Update ();

                return result;
            }
        }

        public class MockBackEnd : IBackEnd
        {
            public List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> BackEndStatesPreSet;
            public X509Certificate2 ServerCertPreSet;
            public McAccount.AccountCapabilityEnum ServerCertCapabilities;
            public BackEnd.AutoDFailureReasonEnum AutoDFailureReasonPreSet;
            private NcProtoControl protoControl;

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
                Assert.AreEqual (McAccount.AccountCapabilityEnum.EmailReaderWriter, capability);
                if (null == protoControl) {
                    protoControl = new MockProtoControl (null, 1);
                }
                return protoControl;
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
        }
    }
}

