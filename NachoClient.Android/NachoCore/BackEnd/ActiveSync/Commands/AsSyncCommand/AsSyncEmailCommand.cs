//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        public static void ServerSaysChangeEmail (XElement command, McFolder folder)
        {
            ProcessEmailItem (command, folder, false);
        }

        public static McEmailMessage ServerSaysAddEmail (XElement command, McFolder folder)
        {
            return ProcessEmailItem (command, folder, true);
        }

        public static McEmailMessage ProcessEmailItem (XElement command, McFolder folder, bool isAdd)
        {   
            AsHelpers aHelp = new AsHelpers ();
            var r = aHelp.ParseEmail (Ns, command);
            McEmailMessage emailMessage = r.GetValue<McEmailMessage> ();
            bool justCreated = false;

            var eMsg = McFolderEntry.QueryByServerId<McEmailMessage> (folder.AccountId, emailMessage.ServerId);
            if (null == eMsg) {
                justCreated = true;
                emailMessage.AccountId = folder.AccountId;
            }

            if (justCreated) {
                /// SCORING - Score based on the # of emails in the thread.
                List<McEmailMessage> emailThread = 
                    McEmailMessage.QueryByThreadTopic (emailMessage.AccountId, emailMessage.ThreadTopic);
                if (0 < emailThread.Count) {
                    emailMessage.ContentScore += emailThread.Count;
                    Log.Info (Log.LOG_BRAIN, "SCORE: ThreadTopic={0}, Subject={1}, ContentScore={2}", 
                        emailMessage.ThreadTopic, emailMessage.Subject, emailMessage.ContentScore);
                }
                emailMessage.Insert ();
            } else {
                emailMessage.AccountId = folder.AccountId;
                emailMessage.Id = eMsg.Id;
                emailMessage.Update ();
            }

            folder.Link (emailMessage);
            if (!justCreated) {
                return null;
            }
            aHelp.InsertAttachments (emailMessage);
            return emailMessage;
        }
    }
}