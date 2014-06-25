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
                cancelCommand = false;
                action = McPending.DbActionEnum.DoNothing;
                return null;
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

