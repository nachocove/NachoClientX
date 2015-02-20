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
            var ua = message.UserAction;

            switch (message.UserAction) {
            case 0:
                message.UserAction = (message.isHot () ? -1 : 1);
                break;
            case 1:
                message.UserAction = -1;
                break;
            case -1:
                message.UserAction = 1;
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            Log.Info (Log.LOG_BRAIN, "HotOrNot: Was = {0}, New = {1}", ua, message.UserAction);
            message.Update ();
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated),
                Account = ConstMcAccount.NotAccountSpecific,
            });
            NcBrain.UpdateMessageScore (message.Id);
        }
    }
}

