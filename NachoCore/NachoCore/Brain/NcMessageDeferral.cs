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
    public static class NcMessageDeferral
    {
        /// User's first day of week
        /// TODO: Must be configurable
        const DayOfWeek FirstDayOfWork = DayOfWeek.Monday;
        const DayOfWeek LastDayOfWork = DayOfWeek.Friday;
        const DayOfWeek FirstDayOfWeekend = DayOfWeek.Saturday;

        public enum MessageDateType
        {
            None,
            Defer,
            Intent,
            Deadline,
        }

        static public NcResult DeferMessage (McEmailMessage message, MessageDeferralType deferralType, DateTime deferUntil)
        {
            message.UpdateWithOCApply<McEmailMessage> ((item) => {
                var em = (McEmailMessage)item;
                em.DeferralType = deferralType;
                return true;
            });
            var utc = deferUntil;
            var local = deferUntil.LocalT ();
            BackEnd.Instance.SetEmailFlagCmd (message.AccountId, message.Id, "Defer until", local, utc, local, utc);
            //NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            return NcResult.OK ();
        }

        static public NcResult DeferThread (McEmailMessageThread thread, MessageDeferralType deferralType, DateTime deferUntil)
        {
            foreach (var message in thread) {
                if (null != message) {
                    DeferMessage (message, deferralType, deferUntil);
                }
            }
            return NcResult.OK ();
        }

        // TODO: Just clear start time, not all of the flags.
        static private NcResult ClearMessageFlags (McEmailMessage message)
        {
            BackEnd.Instance.ClearEmailFlagCmd (message.AccountId, message.Id);
            //NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            return NcResult.OK ();
        }

        static private NcResult ClearMessageThreadFlags (McEmailMessageThread thread)
        {
            foreach (var message in thread) {
                if (null != message) {
                    ClearMessageFlags (message);
                }
            }
            return NcResult.OK ();
        }

        static public NcResult UndeferMessage (McEmailMessage message)
        {
            return ClearMessageFlags (message);
        }

        static public NcResult UndeferThread (McEmailMessageThread thread)
        {
            return ClearMessageThreadFlags (thread);
        }

        static public NcResult RemoveDueDate (McEmailMessage message)
        {
            return ClearMessageFlags (message);
        }

        static public NcResult RemoveDueDate (McEmailMessageThread thread)
        {
            return ClearMessageThreadFlags (thread);
        }

        static public NcResult SetDueDate (McEmailMessage message, DateTime dueOn)
        {
            var start = DateTime.UtcNow;
            BackEnd.Instance.SetEmailFlagCmd (message.AccountId, message.Id, "For follow up by", start.LocalT (), start, dueOn.LocalT (), dueOn);
            //NcBrain.SharedInstance.Enqueue (new NcBrainMessageFlagEvent (message.AccountId, message.Id));
            return NcResult.OK ();
        }

        static public NcResult SetDueDate (McEmailMessageThread thread, DateTime dueOn)
        {
            foreach (var message in thread) {
                if (null != message) {
                    SetDueDate (message, dueOn);
                }
            }
            return NcResult.OK ();
        }

        /// <summary>
        /// Computes the time to defer.
        /// </summary>
        /// <returns>The UTC time when the message is again visible.</returns>
        /// <param name="from">Start time for the computation.</param>
        /// <param name="deferralType">Deferral type.</param>
        public static NcResult ComputeDeferral (DateTime from, MessageDeferralType deferralType, DateTime customDate)
        {
            NcAssert.True (DateTimeKind.Utc == from.Kind);
            NcAssert.True ((MessageDeferralType.Custom != deferralType && MessageDeferralType.DueDate != deferralType) || (DateTimeKind.Utc == customDate.Kind));

            switch (deferralType) {
            case MessageDeferralType.None:
                from = DateTime.MinValue;
                break;
            case MessageDeferralType.OneHour:
                from = TruncateToHour (from.AddMinutes (90));
                break;
            case MessageDeferralType.TwoHours:
                from = TruncateToHour (from.AddMinutes (150));
                break;
            case MessageDeferralType.Later:
                // TODO: Probaly want to choose next free hour (three hours now)
                from = TruncateToHour (from.AddMinutes (210));
                break;
            case MessageDeferralType.EndOfDay:
                if (from.ToLocalTime ().Hour >= 17) {
                    from = AdjustToLocalHour (from, 23);
                } else {
                    from = AdjustToLocalHour (from, 17);
                }
                break;
            case MessageDeferralType.Tonight:
                if (from.ToLocalTime ().Hour > 18) {
                    // Later this evening...
                    from = AdjustToLocalHour (from, 21);
                } else {
                    from = AdjustToLocalHour (from, 19);
                }
                break;
            case MessageDeferralType.Tomorrow:
                from = from.AddDays (1d);
                from = AdjustToLocalHour (from, 8);
                break;
            case MessageDeferralType.ThisWeek:
                // Friday 5pm
                while (from.ToLocalTime ().DayOfWeek != LastDayOfWork) {
                    from = from.AddDays (1);
                }
                from = AdjustToLocalHour (from, 17);
                break;
            case MessageDeferralType.Weekend:
                // Satuday 8am
                do {
                    from = from.AddDays (1);
                } while (from.ToLocalTime ().DayOfWeek != FirstDayOfWeekend);
                from = AdjustToLocalHour (from, 8);
                break;
            case MessageDeferralType.NextWeek:
                do {
                    from = from.AddDays (1.0d);
                } while (from.ToLocalTime ().DayOfWeek != FirstDayOfWork);
                from = AdjustToLocalHour (from, 8);
                break;
            case MessageDeferralType.MonthEnd:
                // Last day
                from = from.ToLocalTime ();
                from = from.AddDays (1.0 - from.Day); // Day is 1..31
                from = from.AddMonths (1);
                from = from.AddDays (-1);
                from = AdjustToLocalHour (from, 8);
                break;
            case MessageDeferralType.NextMonth:
                // First day
                from = from.ToLocalTime ();
                from = from.AddDays (1.0 - from.Day); // Day is 1..32
                from = from.AddMonths (1);
                from = AdjustToLocalHour (from, 8);
                break;
            case MessageDeferralType.Forever:
                from = DateTime.MaxValue.ToLocalTime ().ToUniversalTime ();
                break;
            case MessageDeferralType.DueDate:
            case MessageDeferralType.Custom:
                from = customDate;
                break;
            default:
                NcAssert.CaseError (String.Format ("ComputeDeferral; {0} was unexpected", deferralType));
                return NcResult.Error ("");
            }
            Log.Info (Log.LOG_BRAIN, "Defer until raw={0} utc={1} local={2}", from, from.ToUniversalTime (), from.ToLocalTime ());
            return NcResult.OK (from.ToUniversalTime ());
        }

        static DateTime AdjustToLocalHour (DateTime t, int hour)
        {
            var l = t.ToLocalTime ();
            var n = new DateTime (l.Year, l.Month, l.Day, hour, 0, 0, DateTimeKind.Local);
            return n.ToUniversalTime ();
        }

        static DateTime TruncateToHour (DateTime t)
        {
            NcAssert.True (DateTimeKind.Utc == t.Kind);
            var n = new DateTime (t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc);
            return n;
        }
    }
}

