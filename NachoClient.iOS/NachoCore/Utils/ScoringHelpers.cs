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

        public static void InitTestMode ()
        {
            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
                TestMode.Instance.Add ("markhoton", (parameters) => {
                    ScoringHelpers.UserActionValue = 2;
                    Console.WriteLine ("!!!!! ENTER MARKHOT TEST MODE !!!!!");
                });
                TestMode.Instance.Add ("markhotoff", (parameters) => {
                    ScoringHelpers.UserActionValue = 1;
                    Console.WriteLine ("!!!!! EXIT MARKHOT TEST MODE !!!!!");
                });
            }
        }

        public static void ToggleHotOrNot (McEmailMessage message)
        {
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

