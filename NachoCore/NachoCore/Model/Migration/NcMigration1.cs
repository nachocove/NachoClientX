//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;

using MimeKit;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMigration1 : NcMigration
    {
        private static string[] tables = new string[] {
            "McAccount",
            "McConference",
            "McCred",
            "McMapFolderFolderEntry",
            "McFolder",
            "McEmailAddress",
            "McEmailMessage",
            "McEmailMessageCategory",
            "McEmailMessageDependency",
            "McMeetingRequest",
            "McAttachment",
            "McContact",
            "McContactDateAttribute",
            "McContactStringAttribute",
            "McContactAddressAttribute",
            "McContactEmailAddressAttribute",
            "McPolicy",
            "McProtocolState",
            "McServer",
            "McPending",
            "McPendDep",
            "McCalendar",
            "McException",
            "McAttendee",
            "McCalendarCategory",
            "McRecurrence",
            "McEvent",
            "McTask",
            "McBody",
            "McDocument",
            "McMutables",
            "McPath",
            "McNote",
            "McPortrait",
        };

        public NcMigration1 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return tables.Length;
        }

        public override void Run (CancellationToken token)
        {
            var thisVersion = Version ();
            foreach (var table in tables) {
                token.ThrowIfCancellationRequested ();

                Db.Execute (String.Format ("UPDATE {0} SET MigrationVersion=?", table), thisVersion);
                NcMigration.ProcessedObjects += 1;
            }
        }
    }
}

