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
        public void ServerSaysAddTask (XElement command, McFolder folder)
        {
            var xmlServerId = command.Element (m_ns + Xml.AirSync.ServerId);
            var applicationData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            var task = new McTask () {
                AccountId = BEContext.Account.Id,
            };
            var result = task.FromXmlApplicationData (applicationData);
            if (result.isOK ()) {
                task.ServerId = xmlServerId.Value;
                task.Insert ();
            } else {
                Log.Error (Log.LOG_AS, "Parse of Task failed. Task not added, ServerId {0}", xmlServerId.Value);
            }
        }

        public void ServerSaysChangeTask (XElement command, McFolder folder)
        {
            var xmlServerId = command.Element (m_ns + Xml.AirSync.ServerId);
            var applicationData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            var task = McTask.QueryByServerId<McTask> (BEContext.Account.Id, xmlServerId.Value);
            if (null == task) {
                Log.Error (Log.LOG_AS, "Bad state - getting Update for non-existent Task, ServerId {0}", xmlServerId.Value);
                ServerSaysAddTask (command, folder);
            } else {
                var result = task.FromXmlApplicationData (applicationData);
                if (result.isOK ()) {
                    task.Update ();
                } else {
                    Log.Error (Log.LOG_AS, "Parse of Task failed. Task not updated, ServerId {0}", xmlServerId.Value);
                }
            }
        }
    }
}

