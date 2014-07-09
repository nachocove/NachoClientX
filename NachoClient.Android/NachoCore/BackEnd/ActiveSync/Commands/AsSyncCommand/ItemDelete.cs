﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        private class ApplyItemDelete : AsApplyServerCommand
        {
            public string ClassCode { get; set; }

            public string ServerId { get; set; }

            public ApplyItemDelete (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
                                                                              out McPending.DbActionEnum action,
                                                                              out bool cancelCommand)
            {
                switch (pending.Operation) {
                case McPending.Operations.FolderDelete:
                    cancelCommand = pending.ServerIdDominatesCommand (ServerId);
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.AttachmentDownload:
                case McPending.Operations.CalRespond:
                    cancelCommand = false;
                    action = (pending.ServerId == ServerId) ?
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.EmailMove:
                case McPending.Operations.CalMove:
                case McPending.Operations.ContactMove:
                case McPending.Operations.TaskMove:
                case McPending.Operations.EmailClearFlag:
                case McPending.Operations.EmailMarkFlagDone:
                case McPending.Operations.EmailMarkRead:
                case McPending.Operations.EmailSetFlag:
                case McPending.Operations.CalUpdate:
                case McPending.Operations.ContactUpdate:
                case McPending.Operations.TaskUpdate:
                    cancelCommand = false;
                    if (pending.ServerId == ServerId) {
                        var item = pending.QueryItemUsingServerId ();
                        McFolder.UnlinkAll (item);
                        var laf = McFolder.GetLostAndFoundFolder (AccountId);
                        laf.Link (item);
                        action = McPending.DbActionEnum.Delete;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                case McPending.Operations.EmailForward:
                case McPending.Operations.EmailReply:
                    cancelCommand = false;
                    if (pending.ServerId == ServerId) {
                        pending.ConvertToEmailSend ();
                        action = McPending.DbActionEnum.Update;
                    } else {
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                case McPending.Operations.EmailDelete:
                case McPending.Operations.CalDelete:
                case McPending.Operations.ContactDelete:
                case McPending.Operations.TaskDelete:
                    if (pending.ServerId == ServerId) {
                        cancelCommand = true;
                        action = McPending.DbActionEnum.Delete;
                    } else {
                        cancelCommand = false;
                        action = McPending.DbActionEnum.DoNothing;
                    }
                    return null;

                default:
                    cancelCommand = false;
                    action = McPending.DbActionEnum.DoNothing;
                    return null;
                }
            }

            protected override void ApplyCommandToModel ()
            {
                McItem item = null;
                switch (ClassCode) {
                case Xml.AirSync.ClassCode.Email:
                    item = McFolderEntry.QueryByServerId<McEmailMessage> (AccountId, ServerId);
                    break;
                case Xml.AirSync.ClassCode.Calendar:
                    item = McFolderEntry.QueryByServerId<McCalendar> (AccountId, ServerId);
                    break;
                case Xml.AirSync.ClassCode.Contacts:
                    item = McFolderEntry.QueryByServerId<McContact> (AccountId, ServerId);
                    break;
                case Xml.AirSync.ClassCode.Tasks:
                    item = McFolderEntry.QueryByServerId<McTask> (AccountId, ServerId);
                    break;
                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionCommands UNHANDLED class " + ClassCode);
                    break;
                }
                if (null != item) {
                    item.Delete ();
                }
            }
        }
    }
}

