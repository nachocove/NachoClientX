//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;

namespace NachoCore.SMTP
{
    public partial class SmtpProtoControl : NcProtoControl, IBEContext
    {
        public override NcResult MarkEmailAnswered (McPending pending, McEmailMessage email, bool answered)
        {
            // create a pending that the EmailReaderWriter will pick up (i.e. IMAP).
            var answPending = new McPending (AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter) {
                Operation = McPending.Operations.EmailMarkAnswered,
                EmailSetFlag_FlagType = answered ? McPending.MarkAnsweredFlag : McPending.MarkNotAnsweredFlag,
                ServerId = pending.ServerId,
                ParentId = pending.ParentId,
            };
            answPending.Insert ();
            answPending.MarkPredBlocked (pending.Id);

            int lastVerb;
            DateTime lastVerbTime;
            if (answered) {
                if (email.To.Split (',').ToList ().Count == 1 && string.IsNullOrEmpty (email.Cc)) {
                    lastVerb = (int)AsLastVerbExecutedType.REPLYTOSENDER;
                } else {
                    lastVerb = (int)AsLastVerbExecutedType.REPLYTOALL;
                }
                lastVerbTime = DateTime.UtcNow;
            } else {
                lastVerb = (int)AsLastVerbExecutedType.UNKNOWN;
                lastVerbTime = DateTime.MinValue;
            }

            email = email.UpdateWithOCApply<McEmailMessage> (((record) => {
                var target = (McEmailMessage)record;
                target.LastVerbExecuted = lastVerb;
                target.LastVerbExecutionTime = lastVerbTime;
                return true;
            }));
            return base.MarkEmailAnswered (pending, email, answered);
        }

        public override NcResult MarkMessageForwarded (McPending pending, McEmailMessage email, bool forwarded)
        {
            // IMAP has no \Forwarded flag, so we just don't bother with any pending or command (unlike 'Answered').

            int lastVerb;
            DateTime lastVerbTime;
            if (forwarded) {
                lastVerb = (int)AsLastVerbExecutedType.FORWARD;
                lastVerbTime = DateTime.UtcNow;
            } else {
                lastVerb = (int)AsLastVerbExecutedType.UNKNOWN;
                lastVerbTime = DateTime.MinValue;
            }

            email = email.UpdateWithOCApply<McEmailMessage> (((record) => {
                var target = (McEmailMessage)record;
                target.LastVerbExecuted = lastVerb;
                target.LastVerbExecutionTime = lastVerbTime;
                return true;
            }));
            return base.MarkMessageForwarded (pending, email, forwarded);
        }
    }
}
