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
using Android.Support.V7.App;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;
using NachoCore.ActiveSync;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "EventEditActivity")]            
    public class EventEditActivity : AppCompatActivity
    {
        private const string EXTRA_EVENT_TO_EDIT = "com.nachocove.nachomail.EXTRA_EVENT_TO_EDIT";
        private const string EXTRA_MESSAGE_FOR_MEETING = "com.nachocove.nachomail.EXTRA_MESSAGE_FOR_MEETING";
        private const string EXTRA_START_DATE = "com.nachocove.nachomail.EXTRA_START_DATE";

        private McAccount account;
        private McEvent ev;
        private McCalendar cal;
        private McFolder calendarFolder;

        private EditText titleField;
        private EditText descriptionField;
        private Switch allDayField;
        private TextView startDateField;
        private TextView startTimeField;
        private TextView endDateField;
        private TextView endTimeField;
        private EditText locationField;

        private DateTime startTime;
        private DateTime endTime;
        private bool endTimeChanged = false;
        private bool descriptionChanged = false;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.EventEditActivity);

            var saveButton = FindViewById<ImageView> (Resource.Id.right_button1);
            saveButton.Visibility = ViewStates.Visible;
            saveButton.SetImageResource (Resource.Drawable.icn_send);
            saveButton.Click += SaveButton_Click;

            titleField = FindViewById<EditText> (Resource.Id.event_edit_title);
            descriptionField = FindViewById<EditText> (Resource.Id.event_edit_description);
            allDayField = FindViewById<Switch> (Resource.Id.event_edit_allday_toggle);
            startDateField = FindViewById<TextView> (Resource.Id.event_edit_start_date);
            startTimeField = FindViewById<TextView> (Resource.Id.event_edit_start_time);
            endDateField = FindViewById<TextView> (Resource.Id.event_edit_end_date);
            endTimeField = FindViewById<TextView> (Resource.Id.event_edit_end_time);
            locationField = FindViewById<EditText> (Resource.Id.event_edit_location);

            descriptionField.TextChanged += DescriptionField_TextChanged;

            var navBarTitle = FindViewById<TextView> (Resource.Id.title);
            navBarTitle.Visibility = ViewStates.Visible;

            if (Intent.ActionCreateDocument == Intent.Action) {
                navBarTitle.Text = "New Event";

                DateTime startDate = DateTime.MinValue;
                if (Intent.HasExtra(EXTRA_START_DATE)) {
                    startDate = DateTime.Parse (Intent.GetStringExtra (EXTRA_START_DATE));
                }

                ev = null;
                cal = CalendarHelper.DefaultMeeting (startDate);
                startTime = cal.StartTime.ToLocalTime ();
                endTime = cal.EndTime.ToLocalTime ();

                if (Intent.HasExtra (EXTRA_MESSAGE_FOR_MEETING)) {
                    // Create a meeting based on the recipients and the body of an email message.
                    // TODO Not yet implemented
                    IntentHelper.RetreiveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE_FOR_MEETING));
                }

                // Figure out the correct account for the new event.
                account = NcApplication.Instance.Account;
                if (!account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) || 0 == new NachoFolders (account.Id, NachoFolders.FilterForCalendars).Count ()) {
                    bool foundAccount = false;
                    foreach (var candidateAccountId in McAccount.GetAllConfiguredNonDeviceAccountIds ()) {
                        if (candidateAccountId != account.Id) {
                            var candidateAccount = McAccount.QueryById<McAccount> (candidateAccountId);
                            if (candidateAccount.HasCapability(McAccount.AccountCapabilityEnum.CalWriter) && 0 < new NachoFolders(candidateAccountId, NachoFolders.FilterForCalendars).Count()) {
                                foundAccount = true;
                                account = candidateAccount;
                                break;
                            }
                        }
                    }
                    if (!foundAccount) {
                        NcAlertView.Show (this, "No Calendar Support", "None of the accounts support creating or editing calendar events.", () => {
                            SetResult (Result.Canceled);
                            Finish ();
                        });
                        return;
                    }
                }

                // Figure out the correct calendar within that account for the new event.
                var accountCalendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                calendarFolder = accountCalendars.GetFolder (0);
                for (int f = 0; f < accountCalendars.Count(); ++f) {
                    var calendar = accountCalendars.GetFolder (f);
                    if (Xml.FolderHierarchy.TypeCode.DefaultCal_8 == calendar.Type) {
                        calendarFolder = calendar;
                        break;
                    }
                }
            } else {
                NcAssert.True (Intent.ActionEdit == Intent.Action, "The intent for EventEditActivity must have an action of Edit or CreateDocument.");
                NcAssert.True (Intent.HasExtra (EXTRA_EVENT_TO_EDIT), "When EventEditActivity is called with an Edit action, the event to edit must be specified.");

                navBarTitle.Text = "Edit Event";

                ev = IntentHelper.RetreiveValue<McEvent> (Intent.GetStringExtra (EXTRA_EVENT_TO_EDIT));
                cal = McCalendar.QueryById<McCalendar> (ev.CalendarId);

                if (cal.AllDayEvent) {
                    // Calculate start and end times that will be used if the user changes the event to not be an all-day event.
                    // The times will be hidden from the user until the all-day switch is changed.
                    var tempCal = CalendarHelper.DefaultMeeting ();
                    TimeSpan startTimeOfDay = tempCal.StartTime.ToLocalTime ().TimeOfDay;
                    TimeSpan endTimeOfDay = tempCal.EndTime.ToLocalTime ().TimeOfDay;
                    if (startTimeOfDay > endTimeOfDay) {
                        // This can happen in the current time is between 22:30 and 23:30.
                        endTimeOfDay = startTimeOfDay;
                    }
                    var timeZone = new AsTimeZone (cal.TimeZone).ConvertToSystemTimeZone ();
                    startTime = DateTime.SpecifyKind (CalendarHelper.ConvertTimeFromUtc (cal.StartTime, timeZone), DateTimeKind.Local).Date + startTimeOfDay;
                    // The end time for the event is midnight at the end of the last day.  Subtract a day
                    // so that endTime falls somewhere within the last day of the event.
                    endTime = DateTime.SpecifyKind (CalendarHelper.ConvertTimeFromUtc (cal.EndTime, timeZone), DateTimeKind.Local).AddDays (-1).Date + endTimeOfDay;
                } else {
                    startTime = cal.StartTime.ToLocalTime ();
                    endTime = cal.EndTime.ToLocalTime ();
                }

                if (null == ev || null == cal) {
                    NcAlertView.Show (this, "Event Deleted", "The event can't be edited because it has been deleted.", () => {
                        SetResult (Result.Canceled);
                        Finish ();
                    });
                    return;
                }

                account = McAccount.QueryById<McAccount> (cal.AccountId);

                titleField.Text = cal.GetSubject ();
                locationField.Text = cal.GetLocation ();
                if (McBody.BodyTypeEnum.PlainText_1 == cal.DescriptionType) {
                    descriptionField.Text = cal.Description;
                }
                calendarFolder = McFolder.QueryByFolderEntryId<McCalendar> (cal.AccountId, cal.Id).FirstOrDefault ();
            }

            allDayField.CheckedChange += AllDayField_CheckedChange;
            startDateField.Click += StartDateField_Click;
            startTimeField.Click += StartTimeField_Click;
            endDateField.Click += EndDateField_Click;
            endTimeField.Click += EndTimeField_Click;

            // The text in the date/time fields should look like the default text
            // for an EditText field, not a TextView field.  Copy the necessary
            // information from one of the EditText fields to make that happen.
            startDateField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            startTimeField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            endDateField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            endTimeField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            startDateField.SetTextColor (titleField.TextColors);
            startTimeField.SetTextColor (titleField.TextColors);
            endDateField.SetTextColor (titleField.TextColors);
            endTimeField.SetTextColor (titleField.TextColors);

            allDayField.Checked = cal.AllDayEvent;
            ConfigureStartEndFields ();
        }

        public override void OnBackPressed ()
        {
            new Android.Support.V7.App.AlertDialog.Builder (this)
                .SetTitle ("Are You Sure?")
                .SetMessage ("The event will not be saved.")
                .SetPositiveButton ("Yes", (object sender, DialogClickEventArgs e) => { base.OnBackPressed (); })
                .SetNegativeButton ("Cancel", (EventHandler<DialogClickEventArgs>)null)
                .Create ()
                .Show ();
        }

        public static Intent NewEventIntent (Context context)
        {
            var intent = new Intent (context, typeof(EventEditActivity));
            intent.SetAction (Intent.ActionCreateDocument);
            return intent;
        }

        public static Intent NewEventOnDayIntent (Context context, DateTime day)
        {
            var intent = new Intent (context, typeof(EventEditActivity));
            intent.SetAction (Intent.ActionCreateDocument);
            intent.PutExtra (EXTRA_START_DATE, day.ToString ("O"));
            return intent;
        }

        public static Intent EditEventIntent (Context context, McEvent ev)
        {
            var intent = new Intent (context, typeof(EventEditActivity));
            intent.SetAction (Intent.ActionEdit);
            intent.PutExtra (EXTRA_EVENT_TO_EDIT, IntentHelper.StoreValue (ev));
            return intent;
        }

        public static Intent MeetingFromMessageIntent (Context context, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(EventEditActivity));
            intent.SetAction (Intent.ActionCreateDocument);
            intent.PutExtra (EXTRA_MESSAGE_FOR_MEETING, IntentHelper.StoreValue (message));
            return intent;
        }

        private void StartTimeMaybeChanged (DateTime newStartTime)
        {
            if (startTime == newStartTime) {
                return;
            }
            if (!endTimeChanged) {
                endTime = newStartTime + (endTime - startTime);
            }
            startTime = newStartTime;
            ConfigureStartEndFields ();
        }

        private void EndTimeMaybeChanged (DateTime newEndTime)
        {
            if (endTime == newEndTime) {
                return;
            }
            endTimeChanged = true;
            endTime = newEndTime;
            ConfigureStartEndFields ();
        }

        private void ConfigureStartEndFields ()
        {
            if (allDayField.Checked) {
                startTimeField.Visibility = ViewStates.Gone;
                endTimeField.Visibility = ViewStates.Gone;
            } else {
                startTimeField.Visibility = ViewStates.Visible;
                endTimeField.Visibility = ViewStates.Visible;
            }
            DateTime now = DateTime.Now;
            startDateField.Text = startTime.ToString (now.Year == startTime.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy");
            startTimeField.Text = startTime.ToString ("t");
            endDateField.Text = endTime.ToString (now.Year == endTime.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy");
            endTimeField.Text = endTime.ToString ("t");

            if (ValidStartEndTimes ()) {
                endDateField.SetTextColor (titleField.TextColors);
                endTimeField.SetTextColor (titleField.TextColors);
            } else {
                endDateField.SetTextColor (Android.Graphics.Color.Red);
                endTimeField.SetTextColor (Android.Graphics.Color.Red);
            }
        }

        private bool ValidStartEndTimes ()
        {
            if (allDayField.Checked) {
                return startTime.Date <= endTime.Date;
            }
            return startTime <= endTime;
        }

        private void SaveButton_Click (object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty (FindViewById<EditText> (Resource.Id.event_edit_title).Text)) {

                NcAlertView.ShowMessage (this, "Cannot Save Event", "The title of the event must not be empty.");

            } else if (!ValidStartEndTimes ()) {

                NcAlertView.ShowMessage (this, "Cannot Save Event", "The starting time must be no later than the ending time.");

            } else {

                cal.AccountId = account.Id;
                cal.Subject = titleField.Text;
                cal.AllDayEvent = allDayField.Checked;
                if (cal.AllDayEvent) {
                    // An all-day event is supposed to run midnight to midnight in the local time zone.
                    cal.StartTime = startTime.Date.ToUniversalTime ();
                    cal.EndTime = endTime.AddDays (1.0).Date.ToUniversalTime ();
                } else {
                    cal.StartTime = startTime.ToUniversalTime ();
                    cal.EndTime = endTime.ToUniversalTime ();
                }
                // The event always uses the local time zone.
                cal.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedLocalTimeZone (), cal.StartTime).toEncodedTimeZone ();
                cal.Location = locationField.Text;
                if (descriptionChanged) {
                    cal.SetDescription (descriptionField.Text, McBody.BodyTypeEnum.PlainText_1);
                }

                if (Intent.ActionCreateDocument == Intent.Action) {
                    cal.Insert ();
                    calendarFolder.Link (cal);
                    BackEnd.Instance.CreateCalCmd (account.Id, cal.Id, calendarFolder.Id);
                } else {
                    cal.RecurrencesGeneratedUntil = DateTime.MinValue;
                    cal.Update ();
                    BackEnd.Instance.UpdateCalCmd (cal.AccountId, cal.Id, descriptionChanged);
                }

                SetResult (Result.Ok);
                Finish ();
            }
        }

        private void DescriptionField_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            descriptionChanged = true;
        }

        private void EndTimeField_Click (object sender, EventArgs e)
        {
            new TimePickerFragment (this, endTime, (DateTime time) => {
                EndTimeMaybeChanged (endTime.Date + time.TimeOfDay);
            }).Show (FragmentManager, "endTimePicker");
        }

        private void EndDateField_Click (object sender, EventArgs e)
        {
            new DatePickerFragment (this, endTime, (DateTime date) => {
                EndTimeMaybeChanged (date.Date + endTime.TimeOfDay);
            }).Show (FragmentManager, "startDatePicker");
        }

        private void StartTimeField_Click (object sender, EventArgs e)
        {
            new TimePickerFragment (this, startTime, (DateTime time) => {
                StartTimeMaybeChanged (startTime.Date + time.TimeOfDay);
            }).Show (FragmentManager, "startTimePicker");
        }

        private void StartDateField_Click (object sender, EventArgs e)
        {
            new DatePickerFragment (this, startTime, (DateTime date) => {
                StartTimeMaybeChanged (date.Date + startTime.TimeOfDay);
            }).Show (FragmentManager, "endDatePicker");
        }

        private void AllDayField_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            ConfigureStartEndFields ();
        }

        private delegate void SetDateOrTimeCallback (DateTime dateTime);

        private class DatePickerFragment : DialogFragment
        {
            private Context context;
            private DateTime date;
            private SetDateOrTimeCallback callback;

            public DatePickerFragment (Context context, DateTime date, SetDateOrTimeCallback callback)
            {
                this.context = context;
                this.date = date;
                this.callback = callback;
            }

            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                // DatePicker expects the month to be zero-based (Jan=0, Dec=11).
                // But DateTime uses one-based months (Jan=1, Dec=12).
                // Hence the adjustment to the month value.
                var result = new DatePickerDialog (context, (object sender, DatePickerDialog.DateSetEventArgs e) => {
                    callback (e.Date);
                }, date.Year, date.Month - 1, date.Day);
                Util.ConstrainDatePicker (result.DatePicker, date.ToUniversalTime ());
                return result;
            }
        }

        private class TimePickerFragment : DialogFragment
        {
            private Context context;
            private DateTime time;
            private SetDateOrTimeCallback callback;

            public TimePickerFragment (Context context, DateTime time, SetDateOrTimeCallback callback)
            {
                this.context = context;
                this.time = time;
                this.callback = callback;
            }

            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new TimePickerDialog (context, (object sender, TimePickerDialog.TimeSetEventArgs e) => {
                    callback (new DateTime (time.Year, time.Month, time.Day, e.HourOfDay, e.Minute, 0, DateTimeKind.Local));
                }, time.Hour, time.Minute, string.IsNullOrEmpty (System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator));
            }
        }
    }
}

