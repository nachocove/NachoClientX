// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using DDay.iCal;
using DDay.iCal.Serialization.iCalendar;
using MimeKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class CalendarItemViewController : DialogViewController
    {
        protected enum Action
        {
            undefined,
            create,
            edit,
        };

        protected Action action;
        public McCalendar calendarItem;
        // 'c' is working copy
        protected McCalendar c;
        protected bool editing;
        protected McFolder folder;
        protected McAccount account;
        protected NachoFolders calendars;

        public CalendarItemViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // When user clicks done, check, confirm, and save
            doneButton.Clicked += (object sender, EventArgs e) => {
                // TODO: Check for changes before asking the user
                UIAlertView alert = new UIAlertView ();
                alert.Title = "Confirmation";
                alert.Message = "Save this calendar event?";
                alert.AddButton ("Yes");
                alert.AddButton ("No");
                alert.Dismissed += (object alertSender, UIButtonEventArgs alertEvent) => {
                    if (0 == alertEvent.ButtonIndex) {
                        var iCal = ExtractDialogValues ();
                        SyncMeetingRequest ();
                        SendInvites (iCal);
                        ReloadRoot(ShowDetail ());
                    }
                };
                alert.Show ();
            };

            editButton.Clicked += (object sender, EventArgs e) => {
                ReloadRoot (EditDetail ());
            };

            cancelButton.Clicked += (object sender, EventArgs e) => {
                if (Action.create == action) {
                    NavigationController.PopViewControllerAnimated (true);
                    return;
                }
                if (Action.edit == action) {
                    c = calendarItem;
                    ReloadRoot(ShowDetail ());
                    return;
                }
                NachoAssert.CaseError ();
            };

            // TODO: Need account manager.
            // We only have one account, for now.
            account = BackEnd.Instance.Db.Table<McAccount> ().First ();
            calendars = new NachoFolders (NachoFolders.FilterForCalendars);

            // Set up view
            Pushing = true;
            if (null == calendarItem) {
                action = Action.create;
                c = new McCalendar ();
                c.StartTime = DateTime.Now;
                c.EndTime = c.StartTime.AddMinutes (30.0);
                Root = EditDetail ();
            } else {
                action = Action.edit;
                calendarItem.ReadAncillaryData ();
                c = calendarItem;
                Root = ShowDetail ();
            }
            TableView.SeparatorColor = UIColor.Clear;
        }

        protected void ReloadRoot (RootElement root)
        {
            NSAction animation = new NSAction (delegate {
                Root = root;
                ReloadComplete ();
            });
            UIView.Transition (TableView, 0.3, UIViewAnimationOptions.TransitionCrossDissolve, animation, null);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("CalendarItemToAttendeeView")) {
                var dc = (AttendeeViewController)segue.DestinationViewController;
                dc.SetAttendeeList (c.attendees);
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    dc.GetAttendeeList (ref c.attendees);
                };
            }
        }

        /// <summary>
        /// Shows the calendar, read-only.
        /// </summary>
        protected RootElement ShowDetail ()
        {
            editing = false;
            NavigationItem.LeftBarButtonItem = null;
            NavigationItem.RightBarButtonItem = editButton;

            var root = new RootElement (c.Subject);
            root.UnevenRows = true;

            Section section = null;

            section = new ThinSection ();
            section.Add (new SubjectElement (c.Subject));
            section.Add (new StartTimeElementWithIconIndent (Pretty.FullDateString (c.StartTime)));
            if (c.AllDayEvent) {
                section.Add (new DurationElement (Pretty.AllDayStartToEnd (c.StartTime, c.EndTime)));
            } else {
                section.Add (new DurationElement (Pretty.EventStartToEnd (c.StartTime, c.EndTime)));
            }
            root.Add (section);

            if (c.ResponseRequested) {
                section = new ThinSection ();
                var button1 = new StyledStringElementWithDot ("Accept", UIColor.Green);
                button1.Tapped += () => {
                    UpdateStatus (NcResponseType.Accepted);
                };
                var button2 = new StyledStringElementWithDot ("Tentative", UIColor.Yellow);
                button2.Tapped += () => {
                    UpdateStatus (NcResponseType.Tentative);
                };
                var button3 = new StyledStringElementWithDot ("Decline", UIColor.Red);
                button3.Tapped += () => {
                    UpdateStatus (NcResponseType.Declined);
                };
                section.Add (button1);
                section.Add (button2);
                section.Add (button3);
                root.Add (section);
            }

            if (null != c.Location) {
                section = new ThinSection ();
                section.Add (new LocationElement (c.Location));
                root.Add (section);
            }

            section = new ThinSection ();
            {
                var e = new StyledStringElement ("People");
                var image = UIImage.FromBundle ("ic_action_group");
                e.Image = image.Scale (new SizeF (22.0f, 22.0f));
                e.Font = UIFont.SystemFontOfSize (17.0f);
                e.Tapped += () => {
                    PushAttendeeView ();
                };
                e.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                section.Add (e);
            }
            root.Add (section);

            section = new ThinSection ();
            using (var image = UIImage.FromBundle ("ic_action_event")) {
                var scaledImage = image.Scale (new SizeF (22.0f, 22.0f));
                section.Add (new StyledStringElementWithIcon ("Calendar", MyCalendarName (c), scaledImage));
            }
            using (var image = UIImage.FromBundle ("ic_action_alarms")) {
                var scaledImage = image.Scale (new SizeF (22.0f, 22.0f));
                section.Add (new StyledStringElementWithIcon ("Reminder", Pretty.ReminderString (c.Reminder), scaledImage));
            }
            root.Add (section);

            return root;
        }

        EntryElementWithIcon subjectEntryElement;
        AppointmentEntryElement appointmentEntryElement;
        RootElementWithIcon reminderEntryElement;
        PeopleEntryElement peopleEntryElement;
        EntryElementWithIcon locationEntryElement;
        RootElementWithIcon calendarEntryElement;

        /// <summary>
        /// Edit the (possibly empty) calendar entry
        /// </summary>
        protected RootElement EditDetail ()
        {
            editing = true;
            NavigationItem.LeftBarButtonItem = cancelButton;
            NavigationItem.RightBarButtonItem = doneButton;

            subjectEntryElement = new EntryElementWithIcon (NachoClient.Util.DotWithColor (UIColor.Blue), "Title", c.Subject);
            using (var icon = UIImage.FromBundle ("ic_action_place")) {
                var scaledIcon = icon.Scale (new SizeF (22.0f, 22.0f));
                locationEntryElement = new EntryElementWithIcon (scaledIcon, "Location", c.Location);
            }
            appointmentEntryElement = new AppointmentEntryElement (c.StartTime, c.EndTime, c.AllDayEvent);
            peopleEntryElement = new PeopleEntryElement ();

            // TODO: Get the calendar folder that holds the event
            calendarEntryElement = new RootElementWithIcon ("ic_action_event", "Calendar", new RadioGroup (0)) {
                new CalendarRadioElementSection (calendars)
            };

            reminderEntryElement = new RootElementWithIcon ("ic_action_alarms", "Reminder");
            reminderEntryElement.Add (new ReminderSection (c.Reminder));
            reminderEntryElement.UnevenRows = true;

            appointmentEntryElement.Tapped += (DialogViewController arg1, UITableView arg2, NSIndexPath arg3) => {
                arg2.DeselectRow (arg3, true);
                AppointmentEntryPopup ();
            };

            peopleEntryElement.Tapped += () => {
                AttendeeEntryPopup ();
            };
                
            var root = new RootElement (c.Subject);
            root.UnevenRows = true;

            Section section = null;

            section = new ThinSection ();
            section.Add (subjectEntryElement);
            root.Add (section);

            section = new ThinSection ();
            section.Add (appointmentEntryElement);
            root.Add (section);

            section = new ThinSection ();
            section.Add (peopleEntryElement);
            root.Add (section);

            section = new ThinSection ();
            section.Add (locationEntryElement);
            root.Add (section);

            section = new ThinSection ();
            section.Add (calendarEntryElement);
            section.Add (reminderEntryElement);
            root.Add (section);

            return root;
        }

        /// <summary>
        /// Update the screen representation with new
        /// start, end, and all-day event information.
        /// </summary>
        protected void AppointmentEntryPopup ()
        {
            var allDayEvent = new BooleanElement ("All day event", appointmentEntryElement.allDayEvent);
            var startDateTimeElement = new DateTimeEntryElement ("Start time");
            var endDateTimeElement = new DateTimeEntryElement ("End time");

            startDateTimeElement.DateValue = appointmentEntryElement.startDateTime;
            endDateTimeElement.DateValue = appointmentEntryElement.endDateTime;
            allDayEvent.Value = appointmentEntryElement.allDayEvent;

            var root = new RootElement ("Meeting Time");
            var section = new Section ();
            section.Add (startDateTimeElement);
            section.Add (endDateTimeElement);
            section.Add (allDayEvent);
            root.Add (section);
                  
            var dvc = new DialogViewController (root, true);

            dvc.ViewDisappearing += (object sender, EventArgs e) => {
                appointmentEntryElement.allDayEvent = allDayEvent.Value;
                appointmentEntryElement.startDateTime = startDateTimeElement.DateValue;
                appointmentEntryElement.endDateTime = endDateTimeElement.DateValue;
                Root.Reload (appointmentEntryElement, UITableViewRowAnimation.Fade);
            };

            NavigationController.PushViewController (dvc, true);
        }

        public void AttendeeEntryPopup ()
        {
            PerformSegue ("CalendarItemToAttendeeView", this);
        }

        public void PushAttendeeView ()
        {
            var root = new RootElement (c.Subject);
            var section = new Section ();

            foreach (var attendee in c.attendees) {
                var name = attendee.Name;
                var email = attendee.Email;
                var status = attendee.AttendeeStatus;
                section.Add (new AttendeeElement (name, email, status));
            }
            root.Add (section);

            var dynamic = new DialogViewController (root, true);
            NavigationController.PushViewController (dynamic, true);
        }

        /// <summary>
        /// Extract values from dialog.root into 'c'.
        /// </summary>
        protected IICalendar ExtractDialogValues ()
        {
            c.Subject = subjectEntryElement.Value;
            c.AllDayEvent = appointmentEntryElement.allDayEvent;
            c.StartTime = appointmentEntryElement.startDateTime;
            c.EndTime = appointmentEntryElement.endDateTime;
            // c.attendees is already set via PullAttendees
            c.Location = locationEntryElement.Value;
            var reminderSection = reminderEntryElement [0] as ReminderSection;
            var hiddenElement = reminderSection [0] as HiddenElement;
            c.Reminder = hiddenElement.Value;
            folder = calendars.GetFolder (calendarEntryElement.RadioSelected);
            // Extras
            c.OrganizerName = Pretty.DisplayNameForAccount (account);
            c.OrganizerEmail = account.EmailAddr;
            c.AccountId = account.Id;
            c.DtStamp = DateTime.Now;
            // IICalendar
            var iCal = iCalendarFromMcCalendar (c);
            if (String.IsNullOrEmpty (c.UID)) {
                // Note - this only works becasue DDay makes UID as a dashed GUID.
                c.UID = iCal.Events [0].UID.Replace ("-", null).ToUpper ();
                iCal.Events [0].UID = c.UID;
            } else {
                iCal.Events [0].UID = c.UID;
            }
            return iCal;
        }

        protected IICalendar iCalendarFromMcCalendar (McCalendar c)
        {
            var iCal = new iCalendar ();
            var evt = iCal.Create<DDay.iCal.Event> ();
            evt.Summary = c.Subject;
            evt.Start = new iCalDateTime (c.StartTime);
            evt.End = new iCalDateTime (c.EndTime);
            evt.IsAllDay = c.AllDayEvent;
            evt.Location = c.Location;
            evt.Organizer = new Organizer (account.EmailAddr);
            foreach (var a in c.attendees) {
                var iAttendee = new Attendee ("mailto:" + a.Email);
                iAttendee.CommonName = a.Name;
                evt.Attendees.Add (iAttendee);
            }
            return iCal;
        }

        protected void SyncMeetingRequest ()
        {
            c.Insert ();
            folder.Link (c);
            // FIXME - Steve - Look - just jamming in default cal here.
            BackEnd.Instance.CreateCalCmd (account.Id, c.Id, McFolder.GetDefaultCalendarFolder (account.Id).Id);
        }

        /// <summary>
        /// Sends the message. Message (UID) must already exist in EAS.
        /// </summary>
        protected void SendInvites (IICalendar iCal)
        {
            var mimeMessage = new MimeMessage ();

            mimeMessage.From.Add (new MailboxAddress (Pretty.DisplayNameForAccount (account), account.EmailAddr));

            foreach (var a in c.attendees) {
                mimeMessage.To.Add (new MailboxAddress (a.Name, a.Email));
            }
            if (null != c.Subject) {
                mimeMessage.Subject = c.Subject;
            }
            mimeMessage.Date = System.DateTime.UtcNow;

            var body = new TextPart ("calendar");
            // TODO: REQUEST is coming out quoted; is that ok? (see KLUDGE)
            body.ContentType.Parameters.Add (new Parameter ("method", "REQUEST"));
            // TODO: Do we really need to add name parameter, like AS doc shows?
            body.ContentType.Parameters.Add (new Parameter ("name", "meeting.ics"));

            // TODO: Smarter about character encoding
            using (var iCalStream = new MemoryStream ()) {
                iCalendarSerializer serializer = new iCalendarSerializer ();
                serializer.Serialize (iCal, iCalStream, System.Text.Encoding.ASCII);
                iCalStream.Seek (0, SeekOrigin.Begin);
                using (var textStream = new StreamReader (iCalStream)) {
                    body.Text = textStream.ReadToEnd ();
                }
            }

            body.ContentTransferEncoding = ContentEncoding.EightBit;

            // TODO: Do we really need multipart?
            var msg = new Multipart ("alternative",
                          new TextPart ("plain", "Calendar item"),
                          body
                      );

            mimeMessage.Body = msg;

            MimeHelpers.SendEmail (account.Id, mimeMessage, c.Id);
        }

        protected string MyCalendarName (McCalendar c)
        {
            var candidates = McFolder.QueryByFolderEntryId<McCalendar> (account.Id, c.Id);
            return candidates.First ().DisplayName;
        }

        protected void UpdateStatus (NcResponseType status)
        {
            // FIXME BackEnd.Instance.RespondCalCmd (account.Id, c.Id, status);
        }
    }
}
