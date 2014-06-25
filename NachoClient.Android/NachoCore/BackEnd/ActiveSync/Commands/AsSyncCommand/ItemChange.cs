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
        private class ApplyItemChange : AsApplyServerCommand
        {
            public string ClassCode { get; set; }

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
                cancelCommand = false;
                action = McPending.DbActionEnum.DoNothing;
                return null;
            }

            protected override void ApplyCommandToModel ()
            {
                switch (ClassCode) {
                case Xml.AirSync.ClassCode.Email:
                    ServerSaysChangeEmail (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Calendar:
                    ServerSaysChangeCalendarItem (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Contacts:
                    ServerSaysChangeContact (XmlCommand, Folder);
                    break;
                case Xml.AirSync.ClassCode.Tasks:
                    ServerSaysChangeTask (XmlCommand, Folder);
                    break;
                default:
                    Log.Error (Log.LOG_AS, "AsSyncCommand ProcessCollectionCommands UNHANDLED class " + ClassCode);
                    break;
                }
            }
        }
    }
}

