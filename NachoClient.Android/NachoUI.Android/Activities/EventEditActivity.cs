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
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class EventEditActivityData
    {
        public McEvent Event;
        public McEmailMessage EmailMessage;

        public McCalendar CalendarItem;
        public bool IsAppEvent;
        public McAccount Account;
        public McFolder CalendarFolder;
        public DateTime StartTime;
        public DateTime EndTime;
        public bool EndTimeChanged;
        public bool DescriptionChanged;
    }

    [Activity (Label = "EventEditActivity")]
    public class EventEditActivity : NcActivityWithData<EventEditActivityData>
    {
        public const Result DELETE_EVENT_RESULT_CODE = Result.FirstUser;

        private const string EXTRA_EVENT_TO_EDIT = "com.nachocove.nachomail.EXTRA_EVENT_TO_EDIT";
        private const string EXTRA_MESSAGE_FOR_MEETING = "com.nachocove.nachomail.EXTRA_MESSAGE_FOR_MEETING";
        private const string EXTRA_START_DATE = "com.nachocove.nachomail.EXTRA_START_DATE";

        private const int ATTENDEE_ACTIVITY_REQUEST = 1;

        private const string REMINDER_CHOOSER_TAG = "ReminderChooser";
        private const string CALENDAR_CHOOSER_TAG = "CalendarChooser";

        private McAccount account;
        private McCalendar cal;
        private McFolder calendarFolder;

        private bool isAppEvent = true;

        private ButtonBar buttonBar;
        private EditText titleField;
        private EditText descriptionField;
        private Switch allDayField;
        private TextView startDateField;
        private TextView startTimeField;
        private View startDateArrow;
        private View startTimeArrow;
        private TextView endDateField;
        private TextView endTimeField;
        private View endDateArrow;
        private View endTimeArrow;
        private EditText locationField;
        private TextView attendeeCountField;
        private TextView reminderField;
        private TextView calendarField;
        private View deleteButton;

        private DateTime startTime;
        private DateTime endTime;
        private bool endTimeChanged = false;
        private bool descriptionChanged = false;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.EventEditActivity);

            // "dataFromIntent" is for the non-searializable objects that were passed in via the activity's intent.
            // Those objects may come from either the retained data or directly from the intent.  "dataFromIntent"
            // will never be null after this block of code.
            // "preConfigChange" is for the editor's in-progress state that was saved before Android changed the
            // configuration and restarted the activity.  It will be null if the activity is being launched for the
            // first time.
            var dataFromIntent = RetainedData;
            var preConfigChange = dataFromIntent;
            if (null == dataFromIntent) {
                dataFromIntent = new EventEditActivityData ();
                if (Intent.HasExtra(EXTRA_EVENT_TO_EDIT)) {
                    dataFromIntent.Event = IntentHelper.RetrieveValue<McEvent> (Intent.GetStringExtra (EXTRA_EVENT_TO_EDIT));
                }
                if (Intent.HasExtra(EXTRA_MESSAGE_FOR_MEETING)) {
                    dataFromIntent.EmailMessage = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE_FOR_MEETING));
                }
                RetainedData = dataFromIntent;
            }

            buttonBar = new ButtonBar (FindViewById<View> (Resource.Id.button_bar));

            buttonBar.SetTextButton (ButtonBar.Button.Right1, Resource.String.save, SaveButton_Click);

            titleField = FindViewById<EditText> (Resource.Id.event_edit_title);
            descriptionField = FindViewById<EditText> (Resource.Id.event_edit_description);
            allDayField = FindViewById<Switch> (Resource.Id.event_edit_allday_toggle);
            startDateField = FindViewById<TextView> (Resource.Id.event_edit_start_date);
            startDateArrow = FindViewById<View> (Resource.Id.event_edit_start_date_arrow);
            startTimeField = FindViewById<TextView> (Resource.Id.event_edit_start_time);
            startTimeArrow = FindViewById<View> (Resource.Id.event_edit_start_time_arrow);
            endDateField = FindViewById<TextView> (Resource.Id.event_edit_end_date);
            endDateArrow = FindViewById<View> (Resource.Id.event_edit_end_date_arrow);
            endTimeField = FindViewById<TextView> (Resource.Id.event_edit_end_time);
            endTimeArrow = FindViewById<View> (Resource.Id.event_edit_end_time_arrow);
            locationField = FindViewById<EditText> (Resource.Id.event_edit_location);
            attendeeCountField = FindViewById<TextView> (Resource.Id.event_edit_attendee_count);
            reminderField = FindViewById<TextView> (Resource.Id.event_edit_reminder);
            calendarField = FindViewById<TextView> (Resource.Id.event_edit_calendar);
            deleteButton = FindViewById<View> (Resource.Id.event_delete_button);

            descriptionField.TextChanged += DescriptionField_TextChanged;

            if (null != preConfigChange) {
                cal = preConfigChange.CalendarItem;
                isAppEvent = preConfigChange.IsAppEvent;
                account = preConfigChange.Account;
                calendarFolder = preConfigChange.CalendarFolder;
                startTime = preConfigChange.StartTime;
                endTime = preConfigChange.EndTime;
                endTimeChanged = preConfigChange.EndTimeChanged;
                descriptionChanged = preConfigChange.DescriptionChanged;
            }

            if (Intent.ActionCreateDocument == Intent.Action) {
                buttonBar.SetTitle ("New Event");

                if (null == preConfigChange) {

                    DateTime startDate = DateTime.MinValue;
                    if (Intent.HasExtra (EXTRA_START_DATE)) {
                        startDate = DateTime.Parse (Intent.GetStringExtra (EXTRA_START_DATE));
                    }

                    if (Intent.HasExtra (EXTRA_MESSAGE_FOR_MEETING)) {
                        cal = CalendarHelper.CreateMeeting (dataFromIntent.EmailMessage, startDate);
                        titleField.Text = cal.Subject;
                        descriptionField.Text = cal.Description;
                    } else {
                        cal = CalendarHelper.DefaultMeeting (startDate);
                    }

                    startTime = cal.StartTime.ToLocalTime ();
                    endTime = cal.EndTime.ToLocalTime ();

                    // Figure out the correct account for the new event.
                    account = NcApplication.Instance.DefaultCalendarAccount;
                    if (!account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) || 0 == new NachoFolders (account.Id, NachoFolders.FilterForCalendars).Count ()) {
                        if (AndroidCalendars.DeviceCalendarsExist ()) {
                            isAppEvent = false;
                            account = McAccount.GetDeviceAccount ();
                        } else {
                            bool foundAccount = false;
                            foreach (var candidateAccountId in McAccount.GetAllConfiguredNormalAccountIds ()) {
                                if (candidateAccountId != account.Id) {
                                    var candidateAccount = McAccount.QueryById<McAccount> (candidateAccountId);
                                    if (candidateAccount.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) && 0 < new NachoFolders (candidateAccountId, NachoFolders.FilterForCalendars).Count ()) {
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
                    }

                    // Figure out the correct calendar within that account for the new event.
                    NachoFolders accountCalendars;
                    if (isAppEvent) {
                        accountCalendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                    } else {
                        accountCalendars = new NachoFolders (AndroidCalendars.GetCalendarFolders ().ToArray ());
                    }
                    calendarFolder = accountCalendars.GetFolder (0);
                    for (int f = 0; f < accountCalendars.Count (); ++f) {
                        var calendar = accountCalendars.GetFolder (f);
                        if (Xml.FolderHierarchy.TypeCode.DefaultCal_8 == calendar.Type) {
                            calendarFolder = calendar;
                            break;
                        }
                    }
                }

                deleteButton.Visibility = ViewStates.Gone;
                FindViewById<View> (Resource.Id.event_delete_button_filler).Visibility = ViewStates.Gone;

            } else {
                NcAssert.True (Intent.ActionEdit == Intent.Action, "The intent for EventEditActivity must have an action of Edit or CreateDocument.");
                NcAssert.True (Intent.HasExtra (EXTRA_EVENT_TO_EDIT), "When EventEditActivity is called with an Edit action, the event to edit must be specified.");

                buttonBar.SetTitle ("Edit Event");

                if (null == preConfigChange) {

                    cal = null;
                    if (null != dataFromIntent.Event) {
                        if (0 == dataFromIntent.Event.DeviceEventId) {
                            cal = McCalendar.QueryById<McCalendar> (dataFromIntent.Event.CalendarId);
                        } else {
                            AndroidDeviceCalendarFolder androidCalendarFolder;
                            cal = AndroidCalendars.GetEventDetails (dataFromIntent.Event.DeviceEventId, out androidCalendarFolder);
                            calendarFolder = androidCalendarFolder;
                            isAppEvent = false;
                        }
                    }
                    if (null == dataFromIntent.Event || null == cal) {
                        NcAlertView.Show (this, "Event Deleted", "The event can't be edited because it has been deleted.", () => {
                            SetResult (Result.Canceled);
                            Finish ();
                        });
                        return;
                    }

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

                    if (isAppEvent) {
                        account = McAccount.QueryById<McAccount> (cal.AccountId);
                        calendarFolder = McFolder.QueryByFolderEntryId<McCalendar> (cal.AccountId, cal.Id).FirstOrDefault ();
                    } else {
                        account = McAccount.GetDeviceAccount ();
                        if (null == calendarFolder) {
                            calendarFolder = McFolder.GetDeviceCalendarsFolder ();
                        }
                    }
                }

                titleField.Text = cal.GetSubject ();
                locationField.Text = cal.GetLocation ();
                if (McBody.BodyTypeEnum.PlainText_1 == cal.DescriptionType) {
                    descriptionField.Text = cal.Description;
                }

                deleteButton.Click += DeleteButton_Click;
            }

            allDayField.CheckedChange += AllDayField_CheckedChange;
            startDateField.Click += StartDateField_Click;
            startDateArrow.Click += StartDateField_Click;
            startTimeField.Click += StartTimeField_Click;
            startTimeArrow.Click += StartTimeField_Click;
            endDateField.Click += EndDateField_Click;
            endDateArrow.Click += EndDateField_Click;
            endTimeField.Click += EndTimeField_Click;
            endTimeArrow.Click += EndTimeField_Click;

            // The text in the date/time fields, the reminder field, the attendee count field,
            // and the calendar field should look like the default text for an EditText field,
            // not a TextView field.  Copy the necessary information from one of the EditText
            // fields to make that happen.
            startDateField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            startTimeField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            endDateField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            endTimeField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            attendeeCountField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            reminderField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            calendarField.SetTextSize (Android.Util.ComplexUnitType.Px, titleField.TextSize);
            startDateField.SetTextColor (titleField.TextColors);
            startTimeField.SetTextColor (titleField.TextColors);
            endDateField.SetTextColor (titleField.TextColors);
            endTimeField.SetTextColor (titleField.TextColors);
            attendeeCountField.SetTextColor (titleField.TextColors);
            reminderField.SetTextColor (titleField.TextColors);
            calendarField.SetTextColor (titleField.TextColors);

            allDayField.Checked = cal.AllDayEvent;
            ConfigureStartEndFields ();

            attendeeCountField.Text = string.Format ("( {0} )", cal.attendees.Count);
            attendeeCountField.Click += Attendee_Click;
            var attendeeArrow = FindViewById<ImageView> (Resource.Id.event_edit_attendee_arrow);
            attendeeArrow.Click += Attendee_Click;
            if (!isAppEvent) {
                FindViewById<View> (Resource.Id.event_edit_attendee_section).Visibility = ViewStates.Gone;
            }

            reminderField.Text = Pretty.ReminderString (cal.HasReminder (), cal.GetReminder ());
            reminderField.Click += Reminder_Click;
            var reminderArrow = FindViewById<ImageView> (Resource.Id.event_edit_reminder_arrow);
            reminderArrow.Click += Reminder_Click;

            calendarField.Text = calendarFolder.DisplayName;
            calendarField.Click += Calendar_Click;
            var calendarArrow = FindViewById<ImageView> (Resource.Id.event_edit_calendar_arrow);
            calendarArrow.Click += Calendar_Click;

            if (null != bundle) {
                var reminderFragment = FragmentManager.FindFragmentByTag<ReminderChooserFragment> (REMINDER_CHOOSER_TAG);
                if (null != reminderFragment) {
                    ConfigureReminderChooser (reminderFragment);
                }
                var calendarFragment = FragmentManager.FindFragmentByTag<CalendarChooserFragment> (CALENDAR_CHOOSER_TAG);
                if (null != calendarFragment) {
                    ConfigureCalendarChooser (calendarFragment);
                }
            }
        }

        protected override void OnPause ()
        {
            base.OnPause ();

            var retained = RetainedData;
            retained.CalendarItem = cal;
            retained.IsAppEvent = isAppEvent;
            retained.Account = account;
            retained.CalendarFolder = calendarFolder;
            retained.StartTime = startTime;
            retained.EndTime = endTime;
            retained.EndTimeChanged = endTimeChanged;
            retained.DescriptionChanged = descriptionChanged;
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
            startDateField.Text = Pretty.MediumFullDate (startTime);
            endDateField.Text = Pretty.MediumFullDate (endTime);

            if (allDayField.Checked) {
                startTimeField.Visibility = ViewStates.Gone;
                startTimeArrow.Visibility = ViewStates.Gone;
                endTimeField.Visibility = ViewStates.Gone;
                endTimeArrow.Visibility = ViewStates.Gone;
            } else {
                startTimeField.Visibility = ViewStates.Visible;
                startTimeArrow.Visibility = ViewStates.Visible;
                endTimeField.Visibility = ViewStates.Visible;
                endTimeArrow.Visibility = ViewStates.Visible;
                startTimeField.Text = Pretty.Time (startTime);
                endTimeField.Text = Pretty.Time (endTime);
            }

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
                if (McAccount.AccountTypeEnum.Device == account.AccountType) {
                    var calendars = AndroidCalendars.GetCalendarFolders ();
                    if (0 < calendars.Count) {
                        result.Add (new Tuple<McAccount, NachoFolders> (account, new NachoFolders (calendars.ToArray ())));
                    }
                } else {
                    if (account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                        var calendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                        if (0 < calendars.Count ()) {
                            result.Add (new Tuple<McAccount, NachoFolders> (account, calendars));
                        }
                    }
                }
            }
            if (0 == result.Count) {
                Log.Error (Log.LOG_CALENDAR, "Couldn't find any calendars for the event editor's calendar chooser.");
                result.Add (new Tuple<McAccount, NachoFolders> (account, new NachoFolders (calendarFolder)));
            }
            return result;
        }

        private void DeleteEvent ()
        {
            if (isAppEvent) {
                if (0 != cal.attendees.Count) {
                    var iCalCancelPart = CalendarHelper.MimeCancelFromCalendar (cal);
                    var mimeBody = CalendarHelper.CreateMime ("", iCalCancelPart, new List<McAttachment> ());
                    CalendarHelper.SendMeetingCancelations (account, cal, null, mimeBody);
                }
                BackEnd.Instance.DeleteCalCmd (account.Id, cal.Id);
            } else {
                AndroidCalendars.DeleteEvent (RetainedData.Event.DeviceEventId);
            }
        }

        private void ConfigureReminderChooser (ReminderChooserFragment reminderFragment)
        {
            reminderFragment.SetValues (cal.HasReminder (), (int)cal.GetReminder (), (bool hasReminder, int reminder) => {
                cal.ReminderIsSet = hasReminder;
                if (hasReminder) {
                    cal.Reminder = (uint)reminder;
                }
                reminderField.Text = Pretty.ReminderString (hasReminder, (uint)reminder);
            });
        }

        private void ConfigureCalendarChooser (CalendarChooserFragment calendarFragment)
        {
            calendarFragment.SetValues (GetChoosableCalendars (), calendarFolder, (McFolder selectedFolder) => {
                if (account.Id != selectedFolder.AccountId) {
                    account = McAccount.QueryById<McAccount> (selectedFolder.AccountId);
                    bool newIsAppEvent = McAccount.AccountTypeEnum.Device != account.AccountType;
                    if (isAppEvent && !newIsAppEvent) {
                        FindViewById<View> (Resource.Id.event_edit_attendee_section).Visibility = ViewStates.Gone;
                    } else if (!isAppEvent && newIsAppEvent) {
                        FindViewById<View> (Resource.Id.event_edit_attendee_section).Visibility = ViewStates.Visible;
                    }
                    isAppEvent = newIsAppEvent;
                }
                calendarFolder = selectedFolder;
                calendarField.Text = selectedFolder.DisplayName;
            });
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

                // The app does not keep track of the account owner's name. Use the e-mail address instead.
                cal.OrganizerName = account.EmailAddr;
                cal.OrganizerEmail = account.EmailAddr;
                cal.MeetingStatusIsSet = true;
                cal.ResponseRequestedIsSet = true;
                if (0 == cal.attendees.Count) {
                    cal.MeetingStatus = NcMeetingStatus.Appointment;
                    cal.ResponseRequested = false;
                } else {
                    cal.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
                    cal.ResponseRequested = true;
                }

                // There is no UI for setting the BusyStatus.  For new events, set it to Free for
                // all-day events and Busy for other events.  If the app doesn't explicitly set
                // BusyStatus, some servers will treat it as if it were Free, while others will act
                // as if it were Busy.
                if (!cal.BusyStatusIsSet) {
                    cal.BusyStatus = cal.AllDayEvent ? NcBusyStatus.Free : NcBusyStatus.Busy;
                    cal.BusyStatusIsSet = true;
                }

                if (string.IsNullOrEmpty(cal.UID)) {
                    cal.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpperInvariant ();
                }

                cal.DtStamp = DateTime.UtcNow;

                if (!isAppEvent) {
                    long calendarId = 0;
                    if (calendarFolder is AndroidDeviceCalendarFolder) {
                        calendarId = ((AndroidDeviceCalendarFolder)calendarFolder).DeviceCalendarId;
                    }
                    if (Intent.ActionCreateDocument == Intent.Action) {
                        AndroidCalendars.InsertDeviceEvent (cal, calendarId);
                    } else {
                        AndroidCalendars.UpdateDeviceEvent (cal, RetainedData.Event.DeviceEventId, calendarId);
                    }
                } else if (Intent.ActionCreateDocument == Intent.Action) {
                    cal.Insert ();
                    calendarFolder.Link (cal);
                    BackEnd.Instance.CreateCalCmd (account.Id, cal.Id, calendarFolder.Id);
                } else {
                    cal.RecurrencesGeneratedUntil = DateTime.MinValue;
                    cal.Update ();
                    var oldFolder = McFolder.QueryByFolderEntryId<McCalendar> (cal.AccountId, cal.Id).FirstOrDefault ();
                    if (null != oldFolder && oldFolder.Id != calendarFolder.Id) {
                        BackEnd.Instance.MoveCalCmd (account.Id, cal.Id, calendarFolder.Id);
                        oldFolder.Unlink (cal);
                        calendarFolder.Link (cal);
                    }
                    BackEnd.Instance.UpdateCalCmd (cal.AccountId, cal.Id, descriptionChanged);
                }

                if (0 < cal.attendees.Count) {
                    string plainTextDescription = "";
                    if (McBody.BodyTypeEnum.PlainText_1 == cal.DescriptionType) {
                        plainTextDescription = cal.Description;
                    }
                    var iCalPart = CalendarHelper.MimeRequestFromCalendar (cal);
                    var mimeBody = CalendarHelper.CreateMime (plainTextDescription, iCalPart, cal.attachments);
                    CalendarHelper.SendInvites (account, cal, null, null, mimeBody, null);
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
            if (allDayField.Checked && !endTimeChanged && startTime.Date != endTime.Date && TimeSpan.FromHours (1) >= endTime - startTime) {
                // The user changed the event be an all-day event.  The event is no more than
                // an hour long, its start and end times are on different days, and the user
                // hasn't explicitly changed the end time.  It is more likely that the user
                // wants the event to be a single day rather than a multi-day all-day event.
                // If the app is guessing incorrectly, the user can still correct the dates
                // before saving the event.
                endTime = startTime;
            }
            ConfigureStartEndFields ();
        }

        private void StartDateField_Click (object sender, EventArgs e)
        {
            DateTime minDate, maxDate;
            DatePickerRangeForEvent (startTime, endTime, out minDate, out maxDate);
            DatePicker.Show (this, startTime, minDate, maxDate, (DateTime date) => {
                StartTimeMaybeChanged (date.Date + startTime.TimeOfDay);
            });
        }

        private void StartTimeField_Click (object sender, EventArgs e)
        {
            TimePicker.Show (this, startTime.TimeOfDay, (TimeSpan time) => {
                StartTimeMaybeChanged (startTime.Date + time);
            });
        }

        private void EndDateField_Click (object sender, EventArgs e)
        {
            DateTime minDate, maxDate;
            DatePickerRangeForEvent (startTime, endTime, out minDate, out maxDate);
            DatePicker.Show (this, endTime, minDate, maxDate, (DateTime date) => {
                EndTimeMaybeChanged (date.Date + endTime.TimeOfDay);
            });
        }

        private void EndTimeField_Click (object sender, EventArgs e)
        {
            TimePicker.Show (this, endTime.TimeOfDay, (TimeSpan time) => {
                EndTimeMaybeChanged (endTime.Date + time);
            });
        }

        private void Reminder_Click (object sender, EventArgs e)
        {
            var reminderFragment = new ReminderChooserFragment ();
            ConfigureReminderChooser (reminderFragment);
            reminderFragment.Show (FragmentManager, REMINDER_CHOOSER_TAG);
        }

        private void Attendee_Click (object sender, EventArgs e)
        {
            StartActivityForResult (AttendeeEditActivity.AttendeeEditIntent (this, account.Id, cal.attendees), ATTENDEE_ACTIVITY_REQUEST);
        }

        private void Calendar_Click (object sender, EventArgs e)
        {
            var calendarFragment = new CalendarChooserFragment ();
            ConfigureCalendarChooser (calendarFragment);
            calendarFragment.Show (FragmentManager, CALENDAR_CHOOSER_TAG);
        }

        private void DeleteButton_Click (object sender, EventArgs e)
        {
            string message;
            if (0 == cal.attendees.Count) {
                message = "Delete this event?";
            } else {
                message = "Cancel this meeting and notify the attendees?";
            }
            NcAlertView.Show (this, "Delete Event", message, () => {
                DeleteEvent ();
                SetResult (DELETE_EVENT_RESULT_CODE);
                Finish ();
            }, () => {
            });
        }
    }
}
