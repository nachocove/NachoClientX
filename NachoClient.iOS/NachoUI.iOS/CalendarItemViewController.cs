// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using MimeKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using SWRevealViewControllerBinding;

namespace NachoClient.iOS
{
    public partial class CalendarItemViewController : NcDialogViewController, INachoCalendarItemEditor
    {
        protected INachoCalendarItemEditorParent owner;
        protected CalendarItemEditorAction action;
        protected McCalendar item;
        protected McCalendar c;
        protected McFolder folder;
        protected McAccount account;
        protected NachoFolders calendars;
        public bool showMenu;

        public CalendarItemViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// Interface INachoCalendarItemEditor
        /// </summary>
        /// <param name="owner">Owner.</param>
        public void SetOwner (INachoCalendarItemEditorParent owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Interface INachoCalendarItemEditor
        /// </summary>
        /// <param name="item">Item.</param>
        /// <param name="action">Action.</param>
        public void SetCalendarItem (McCalendar item, CalendarItemEditorAction action)
        {
            this.item = item;
            this.action = action;
        }

        /// <summary>
        /// Interface INachoCalendarItemEditor
        /// </summary>
        /// <param name="animated">If set to <c>true</c> animated.</param>
        /// <param name="action">Action.</param>
        public void DismissCalendarItemEditor (bool animated, NSAction action)
        {
            owner = null;
            NavigationController.PopViewControllerAnimated (true);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            if (showMenu) {
                // Navigation
                revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
                revealButton.Target = this.RevealViewController ();
                this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);
                nachoButton.Clicked += (object sender, EventArgs e) => {
                    PerformSegue ("CalendarItemToNachoNow", this);
                };
                NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            }

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
                        ExtractDialogValues ();
                        SyncMeetingRequest ();
                        SendInvites ();
                        ReloadRoot (ShowDetail ());
                    }
                };
                alert.Show ();
            };

            editButton.Clicked += (object sender, EventArgs e) => {
                ReloadRoot (EditDetail ());
            };

            cancelButton.Clicked += (object sender, EventArgs e) => {
                if (CalendarItemEditorAction.create == action) {
                    NavigationController.PopViewControllerAnimated (true);
                    return;
                }
                if (CalendarItemEditorAction.edit == action || CalendarItemEditorAction.view == action) {
                    c = item;
                    ReloadRoot (ShowDetail ());
                    return;
                }
                NcAssert.CaseError ();
            };

            // TODO: Need account manager.
            // We only have one account, for now.
            account = NcModel.Instance.Db.Table<McAccount> ().First ();
            calendars = new NachoFolders (NachoFolders.FilterForCalendars);

            // Set up view
            Pushing = true;

            switch (action) {
            case CalendarItemEditorAction.create:
                c = CalendarHelper.DefaultMeeting ();
                Root = EditDetail ();
                break;
            case CalendarItemEditorAction.edit:
                c = item;
                Root = EditDetail ();
                break;
            case CalendarItemEditorAction.view:
                c = item;
                Root = ShowDetail ();
                break;
            default:
                NcAssert.CaseError ();
                break;
            }


            TableView.SeparatorColor = UIColor.Clear;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
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
                    c.attendees = dc.GetAttendeeList ();
                };
            }
        }

        /// <summary>
        /// Shows the calendar, read-only.
        /// </summary>
        protected RootElement ShowDetail ()
        {
            NavigationItem.LeftBarButtonItem = null;
            NavigationItem.RightBarButtonItem = editButton;

            var root = new RootElement (c.Subject);
            root.UnevenRows = true;

            Section section = null;

            section = new ThinSection ();
            section.Add (new SubjectElement (Pretty.SubjectString (c.Subject)));
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
                
            // TODO: Give section an icon
            RenderBodyIfAvailable (root);

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

        void RenderBodyIfAvailable (RootElement root)
        {
            if (0 == c.BodyId) {
                return;
            }

            // FIXME: Make sure the body is mime

            MimeMessage mimeMsg;
            try {
                string body = c.GetBody ();
                if (null == body) {
                    return;
                }
                using (var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (body))) {
                    var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                    mimeMsg = bodyParser.ParseMessage ();
                    MimeHelpers.DumpMessage (mimeMsg, 0);
                    var list = new List<MimeEntity> ();
                    MimeHelpers.MimeDisplayList (mimeMsg, ref list);
                    RenderDisplayList (list, root);
                }
            } catch (Exception e) {
                // TODO: Find root cause
                NachoCore.Utils.Log.Error (Log.LOG_UI, "CalendarItemView exception ignored:\n{0}", e);
                return;
            }
        }

        protected void RenderDisplayList (List<MimeEntity> list, RootElement root)
        {
            for (var i = 0; i < list.Count; i++) {
                var entity = list [i];
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
//                    RenderHtml (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "calendar")) {
//                    RenderCalendar (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "*")) {
//                    RenderText (part);
                    continue;
                }
                if (part.ContentType.Matches ("image", "*")) {
//                    RenderImage (part);
                    continue;
                }
                if (part.ContentType.Matches ("application", "ms-tnef")) {
                    // Gets the decoded text content.
                    var decodedStream = new MemoryStream ();
                    part.ContentObject.DecodeTo (decodedStream);
                    decodedStream.Seek (0L, SeekOrigin.Begin);
                    var tnef = new TnefEncoding (decodedStream.ToArray ());
                    var nsError = new NSError ();
                    var nsAttributes = new NSAttributedStringDocumentAttributes ();
                    nsAttributes.DocumentType = NSDocumentType.RTF;
                    var attributedString = new NSAttributedString (tnef.Body, nsAttributes, ref nsError);
                    var tv = new UITextView (new RectangleF (0, 0, 320, 1));
                    tv.AttributedText = attributedString;
                    tv.AutoresizingMask = UIViewAutoresizing.FlexibleBottomMargin;
                    tv.UserInteractionEnabled = false;
                    tv.SizeToFit ();
                    var e = new UIViewElement ("", tv, true);
                    var s = new ThinSection ();
                    s.Add (e);
                    root.Add (s);
                    continue;
                }
            }
        }

        EntryElementWithIcon subjectEntryElement;
        AppointmentEntryElement appointmentEntryElement;
        RootElementWithIcon reminderEntryElement;
        PeopleEntryElement peopleEntryElement;
        EntryElementWithIcon locationEntryElement;
        RootElementWithIcon calendarEntryElement;
        RootElementWithIcon timezoneEntryElement;

        /// <summary>
        /// Edit the (possibly empty) calendar entry
        /// </summary>
        protected RootElement EditDetail ()
        {
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
            timezoneEntryElement = TimeZonePopup ();
            section.Add (timezoneEntryElement);
            root.Add (section);

            section = new ThinSection ();
            section.Add (calendarEntryElement);
            section.Add (reminderEntryElement);
            root.Add (section);

            return root;
        }

        protected RootElementWithIcon TimeZonePopup ()
        {
            var root = new RootElementWithIcon ("ic_action_map", "Timezone", new RadioGroup (0));
            var section = new Section ("Timezones");
            root.Add (section);

            var l = TimeZoneInfo.Local;
            section.Add (new RadioElementWithData (l.StandardName, l.Id));
            ReadOnlyCollection<TimeZoneInfo> timeZones = TimeZoneInfo.GetSystemTimeZones (); 
            foreach (TimeZoneInfo timeZone in timeZones) {
                var e = new RadioElementWithData (timeZone.DisplayName, timeZone.Id);
                section.Add (e);
            }
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
        protected void ExtractDialogValues ()
        {
            c.Subject = subjectEntryElement.Value;
            c.AllDayEvent = appointmentEntryElement.allDayEvent;
            c.StartTime = appointmentEntryElement.startDateTime.ToUniversalTime ();
            c.EndTime = appointmentEntryElement.endDateTime.ToUniversalTime ();
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
            c.DtStamp = DateTime.UtcNow;
            if (0 == c.attendees.Count) {
                c.MeetingStatusIsSet = true;
                c.MeetingStatus = NcMeetingStatus.Appointment;
                c.ResponseRequested = false;
                c.ResponseRequestedIsSet = true;
            } else {
                c.MeetingStatusIsSet = true;
                c.MeetingStatus = NcMeetingStatus.Meeting;
                c.ResponseRequested = true;
                c.ResponseRequestedIsSet = true;
            }
            // Timezone
            var tzid = RadioElementWithData.SelectedData (timezoneEntryElement);
            var tzi = TimeZoneInfo.FindSystemTimeZoneById (tzid);
            var tz = new AsTimeZone (tzi);
            c.TimeZone = tz.toEncodedTimeZone ();
            if (String.IsNullOrEmpty (c.UID)) {
                c.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpper ();
            }
        }

        protected void SyncMeetingRequest ()
        {
            // TODO: If calendar changes folders
            if (0 == c.Id) {
                c.Insert (); // new entry
                folder.Link (c);
                BackEnd.Instance.CreateCalCmd (account.Id, c.Id, folder.Id);
            } else {
                c.Update ();
                BackEnd.Instance.UpdateCalCmd (account.Id, c.Id);
            }

        }

        /// <summary>
        /// Sends the message. Message (UID) must already exist in EAS.
        /// </summary>
        protected void SendInvites ()
        {
            var tzid = RadioElementWithData.SelectedData (timezoneEntryElement);

            CalendarHelper.SendInvites (account, c, tzid);
        }

        protected string MyCalendarName (McCalendar c)
        {
            var candidates = McFolder.QueryByFolderEntryId<McCalendar> (account.Id, c.Id);
            if ((null == candidates) || (0 == candidates.Count)) {
                return "None";
            } else {
                return candidates.First ().DisplayName;
            }
        }

        protected void UpdateStatus (NcResponseType status)
        {
            // FIXME BackEnd.Instance.RespondCalCmd (account.Id, c.Id, status);
        }
    }
}
