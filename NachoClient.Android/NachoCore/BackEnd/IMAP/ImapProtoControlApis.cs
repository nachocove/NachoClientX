//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public partial class ImapProtoControl : NcProtoControl, IBEContext
    {
        public override NcResult SetEmailFlagCmd (int emailMessageId, string flagType, 
            DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                // TODO Do something to save this on the server.
                // Set the Flag info in the DB item.
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
                emailMessage.FlagType = flagType;
                emailMessage.FlagStartDate = start;
                emailMessage.FlagUtcStartDate = utcStart;
                emailMessage.FlagDue = due;
                emailMessage.FlagUtcDue = utcDue;
                emailMessage.Update ();
                result = NcResult.OK ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded));
            Log.Warn (Log.LOG_IMAP, "Flag saved in DB, but not on server.");
            return result;
        }

        public override NcResult ClearEmailFlagCmd (int emailMessageId)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }

                result = NcResult.OK ();
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
                emailMessage.Update ();
            });
            StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded));
            Log.Warn (Log.LOG_IMAP, "Flag cleared in DB, but not on server.");
            return result;
        }

        public override NcResult MarkEmailFlagDone (int emailMessageId,
            DateTime completeTime, DateTime dateCompleted)
        {
            NcResult result = NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure);
            NcResult.SubKindEnum subKind;
            McEmailMessage emailMessage;
            McFolder folder;
            NcModel.Instance.RunInTransaction (() => {
                if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, -1, out folder, out subKind)) {
                    result = NcResult.Error (subKind);
                    return;
                }
                result = NcResult.OK ();
                emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
                emailMessage.FlagCompleteTime = completeTime;
                emailMessage.FlagDateCompleted = dateCompleted;
                emailMessage.Update ();
            });
            Log.Warn (Log.LOG_IMAP, "Marked Done in DB, but not on server.");
            return result;
        }
    }
}

