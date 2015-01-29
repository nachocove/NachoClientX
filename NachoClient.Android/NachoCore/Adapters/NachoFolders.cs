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
        };

        public static Xml.FolderHierarchy.TypeCode[] FilterForFolders = {
            Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            Xml.FolderHierarchy.TypeCode.DefaultInbox_2,
            Xml.FolderHierarchy.TypeCode.DefaultDrafts_3,
            Xml.FolderHierarchy.TypeCode.DefaultDeleted_4,
            Xml.FolderHierarchy.TypeCode.DefaultSent_5,
            Xml.FolderHierarchy.TypeCode.DefaultOutbox_6,
            Xml.FolderHierarchy.TypeCode.UserCreatedMail_12,
            Xml.FolderHierarchy.TypeCode.UserCreatedCal_13,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForCalendars = {
            Xml.FolderHierarchy.TypeCode.DefaultCal_8,
            Xml.FolderHierarchy.TypeCode.UserCreatedCal_13,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForContacts = {
            Xml.FolderHierarchy.TypeCode.DefaultContacts_9,
            Xml.FolderHierarchy.TypeCode.UserCreatedContacts_14,
        };

        List<McFolder> list;
        Xml.FolderHierarchy.TypeCode[] types;

        // TODO: Should use Nacho type
        public NachoFolders (Xml.FolderHierarchy.TypeCode[] types)
        {
            this.types = types;
            Refresh ();
        }

        public void Refresh()
        {
            // TODO: Make this a query
            list = new List<McFolder> ();
            var account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            if (null == account) {
                return;
            }
            var temp = NcModel.Instance.Db.Table<McFolder> ().Where (f => f.AccountId == account.Id).OrderBy (f => f.DisplayName).ToList ();
            foreach (var l in temp) {
                if (!l.IsHidden && (!l.IsClientOwned || DraftsHelper.IsDraftsFolder(l))) {
                    // TODO: Need a matching enumeration
                    var match = (Xml.FolderHierarchy.TypeCode)l.Type;
                    if (Array.IndexOf (types, match) >= 0) {
                        list.Add (l);
                    }
                }
            } 
        }

        public int Count ()
        {
            return list.Count;
        }

        public McFolder GetFolder (int i)
        {
            return list.ElementAt (i);
        }

        public McFolder GetFolderByFolderID(int id)
        {
            return McAbstrObject.QueryById<McFolder>(id);
        }
    }
}
