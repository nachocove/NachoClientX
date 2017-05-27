//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoFolders : INachoFolders
    {
        //        Xml.FolderHierarchy.TypeCode.UserCreatedGeneric,
        //        Xml.FolderHierarchy.TypeCode.DefaultInbox,
        //        Xml.FolderHierarchy.TypeCode.DefaultDrafts,
        //        Xml.FolderHierarchy.TypeCode.DefaultDeleted,
        //        Xml.FolderHierarchy.TypeCode.DefaultSent,
        //        Xml.FolderHierarchy.TypeCode.DefaultOutbox,
        //        Xml.FolderHierarchy.TypeCode.DefaultTasks,
        //        Xml.FolderHierarchy.TypeCode.DefaultCal,
        //        Xml.FolderHierarchy.TypeCode.DefaultContacts,
        //        Xml.FolderHierarchy.TypeCode.DefaultNotes,
        //        Xml.FolderHierarchy.TypeCode.DefaultJournal,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedMail,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedCal,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedContacts,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedTasks,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedJournal,
        //        Xml.FolderHierarchy.TypeCode.UserCreatedNotes,

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForEmail = {
            Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            Xml.FolderHierarchy.TypeCode.DefaultInbox_2,
            Xml.FolderHierarchy.TypeCode.DefaultDrafts_3,
            Xml.FolderHierarchy.TypeCode.DefaultDeleted_4,
            Xml.FolderHierarchy.TypeCode.DefaultSent_5,
            Xml.FolderHierarchy.TypeCode.DefaultOutbox_6,
            Xml.FolderHierarchy.TypeCode.UserCreatedMail_12,
            Xml.FolderHierarchy.TypeCode.Unknown_18,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForCalendars = {
            Xml.FolderHierarchy.TypeCode.DefaultCal_8,
            Xml.FolderHierarchy.TypeCode.UserCreatedCal_13,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForContacts = {
            Xml.FolderHierarchy.TypeCode.DefaultContacts_9,
            Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14,
        };

        int accountId;
        List<McFolder> FoldersList;
        Xml.FolderHierarchy.TypeCode[] types;

        // TODO: Should use Nacho type
        public NachoFolders (int accountId, Xml.FolderHierarchy.TypeCode[] types)
        {
            this.accountId = accountId;
            this.types = types;
            Refresh ();
        }

        public NachoFolders (params McFolder[] folders)
        {
            this.accountId = 0;
            this.types = new Xml.FolderHierarchy.TypeCode[0];
            FoldersList = new List<McFolder> (folders);
        }

        public void Refresh()
        {
            FoldersList = McFolder.QueryNonHiddenFoldersOfType (accountId, types);
        }

        public int Count ()
        {
            return FoldersList.Count;
        }

        public McFolder GetFolder (int i)
        {
            return FoldersList.ElementAt (i);
        }

        public McFolder GetFolderByFolderID(int id)
        {
            return McAbstrObject.QueryById<McFolder>(id);
        }

        public McFolder GetFirstOfTypeOrDefault (Xml.FolderHierarchy.TypeCode folderType)
        {
            if (FoldersList.Count > 0) {
                foreach (var folder in FoldersList) {
                    if (folder.Type == folderType) {
                        return folder;
                    }
                }
                return FoldersList [0];
            }
            return null;
        }
    }
}
