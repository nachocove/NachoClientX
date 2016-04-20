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
    [Activity (Label = "AttendeeViewActivity")]
    public class AttendeeViewActivity : AttendeeBaseActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.AttendeeViewActivity);

            var fragment = FragmentManager.FindFragmentById<AttendeeListViewFragment> (Resource.Id.attendee_view_fragment);
            fragment.AccountId = AccountIdFromIntent (Intent);
            var attendees = RetainedData;
            if (null == attendees) {
                attendees = AttendeesFromIntent (Intent);
                RetainedData = attendees;
            }
            fragment.Attendees = attendees;
        }

        public static Intent AttendeeViewIntent (Context context, int accountId, IList<McAttendee> attendees)
        {
            return AttendeesIntent (context, typeof(AttendeeViewActivity), Intent.ActionView, accountId, attendees);
        }
    }
}

