//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Text;
using DDay.iCal;
using DDay.iCal.Serialization.iCalendar;
using MimeKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;

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

