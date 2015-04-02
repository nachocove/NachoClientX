//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;

namespace NachoCore
{
    public class NcEmailArchiver
    {
        protected const string ArchiveFolderName = "Archive";

        public NcEmailArchiver ()
        {
        }

        public static void Move (McEmailMessage message, McFolder folder)
        {
            var src = McFolder.QueryByFolderEntryId<McEmailMessage> (message.AccountId, message.Id).FirstOrDefault ();
            if (src.Id == folder.Id) {
                return;
            }
            BackEnd.Instance.MoveEmailCmd (message.AccountId, message.Id, folder.Id);
        }

        public static void Move (McEmailMessageThread thread, McFolder folder)
        {
            foreach (var message in thread) {
                Move (message, folder);
            }
        }

        public static void Archive (McEmailMessage message)
        {
            McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (message.AccountId);
            Move (message, archiveFolder); // Do not archive messages in Archive folder
        }

        public static void Archive (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                Archive (message);
            }
        }

        public static void Delete (McEmailMessage message)
        {
            BackEnd.Instance.DeleteEmailCmd (message.AccountId, message.Id);
        }

        public static void Delete (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                Delete (message);
            }
        }
    }
}
