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
        private class ApplyItemAdd : NcApplyServerCommand
        {
            public string ClassCode { get; set; }

            public string ServerId { get; set; }

            public XElement XmlCommand { get; set; }

            public McFolder Folder { get; set; }

            public ApplyItemAdd (int accountId)
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

                default:
                    cancelCommand = false;
                    action = McPending.DbActionEnum.DoNothing;
                    return null;
                }
            }

            protected override void ApplyCommandToModel ()
            {
                switch (ClassCode) {
                case Xml.AirSync.ClassCode.Contacts:
                    ServerSaysAddOrChangeContact (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Email:
                    ServerSaysAddOrChangeEmail (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Calendar:
                    ServerSaysAddOrChangeCalendarItem (AccountId, XmlCommand, Folder);
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

