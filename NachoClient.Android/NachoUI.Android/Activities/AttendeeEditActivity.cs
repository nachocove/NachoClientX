﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
    [Activity (Label = "AttendeeEditActivity")]
    public class AttendeeEditActivity : AttendeeBaseActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.AttendeeEditActivity);

            var fragment = FragmentManager.FindFragmentById<AttendeeListEditFragment> (Resource.Id.attendee_edit_fragment);
            fragment.Attendees = base.AttendeesFromIntent (Intent);
        }

        public static Intent AttendeeEditIntent (Context context, IList<McAttendee> attendees)
        {
            return AttendeesIntent (context, typeof(AttendeeEditActivity), Intent.ActionEdit, attendees);
        }
    }
}
