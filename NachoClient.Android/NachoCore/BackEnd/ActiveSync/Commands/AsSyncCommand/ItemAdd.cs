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
        private class ApplyItemAdd : AsApplyServerCommand
        {
            public string ClassCode { get; set; }

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
                cancelCommand = false;
                action = McPending.DbActionEnum.DoNothing;
                return null;
            }

            protected override void ApplyCommandToModel ()
            {
                switch (ClassCode) {
                case Xml.AirSync.ClassCode.Contacts:
                    ServerSaysAddContact (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Email:
                    ServerSaysAddEmail (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Calendar:
                    ServerSaysAddCalendarItem (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Tasks:
                    ServerSaysAddTask (XmlCommand, Folder);
                    break;
                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionCommands UNHANDLED class " + ClassCode);
                    break;
                }
            }
        }
    }
}

