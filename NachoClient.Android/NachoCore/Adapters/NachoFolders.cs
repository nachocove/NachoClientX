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
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedGeneric,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultInbox,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultDrafts,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultDeleted,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultSent,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultOutbox,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultTasks,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultCal,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultContacts,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultNotes,
        //        ProtoControl.FolderHierarchy.TypeCode.DefaultJournal,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedMail,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedCal,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedContacts,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedTasks,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedJournal,
        //        ProtoControl.FolderHierarchy.TypeCode.UserCreatedNotes,

        public static readonly ProtoControl.FolderHierarchy.TypeCode[] FilterForEmail = {
            ProtoControl.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            ProtoControl.FolderHierarchy.TypeCode.DefaultInbox_2,
            ProtoControl.FolderHierarchy.TypeCode.DefaultDrafts_3,
            ProtoControl.FolderHierarchy.TypeCode.DefaultDeleted_4,
            ProtoControl.FolderHierarchy.TypeCode.DefaultSent_5,
            ProtoControl.FolderHierarchy.TypeCode.DefaultOutbox_6,
            ProtoControl.FolderHierarchy.TypeCode.UserCreatedMail_12,
            ProtoControl.FolderHierarchy.TypeCode.Unknown_18,
        };

        public static readonly ProtoControl.FolderHierarchy.TypeCode[] FilterForCalendars = {
            ProtoControl.FolderHierarchy.TypeCode.DefaultCal_8,
            ProtoControl.FolderHierarchy.TypeCode.UserCreatedCal_13,
        };

        public static readonly ProtoControl.FolderHierarchy.TypeCode[] FilterForContacts = {
            ProtoControl.FolderHierarchy.TypeCode.DefaultContacts_9,
            ProtoControl.FolderHierarchy.TypeCode.UserCreatedContacts_14,
        };

        int accountId;
        List<McFolder> FoldersList;
        ProtoControl.FolderHierarchy.TypeCode[] types;

        // TODO: Should use Nacho type
        public NachoFolders (int accountId, ProtoControl.FolderHierarchy.TypeCode[] types)
        {
            this.accountId = accountId;
            this.types = types;
            Refresh ();
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
    }
}
