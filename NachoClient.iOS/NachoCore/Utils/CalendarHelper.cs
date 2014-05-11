//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using DDay.iCal;

namespace NachoCore.Utils
{
    public class CalendarHelper
    {
        public CalendarHelper ()
        {
        }

        public static void ExtrapolateTimes (ref DDay.iCal.Event evt)
        {
//            if (evt.End == null && evt.Start != null && evt.Duration != default(TimeSpan)) {
//                evt.End = evt.Start.Add (evt.Duration);
//            } else if (evt.Duration == default(TimeSpan) && evt.Start != null && evt.End != null) {
//                evt.Duration = evt.DTEnd.Subtract (evt.Start);
//            } else if (evt.Start == null && evt.Duration != default(TimeSpan) && evt.End != null) {
//                evt.Start = evt.End.Subtract (evt.Duration);
//            }
        }
    }
}

