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
        private EditText locationField;

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

        private void SaveButton_Click (object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty (FindViewById<EditText> (Resource.Id.event_edit_title).Text)) {
                NcAlertView.ShowMessage (this, "Cannot Save Event", "The title of the event must not be empty.");
            } else {

                cal.AccountId = account.Id;
                cal.Subject = titleField.Text;
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

    }
}

