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

        private static void RemoveDeadline (McEmailMessage message)
        {
            if (message.HasDueDate ()) {
                Brain.NcMessageDeferral.RemoveDueDate (message);
            }
        }

        public static void Move (McEmailMessage message, McFolder folder)
        {
            RemoveDeferral (message);
            BackEnd.Instance.MoveEmailCmd (message.AccountId, message.Id, folder.Id);
        }

        public static void Move (List<McEmailMessage> messages, McFolder folder)
        {
            if (0 == messages.Count) {
                return;
            }

            var accountSet = EmailHelper.AccountSet (messages);
            NcAssert.True (1 == accountSet.Count);

            foreach (var message in messages) {
                RemoveDeferral (message);
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

        private static bool ShouldDeleteInsteadOfArchive (int accountId)
        {
            // Google doesn't archive. All messages are deemed 'archived' by being in the 'All Mails' folder (aka label).
            // Archiving is simply deleting from the current folder (i.e. removing the label for the folder), and finding
            // it via Search or directly in the All Mails folder. See https://support.google.com/mail/answer/6576?hl=en
            return McAccount.QueryById<McAccount> (accountId).AccountService.HasFlag (McAccount.AccountServiceEnum.GoogleDefault);
        }


        public static void Archive (McEmailMessage message)
        {
            if (ShouldDeleteInsteadOfArchive (message.AccountId)) {
                Delete (message, true);
            } else {
                McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (message.AccountId);
                Move (message, archiveFolder); // Do not archive messages in Archive folder
            }
        }

        public static void Archive (List<McEmailMessage> messages)
        {
            if (0 == messages.Count) {
                return;
            }
            foreach (var message in messages) {
                RemoveDeferral (message);
            }
            var accountSet = EmailHelper.AccountSet (messages);
            foreach (var accountId in accountSet) {
                var Ids = messages.Where(x => x.AccountId == accountId).Select(x => x.Id).ToList();
                if (ShouldDeleteInsteadOfArchive (accountId)) {
                    BackEnd.Instance.DeleteEmailsCmd (accountId, Ids, true);
                } else {
                    McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (accountId);
                    BackEnd.Instance.MoveEmailsCmd (accountId, Ids, archiveFolder.Id);
                }
            }
        }

        public static void Archive (McEmailMessageThread thread)
        {
            var messages = new List<McEmailMessage> ();
            foreach (var message in thread) {
                messages.Add (message);
            }
            Archive (messages);
        }

        public static void Delete (McEmailMessage message, bool justDelete = false)
        {
            RemoveDeferral (message);
            RemoveDeadline (message);
            BackEnd.Instance.DeleteEmailCmd (message.AccountId, message.Id, justDelete);
        }

        public static void Delete (List<McEmailMessage> messages, bool justDelete = false)
        {
            if (0 == messages.Count) {
                return;
            }
            foreach (var message in messages) {
                RemoveDeferral (message);
                RemoveDeadline (message);
            }
            var accountSet = EmailHelper.AccountSet (messages);
            foreach (var accountId in accountSet) {
                var Ids = messages.Where (x => x.AccountId == accountId).Select (x => x.Id).ToList ();
                BackEnd.Instance.DeleteEmailsCmd (accountId, Ids, justDelete);
            }
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
