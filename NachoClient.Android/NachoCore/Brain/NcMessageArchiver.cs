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

        public static void Archive (McEmailMessage message)
        {
            McFolder archiveFolder = McFolder.GetOrCreateArchiveFolder (message.AccountId);
            BackEnd.Instance.MoveEmailCmd (message.AccountId, message.Id, archiveFolder.Id);
        }

        public static void Delete (McEmailMessage message)
        {
            BackEnd.Instance.DeleteEmailCmd (message.AccountId, message.Id);
        }
    }
}

