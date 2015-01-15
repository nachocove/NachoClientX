//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;
using System.Text;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    public class DraftsHelper
    {
        public enum DraftType {
            Email,
            Calendar,
        }

        public class DraftInfo
        {
            public string subject;
            public string recipients;
            public string date;
            public string body;
            public DraftsHelper.DraftType type;

            public DraftInfo (string subject, string recipients, string date, string body, DraftsHelper.DraftType type)
            {
                this.subject = subject;
                this.recipients = recipients;
                this.date = date;
                this.body = body;
                this.type = type;
            }

            public DraftInfo ()
            {

            }
        }

        //TODO: Might want this eventually
//        public static bool IsDraftItem (McAbstrItem item, DraftType type)
//        {
//            switch (type) {
//            case DraftType.Calendar:
//                McCalendar calendar = (McCalendar)item;
//            case DraftType.Email:
//                //TODO: email
//                return false;
//            default:
//                return false;
//            }
//        }

        public static bool IsDraftsFolder (McFolder folder)
        {
            //TODO: Expand for email-drafts
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedCal_13 == folder.Type) {
                return true;
            }
            return false;
        }

        public static DraftType FolderToDraftType (McFolder folder)
        {
            switch (folder.Type) {
            case Xml.FolderHierarchy.TypeCode.UserCreatedCal_13:
                return DraftsHelper.DraftType.Calendar;
            case Xml.FolderHierarchy.TypeCode.UserCreatedMail_12:
                return DraftsHelper.DraftType.Email;
            }
            return DraftsHelper.DraftType.Email;
        }

        public static List<McCalendar> GetCalendarDrafts (int accountId)
        {
            List<McCalendar> calendarDrafts = McCalendar.QueryByFolderId<McCalendar> (accountId, McFolder.GetCalendarDraftsFolder (accountId).Id);
            Console.WriteLine (calendarDrafts.Count);
            return calendarDrafts;
        }

        protected static string GetSubject (DraftType type, McAbstrItem draft)
        {
            switch (type) {
            case DraftType.Calendar:
                McCalendar draftCalendar = (McCalendar)draft;
                if (!String.IsNullOrEmpty (draftCalendar.Subject)) {
                    return draftCalendar.Subject;
                } 
                return "This event has no title";
            case DraftType.Email:
                //TODO: Email Subject
                return "";
            default:
                return "";
            }
        }

        protected static string GetRecipients (McAbstrItem draft, DraftType type)
        {
            switch (type) {
            case DraftType.Calendar:
                McCalendar draftCalendar = (McCalendar)draft;
                if (!String.IsNullOrEmpty (CalendarHelper.AttendeesToString (draftCalendar.attendees))) {
                    return CalendarHelper.AttendeesToString (draftCalendar.attendees);
                }
                return "No attendees have been chosen";
            case DraftType.Email:
                //TODO: Email Recipients
                return "";
            default:
                return "";
            }
        }

        protected static string GetDate (McAbstrItem draft, DraftType type)
        {
            switch (type) {
            case DraftType.Calendar:
                McCalendar draftCalendar = (McCalendar)draft;
                return Pretty.FullDateTimeString (draftCalendar.StartTime);
            case DraftType.Email:
                //TODO: Email Date
                return "";
            default:
                return "";
            }
        }

        protected static string GetBody (McAbstrItem draft, DraftType type)
        {
            switch (type) {
            case DraftType.Calendar:
                McCalendar draftCalendar = (McCalendar)draft;
                if (null != McBody.GetContentsString (draftCalendar.BodyId)) {
                    return McBody.GetContentsString (draftCalendar.BodyId);
                }
                return "No event description provided";
            case DraftType.Email:
                //TODO: Email Body
                return "";
            default:
                return "";
            }
        }

        public static DraftInfo DraftToDraftInfo (McAbstrItem draft, DraftType type)
        {
            return new DraftInfo (
                GetSubject(type, draft),
                GetRecipients(draft, type),
                GetDate (draft, type),
                GetBody(draft, type),
                type
            );
        }
    }
}

