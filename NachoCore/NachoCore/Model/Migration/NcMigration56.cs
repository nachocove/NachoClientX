//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    /// <summary>
    /// Remove obsolete indexes from the database.
    /// </summary>
    /// <remarks>
    /// Indexes have been overused in the past.  Indexes were maintained that were never used in any way,
    /// or that were not useful due to a small number of distinct values in the column.  Removing the
    /// [Indexed] attribute from the class definition does not delete the index from existing databases.
    /// The indexes have to be dropped explicitly, which is what this migration does.
    /// </remarks>
    public class NcMigration56 : NcMigration
    {
        private string[] obsoleteIndexes = {
            "McAttachment_IsValid",
            "McBody_IsValid",
            "McDocument_IsValid",
            "McPortrait_IsValid",
            "McCalendar_IsAwaitingDelete",
            "McContact_IsAwaitingDelete",
            "McEmailMessage_IsAwaitingDelete",
            "McException_IsAwaitingDelete",
            "McFolder_IsAwaitingDelete",
            "McMeetingRequest_IsAwaitingDelete",
            "McTask_IsAwaitingDelete",
            "McCalendar_IsAwaitingCreate",
            "McContact_IsAwaitingCreate",
            "McEmailMessage_IsAwaitingCreate",
            "McException_IsAwaitingCreate",
            "McFolder_IsAwaitingCreate",
            "McMeetingRequest_IsAwaitingCreate",
            "McTask_IsAwaitingCreate",
            "McCalendar_OwnerEpoch",
            "McContact_OwnerEpoch",
            "McEmailMessage_OwnerEpoch",
            "McException_OwnerEpoch",
            "McMeetingRequest_OwnerEpoch",
            "McTask_OwnerEpoch",
            "McCalendar_HasBeenGleaned",
            "McContact_HasBeenGleaned",
            "McEmailMessage_HasBeenGleaned",
            "McException_HasBeenGleaned",
            "McMeetingRequest_HasBeenGleaned",
            "McTask_HasBeenGleaned",
            "McAccount_MigrationVersion",
            "McAttachment_MigrationVersion",
            "McAttendee_MigrationVersion",
            "McBody_MigrationVersion",
            "McBrainEvent_MigrationVersion",
            "McCalendarCategory_MigrationVersion",
            "McCalendar_MigrationVersion",
            "McChatMessage_MigrationVersion",
            "McChatParticipant_MigrationVersion",
            "McChat_MigrationVersion",
            "McConference_MigrationVersion",
            "McContactAddressAttribute_MigrationVersion",
            "McContactDateAttribute_MigrationVersion",
            "McContactEmailAddressAttribute_MigrationVersion",
            "McContactStringAttribute_MigrationVersion",
            "McContact_MigrationVersion",
            "McCred_MigrationVersion",
            "McDocument_MigrationVersion",
            "McEmailAddressScore_MigrationVersion",
            "McEmailAddress_MigrationVersion",
            "McEmailMessageCategory_MigrationVersion",
            "McEmailMessageDependency_MigrationVersion",
            "McEmailMessageNeedsUpdate_MigrationVersion",
            "McEmailMessageScore_MigrationVersion",
            "McEmailMessage_MigrationVersion",
            "McEvent_MigrationVersion",
            "McException_MigrationVersion",
            "McFolder_MigrationVersion",
            "McLicenseInformation_MigrationVersion",
            "McMapAttachmentItem_MigrationVersion",
            "McMapEmailAddressEntry_MigrationVersion",
            "McMapFolderFolderEntry_MigrationVersion",
            "McMeetingRequest_MigrationVersion",
            "McMigration_MigrationVersion",
            "McMutables_MigrationVersion",
            "McNote_MigrationVersion",
            "McPath_MigrationVersion",
            "McPendDep_MigrationVersion",
            "McPending_MigrationVersion",
            "McPolicy_MigrationVersion",
            "McPortrait_MigrationVersion",
            "McProtocolState_MigrationVersion",
            "McRecurrence_MigrationVersion",
            "McServer_MigrationVersion",
            "McTask_MigrationVersion",
            "McContact_Source",
            "McContact_IsVip",
            "McContact_IndexVersion",
            "McEmailMessage_IsRead",
            "McFolder_IsClientOwned",
            "McFolder_IsHidden",
            "McPending_DeferredSerialIssueOnly",
            "McPending_DelayNotAllowed",
        };

        public override int GetNumberOfObjects ()
        {
            return obsoleteIndexes.Length;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var db = NcModel.Instance.Db;
            foreach (var obsoleteIndex in obsoleteIndexes) {
                token.ThrowIfCancellationRequested ();
                db.Execute (string.Format ("DROP INDEX IF EXISTS {0}", obsoleteIndex));
                UpdateProgress (1);
            }
        }
    }
}

