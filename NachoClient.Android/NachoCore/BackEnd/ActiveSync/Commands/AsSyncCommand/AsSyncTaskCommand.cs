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
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeTask: No ServerId present.");
                return;
            }
            var applicationData = command.Element (Ns + Xml.AirSync.ApplicationData);
            var task = McTask.QueryByServerId<McTask> (folder.AccountId, xmlServerId.Value);
            if (null == task) {
                task = new McTask () {
                    AccountId = folder.AccountId,
                };
                try {
                    var result = task.FromXmlApplicationData (applicationData);
                    NcAssert.True (result.isOK());
                } catch (Exception ex) {
                    Log.Error (Log.LOG_AS, "Parse of Task failed on Add. ServerId {0}", xmlServerId.Value);
                }
                task.ServerId = xmlServerId.Value;
                task.Insert ();
                folder.Link (task);
            } else {
                try {
                    var result = task.FromXmlApplicationData (applicationData);
                    NcAssert.True (result.isOK());
                } catch (Exception ex) {
                    Log.Error (Log.LOG_AS, "Parse of Task failed on Change. ServerId {0}", xmlServerId.Value);
                }
                task.Update ();
            }
        }
    }
}

