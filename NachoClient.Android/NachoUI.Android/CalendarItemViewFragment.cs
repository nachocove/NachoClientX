using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;

namespace NachoClient.AndroidClient
{
    public class CalendarItemViewFragment : Android.Support.V4.App.Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.HasOptionsMenu = true;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.CalendarItemViewFragment, container, false);

            var calendarItemId = this.Arguments.GetInt ("calendarItemId", 0);

            var calendarItem = NcModel.Instance.Db.Get<McCalendar> (calendarItemId);

            var valueField = rootView.FindViewById<TextView> (Resource.Id.value);
            valueField.Text = calendarItem.Subject;

            return rootView;
        }
    }
}
