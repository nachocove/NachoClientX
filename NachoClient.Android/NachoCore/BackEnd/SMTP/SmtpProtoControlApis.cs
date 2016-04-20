//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.SMTP
{
    public partial class SmtpProtoControl : NcProtoControl, IBEContext
    {
        public override NcResult MarkEmailAnswered (McPending pending, bool answered)
        {
            var answPending = new McPending (AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                Operation = McPending.Operations.EmailMarkAnswered,
                EmailSetFlag_FlagType = McPending.MarkAnsweredFlag,
                ServerId = pending.ServerId,
                ParentId = pending.ParentId,
            };
            answPending.Insert ();
            answPending.MarkPredBlocked (pending.Id);
            return NcResult.OK ();
        }
    }
}
