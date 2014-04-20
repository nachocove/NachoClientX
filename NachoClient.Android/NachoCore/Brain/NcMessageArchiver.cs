//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NcEmailArchiver
    {
        protected const string ArchiveFolderName = "Archive";

        public NcEmailArchiver ()
        {
        }

        public static void Archive (McEmailMessage message)
        {
            var type = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
            var folder = McFolder.GetUserFolder (message.AccountId, type, 0, ArchiveFolderName);
            if (null == folder) {
                BackEnd.Instance.CreateFolderCmd (message.AccountId, ArchiveFolderName, type, false, false);
            }
            folder = McFolder.GetUserFolder (message.AccountId, type, 0, ArchiveFolderName);
            NachoAssert.True (null != folder);

            BackEnd.Instance.MoveItemCmd (message.AccountId, message.Id, folder.Id);
        }

        public static void Delete(McEmailMessage message)
        {
            BackEnd.Instance.DeleteEmailCmd (message.AccountId, message.Id);
        }
    }
}

