//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        private class ApplyItemChange : NcApplyServerCommand
        {
            public string ClassCode { get; set; }

            public string ServerId { get; set; }

            public XElement XmlCommand { get; set; }

            public McFolder Folder { get; set; }

            public ApplyItemChange (int accountId)
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
                    cancelCommand = false;
                    action = (pending.ServerId == ServerId &&
                        !AsHelpers.EmailMessageHasAttachment (XmlCommand, pending.AttachmentId)) ?
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.CalRespond:
                    cancelCommand = false;
                    action = (pending.ServerId == ServerId &&
                        AsHelpers.TimeOrLocationChanged (XmlCommand, ServerId)) ?
                        McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
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
                    cancelCommand = (pending.ServerId == ServerId);
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.EmailClearFlag:
                case McPending.Operations.EmailMarkFlagDone:
                case McPending.Operations.EmailMarkRead:
                case McPending.Operations.EmailSetFlag:
                    // TODO: Ex: server Change sets is-read flag, we could delete a EmailMarkRead pending.
                    // Should be harmless. Note that in the set-flag cases, the client can win (rather than the
                    // server) with the current implementation.
                    cancelCommand = false;
                    action = McPending.DbActionEnum.DoNothing;
                    return null;

                case McPending.Operations.CalUpdate:
                case McPending.Operations.ContactUpdate:
                case McPending.Operations.TaskUpdate:
                    cancelCommand = false;
                    action = (pending.ServerId == ServerId) ?
                    McPending.DbActionEnum.Delete : McPending.DbActionEnum.DoNothing;
                    return null;

                default:
                    cancelCommand = false;
                    action = McPending.DbActionEnum.DoNothing;
                    return null;
                }
            }

            protected override void ApplyCommandToModel ()
            {
                switch (ClassCode) {
                case Xml.AirSync.ClassCode.Email:
                    ServerSaysAddOrChangeEmail (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Calendar:
                    ServerSaysAddOrChangeCalendarItem (AccountId, XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Contacts:
                    ServerSaysAddOrChangeContact (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Tasks:
                    ServerSaysAddOrChangeTask (XmlCommand, Folder);
                    break;
                default:
                    Log.Error (Log.LOG_AS, "{0} ProcessCollectionCommands UNHANDLED class {1}", CmdNameWithAccount, ClassCode);
                    break;
                }
            }
        }
    }
}

