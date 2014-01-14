//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.UIKit;
using NachoClient.iOS;
using NachoCore;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoClient
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
            Xml.FolderHierarchy.TypeCode.DefaultInbox,
            Xml.FolderHierarchy.TypeCode.DefaultDrafts,
            Xml.FolderHierarchy.TypeCode.DefaultDeleted,
            Xml.FolderHierarchy.TypeCode.DefaultSent,
            Xml.FolderHierarchy.TypeCode.DefaultOutbox,
            Xml.FolderHierarchy.TypeCode.UserCreatedMail,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForCalendars = {
            Xml.FolderHierarchy.TypeCode.DefaultCal,
            Xml.FolderHierarchy.TypeCode.UserCreatedCal,
        };

        public static readonly Xml.FolderHierarchy.TypeCode[] FilterForContacts = {
            Xml.FolderHierarchy.TypeCode.DefaultContacts,
            Xml.FolderHierarchy.TypeCode.UserCreatedContacts,
        };

        List<McFolder> list;

        // TODO: Should use Nacho type
        public NachoFolders (Xml.FolderHierarchy.TypeCode[] types)
        {
            // TODO: Fix this
            list = new List<McFolder> ();
            var temp = BackEnd.Instance.Db.Table<McFolder> ().OrderBy (f => f.DisplayName).ToList ();
            foreach (var l in temp) {
                if (!l.IsHidden) {
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
    }
}
