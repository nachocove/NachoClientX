
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
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public interface IEventViewFragmentOwner
    {
        McEvent EventToView { get; }
    }

    public class EventViewFragment : Fragment
    {
        private const int EDIT_REQUEST_CODE = 1;
        private const int NOTE_REQUEST_CODE = 2;

        private const string REMINDER_CHOOSER_TAG = "ReminderChooser";

        private McEvent ev;
        private AndroidEventDetail detail;

        private View view;
        private ButtonBar buttonBar;

        private View attendButtonView;
        private View maybeButtonView;
        private View declineButtonView;

        private ImageView attendButtonImage;
        private ImageView maybeButtonImage;
        private ImageView declineButtonImage;

        private NcResponseType userResponse = NcResponseType.None;
        private string responseCmdToken = null;

        private NcEmailMessageBundle bodyMessageBundle;
        private Android.Webkit.WebView webView;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            view = inflater.Inflate (Resource.Layout.EventViewFragment, container, false);
            buttonBar = new ButtonBar (view);

            attendButtonView = view.FindViewById<View> (Resource.Id.event_attend_view);
            maybeButtonView = view.FindViewById<View> (Resource.Id.event_maybe_view);
            declineButtonView = view.FindViewById<View> (Resource.Id.event_decline_view);

            attendButtonImage = view.FindViewById<ImageView> (Resource.Id.event_attend_button);
            maybeButtonImage = view.FindViewById<ImageView> (Resource.Id.event_maybe_button);
            declineButtonImage = view.FindViewById<ImageView> (Resource.Id.event_decline_button);

            BindListeners ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);
            this.ev = ((IEventViewFragmentOwner)Activity).EventToView;
            this.detail = new AndroidEventDetail (ev);
            if (!detail.IsValid) {
                EventWasDeleted ();
                return;
            }
            userResponse = detail.SpecificItem.HasResponseType () ? detail.SpecificItem.GetResponseType () : NcResponseType.None;
            BindEventView ();
            if (null != savedInstanceState) {
                var reminderFragment = FragmentManager.FindFragmentByTag<ReminderChooserFragment> (REMINDER_CHOOSER_TAG);
                if (null != reminderFragment) {
                    ConfigureReminderChooser (reminderFragment);
                }
            }
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {

            case EDIT_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    // The event was edited. Refresh the UI.
                    detail.HasBeenEdited = true;
                    BindEventView ();
                } else if (EventEditActivity.DELETE_EVENT_RESULT_CODE == resultCode) {
                    // The event was deleted.  Close the event detail view.
                    this.Activity.SetResult (Result.Ok);
                    this.Activity.Finish ();
                }
                break;

            case NOTE_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    NcAssert.True (detail.IsAppEvent);
                    string newNoteText = NoteActivity.ModifiedNoteText (data);
                    if (null != newNoteText) {
                        var note = McNote.QueryByTypeId (detail.SeriesItem.Id, McNote.NoteType.Event).FirstOrDefault ();
                        if (null == note) {
                            note = new McNote () {
                                AccountId = detail.Account.Id,
                                DisplayName = string.Format ("{0} - {1}", detail.SpecificItem.GetSubject (), Pretty.ShortDate (DateTime.UtcNow)),
                                TypeId = detail.SeriesItem.Id,
                                noteType = McNote.NoteType.Event,
                                noteContent = newNoteText,
                            };
                            note.Insert ();
                        } else {
                            note.noteContent = newNoteText;
                            note.Update ();
                        }
                        BindEventView ();
                    }
                }
                break;
            }
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        ViewStates VisibleIfTrue (bool b)
        {
            return b ? ViewStates.Visible : ViewStates.Gone;
        }

        ViewStates GoneIfNullOrEmpty (string s)
        {
            return VisibleIfTrue (!String.IsNullOrEmpty (s));
        }

        /// <summary>
        /// UI configuration that should happen only once, when the view hierarchy is first created,
        /// and that is independent of the data being displayed. This will consist mostly of adding
        /// event listeners.
        /// </summary>
        private void BindListeners ()
        {
            var attendeesView = view.FindViewById<View> (Resource.Id.event_attendee_view);
            attendeesView.Click += AttendeesView_Click;

            var reminderView = view.FindViewById<View> (Resource.Id.event_reminder_label);
            reminderView.Click += ReminderView_Click;
            var reminderArrow = view.FindViewById<View> (Resource.Id.event_reminder_arrow);
            reminderArrow.Click += ReminderView_Click;

            var notesView = view.FindViewById<View> (Resource.Id.event_notes_label);
            notesView.Click += NotesView_Click;
            var notesArrow = view.FindViewById<View> (Resource.Id.event_notes_arrow);
            notesArrow.Click += NotesView_Click;

            attendButtonView.Click += AttendButton_Click;
            maybeButtonView.Click += MaybeButton_Click;
            declineButtonView.Click += DeclineButton_Click;

            var removeButton = view.FindViewById<View> (Resource.Id.event_remove_view);
            removeButton.Click += RemoveButton_Click;
        }

        /// <summary>
        /// Configure the view based on the data being displayed.
        /// </summary>
        void BindEventView ()
        {
            // TODO: Attachments

            detail.Refresh ();

            if (!detail.IsValid) {
                EventWasDeleted ();
                return;
            }

            buttonBar.SetTitle (Pretty.LongMonthForceYear (detail.StartTime));

            if (detail.CanEdit) {
                buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.gen_edit, EditButton_Click);
            } else {
                buttonBar.ClearButton (ButtonBar.Button.Right1);
            }

            ConfigureRsvpBar ();

            var titleView = view.FindViewById<TextView> (Resource.Id.event_title);
            titleView.Text = detail.SpecificItem.GetSubject ();

            var whenView = view.FindViewById<TextView> (Resource.Id.event_when_label);
            var durationView = view.FindViewById<TextView> (Resource.Id.event_duration_label);
            var recurrenceView = view.FindViewById<TextView> (Resource.Id.event_recurrence_label);

            whenView.Text = detail.DateString;
            durationView.Text = detail.DurationString;

            recurrenceView.Visibility = VisibleIfTrue (detail.IsRecurring);
            if (detail.IsRecurring) {
                recurrenceView.Text = detail.RecurrenceString;
            }

            var location = detail.SpecificItem.GetLocation ();
            var locationView = view.FindViewById<View> (Resource.Id.location_view);
            locationView.Visibility = GoneIfNullOrEmpty (location);
            if (!String.IsNullOrEmpty (location)) {
                var locationLabel = view.FindViewById<TextView> (Resource.Id.event_location_label);
                locationLabel.Text = location;
            }

            webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.event_description_webview);
            var webClient = new NachoWebViewClient ();
            webView.SetWebViewClient (webClient);
            if (detail.IsAppEvent) {
                var body = McBody.QueryById<McBody> (detail.SpecificItem.BodyId);
                if (McBody.IsNontruncatedBodyComplete (body)) {
                    bodyMessageBundle = new NcEmailMessageBundle (body);
                    if (bodyMessageBundle.NeedsUpdate) {
                        NcTask.Run (delegate {
                            bodyMessageBundle.Update ();
                            InvokeOnUIThread.Instance.Invoke (RenderBody);
                        }, "MessageDownloader_UpdateBundle");
                    } else {
                        RenderBody ();
                    }
                } else {
                    webView.Visibility = ViewStates.Gone;
                }
            } else {
                var messageWrapper = new MimeKit.MimeMessage ();
                var part = new MimeKit.TextPart ("plain");
                part.Text = detail.SpecificItem.Description;
                messageWrapper.Body = part;
                bodyMessageBundle = new NcEmailMessageBundle (messageWrapper);
                bodyMessageBundle.Update ();
                RenderBody ();
            }

            var reminderView = view.FindViewById<TextView> (Resource.Id.event_reminder_label);
            reminderView.Text = detail.ReminderString;
            var reminderArrow = view.FindViewById<ImageView> (Resource.Id.event_reminder_arrow);
            reminderArrow.Visibility = VisibleIfTrue (detail.CanChangeReminder);

            if (0 == detail.SpecificItem.attachments.Count) {
                view.FindViewById<View> (Resource.Id.event_attachments_view).Visibility = ViewStates.Gone;
            } else {
                var attachmentTextView = view.FindViewById<TextView> (Resource.Id.event_attachment_placeholder);
                attachmentTextView.Text = string.Format ("{0} attachments. Not yet implemented.", detail.SpecificItem.attachments.Count);
            }

            var organizer_view = view.FindViewById<View> (Resource.Id.event_organizer_view);
            organizer_view.Visibility = VisibleIfTrue (detail.HasNonSelfOrganizer);
            if (detail.HasNonSelfOrganizer) {
                var organizerName = detail.SeriesItem.OrganizerName;
                var organizerNameLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_label);
                organizerNameLabel.Visibility = GoneIfNullOrEmpty (organizerName);
                if (!String.IsNullOrEmpty (organizerName)) {
                    organizerNameLabel.Text = organizerName;
                }
                var organizerEmailLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_email_label);
                organizerEmailLabel.Text = detail.SeriesItem.OrganizerEmail;
                var imageView = view.FindViewById<ContactPhotoView> (Resource.Id.event_organizer_initials);
                var initials = "";
                if (String.IsNullOrEmpty (detail.SeriesItem.OrganizerName)) {
                    initials = ContactsHelper.NameToLetters (detail.SeriesItem.OrganizerName);
                } else {
                    initials = ContactsHelper.NameToLetters (detail.SeriesItem.OrganizerEmail);
                }
                var color = Util.ColorResourceForEmail (detail.Account.Id, detail.SeriesItem.OrganizerEmail);
                imageView.SetEmailAddress (detail.Account.Id, detail.SeriesItem.OrganizerEmail, initials, color);
            }

            var attendees = detail.SpecificItem.attendees;
            if (0 == attendees.Count) {
                view.FindViewById<View> (Resource.Id.event_attendee_view).Visibility = ViewStates.Gone;
            } else {
                for (int a = 0; a < 5; ++a) {
                    var attendeePhotoView = AttendeeInitialsView (view, a);
                    var attendeeNameView = AttendeeNameView (view, a);
                    if (4 == a && 5 < attendees.Count) {
                        attendeePhotoView.SetPortraitId (0, string.Format ("+{0}", attendees.Count - a), Resource.Drawable.UserColor0);
                        attendeeNameView.Text = "";
                    } else if (a < attendees.Count) {
                        var attendee = attendees [a];
                        var initials = ContactsHelper.NameToLetters (attendee.DisplayName);
                        var color = Util.ColorResourceForEmail (detail.Account.Id, attendee.Email);
                        attendeePhotoView.SetEmailAddress (detail.Account.Id, attendee.Email, initials, color);
                        attendeeNameView.Text = GetFirstName (attendee.DisplayName);
                    } else {
                        attendeePhotoView.Visibility = ViewStates.Gone;
                        attendeeNameView.Visibility = ViewStates.Gone;
                    }
                }
            }

            var notesSection = view.FindViewById<View> (Resource.Id.event_notes_section);
            if (detail.IsAppEvent) {
                notesSection.Visibility = ViewStates.Visible;
                var notesLabel = view.FindViewById<TextView> (Resource.Id.event_notes_label);
                var note = McNote.QueryByTypeId (detail.SeriesItem.Id, McNote.NoteType.Event).FirstOrDefault ();
                if (null == note) {
                    notesLabel.Text = "";
                } else {
                    notesLabel.Text = note.noteContent;
                }
            } else {
                notesSection.Visibility = ViewStates.Gone;
            }

            var calendarView = view.FindViewById<TextView> (Resource.Id.event_calendar_label);
            calendarView.Text = detail.CalendarNameString;

            var cancelView = view.FindViewById<View> (Resource.Id.event_cancel_view);
            var cancelSeparator = view.FindViewById<View> (Resource.Id.event_cancel_separator);
            cancelView.Visibility = VisibleIfTrue (detail.ShowCancelMeetingButton);
            cancelSeparator.Visibility = VisibleIfTrue (detail.ShowCancelMeetingButton);

        }

        void RenderBody ()
        {
            if (bodyMessageBundle.FullHtmlUrl != null) {
                webView.LoadUrl (bodyMessageBundle.FullHtmlUrl.AbsoluteUri);
            } else {
                var html = bodyMessageBundle.FullHtml;
                webView.LoadDataWithBaseURL (bodyMessageBundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
            }
        }

        public void ConfigureRsvpBar ()
        {
            var rsvpView = view.FindViewById<View> (Resource.Id.event_rsvp_view);
            var removeView = view.FindViewById<View> (Resource.Id.event_remove_view);
            var cancelledView = view.FindViewById<View> (Resource.Id.event_cancelled_view);
            var organizerView = view.FindViewById<View> (Resource.Id.event_self_organizer_view);
            var separatorView = view.FindViewById<View> (Resource.Id.event_top_separator);

            rsvpView.Visibility = ViewStates.Gone;
            removeView.Visibility = ViewStates.Gone;
            cancelledView.Visibility = ViewStates.Gone;
            organizerView.Visibility = ViewStates.Gone;
            separatorView.Visibility = ViewStates.Visible;

            if (detail.SpecificItem.MeetingStatus == NcMeetingStatus.MeetingAttendeeCancelled) {
                if (detail.Account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                    // Show "Remove from calendar"
                    removeView.Visibility = ViewStates.Visible;
                } else {
                    // The calendar is not writable, so the "Remove from calendar" button doesn't make sense.
                    // Instead, just show a message about the meeting being cancelled.
                    cancelledView.Visibility = ViewStates.Visible;
                }
            } else if (detail.IsAppointment) {
                // It's an appointment.  Leave out the header section entirely.
                separatorView.Visibility = ViewStates.Gone;
            } else if (detail.IsOrganizer) {
                // Show "You are the organizer"
                organizerView.Visibility = ViewStates.Visible;
            } else {
                // Show the Attend, Maybe, and Decline buttons.
                rsvpView.Visibility = ViewStates.Visible;
                SelectButtonForResponse (userResponse);
            }
        }

        private ContactPhotoView AttendeeInitialsView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<ContactPhotoView> (id);
        }

        private TextView AttendeeNameView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_name_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_name_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_name_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_name_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_name_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<TextView> (id);
        }

        private static string GetFirstName (string displayName)
        {
            string[] names = displayName.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (names [0] == null) {
                return "";
            }
            if (names [0].Length > 1) {
                return char.ToUpper (names [0] [0]) + names [0].Substring (1);
            }
            return names [0].ToUpper ();
        }

        private void ClearResponseButtons ()
        {
            attendButtonImage.SetImageResource (Resource.Drawable.event_attend);
            attendButtonView.Enabled = true;
            maybeButtonImage.SetImageResource (Resource.Drawable.event_maybe);
            maybeButtonView.Enabled = true;
            declineButtonImage.SetImageResource (Resource.Drawable.event_decline);
            declineButtonView.Enabled = true;
        }

        private void SelectButtonForResponse (NcResponseType response)
        {
            ClearResponseButtons ();

            switch (response) {
            case NcResponseType.Accepted:
                attendButtonImage.SetImageResource (Resource.Drawable.event_attend_active);
                attendButtonView.Enabled = false;
                break;
            case NcResponseType.Tentative:
                maybeButtonImage.SetImageResource (Resource.Drawable.event_maybe_active);
                maybeButtonView.Enabled = false;
                break;
            case NcResponseType.Declined:
                declineButtonImage.SetImageResource (Resource.Drawable.event_decline_active);
                declineButtonView.Enabled = false;
                break;
            default:
                // For any other response, leave all of the buttons unset.
                break;
            }
        }

        private void MakeStatusUpdates (NcResponseType response, bool occurrenceOnly)
        {
            userResponse = response;
            if (detail.IsAppEvent) {
                if (occurrenceOnly) {
                    DateTime occurrenceStartTime;
                    if (detail.SpecificItem is McException) {
                        occurrenceStartTime = ((McException)detail.SpecificItem).ExceptionStartTime;
                    } else {
                        occurrenceStartTime = detail.Occurrence.StartTime;
                    }
                    var cmdResult = BackEnd.Instance.RespondCalCmd (detail.Account.Id, detail.SeriesItem.Id, response, occurrenceStartTime);
                    if (cmdResult.isOK ()) {
                        responseCmdToken = cmdResult.GetValue<string> ();
                        if (detail.SeriesItem.ResponseRequestedIsSet && detail.SeriesItem.ResponseRequested) {
                            var iCalPart = CalendarHelper.MimeResponseFromCalendar (detail.SeriesItem, response, occurrenceStartTime);
                            var mimeBody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                            CalendarHelper.SendMeetingResponse (detail.Account, detail.SeriesItem, mimeBody, response);
                        }
                    }
                } else {
                    var cmdResult = BackEnd.Instance.RespondCalCmd (detail.Account.Id, detail.SeriesItem.Id, response);
                    if (cmdResult.isOK ()) {
                        responseCmdToken = cmdResult.GetValue<string> ();
                        if (detail.SeriesItem.ResponseRequestedIsSet && detail.SeriesItem.ResponseRequested) {
                            var iCalPart = CalendarHelper.MimeResponseFromCalendar (detail.SeriesItem, response);
                            var mimebody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                            CalendarHelper.SendMeetingResponse (detail.Account, detail.SeriesItem, mimebody, response);
                        }
                    }
                }
            }
        }

        private void UpdateStatus (NcResponseType response)
        {
            if (detail.IsRecurring) {
                if (NcResponseType.Declined == response) {
                    new AlertDialog.Builder (this.Activity)
                        .SetTitle ("Decline Meeting?")
                        .SetMessage ("Declining the meeting will also delete the meeting from your calendar.")
                        .SetPositiveButton ("Decline", (sender, e) => {
                            new AlertDialog.Builder (this.Activity)
                                .SetTitle ("Series or Occurrence?")
                                .SetMessage ("Decline the entire series or just this one occurrence?")
                                .SetPositiveButton ("Series", (sender1, e1) => {
                                    MakeStatusUpdates (response, false);
                                    this.Activity.SetResult (Result.Ok);
                                    this.Activity.Finish ();
                                })
                                .SetNegativeButton ("Occurrence", (sender1, e1) => {
                                    MakeStatusUpdates (response, true);
                                    this.Activity.SetResult (Result.Ok);
                                    this.Activity.Finish ();
                                })
                                .SetNeutralButton ("Cancel", (sender1, e1) => {
                                    SelectButtonForResponse (userResponse);
                                })
                                .Create ().Show ();
                        })
                        .SetNegativeButton ("Cancel", (sender, e) => {
                            SelectButtonForResponse (userResponse);
                        })
                        .Create ().Show ();
                } else {
                    new AlertDialog.Builder (this.Activity)
                        .SetTitle ("Series or Occurrence?")
                        .SetMessage ("Respond to the entire series or just this one occurrence?")
                        .SetPositiveButton ("Series", (sender, e) => {
                            MakeStatusUpdates (response, false);
                        })
                        .SetNegativeButton ("Occurrence", (sender, e) => {
                            MakeStatusUpdates (response, true);
                        })
                        .SetNeutralButton ("Cancel", (sender, e) => {
                            SelectButtonForResponse (userResponse);
                        })
                        .Create ().Show ();
                }
            } else if (NcResponseType.Declined == response) {
                new AlertDialog.Builder (this.Activity)
                    .SetTitle ("Decline Meeting?")
                    .SetMessage ("Declining the meeting will also delete the meeting from your calendar.")
                    .SetPositiveButton ("Decline", (sender, e) => {
                        MakeStatusUpdates (response, false);
                        this.Activity.SetResult (Result.Ok);
                        this.Activity.Finish ();
                    })
                    .SetNegativeButton ("Cancel", (sender, e) => {
                        SelectButtonForResponse (userResponse);
                    })
                    .Create ().Show ();
            } else {
                MakeStatusUpdates (response, false);
            }
        }

        private void UserResponseChanged (NcResponseType response)
        {
            if (McAccount.AccountTypeEnum.Device == detail.Account.AccountType) {
                NcAlertView.ShowMessage (this.Activity, "Can't Change Response",
                    "Your response to this meeting can't be changed because the meeting is managed by the device's calendar, not by Nacho Mail." +
                    "Use a different calendar app to change your response.");
            } else if (!detail.Account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                NcAlertView.ShowMessage (this.Activity, "Can't Change Response",
                    "Your response to the meeting can't be changed because the meeting is stored in a calendar that is not writable by this app. " +
                    "Use a different client to change your response.");
            } else {
                SelectButtonForResponse (response);
                UpdateStatus (response);
            }
        }

        private void EventWasDeleted ()
        {
            NcAlertView.Show (this.Activity, "Deleted Event", "The event has been deleted.", () => {
                this.Activity.SetResult (Result.Canceled);
                this.Activity.Finish ();
            });
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_CalendarChanged == s.Status.SubKind) {
                BindEventView ();
            }
            if (NcResult.SubKindEnum.Info_CalendarUpdateSucceeded == s.Status.SubKind) {
                if (null != responseCmdToken && null != s.Tokens && s.Tokens.Contains (responseCmdToken)) {
                    responseCmdToken = null;
                    detail.Refresh ();
                    if (!detail.IsValid) {
                        EventWasDeleted ();
                    }
                }
            }
        }

        private void ConfigureReminderChooser (ReminderChooserFragment reminderFragment)
        {
            reminderFragment.SetValues (detail.SpecificItem.HasReminder (), (int)detail.SpecificItem.GetReminder (),
                (bool hasReminder, int reminder) => {
                    detail.SpecificItem.ReminderIsSet = hasReminder;
                    if (hasReminder) {
                        detail.SpecificItem.Reminder = (uint)reminder;
                    }
                    if (detail.IsAppEvent) {
                        detail.SpecificItem.Update ();
                        BackEnd.Instance.UpdateCalCmd (detail.Account.Id, detail.SpecificItem.Id, false);
                    } else {
                        AndroidCalendars.UpdateEventReminder (detail.SpecificItem, detail.Occurrence.DeviceEventId, removeExisting: true);
                    }
                    BindEventView ();
                });
        }

        private void EditButton_Click (object sender, EventArgs e)
        {
            StartActivityForResult (EventEditActivity.EditEventIntent (this.Activity, ev), EDIT_REQUEST_CODE);
        }

        private void AttendeesView_Click (object sender, EventArgs e)
        {
            StartActivity (AttendeeViewActivity.AttendeeViewIntent (this.Activity, detail.Account.Id, detail.SpecificItem.attendees));
        }

        private void ReminderView_Click (object sender, EventArgs e)
        {
            if (detail.CanChangeReminder) {
                var reminderFragment = new ReminderChooserFragment ();
                ConfigureReminderChooser (reminderFragment);
                reminderFragment.Show (FragmentManager, REMINDER_CHOOSER_TAG);
            }
        }

        private void NotesView_Click (object sender, EventArgs e)
        {
            NcAssert.True (detail.IsAppEvent);
            string noteText = "";
            var note = McNote.QueryByTypeId (detail.SeriesItem.Id, McNote.NoteType.Event).FirstOrDefault ();
            if (null != note) {
                noteText = note.noteContent;
            }

            var title = Pretty.NoteTitle (detail.SpecificItem.GetSubject ());
            StartActivityForResult (
                NoteActivity.EditNoteIntent (this.Activity, title, null, noteText, insertDate: false),
                NOTE_REQUEST_CODE);
        }

        private void AttendButton_Click (object sender, EventArgs e)
        {
            UserResponseChanged (NcResponseType.Accepted);
        }

        private void MaybeButton_Click (object sender, EventArgs e)
        {
            UserResponseChanged (NcResponseType.Tentative);
        }

        private void DeclineButton_Click (object sender, EventArgs e)
        {
            UserResponseChanged (NcResponseType.Declined);
        }

        private void RemoveButton_Click (object sender, EventArgs e)
        {
            // Remove the item from the calendar
            if (detail.SpecificItem is McException && NcMeetingStatus.MeetingAttendeeCancelled != detail.SeriesItem.MeetingStatus) {
                // The user is viewing an occurrence of a recurring meeting, and it appears that the
                // entire series has not been canceled.  So delete just this one occurrence.
                CalendarHelper.CancelOccurrence (detail.SeriesItem, ((McException)detail.SpecificItem).ExceptionStartTime);
                BackEnd.Instance.UpdateCalCmd (detail.Account.Id, detail.SeriesItem.Id, sendBody: false);
            } else {
                BackEnd.Instance.DeleteCalCmd (detail.Account.Id, detail.SeriesItem.Id);
            }

            this.Activity.Finish ();
        }
    }

    public class AndroidEventDetail : NcEventDetail
    {
        bool isAppEvent;
        string calendarName;
        bool isWritableDeviceCalendar;

        public AndroidEventDetail (McEvent occurrence)
        {
            isAppEvent = 0 == occurrence.DeviceEventId;
            if (isAppEvent) {
                base.Initialize (occurrence);
            } else {
                AndroidDeviceCalendarFolder folder;
                var calItem = AndroidCalendars.GetEventDetails (occurrence.DeviceEventId, out folder);
                if (null == folder) {
                    calendarName = null;
                    isWritableDeviceCalendar = false;
                } else {
                    calendarName = folder.DisplayName;
                    isWritableDeviceCalendar = folder.IsWritable;
                }
                base.Initialize (occurrence, calItem, calItem, McAccount.GetDeviceAccount ());
            }
        }

        public bool IsAppEvent {
            get {
                return isAppEvent;
            }
        }

        public override void Refresh ()
        {
            if (isAppEvent) {
                base.Refresh ();
            } else {
                AndroidDeviceCalendarFolder folder;
                var calItem = AndroidCalendars.GetEventDetails (Occurrence.DeviceEventId, out folder);
                if (null == folder) {
                    calendarName = null;
                    isWritableDeviceCalendar = false;
                } else {
                    calendarName = folder.DisplayName;
                    isWritableDeviceCalendar = folder.IsWritable;
                }
                base.Initialize (Occurrence, calItem, calItem, McAccount.GetDeviceAccount ());
            }
        }

        public override bool CanEdit {
            get {
                if (isAppEvent) {
                    return base.CanEdit;
                }
                return isWritableDeviceCalendar && IsOrganizer && !IsRecurring && 0 == SeriesItem.attendees.Count;
            }
        }

        public override bool ShowCancelMeetingButton {
            get {
                return isAppEvent && base.ShowCancelMeetingButton;
            }
        }

        public override string CalendarNameString {
            get {
                if (isAppEvent) {
                    return base.CalendarNameString;
                } else {
                    return string.Format ("{0} : {1}", Account.DisplayName, calendarName);
                }
            }
        }
    }
}
