//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoClient.Build;

namespace NachoCore.Utils
{
    public class ScoringHelpers
    {
        private static int UserActionValue = 1;

        public static void SetTestMode (bool enable)
        {
            UserActionValue = (enable ? 2 : 1);
        }

        public static int ToggleHotOrNot (McEmailMessage message)
        {
            if (null == message) {
                return 0;
            }

            int oldUserAction = message.UserAction;
            int newUserAction = 0;

            switch (oldUserAction) {
            case 0:
                newUserAction = (message.isHot () ? -UserActionValue : UserActionValue);
                break;
            case 1:
            case 2:
                newUserAction = -UserActionValue;
                break;
            case -1:
            case -2:
                newUserAction = UserActionValue;
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            NcAssert.True (-1 == newUserAction || 1 == newUserAction || -2 == newUserAction || +2 == newUserAction);
            Log.Info (Log.LOG_BRAIN, "HotOrNot: Was = {0}, New = {1}", oldUserAction, newUserAction);
            NcTask.Run (() => {
                message = message.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.UserAction = newUserAction;
                    return true;
                });
                if (null != message) {
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated),
                        Account = McAccount.QueryById<McAccount> (message.AccountId),
                    });
                    if (oldUserAction != newUserAction) {
                        NcBrain.UpdateUserAction (message.AccountId, message.Id, message.UserAction);
                    }
                }
            }, "ToggleHotOrNot");
            return newUserAction;
        }

        public static void ToggleHotOrNot (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                ToggleHotOrNot (message);
            }
        }

    }
}

