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


        public NcMessageDeferral ()
        {
        }

        static public NcResult DeferThread (McEmailMessageThread thread, MessageDeferralType deferralType, DateTime deferUntil)
        {
            if (MessageDeferralType.None == deferralType) {
                UndeferThread (thread);
                return NcResult.OK ();
            }

            if (MessageDeferralType.Custom != deferralType) {
                NcResult r = ComputeDeferral (DateTime.UtcNow, deferralType, deferUntil);
                if (r.isError ()) {
                    return r;
                }
                deferUntil = r.GetValue<DateTime> ();
            }
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
        public static NcResult ComputeDeferral (DateTime from, MessageDeferralType deferralType, DateTime customDate)
        {
            NcAssert.True (DateTimeKind.Utc == from.Kind);
            NcAssert.True ((MessageDeferralType.Custom != deferralType) || (DateTimeKind.Utc == customDate.Kind));

            switch (deferralType) {
            case MessageDeferralType.None:
                from = DateTime.MinValue;
                break;
            case MessageDeferralType.OneHour:
                from = AdjustToHour (from, from.AddMinutes (90).Hour);
                break;
            case MessageDeferralType.TwoHours:
                from = AdjustToHour (from, from.AddMinutes (150).Hour);
                break;
            case MessageDeferralType.Later:
                // TODO: Probaly want to choose next free hour
                from = AdjustToHour (from, from.AddMinutes (270).Hour);
                break;
            case MessageDeferralType.EndOfDay:
                if (from.Hour >= 17) {
                    from = AdjustToHour (from, 23);
                } else {
                    from = AdjustToHour (from, 17);
                }
                break;
            case MessageDeferralType.Tonight:
                if (from.Hour > 18) {
                    // Later this evening...
                    from = from.AddHours (2);
                } else {
                    from = AdjustToHour (from, 19);
                }
                break;
            case MessageDeferralType.Tomorrow:
                from = from.AddDays (1d);
                from = AdjustToHour (from, 8);
                break;
            case MessageDeferralType.NextWeek:
                do {
                    from = from.AddDays (1.0d);
                } while(from.DayOfWeek != FirstDayOfWork);
                from = AdjustToHour (from, 8);
                break;
            case MessageDeferralType.MonthEnd:
                // Last day
                from = from.AddMonths (1);
                from = from.AddDays (-from.Day); // Day is 1..31
                from = AdjustToHour (from, 8);
                break;
            case MessageDeferralType.NextMonth:
                // First day
                from = from.AddMonths (1);
                from = from.AddDays (1.0 - from.Day); // Day is 1..32
                from = AdjustToHour (from, 8);
                break;
            case MessageDeferralType.Forever:
                from = DateTime.MaxValue;
                break;
            case MessageDeferralType.Custom:
                from = customDate;
                break;
            default:
                NcAssert.CaseError (String.Format ("ComputeDeferral; {0} was unexpected", deferralType));
                return NcResult.Error ("");
            }
            Console.WriteLine ("Defer until raw={0} utc={1} local={2}", from, from.ToUniversalTime(), from.ToLocalTime());
            return NcResult.OK (from.ToUniversalTime ());
        }

        static DateTime AdjustToHour (DateTime t, int hour)
        {
            return new DateTime (t.Year, t.Month, t.Day, hour, 0, 0);
        }

    }
}

