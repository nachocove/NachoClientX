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
    public class EventEditActivity : NcActivity
    {
        private const string EXTRA_EVENT_TO_EDIT = "com.nachocove.nachomail.EXTRA_EVENT_TO_EDIT";
        private const string EXTRA_MESSAGE_FOR_MEETING = "com.nachocove.nachomail.EXTRA_MESSAGE_FOR_MEETING";
        private const string EXTRA_START_DATE = "com.nachocove.nachomail.EXTRA_START_DATE";

        private const int ATTENDEE_ACTIVITY_REQUEST = 1;

        private McAccount account;
        private McEvent ev;
        private McCalendar cal;
        private McFolder calendarFolder;

        private ButtonBar buttonBar;
        private EditText titleField;
        private EditText descriptionField;
        private Switch allDayField;
        private TextView startField;
        private TextView endField;
        private EditText locationField;
        private TextView attendeeCountField;
        private TextView reminderField;
        private TextView calendarField;

        private DateTime startTime;
        private DateTime endTime;
        private bool endTimeChanged = false;
        private bool descriptionChanged = false;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.EventEditActivity);

            buttonBar = new ButtonBar (FindViewById<View> (Resource.Id.button_bar));

            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            titleField = FindViewById<EditText> (Resource.Id.event_edit_title);
            descriptionField = FindViewById<EditText> (Resource.Id.event_edit_description);
            allDayField = FindViewById<Switch> (Resource.Id.event_edit_allday_toggle);
            startField = FindViewById<TextView> (Resource.Id.event_edit_start);
            endField = FindViewById<TextView> (Resource.Id.event_edit_end);
            locationField = FindViewById<EditText> (Resource.Id.event_edit_location);
            attendeeCountField = FindViewById<TextView> (Resource.Id.event_edit_attendee_count);
            reminderField = FindViewById<TextView> (Resource.Id.event_edit_reminder);
            calendarField = FindViewById<TextView> (Resource.Id.event_edit_calendar);

            descriptionField.TextChanged += DescriptionField_TextChanged;

            if (Intent.ActionCreateDocument == Intent.Action) {
                buttonBar.SetTitle ("New Event");

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
                    IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE_FOR_MEETING));
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

                buttonBar.SetTitle ("Edit Event");

                ev = IntentHelper.RetrieveValue<McEvent> (Intent.GetStringExtra (EXTRA_EVENT_TO_EDIT));
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
            startField.Click += StartField_Click;
            endField.Click += EndField_Click;
            var startFieldArrow = FindViewById<ImageView> (Resource.Id.event_edit_start_arrow);
            startFieldArrow.Click += StartField_Click;
            var endFieldArrow = FindViewById<ImageView> (Resource.Id.event_edit_end_arrow);
            endFieldArrow.Click += EndField_Click;

            // The text in the date/time fields, the reminder field, the attende count field,
            // and the calendar field should look like the default text for an EditText field,
            // not a TextView field.  Copy the necessary information from one of the EditText
            // fields to make that happen.
            startField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            endField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            attendeeCountField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            reminderField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            calendarField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            startField.SetTextColor (titleField.TextColors);
            endField.SetTextColor (titleField.TextColors);
            attendeeCountField.SetTextColor (titleField.TextColors);
            reminderField.SetTextColor (titleField.TextColors);
            calendarField.SetTextColor (titleField.TextColors);

            allDayField.Checked = cal.AllDayEvent;
            ConfigureStartEndFields ();

            attendeeCountField.Text = string.Format ("( {0} )", cal.attendees.Count);
            attendeeCountField.Click += Attendee_Click;
            var attendeeArrow = FindViewById<ImageView> (Resource.Id.event_edit_attendee_arrow);
            attendeeArrow.Click += Attendee_Click;

            reminderField.Text = Pretty.ReminderString (cal.HasReminder (), cal.GetReminder ());
            reminderField.Click += Reminder_Click;
            var reminderArrow = FindViewById<ImageView> (Resource.Id.event_edit_reminder_arrow);
            reminderArrow.Click += Reminder_Click;

            calendarField.Text = calendarFolder.DisplayName;
            calendarField.Click += Calendar_Click;
            var calendarArrow = FindViewById<ImageView> (Resource.Id.event_edit_calendar_arrow);
            calendarArrow.Click += Calendar_Click;
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            if (ATTENDEE_ACTIVITY_REQUEST == requestCode && Result.Ok == resultCode && null != data) {
                cal.attendees = AttendeeEditActivity.AttendeesFromIntent (data);
                attendeeCountField.Text = string.Format ("( {0} )", cal.attendees.Count);
            }
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
                startField.Text = Pretty.MediumFullDate (startTime);
                endField.Text = Pretty.MediumFullDate (endTime);
            } else {
                startField.Text = Pretty.MediumFullDateTime (startTime);
                endField.Text = Pretty.MediumFullDateTime (endTime);
            }

            if (ValidStartEndTimes ()) {
                endField.SetTextColor (titleField.TextColors);
            } else {
                endField.SetTextColor (Android.Graphics.Color.Red);
            }
        }

        private bool ValidStartEndTimes ()
        {
            if (allDayField.Checked) {
                return startTime.Date <= endTime.Date;
            }
            return startTime <= endTime;
        }

        private void DatePickerRangeForEvent (DateTime startTime, DateTime endTime, out DateTime minDate, out DateTime maxDate)
        {
            minDate = DateTime.Now.AddYears (-5);
            if (minDate > startTime.AddYears (-1)) {
                minDate = startTime.AddYears (-1);
            }
            maxDate = DateTime.Now.AddYears (50);
            if (maxDate < endTime.AddYears (1)) {
                maxDate = endTime.AddYears (1);
            }
        }

        private List<Tuple<McAccount, NachoFolders>> GetChoosableCalendars ()
        {
            var result = new List<Tuple<McAccount, NachoFolders>> ();
            IEnumerable<McAccount> candidateAccounts;
            if (Intent.ActionCreateDocument == Intent.Action && 0 == cal.attachments.Count) {
                candidateAccounts = McAccount.GetAllAccounts ();
            } else {
                candidateAccounts = new McAccount[] { account };
            }
            foreach (var account in candidateAccounts) {
                if (account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                    var calendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                    if (0 < calendars.Count ()) {
                        result.Add (new Tuple<McAccount, NachoFolders> (account, calendars));
                    }
                }
            }
            if (0 == result.Count) {
                Log.Error (Log.LOG_CALENDAR, "Couldn't find any calendars for the event editor's calendar chooser.");
                result.Add (new Tuple<McAccount, NachoFolders> (account, new NachoFolders (calendarFolder)));
            }
            return result;
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

        private void AllDayField_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            ConfigureStartEndFields ();
        }

        private void StartField_Click (object sender, EventArgs e)
        {
            DateTime minDate, maxDate;
            DatePickerRangeForEvent (startTime, endTime, out minDate, out maxDate);
            DateTimePicker.Show (this, startTime, !allDayField.Checked, minDate, maxDate, null,
                (DateTime newDateTime) => {
                    StartTimeMaybeChanged (newDateTime.ToLocalTime ());
                });
        }

        private void EndField_Click (object sender, EventArgs e)
        {
            DateTime minDate, maxDate;
            DatePickerRangeForEvent (startTime, endTime, out minDate, out maxDate);
            DateTimePicker.Show (this, endTime, !allDayField.Checked, minDate, maxDate, null,
                (DateTime newDateTime) => {
                    EndTimeMaybeChanged (newDateTime.ToLocalTime ());
                });
        }

        private void Reminder_Click (object sender, EventArgs e)
        {
            ReminderChooser.Show (this, cal.HasReminder (), (int)cal.GetReminder (), (bool hasReminder, int reminder) => {
                cal.ReminderIsSet = hasReminder;
                if (hasReminder) {
                    cal.Reminder = (uint)reminder;
                }
                reminderField.Text = Pretty.ReminderString (hasReminder, (uint)reminder);
            });
        }

        private void Attendee_Click (object sender, EventArgs e)
        {
            StartActivityForResult (AttendeeEditActivity.AttendeeEditIntent (this, account.Id, cal.attendees), ATTENDEE_ACTIVITY_REQUEST);
        }

        private void Calendar_Click (object sender, EventArgs e)
        {
            CalendarChooserDialog.Show (this, GetChoosableCalendars (), calendarFolder, (McFolder chosenFolder) => {
                if (account.Id != chosenFolder.AccountId) {
                    account = McAccount.QueryById<McAccount> (chosenFolder.AccountId);
                }
                calendarFolder = chosenFolder;
                calendarField.Text = chosenFolder.DisplayName;
            });
        }
    }
}

