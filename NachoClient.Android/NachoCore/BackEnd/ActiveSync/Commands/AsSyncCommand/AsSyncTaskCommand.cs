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
        public static void ServerSaysAddTask (XElement command, McFolder folder)
        {

        }

        public static void ServerSaysAddOrChangeTask (XElement command, McFolder folder)
        {
            XNamespace Ns = Xml.AirSync.Ns;
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            var applicationData = command.Element (Ns + Xml.AirSync.ApplicationData);
            var task = McTask.QueryByServerId<McTask> (folder.AccountId, xmlServerId.Value);
            if (null == task) {
                task = new McTask () {
                    AccountId = folder.AccountId,
                };
                var result = task.FromXmlApplicationData (applicationData);
                if (result.isOK ()) {
                    task.ServerId = xmlServerId.Value;
                    task.Insert ();
                    folder.Link (task);
                } else {
                    Log.Error (Log.LOG_AS, "Parse of Task failed. Task not added, ServerId {0}", xmlServerId.Value);
                }
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

