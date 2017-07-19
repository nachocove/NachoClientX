//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoClient.AndroidClient;
using Android.Content;

namespace NachoPlatform
{

    public class Strings : IStrings
    {
        public static Strings Instance { get; private set; }

        public static void Init (Context context)
        {
            Instance = new Strings (context);
        }

        Context Context;

        private Strings (Context context)
        {
            Context = context;
        }

        public string CompactMinutesFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_minutes_format);
            }
        }

        public string CompactHoursFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_hours_format);
            }
        }

        public string CompactHourMinutesFormat {
            get {
                return Context.GetString (Resource.String.pretty_compact_hours_minutes_format);
            }
        }

        public string CompactDayPlus {
            get {
                return Context.GetString (Resource.String.pretty_compact_day_plus);
            }
        }
    }
}
