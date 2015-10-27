//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class AttendeeBaseActivity : NcActivity
    {
        private const string EXTRA_ATTENDEES = "com.nachocove.nachomail.EXTRA_ATTENDEES";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
        }

        protected static Intent AttendeesIntent (Context context, Type activityType, string action, IList<McAttendee> attendees)
        {
            var intent = new Intent (context, activityType);
            intent.SetAction (action);
            intent.PutExtra (EXTRA_ATTENDEES, IntentHelper.StoreValue (attendees));
            return intent;
        }

        protected static Intent ResultIntent (IList<McAttendee> attendees)
        {
            var intent = new Intent ();
            intent.PutExtra (EXTRA_ATTENDEES, IntentHelper.StoreValue (attendees));
            return intent;
        }

        public IList<McAttendee> AttendeesFromIntent (Intent intent)
        {
            return IntentHelper.RetreiveValue<IList<McAttendee>> (intent.GetStringExtra (EXTRA_ATTENDEES));
        }
    }
}

