//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public class NcMessageDeferral
    {
        /// User's first day of week
        /// TODO: Must be configurable
        const DayOfWeek FirstDayOfWork = DayOfWeek.Monday;
        /// User's first time in the morning
        /// TODO: Must be configurable
        const double DawnOffset = 0.0d;

        public NcMessageDeferral ()
        {
        }

        /// <summary>
        /// Defer the messages in a the thread.
        /// </summary>
        /// <returns>An NcResult with the status of the update.</returns>
        /// <param name="thread">Message list</param>
        /// <param name="deferralType">Delay type.</param>
        static public NcResult DeferThread (McEmailMessageThread thread, MessageDeferralType deferralType)
        {
            NcResult r = ComputeDeferral (DateTime.Now, deferralType);
            if (r.isError ()) {
                return r;
            }
            var deferUntil = r.GetValue<DateTime> ();
            return DeferThread (thread, deferralType, deferUntil);
        }

        static public NcResult DeferThread (McEmailMessageThread thread, MessageDeferralType deferralType, DateTime deferUntil)
        {
            foreach (var message in thread) {
                message.DeferralType = deferralType;
                message.Update ();
                var utc = deferUntil;
                var local = deferUntil.LocalT ();
                BackEnd.Instance.SetEmailFlagCmd (message.AccountId, message.Id, "Defer until", local, utc, local, utc);
                NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            }
            return NcResult.OK ();
        }

        static public NcResult ClearMessageFlags (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                BackEnd.Instance.ClearEmailFlagCmd (message.AccountId, message.Id);
                NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            }
            return NcResult.OK ();
        }

        static public NcResult UndeferThread (McEmailMessageThread thread)
        {
            return ClearMessageFlags (thread);
        }

        static public NcResult SetDueDate (McEmailMessageThread thread, DateTime dueOn)
        {
            foreach (var message in thread) {
                var start = DateTime.UtcNow;
                BackEnd.Instance.SetEmailFlagCmd (message.AccountId, message.Id, "For follow up by", start.LocalT (), start, dueOn.LocalT (), dueOn);
                NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            }
            return NcResult.OK ();
        }

        /// <summary>
        /// Computes the time to defer.
        /// </summary>
        /// <returns>The UTC time when the message is again visible.</returns>
        /// <param name="from">Start time for the computation.</param>
        /// <param name="deferralType">Deferral type.</param>
        private static NcResult ComputeDeferral (DateTime from, MessageDeferralType deferralType)
        {
            switch (deferralType) {
            case MessageDeferralType.Later:
                // TODO: Probaly want to choose next free hour
                from = from.AddHours (4.0d);
                break;
            case MessageDeferralType.Tonight:
                if (from.Hour >= 18.0d) {
                    // Later this evening...
                    from = from.AddHours (4.0d);
                } else {
                    // Until six pm
                    from = from.AddHours (18.0 - from.Hour);
                }
                break;
            case MessageDeferralType.Tomorrow:
                from = from.AddDays (1d);
                from = AdjustToDawn (from);
                break;
            case MessageDeferralType.NextWeek:
                do {
                    from = from.AddDays (1.0d);
                } while(from.DayOfWeek != FirstDayOfWork);
                from = AdjustToDawn (from);
                break;
            case MessageDeferralType.MonthEnd:
                // Last day
                from = from.AddMonths (1);
                from = from.AddDays (-from.Day); // Day is 1..31
                from = AdjustToDawn (from);
                break;
            case MessageDeferralType.NextMonth:
                // First day
                from = from.AddMonths (1);
                from = from.AddDays (1.0 - from.Day); // Day is 1..32
                from = AdjustToDawn (from);
                break;
            case MessageDeferralType.Forever:
                from = DateTime.MaxValue;
                break;
            case MessageDeferralType.Custom:
            case MessageDeferralType.None:
            default:
                NcAssert.CaseError ();
                return NcResult.Error (String.Format ("ComputeDeferral; {0} was unexpected", deferralType));
            }
            return NcResult.OK (from.ToUniversalTime ());
        }

        /// <summary>
        /// Adjusts a DateTime to the beginning a user's day
        /// </summary>
        /// <returns>The adjusted time</returns>
        /// <param name="t">the time to adjust</param>
        static DateTime AdjustToDawn (DateTime t)
        {
            t = t.AddHours (t.Hour);
            t = t.AddMinutes (t.Minute);
            t = t.AddSeconds (t.Second);
            t = t.AddSeconds (DawnOffset);
            return t;
        }
    }
}

