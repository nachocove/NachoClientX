//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;

namespace NachoCore
{
    public static class NcEmailArchiver
    {
        private const string ArchiveFolderName = "Archive";

        // By our rule, if a user moves or deletes a deferred
        // message, the deferral is removed. A deferral won't
        // be removed when a message is re-sent (e.g. reply).
        private static void RemoveDeferral (McEmailMessage message)
        {
            if (message.IsDeferred ()) {
                Brain.NcMessageDeferral.UndeferMessage (message);
            }
        }

        public static void Move (McEmailMessage message, McFolder folder)
        {
            RemoveDeferral (message);
            var src = McFolder.QueryByFolderEntryId<McEmailMessage> (message.AccountId, message.Id).FirstOrDefault ();
            if (src.Id == folder.Id) {
                return;
            }
            BackEnd.Instance.MoveEmailCmd (message.AccountId, message.Id, folder.Id);
        }

        public static void Move (List<McEmailMessage> messages, McFolder folder)
        {
            if (0 == messages.Count) {
                return;
            }
            var Ids = messages.Select (x => x.Id).ToList ();
            BackEnd.Instance.MoveEmailsCmd (folder.AccountId, Ids, folder.Id);
        }

        public static void Move (McEmailMessageThread thread, McFolder folder)
        {
            var messages = new List<McEmailMessage> ();
            foreach (var message in thread) {
                messages.Add (message);
            }
            Move (messages, folder);
        }

        public static void Archive (McEmailMessage message)
        {
            McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (message.AccountId);
            Move (message, archiveFolder); // Do not archive messages in Archive folder
        }

        public static void Archive (List<McEmailMessage> messages)
        {
            if (0 == messages.Count) {
                return;
            }
            var Ids = messages.Select (x => x.Id).ToList ();
            int accountId = messages [0].AccountId;
            McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (accountId);
            BackEnd.Instance.MoveEmailsCmd (accountId, Ids, archiveFolder.Id);
        }

        public static void Archive (McEmailMessageThread thread)
        {
            var messages = new List<McEmailMessage> ();
            foreach (var message in thread) {
                messages.Add (message);
            }
            Archive (messages);
        }

        public static void Delete (McEmailMessage message)
        {
            RemoveDeferral (message);
            BackEnd.Instance.DeleteEmailCmd (message.AccountId, message.Id);
        }

        public static void Delete (List<McEmailMessage> messages)
        {
            if (0 == messages.Count) {
                return;
            }
            var Ids = messages.Select (x => x.Id).ToList ();
            int accountId = messages [0].AccountId;
            BackEnd.Instance.DeleteEmailsCmd (accountId, Ids);
        }

        public static void Delete (McEmailMessageThread thread)
        {
            var messages = new List<McEmailMessage> ();
            foreach (var message in thread) {
                messages.Add (message);
            }
            Delete (messages);
        }
    }
}
