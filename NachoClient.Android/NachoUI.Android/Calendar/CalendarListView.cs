﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public class CalendarListView : View
    {
        public CalendarListView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public CalendarListView (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public CalendarListView (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
        }
    }
}
