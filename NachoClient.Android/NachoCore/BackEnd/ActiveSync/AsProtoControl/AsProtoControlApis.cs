//  Copyright (C) 2014, 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public partial class AsProtoControl : NcProtoControl, IBEContext
    {
        
        protected override NcResult SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
            int folderId, bool originalEmailIsEmbedded, SendEmailKind kind)
        {
            Log.Info (Log.LOG_AS, "SmartEmailCmd({0},{1},{2},{3},{4})", Op, newEmailMessageId, refdEmailMessageId, folderId, originalEmailIsEmbedded);
            if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                return SendEmailCmd (newEmailMessageId);
            }
            return base.SmartEmailCmd (Op, newEmailMessageId, refdEmailMessageId, folderId, originalEmailIsEmbedded, kind);
        }

        public override NcResult SetEmailFlagCmd (int emailMessageId, string flagType, 
                                                DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.EmailSetFlag,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                    EmailSetFlag_FlagType = flagType,
                    EmailSetFlag_Start = start,
                    EmailSetFlag_UtcStart = utcStart,
                    EmailSetFlag_Due = due,
                    EmailSetFlag_UtcDue = utcDue,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                // Set the Flag info in the DB item.
                emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
                    target.FlagType = flagType;
                    target.FlagStartDate = start;
                    target.FlagUtcStartDate = utcStart;
                    target.FlagDue = due;
                    target.FlagUtcDue = utcDue;
                    return true;
                });
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCSF");
            }, "SetEmailFlagCmd");
            return result;
        }

        public override NcResult ClearEmailFlagCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }

                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.EmailClearFlag,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                    return true;
                });
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCCF");
            }, "ClearEmailFlagCmd");
            return result;
        }

        public override NcResult MarkEmailFlagDone (int emailMessageId,
                                                  DateTime completeTime, DateTime dateCompleted)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                    Operation = McPending.Operations.EmailMarkFlagDone,
                    ServerId = emailMessage.ServerId,
                    ParentId = folder.ServerId,
                    EmailMarkFlagDone_CompleteTime = completeTime,
                    EmailMarkFlagDone_DateCompleted = dateCompleted,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
                    target.FlagCompleteTime = completeTime;
                    target.FlagDateCompleted = dateCompleted;
                    return true;
                });
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCCF");
            }, "MarkEmailFlagDone");
            return result;
        }

        public override NcResult DnldCalBodyCmd (int calId)
        {
            // I can't get this command to work.  The server always responds with "Bad or malformed request."
            // Return an error immediately.  The code to run the command is left here, commented out, in case
            // we want to try again.
            Log.Error (Log.LOG_AS, "DnldCalBody command is not supported.");
            return NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            #if false
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McCalendar cal;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McCalendar> (calId, out cal, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (AccountId) {
                    Operation = McPending.Operations.CalBodyDownload,
                    ServerId = cal.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)CtlEvt.E.PendQHot, "ASPCDNLDCALBOD");
            }, "DnldCalBodyCmd");
            return result;
            #endif
        }

        public override NcResult ForwardCalCmd (int newEmailMessageId, int forwardedCalId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var refdCalEvent = McCalendar.QueryById<McCalendar> (forwardedCalId);
                var newEmailMessage = McEmailMessage.QueryById<McEmailMessage> (newEmailMessageId);
                var folder = McFolder.QueryById<McFolder> (folderId);
                if (null == refdCalEvent || null == newEmailMessage) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                if (null == folder) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.CalReader, newEmailMessage) {
                    Operation = McPending.Operations.CalForward,
                    ServerId = refdCalEvent.ServerId,
                    ParentId = folder.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "ASPCCALF");
            }, "ForwardCalCmd");
            return result;
        }

        public override NcResult DnldContactBodyCmd (int contactId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McContact contact;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McContact> (contactId, out contact, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var body = contact.GetBody ();
                if (McBody.IsNontruncatedBodyComplete (body)) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsNontruncatedBodyComplete);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.ContactReader) {
                    Operation = McPending.Operations.ContactBodyDownload,
                    ServerId = contact.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "ASPCDNLDCONBOD");
            }, "DnldContactBodyCmd");
            return result;
        }

        public override NcResult CreateTaskCmd (int taskId, int folderId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McTask> (taskId, out task, folderId, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.TaskWriter, task) {
                    Operation = McPending.Operations.TaskCreate,
                    ParentId = folder.ServerId,
                    ClientId = task.ClientId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCCRETSK");
            }, "CreateTaskCmd");
            return result;
        }

        public override NcResult UpdateTaskCmd (int taskId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var task = McTask.QueryById<McTask> (taskId);
                if (null == task) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (AccountId, taskId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.TaskWriter, task) {
                    Operation = McPending.Operations.TaskUpdate,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCCHGTSK");
            }, "UpdateTaskCmd");
            return result;
        }

        public override NcResult DeleteTaskCmd (int taskId, bool lastInSeq = true)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcModel.Instance.RunInTransaction (() => {
                var task = McTask.QueryById<McTask> (taskId);
                if (null == task) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                    return;
                }
                var folders = McFolder.QueryByFolderEntryId<McTask> (AccountId, taskId);
                if (null == folders || 0 == folders.Count) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing);
                    return;
                }
                var primeFolder = folders.First ();
                if (primeFolder.IsClientOwned) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
                    return;
                }

                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.TaskWriter) {
                    Operation = McPending.Operations.ContactDelete,
                    ParentId = primeFolder.ServerId,
                    ServerId = task.ServerId,
                };   
                pending.Insert ();
                result = NcResult.OK (pending.Token);
                task.Delete ();
            });
            if (lastInSeq) {
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_TaskSetChanged));
                NcTask.Run (delegate {
                    Sm.PostEvent ((uint)PcEvt.E.PendQOrHint, "ASPCDELTSK");
                }, "DeleteTaskCmd");
            }
            return result;
        }

        public override NcResult MoveTaskCmd (int taskId, int destFolderId, bool lastInSeq = true)
        {
            var task = McTask.QueryById<McTask> (taskId);
            if (null == task) {
                return NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
            }
            var srcFolder = McFolder.QueryByFolderEntryId<McTask> (AccountId, taskId).FirstOrDefault ();

            return MoveItemCmd (McPending.Operations.TaskMove, McAccount.AccountCapabilityEnum.TaskWriter,
                NcResult.SubKindEnum.Info_TaskSetChanged,
                task, srcFolder, destFolderId, lastInSeq);
        }

        public override NcResult DnldTaskBodyCmd (int taskId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McTask task;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<McTask> (taskId, out task, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                var body = task.GetBody ();
                if (McBody.IsNontruncatedBodyComplete (body)) {
                    result = NcResult.Error (NcResult.SubKindEnum.Error_IsNontruncatedBodyComplete);
                    return;
                }
                var pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.TaskReader) {
                    Operation = McPending.Operations.TaskBodyDownload,
                    ServerId = task.ServerId,
                };
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "ASPCDNLDTBOD");
            }, "DnldTaskBodyCmd");
            return result;
        }

        public override NcResult RespondEmailCmd (int emailMessageId, NcResponseType response)
        {
            return RespondItemCmd<McEmailMessage> (emailMessageId, response);
        }

        public override NcResult RespondCalCmd (int calId, NcResponseType response, DateTime? instance = null)
        {
            return RespondItemCmd<McCalendar> (calId, response, instance);
        }

        private NcResult RespondItemCmd<T> (int itemId, NcResponseType response, DateTime? instance = null)
            where T : McAbstrItem, new ()
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            McPending pending;
            NcModel.Instance.RunInTransaction (() => {
                T item;
                McFolder folder;
                NcResult.SubKindEnum subKind;
                if (!GetItemAndFolder<T> (itemId, out item, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                // From MS-ASCMD:
                // When protocol versions 2.5, 12.0, 12.1, or 14.0 are used, the MeetingResponse command cannot be used to modify meeting requests in the Calendar folder.
                // AND
                // In Exchange 2007, the MeetingResponse command is used to accept, tentatively accept, or decline a meeting request only in the user's Inbox folder.
                // 
                // In this (these?) scenarios, update the Cal item in the DB, and Sync the change to the server.
                //
                if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 != folder.Type &&
                    14.1 > Convert.ToDouble (ProtocolState.AsProtocolVersion, System.Globalization.CultureInfo.InvariantCulture)) {
                    var cal = item as McCalendar;
                    if (null == cal) {
                        result = NcResult.Error (NcResult.SubKindEnum.Error_ItemMissing);
                        Log.Error (Log.LOG_AS, "Cannot respond to an email-invite message not in Inbox for older EAS ({0})", folder.Type);
                        return;
                    }
                    switch (response) {
                    case NcResponseType.Accepted:
                        cal.ResponseType = NcResponseType.Accepted;
                        break;
                    case NcResponseType.Tentative:
                        cal.ResponseType = NcResponseType.Tentative;
                        break;
                    case NcResponseType.Declined:
                        cal.ResponseType = NcResponseType.Declined;
                        break;
                    default:
                        result = NcResult.Error (NcResult.SubKindEnum.Error_InvalidResponseType);
                        return;
                    }
                    cal.ResponseTypeIsSet = true;
                    cal.Update ();
                    pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.CalWriter, cal) {
                        Operation = McPending.Operations.CalUpdate,
                        ParentId = folder.ServerId,
                        ServerId = cal.ServerId,
                        CalUpdate_SendBody = false,
                    }; 
                } else {
                    Xml.MeetingResp.UserResponseCode apiResponse;
                    switch (response) {
                    case NcResponseType.Accepted:
                        apiResponse = Xml.MeetingResp.UserResponseCode.Accepted_1;
                        break;

                    case NcResponseType.Tentative:
                        apiResponse = Xml.MeetingResp.UserResponseCode.Tentatively_2;
                        break;

                    case NcResponseType.Declined:
                        apiResponse = Xml.MeetingResp.UserResponseCode.Declined_3;
                        break;

                    default:
                        result = NcResult.Error (NcResult.SubKindEnum.Error_InvalidResponseType);
                        return;
                    }

                    pending = new McPending (AccountId, McAccount.AccountCapabilityEnum.CalWriter) {
                        Operation = McPending.Operations.CalRespond,
                        ServerId = item.ServerId,
                        ParentId = folder.ServerId,
                        CalResponse = apiResponse,
                    };
                    if (null != instance) {
                        pending.CalResponseInstance = (DateTime)instance;
                    }
                }
                pending.Insert ();
                result = NcResult.OK (pending.Token);
            });
            NcTask.Run (delegate {
                Sm.PostEvent ((uint)PcEvt.E.PendQHot, "ASPCRESPCAL");
            }, "RespondItemCmd");
            return result;
        }
    }
}