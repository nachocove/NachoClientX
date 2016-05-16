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
            string cmdNameWithAccount = string.Format ("AsSyncCommand({0})", folder.AccountId);
            XNamespace Ns = Xml.AirSync.Ns;
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeTask: No ServerId present.", cmdNameWithAccount);
                return;
            }
            var applicationData = command.Element (Ns + Xml.AirSync.ApplicationData);
            // If the server attempts to overwrite, delete the pre-existing record first.
            var task = McTask.QueryByServerId<McTask> (folder.AccountId, xmlServerId.Value);
            if (Xml.AirSync.Add == command.Name.LocalName && null != task) {
                task.Delete ();
                task = null;
            }
            if (null == task) {
                task = new McTask () {
                    AccountId = folder.AccountId,
                };
                try {
                    var result = task.FromXmlApplicationData (applicationData);
                    NcAssert.True (result.isOK());
                } catch (Exception ex) {
                    Log.Error (Log.LOG_AS, "{0}: Parse of Task failed on Add. ServerId {1}, ex {2}", cmdNameWithAccount, xmlServerId.Value, ex);
                }
                task.ServerId = xmlServerId.Value;
                task.Insert ();
                folder.Link (task);
            } else {
                try {
                    var result = task.FromXmlApplicationData (applicationData);
                    NcAssert.True (result.isOK());
                } catch (Exception ex) {
                    Log.Error (Log.LOG_AS, "{0}: Parse of Task failed on Change. ServerId {1}, ex {2}", cmdNameWithAccount, xmlServerId.Value, ex);
                }
                task.Update ();
            }
        }
    }
}

