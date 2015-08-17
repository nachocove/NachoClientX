//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoCore.Utils
{
    public class ScoringHelpers
    {
        public static void ToggleHotOrNot (McEmailMessage message)
        {
            int oldUserAction = message.UserAction;
            int newUserAction = 0;

            switch (oldUserAction) {
            case 0:
                newUserAction = (message.isHot () ? -1 : 1);
                break;
            case 1:
                newUserAction = -1;
                break;
            case -1:
                newUserAction = 1;
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            NcAssert.True (-1 == newUserAction || 1 == newUserAction);
            Log.Info (Log.LOG_BRAIN, "HotOrNot: Was = {0}, New = {1}", oldUserAction, newUserAction);
            message = message.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.UserAction = newUserAction;
                return true;
            });
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated),
                Account = McAccount.QueryById<McAccount> (message.AccountId),
            });
            if (oldUserAction != newUserAction) {
                NcBrain.UpdateUserAction (message.AccountId, message.Id, message.UserAction);
            }
        }

        public static void ToggleHotOrNot (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                ToggleHotOrNot (message);
            }
        }

    }
}

