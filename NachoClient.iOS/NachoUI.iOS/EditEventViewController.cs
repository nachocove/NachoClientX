// The editor for calendar events.
//
// Copyright 2014, 2015 Nacho Cove, Inc. All rights reserved.

using System;

using Foundation;
using UIKit;

using System.IO;
using CoreGraphics;
using System.Collections.Generic;
using MimeKit;

using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using NachoCore;
using NachoCore.ActiveSync;

namespace NachoClient.iOS
{
    public interface IEditEventViewOwner
    {
        /// <summary>
        /// Called when the event being edited was deleted.  The owner should dismiss the event
        /// editor view, and may also dismiss its own view.
        /// </summary>
        void EventWasDeleted (EditEventViewController vc);
    }

    public partial class EditEventViewController
        : NcUIViewControllerNoLeaks, INachoAttendeeListChooserDelegate, IUcAttachmentBlockDelegate, INachoFileChooserParent
    {
        protected CalendarItemEditorAction action;
        protected McCalendar item;
        protected McCalendar c;
        protected DateTime startingDate;
        protected McAccount account;
        protected string TempPhone = "";
        protected McFolder calendarFolder;

        IEditEventViewOwner owner;

        UIView contentView;
        UIScrollView scrollView;

        UITextField titleField;
        UITextView descriptionTextView;
        UILabel descriptionPlaceHolder;

        UIBarButtonItem doneButton;
        UIBarButtonItem cancelButton;

        UIView titleView;
        UIView descriptionView;

        UIView allDayView;
        UISwitch allDaySwitch;
        UIView startView;
        UIDatePicker startDatePicker;
        UIView endView;
        UIDatePicker endDatePicker;

        UIView locationView;
        UITextField locationField;
        UcAttachmentBlock attachmentView;
        UIView attachmentBGView;
        UIView peopleView;

        UIView alertsView;

        UIView calendarView;

        UIView deleteView;

        DateTime startDate;
        DateTime endDate;

        UILabel startDateLabel;
        UILabel endDateLabel;

        protected UITapGestureRecognizer backgroundTapGesture;
        protected UITapGestureRecognizer.Token backgroundTapGestureToken;
        protected UITapGestureRecognizer startDatePickerTapGesture;
        protected UITapGestureRecognizer.Token startDatePickerTapGestureToken;
        protected UITapGestureRecognizer endDatePickerTapGesture;
        protected UITapGestureRecognizer.Token endDatePickerTapGestureToken;
        protected UITapGestureRecognizer peopleTapGesture;
        protected UITapGestureRecognizer.Token peopleTapGestureToken;
        protected UITapGestureRecognizer alertTapGesture;
        protected UITapGestureRecognizer.Token alertTapGestureToken;
        protected UITapGestureRecognizer calendarTapGesture;
        protected UITapGestureRecognizer.Token calendarTapGestureToken;
        protected UITapGestureRecognizer deleteTapGesture;
        protected UITapGestureRecognizer.Token deleteTapGestureToken;

        UIColor separatorColor = A.Color_NachoBorderGray;
        protected static nfloat SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected int LINE_OFFSET = 30;
        protected int CELL_HEIGHT = 44;
        protected int PICKER_HEIGHT = 216;
        protected nfloat TEXT_LINE_HEIGHT = 19.124f;
        protected nfloat DESCRIPTION_OFFSET = 0f;
        protected nfloat DELETE_BUTTON_OFFSET = 0f;
        protected UIFont labelFont = A.Font_AvenirNextMedium14;

        protected int currentStartPickerHeight = 0;
        protected int currentEndPickerHeight = 0;

        protected bool startDateOpen = false;
        protected bool endDateOpen = false;
        protected bool endChanged = false;
        protected bool eventEditStarted = false;
        protected bool attachmentsInitialized = false;
        protected bool timesAreSet = true;
        protected bool descriptionWasEdited = false;
        protected bool suppressLayout = false;
        protected bool isSimpleEvent = false;

        protected bool calendarItemIsMissing = false;
        protected bool noCalendarAccess = false;
        protected bool noDeviceCalendar = false;

        protected event Action OpenKeyboardAction;

        protected UIView line1;
        protected UIView line2;
        protected UIView line3;
        protected UIView line4;
        protected UIView line5;
        protected UIView line6;
        protected UIView line7;
        protected UIView line8;
        protected UIView line9;
        // line10 was used for the phone number, which was removed from the view
        protected UIView line11;
        protected UIView line12;
        protected UIView line13;
        protected UIView line14;
        protected UIView line15;
        protected UIView line16;
        protected UIView separator3;
        protected UIView separator4;
        protected UIView strikethrough;
        protected UIView endDivider;
        protected UIView startDivider;
        protected UIColor solidTextColor = A.Color_NachoDarkText;

        const int PEOPLE_DETAIL_TAG = 206;
        const int ALERT_DETAIL_TAG = 207;
        const int CAL_DETAIL_TAG = 210;

        protected UIColor CELL_COMPONENT_BG_COLOR = UIColor.White;

        public EditEventViewController () : base ()
        {
        }

        public EditEventViewController (IntPtr handle)
            : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            switch (action) {

            case CalendarItemEditorAction.create:
                if (null == item) {
                    if (0001 != startingDate.Year) {
                        c = CalendarHelper.DefaultMeeting (startingDate);
                    } else {
                        c = CalendarHelper.DefaultMeeting ();
                    }
                } else {
                    c = item;
                }
                break;

            case CalendarItemEditorAction.edit:
                if (null == item) {
                    calendarItemIsMissing = true;
                    // Create a dummy item so the UI elements can be created and the view can finish
                    // loading before we can throw up the dialog to tell the user about the problem.
                    c = CalendarHelper.DefaultMeeting ();
                } else {
                    c = item;
                }
                if (c.AllDayEvent) {
                    timesAreSet = false;
                }
                break;

            default:
                NcAssert.CaseError ();
                break;
            }

            scrollView = new UIScrollView (View.Bounds);
            contentView = new UIView (scrollView.Bounds);
            scrollView.AddSubview (contentView);
            View.AddSubview (scrollView);

            base.ViewDidLoad ();
        }

        public void SetOwner (IEditEventViewOwner owner)
        {
            this.owner = owner;
        }

        public void SetCalendarEvent (McEvent e, CalendarItemEditorAction action)
        {
            if (null == e) {
                this.item = null;
            } else {
                this.item = McCalendar.QueryById<McCalendar> (e.CalendarId);
                calendarFolder = GetCalendarFolderForItem ();
            }
            this.action = action;
            SetAccount ();
        }

        public void SetCalendarItem (McCalendar c)
        {
            this.item = c;
            if (null == c || 0 == c.Id) {
                this.action = CalendarItemEditorAction.create;
            } else {
                this.action = CalendarItemEditorAction.edit;
                calendarFolder = GetCalendarFolderForItem ();
            }
            SetAccount ();
        }

        private void SetAccount ()
        {
            if (null != item && CalendarItemEditorAction.edit == action) {
                account = McAccount.QueryById<McAccount> (item.AccountId);
            } else {
                account = NcApplication.Instance.DefaultCalendarAccount;
            }
            var accountCalendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
            if (!account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) || 0 == accountCalendars.Count ()) {
                Log.Info (Log.LOG_CALENDAR, "The current account does not support writing to calendars. Using the device account instead.");
                account = McAccount.GetDeviceAccount ();
                if (!Calendars.Instance.AuthorizationStatus || 0 == new NachoFolders (account.Id, NachoFolders.FilterForCalendars).Count ()) {
                    Log.Info (Log.LOG_CALENDAR, "The device calendar is inaccessible or doesn't have any calendars. Looking for another account to use for the new calendar item.");
                    bool foundWritableCalendar = false;
                    foreach (var candidateAccountId in McAccount.GetAllConfiguredNormalAccountIds ()) {
                        var candidateAccount = McAccount.QueryById<McAccount> (candidateAccountId);
                        if (candidateAccount.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) && 0 < new NachoFolders (candidateAccountId, NachoFolders.FilterForCalendars).Count ()) {
                            foundWritableCalendar = true;
                            account = candidateAccount;
                            break;
                        }
                    }
                    if (!foundWritableCalendar) {
                        if (Calendars.Instance.AuthorizationStatus) {
                            noDeviceCalendar = true;
                        } else {
                            noCalendarAccess = true;
                        }
                    }
                }
            }

            // There are additional restrictions on the event when it is on the device calendar.
            if (McAccount.AccountTypeEnum.Device == account.AccountType) {
                isSimpleEvent = true;
                if (CalendarItemEditorAction.create == action && null != item) {
                    // The Create Event gesture on an email message will create a meeting with attendees.
                    // But the app does not support creating meetings on the device calendar.  Remove any
                    // attendees from the calendar item.
                    item.attendees = new List<McAttendee> ();
                }
            }
        }

        private void ChangeToAccount (int newAccountId) {
            account = McAccount.QueryById<McAccount> (newAccountId);
            isSimpleEvent = McAccount.AccountTypeEnum.Device == account.AccountType;
            if (isSimpleEvent) {
                // Calendar items in the device calendar don't support attendees or attachments.
                c.attendees = new List<McAttendee> ();
            }
        }

        public void SetStartingDate (DateTime startingDate)
        {
            this.startingDate = startingDate;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.DidShowNotification);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.DidShowNotification, DidShowKeyboard);

            if (calendarItemIsMissing) {
                NcAlertView.Show (this, "Event Deleted", "The event can't be edited because it was deleted.",
                    new NcAlertAction ("OK", NcAlertActionStyle.Cancel, () => {
                        DismissView ();
                    }));
            }

            if (noCalendarAccess) {
                NcAlertView.Show (this, "No Calendar Access",
                    "The app doesn't have access to the device's calendar. To create or update events in the device calendar, use the Settings app to grant Nacho Mail access to the calendar.",
                    new NcAlertAction ("OK", NcAlertActionStyle.Cancel, () => {
                        DismissView ();
                    }));
            }

            if (noDeviceCalendar) {
                NcAlertView.Show (this, "No device calendars",
                    "The Calendar app does not have any accessible calendars.",
                    new NcAlertAction ("OK", NcAlertActionStyle.Cancel, () => {
                        DismissView ();
                    }));
            }
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return true;
            }
        }

        protected override void OnKeyboardChanged ()
        {
            LayoutView ();
        }

        private void DidShowKeyboard (NSNotification notification)
        {
            if (null != OpenKeyboardAction) {
                OpenKeyboardAction ();
            }
        }

        void ShowAttendees ()
        {
            var dc = new EventAttendeeViewController ();
            ExtractValues ();
            dc.Setup (this, account, c.attendees, c, editing: true,
                organizer: CalendarHelper.IsOrganizer (c.OrganizerEmail, account.EmailAddr), recurring: false);
            NavigationController.PushViewController (dc, true);
        }

        void ShowAlert ()
        {
            var dc = new AlertChooserViewController ();
            dc.SetReminder (c.ReminderIsSet, c.Reminder);
            ExtractValues ();
            dc.ViewDisappearing += (object s, EventArgs e) => {
                uint reminder;
                c.ReminderIsSet = dc.GetReminder (out reminder);
                if (c.ReminderIsSet) {
                    c.Reminder = reminder;
                }
            };
            NavigationController.PushViewController (dc, true);
        }

        void ShowCalendarChooser ()
        {
            var dc = new ChooseCalendarViewController();
            ExtractValues ();
            dc.SetCalendars (GetChoosableCalendars (), calendarFolder);
            dc.ViewDisappearing += (object s, EventArgs e) => {
                var newCalendar = dc.GetSelectedCalendar ();
                if (null != newCalendar) {
                    if (newCalendar.AccountId != calendarFolder.AccountId) {
                        ChangeToAccount (newCalendar.AccountId);
                    }
                    calendarFolder = newCalendar;
                }
            };
            NavigationController.PushViewController (dc, true);
        }

        protected override void CreateViewHierarchy ()
        {
            backgroundTapGesture = new UITapGestureRecognizer ();
            backgroundTapGestureToken = backgroundTapGesture.AddTarget (DismissKeyboard);
            contentView.AddGestureRecognizer (backgroundTapGesture);

            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            scrollView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;

            doneButton = new NcUIBarButtonItem ();
            cancelButton = new NcUIBarButtonItem ();

            doneButton.Title = (CalendarItemEditorAction.create == action ? "Send" : "Done");
            doneButton.AccessibilityLabel = "Done";

            Util.SetAutomaticImageForButton (cancelButton, "icn-close");
            cancelButton.AccessibilityLabel = "Close";

            cancelButton.Clicked += CancelButtonClicked;

            doneButton.Clicked += DoneButtonClicked;

            NavigationItem.LeftBarButtonItem = cancelButton;
            NavigationItem.RightBarButtonItem = doneButton;

            //Title
            titleView = new UIView (new CGRect (0, 30, SCREEN_WIDTH, CELL_HEIGHT));
            titleView.BackgroundColor = UIColor.White;

            titleField = new UITextField (new CGRect (15, 12.438f, SCREEN_WIDTH - 30, TEXT_LINE_HEIGHT));
            titleField.AccessibilityLabel = "Title";
            titleField.Font = labelFont;
            titleField.TextColor = solidTextColor;
            titleField.Placeholder = "Title";
            titleField.ClearButtonMode = UITextFieldViewMode.Always;
            titleView.AddSubview (titleField);

            titleField.ShouldReturn += TextFieldResignFirstResponder;

            titleField.EditingDidBegin += EditingDidBegin;

            //Description
            descriptionView = new UIView (new CGRect (0, 74, SCREEN_WIDTH, CELL_HEIGHT));
            descriptionView.BackgroundColor = UIColor.White;

            descriptionPlaceHolder = new UILabel (new CGRect (15, 12.438f, SCREEN_WIDTH - 30, TEXT_LINE_HEIGHT));
            descriptionPlaceHolder.Text = "Description";
            descriptionPlaceHolder.Font = labelFont;
            descriptionPlaceHolder.TextColor = new UIColor (.8f, .8f, .8f, 1f);

            descriptionTextView = new NcTextView (new CGRect (15, 12.438f, SCREEN_WIDTH - 30, TEXT_LINE_HEIGHT));
            descriptionTextView.Font = labelFont;
            descriptionTextView.TextColor = solidTextColor;
            descriptionTextView.BackgroundColor = UIColor.Clear;
            descriptionTextView.SelectedRange = new NSRange (0, 0);
            descriptionTextView.ContentInset = new UIEdgeInsets (-7, -4, 0, 0);
            descriptionTextView.ScrollEnabled = false;

            descriptionTextView.Changed += DescriptionTextViewChanged;
            descriptionTextView.SelectionChanged += DescriptionTextViewSelectionChanged;
            descriptionTextView.Ended += DescriptionTextViewEnded;
            descriptionView.AddSubview (descriptionTextView);
            descriptionView.AddSubview (descriptionPlaceHolder);

            // If a new event is being created, mark the description as having been edited so that the
            // new event will always have a body, even if the user leaves the description field blank.
            descriptionWasEdited = CalendarItemEditorAction.create == action;

            //All Day Event
            allDayView = new UIView (new CGRect (0, (LINE_OFFSET * 2) + (CELL_HEIGHT * 2), SCREEN_WIDTH, CELL_HEIGHT));
            allDayView.BackgroundColor = UIColor.White;
            UILabel allDayLabel = new UILabel (new CGRect (15, 12.438f, 50, TEXT_LINE_HEIGHT));
            allDayLabel.Text = "All Day";
            allDayLabel.Font = labelFont;
            allDayLabel.TextColor = solidTextColor;
            allDayView.AddSubview (allDayLabel);

            allDaySwitch = new UISwitch ();
            allDaySwitch.AccessibilityLabel = "All day";
            allDaySwitch.SizeToFit ();
            allDaySwitch.OnTintColor = A.Color_NachoTeal;
            allDaySwitch.HorizontalAlignment = UIControlContentHorizontalAlignment.Right;
            allDaySwitch.Frame = new CGRect (SCREEN_WIDTH - allDaySwitch.Frame.Width - 15, 6.5f, allDaySwitch.Frame.Width, TEXT_LINE_HEIGHT);
            allDayView.AddSubview (allDaySwitch);

            //Start Time
            startView = new UIView (new CGRect (0, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3), SCREEN_WIDTH, CELL_HEIGHT));
            startView.BackgroundColor = UIColor.White;
            UILabel startLabel = new UILabel (new CGRect (15, 12.438f, 40, TEXT_LINE_HEIGHT));
            startLabel.Text = "Starts";
            startLabel.Font = labelFont;
            startLabel.TextColor = solidTextColor;
            startView.AddSubview (startLabel);

            startDateLabel = new UILabel ();
            startDateLabel.SizeToFit ();
            startDateLabel.TextAlignment = UITextAlignment.Right;
            startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            startDateLabel.Font = labelFont;
            startDateLabel.TextColor = A.Color_808080;
            startView.AddSubview (startDateLabel);

            startDatePicker = null;

            startDivider = Util.AddHorizontalLine (15, CELL_HEIGHT, SCREEN_WIDTH, separatorColor);
            startDivider.Hidden = true;
            startView.AddSubview (startDivider);

            startDatePickerTapGesture = new UITapGestureRecognizer ();
            startDatePickerTapGestureToken = startDatePickerTapGesture.AddTarget (StartDatePickerTapAction);
            startView.AddGestureRecognizer (startDatePickerTapGesture);

            //End Time
            endView = new UIView (new CGRect (0, (LINE_OFFSET * 2) + (CELL_HEIGHT * 4), SCREEN_WIDTH, CELL_HEIGHT));
            endView.BackgroundColor = UIColor.White;
            UILabel endLabel = new UILabel (new CGRect (15, 12.438f, 30, TEXT_LINE_HEIGHT));
            endLabel.Text = "Until";
            endLabel.Font = labelFont;
            endLabel.TextColor = solidTextColor;
            endView.AddSubview (endLabel);

            endDateLabel = new UILabel ();
            endDateLabel.SizeToFit ();
            endDateLabel.TextAlignment = UITextAlignment.Right;
            endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            endDateLabel.Font = labelFont;
            endDateLabel.TextColor = A.Color_808080;
            endView.AddSubview (endDateLabel);

            endDatePicker = null;

            endDivider = Util.AddHorizontalLine (15, CELL_HEIGHT, SCREEN_WIDTH, separatorColor);
            startDivider.Hidden = true;
            endView.AddSubview (endDivider);

            endDatePickerTapGesture = new UITapGestureRecognizer ();
            endDatePickerTapGestureToken = endDatePickerTapGesture.AddTarget (EndDatePickerTapAction);
            endView.AddGestureRecognizer (endDatePickerTapGesture);

            allDaySwitch.ValueChanged += AllDaySwitchValueChanged;
            strikethrough = Util.AddHorizontalLine (0, CELL_HEIGHT / 2, SCREEN_WIDTH, A.Color_NachoRed);
            strikethrough.Hidden = true;
            endView.AddSubview (strikethrough);

            //Location
            locationView = new UIView (new CGRect (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 5), SCREEN_WIDTH, CELL_HEIGHT));
            locationView.BackgroundColor = UIColor.White;

            locationField = new UITextField (new CGRect (15, 12.438f, SCREEN_WIDTH - 24, TEXT_LINE_HEIGHT));
            locationField.AccessibilityLabel = "Location";
            locationField.Font = labelFont;
            locationField.TextColor = solidTextColor;
            locationField.ClearButtonMode = UITextFieldViewMode.Always;
            locationField.Placeholder = "Location";
            locationField.EditingDidBegin += LocationEditingDidBegin;
            locationField.EditingDidEnd += LocationEditingDidEnd;
            locationField.ShouldReturn += TextFieldResignFirstResponder;
            locationView.AddSubview (locationField);

            //Attachments
            attachmentView = new UcAttachmentBlock (this, 44, true);
            attachmentView.Frame = new CGRect (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 6), SCREEN_WIDTH, CELL_HEIGHT);

            attachmentBGView = new UIView (new CGRect (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 6), SCREEN_WIDTH, CELL_HEIGHT * 2));
            attachmentBGView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            //People
            peopleView = new UIView (new CGRect (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 7), SCREEN_WIDTH, CELL_HEIGHT));
            peopleView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            UILabel peopleLabel = new UILabel (new CGRect (15, 12.438f, 200, TEXT_LINE_HEIGHT));
            peopleLabel.Text = "Attendees (0)";
            peopleLabel.Tag = PEOPLE_DETAIL_TAG;
            peopleLabel.Font = labelFont;
            peopleLabel.TextColor = solidTextColor;
            peopleView.AddSubview (peopleLabel);

            Util.AddArrowAccessory (SCREEN_WIDTH - 15 - 12, CELL_HEIGHT / 2 - 6, 12, peopleView);

            peopleTapGesture = new UITapGestureRecognizer ();
            peopleTapGestureToken = peopleTapGesture.AddTarget (PeopleTapAction);
            peopleView.AddGestureRecognizer (peopleTapGesture);

            //Alerts
            alertsView = new UIView (new CGRect (0, (LINE_OFFSET * 4) + (CELL_HEIGHT * 8), SCREEN_WIDTH, CELL_HEIGHT));
            alertsView.BackgroundColor = UIColor.White;

            Util.AddArrowAccessory (SCREEN_WIDTH - 15 - 12, CELL_HEIGHT / 2 - 6, 12, alertsView);

            UILabel alertsLabel = new UILabel (new CGRect (15, 12.438f, 70, TEXT_LINE_HEIGHT));
            alertsLabel.Text = "Reminder";
            alertsLabel.Font = labelFont;
            alertsLabel.TextColor = solidTextColor;
            alertsView.AddSubview (alertsLabel);

            UILabel alertsDetailLabel = new UILabel ();
            alertsDetailLabel.Text = "None";
            alertsDetailLabel.Tag = ALERT_DETAIL_TAG;
            alertsDetailLabel.SizeToFit ();
            alertsDetailLabel.TextAlignment = UITextAlignment.Right;
            alertsDetailLabel.Frame = new CGRect (SCREEN_WIDTH - alertsDetailLabel.Frame.Width - 34, 12.438f, alertsDetailLabel.Frame.Width, TEXT_LINE_HEIGHT);
            alertsDetailLabel.Font = labelFont;
            alertsDetailLabel.TextColor = A.Color_808080;
            alertsView.AddSubview (alertsDetailLabel);

            alertTapGesture = new UITapGestureRecognizer ();
            alertTapGestureToken = alertTapGesture.AddTarget (AlertTapAction);
            alertsView.AddGestureRecognizer (alertTapGesture);

            //Calendar
            calendarView = new UIView (new CGRect (0, (LINE_OFFSET * 5) + (CELL_HEIGHT * 9), SCREEN_WIDTH, CELL_HEIGHT));
            calendarView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            UILabel calendarLabel = new UILabel (new CGRect (15, 12.438f, 70, TEXT_LINE_HEIGHT));
            calendarLabel.Text = "Calendar";
            calendarLabel.Font = labelFont;
            calendarLabel.TextColor = solidTextColor;
            calendarView.AddSubview (calendarLabel);

            Util.AddArrowAccessory (SCREEN_WIDTH - 15 - 12, CELL_HEIGHT / 2 - 6, 12, calendarView);

            UILabel calendarDetailLabel = new UILabel ();
            calendarDetailLabel.Tag = CAL_DETAIL_TAG;
            calendarDetailLabel.TextAlignment = UITextAlignment.Right;
            calendarDetailLabel.Font = labelFont;
            calendarDetailLabel.TextColor = A.Color_808080;
            calendarView.AddSubview (calendarDetailLabel);

            calendarTapGesture = new UITapGestureRecognizer ();
            calendarTapGestureToken = calendarTapGesture.AddTarget (CalendarTapAction);
            calendarView.AddGestureRecognizer (calendarTapGesture);

            deleteView = new UIView (new CGRect (0, (LINE_OFFSET * 6) + (CELL_HEIGHT * 10), View.Frame.Width, CELL_HEIGHT));
            deleteView.Layer.BorderColor = separatorColor.CGColor;
            deleteView.Layer.BorderWidth = .5f;
            deleteView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            UILabel deleteLabel = new UILabel (new CGRect (25 + 24, 12.438f, 120, TEXT_LINE_HEIGHT));
            deleteLabel.Text = "Delete Event";
            deleteLabel.Font = labelFont;
            deleteLabel.TextColor = solidTextColor;
            deleteView.AddSubview (deleteLabel);

            UIImageView deleteIcon = new UIImageView (new CGRect (15, (CELL_HEIGHT / 2) - 12, 24, 24));
            deleteIcon.Image = UIImage.FromBundle ("email-delete-two");
            deleteView.AddSubview (deleteIcon);

            deleteTapGesture = new UITapGestureRecognizer ();
            deleteTapGestureToken = deleteTapGesture.AddTarget (DeleteTapAction);
            deleteView.AddGestureRecognizer (deleteTapGesture);
            deleteView.Hidden = true;

            //Content View
            contentView.Frame = new CGRect (0, 0, SCREEN_WIDTH, (LINE_OFFSET * 9) + (CELL_HEIGHT * 11));
            contentView.AutoresizingMask = UIViewAutoresizing.None;
            contentView.BackgroundColor = A.Color_NachoNowBackground;
            contentView.AddSubviews (new UIView[] {
                titleView,
                descriptionView,
                allDayView,
                startView,
                endView,
                locationView,
                attachmentBGView,
                attachmentView,
                peopleView,
                alertsView,
                calendarView,
                deleteView
            }); 
            //LO
            line1 = Util.AddHorizontalLine (0, LINE_OFFSET, SCREEN_WIDTH, separatorColor);
            line2 = Util.AddHorizontalLine (15, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, separatorColor);
            line3 = Util.AddHorizontalLine (0, LINE_OFFSET + (CELL_HEIGHT * 2), SCREEN_WIDTH, separatorColor);
            //LO
            line4 = Util.AddHorizontalLine (0, (LINE_OFFSET * 2) + (CELL_HEIGHT * 2), SCREEN_WIDTH, separatorColor);
            line5 = Util.AddHorizontalLine (15, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3), SCREEN_WIDTH, separatorColor);
            line6 = Util.AddHorizontalLine (15, (LINE_OFFSET * 2) + (CELL_HEIGHT * 4), SCREEN_WIDTH, separatorColor);
            line7 = Util.AddHorizontalLine (0, (LINE_OFFSET * 2) + (CELL_HEIGHT * 5), SCREEN_WIDTH, separatorColor);
            //LO
            line8 = Util.AddHorizontalLine (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 5), SCREEN_WIDTH, separatorColor);
            line9 = Util.AddHorizontalLine (15, (LINE_OFFSET * 3) + (CELL_HEIGHT * 6), SCREEN_WIDTH, separatorColor);
            line11 = Util.AddHorizontalLine (15, (LINE_OFFSET * 3) + (CELL_HEIGHT * 7), SCREEN_WIDTH, separatorColor);
            line12 = Util.AddHorizontalLine (0, (LINE_OFFSET * 3) + (CELL_HEIGHT * 8), SCREEN_WIDTH, separatorColor);
            //LO
            line13 = Util.AddHorizontalLine (0, (LINE_OFFSET * 4) + (CELL_HEIGHT * 8), SCREEN_WIDTH, separatorColor);
            line14 = Util.AddHorizontalLine (0, (LINE_OFFSET * 4) + (CELL_HEIGHT * 9), SCREEN_WIDTH, separatorColor);
            //LO
            line15 = Util.AddHorizontalLine (0, (LINE_OFFSET * 5) + (CELL_HEIGHT * 9), SCREEN_WIDTH, separatorColor);
            line16 = Util.AddHorizontalLine (0, (LINE_OFFSET * 5) + (CELL_HEIGHT * 10), SCREEN_WIDTH, separatorColor);
            //LO

            // The two gray bars between fields that are below the start/end time fields need to be opaque
            // so that the date pickers won't be visible in those gaps during the expand/collapse animations
            // of the date pickers.  The other gray bars can be nothing, just letting the underlying contentView
            // show through.  Only these two gray bars need to be opaque.
            separator3 = new UIView (new CGRect (0, line7.Frame.Bottom, SCREEN_WIDTH, line8.Frame.Top - line7.Frame.Bottom));
            separator3.BackgroundColor = contentView.BackgroundColor;
            separator4 = new UIView (new CGRect (0, line12.Frame.Bottom, SCREEN_WIDTH, line13.Frame.Top - line12.Frame.Bottom));
            separator4.BackgroundColor = contentView.BackgroundColor;

            contentView.AddSubviews (new UIView[] {
                separator3, separator4,
                line1,
                line2,
                line3,
                line4,
                line5,
                line6,
                line7,
                line8,
                line9,
                line11,
                line12,
                line13,
                line14,
                line15,
                line16,
            }); 

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            if (action == CalendarItemEditorAction.edit) {
                DELETE_BUTTON_OFFSET = TEXT_LINE_HEIGHT + CELL_HEIGHT;
            }
            scrollView.ContentSize = new CGSize (SCREEN_WIDTH, (LINE_OFFSET * 8) + (CELL_HEIGHT * 10) + DELETE_BUTTON_OFFSET);
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
        }

        protected override void ConfigureAndLayout ()
        {
            suppressLayout = true;

            if (CalendarItemEditorAction.create == action) {
                NavigationItem.Title = "New Event";
                deleteView.Hidden = true;
            } else {
                NavigationItem.Title = "Edit Event";
                deleteView.Hidden = false;
            }

            // Title
            titleField.Text = c.Subject;

            // Description
            switch (c.DescriptionType) {
            case McAbstrFileDesc.BodyTypeEnum.None:
                // The event doesn't have a body
                descriptionTextView.Text = "";
                descriptionPlaceHolder.Hidden = false;
                break;
            case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                descriptionTextView.Text = c.Description;
                descriptionPlaceHolder.Hidden = !string.IsNullOrEmpty (c.Description);
                break;
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                descriptionTextView.Text = Util.ConvertToPlainText (c.Description, NSDocumentType.HTML);
                descriptionPlaceHolder.Hidden = descriptionTextView.HasText;
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                descriptionTextView.Text = Util.ConvertToPlainText (c.Description, NSDocumentType.RTF);
                descriptionPlaceHolder.Hidden = descriptionTextView.HasText;
                break;
            default:
                Log.Error (Log.LOG_CALENDAR, "Unexpected description type, {0}, for calendar item {1}", c.DescriptionType, c.Id);
                // Act as if it didn't have a body.
                descriptionTextView.Text = "";
                descriptionPlaceHolder.Hidden = false;
                break;
            }
            descriptionTextView.SizeToFit ();
            descriptionTextView.Frame = new CGRect (15, 12.438f, SCREEN_WIDTH - 30, descriptionTextView.Frame.Height);
            DESCRIPTION_OFFSET = descriptionTextView.Frame.Height;

            //all day event
            allDaySwitch.SetState (c.AllDayEvent, false);

            //start date
            if (c.AllDayEvent) {
                startDateLabel.Text = Pretty.MediumFullDate (c.StartTime);
            } else {
                startDateLabel.Text = Pretty.MediumFullDateTime (c.StartTime);
            }
            startDate = c.StartTime;
            startDateLabel.SizeToFit ();
            startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);

            //end date
            if (c.AllDayEvent) {
                var endDay = CalendarHelper.ReturnAllDayEventEndTime (c.EndTime);
                endDateLabel.Text = Pretty.MediumFullDate (endDay);
                endDate = endDay;
            } else {
                endDateLabel.Text = Pretty.MediumFullDateTime (c.EndTime);
                endDate = c.EndTime;
            }
            endDateLabel.SizeToFit ();
            endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);

            // Location
            locationField.Text = c.Location;

            //attachments view
            if (!attachmentsInitialized && null != this.item) {
                foreach (var attachment in this.item.attachments) {
                    attachmentView.Append (attachment);
                }
                attachmentsInitialized = true;
            }
            attachmentView.ConfigureView ();
            attachmentView.Hidden = false;

            //people view
            var peopleDetailLabelView = contentView.ViewWithTag (PEOPLE_DETAIL_TAG) as UILabel;
            peopleDetailLabelView.Text = (0 != c.attendees.Count ? "Attendees: (" + c.attendees.Count + ")" : "Attendees:");

            //alert view
            var alertDetailLabelView = contentView.ViewWithTag (ALERT_DETAIL_TAG) as UILabel;
            alertDetailLabelView.Text = Pretty.ReminderString (c.ReminderIsSet, c.Reminder);
            alertDetailLabelView.SizeToFit ();
            alertDetailLabelView.Frame = new CGRect (SCREEN_WIDTH - alertDetailLabelView.Frame.Width - 34, 12.438f, alertDetailLabelView.Frame.Width, TEXT_LINE_HEIGHT);

            //calendar view
            if (null == calendarFolder && CalendarItemEditorAction.create == action && !noCalendarAccess && !noDeviceCalendar) {
                // Choose the initial calendar for the item.  Look for a default calendar folder within
                // the selected account.
                var accountCalendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                calendarFolder = accountCalendars.GetFolder (0);
                for (int f = 0; f < accountCalendars.Count(); ++f) {
                    var calendar = accountCalendars.GetFolder (f);
                    if (Xml.FolderHierarchy.TypeCode.DefaultCal_8 == calendar.Type) {
                        calendarFolder = calendar;
                        break;
                    }
                }
            }

            var calendarDetailLabelView = contentView.ViewWithTag (CAL_DETAIL_TAG) as UILabel;
            if (null == calendarFolder) {
                calendarDetailLabelView.Text = "";
            } else {
                calendarDetailLabelView.Text = calendarFolder.DisplayName;
            }
            calendarDetailLabelView.SizeToFit ();
            calendarDetailLabelView.Frame = new CGRect (SCREEN_WIDTH - calendarDetailLabelView.Frame.Width - 34, 12.438f, calendarDetailLabelView.Frame.Width, TEXT_LINE_HEIGHT);

            suppressLayout = false;
            LayoutView ();
        }

        protected override void Cleanup ()
        {
            // Event handlers

            cancelButton.Clicked -= CancelButtonClicked;
            doneButton.Clicked -= DoneButtonClicked;
            titleField.ShouldReturn -= TextFieldResignFirstResponder;
            titleField.EditingDidBegin -= EditingDidBegin;
            descriptionTextView.Changed -= DescriptionTextViewChanged;
            descriptionTextView.SelectionChanged -= DescriptionTextViewSelectionChanged;
            descriptionTextView.Ended -= DescriptionTextViewEnded;
            allDaySwitch.ValueChanged -= AllDaySwitchValueChanged;
            locationField.EditingDidBegin -= LocationEditingDidBegin;
            locationField.EditingDidEnd -= LocationEditingDidEnd;
            locationField.ShouldReturn -= TextFieldResignFirstResponder;

            if (null != startDatePicker) {
                startDatePicker.ValueChanged -= StartDatePickerValueChanged;
            }
            if (null != endDatePicker) {
                endDatePicker.ValueChanged -= EndDatePickerValueChanged;
            }

            // Gesture recognizers

            backgroundTapGesture.RemoveTarget (backgroundTapGestureToken);
            contentView.RemoveGestureRecognizer (backgroundTapGesture);

            startDatePickerTapGesture.RemoveTarget (startDatePickerTapGestureToken);
            startView.RemoveGestureRecognizer (startDatePickerTapGesture);

            endDatePickerTapGesture.RemoveTarget (endDatePickerTapGestureToken);
            endView.RemoveGestureRecognizer (endDatePickerTapGesture);

            peopleTapGesture.RemoveTarget (peopleTapGestureToken);
            peopleView.RemoveGestureRecognizer (peopleTapGesture);

            alertTapGesture.RemoveTarget (alertTapGestureToken);
            alertsView.RemoveGestureRecognizer (alertTapGesture);

            calendarTapGesture.RemoveTarget (calendarTapGestureToken);
            calendarView.RemoveGestureRecognizer (calendarTapGesture);

            deleteTapGesture.RemoveTarget (deleteTapGestureToken);
            deleteView.RemoveGestureRecognizer (deleteTapGesture);

            titleField = null;
            descriptionTextView = null;
            descriptionPlaceHolder = null;
            doneButton = null;
            cancelButton = null;
            titleView = null;
            descriptionView = null;
            allDayView = null;
            allDaySwitch = null;
            startView = null;
            startDatePicker = null;
            endView = null;
            endDatePicker = null;
            locationView = null;
            locationField = null;
            attachmentView = null;
            attachmentBGView = null;
            peopleView = null;
            alertsView = null;
            calendarView = null;
            deleteView = null;
            startDateLabel = null;
            endDateLabel = null;
            backgroundTapGesture = null;
            startDatePickerTapGesture = null;
            endDatePickerTapGesture = null;
            peopleTapGesture = null;
            alertTapGesture = null;
            calendarTapGesture = null;
            deleteTapGesture = null;
            line1 = null;
            line2 = null;
            line3 = null;
            line4 = null;
            line5 = null;
            line6 = null;
            line7 = null;
            line8 = null;
            line9 = null;
            line11 = null;
            line12 = null;
            line13 = null;
            line14 = null;
            line15 = null;
            line16 = null;
            separator3 = null;
            separator4 = null;
            strikethrough = null;
            endDivider = null;
            startDivider = null;
        }

        // UIDatePicker leaks memory.  Quite badly.  A couple of megabytes every time one is added to the
        // view hierarchy.  I haven't found any way to avoid this leak.  The best we can do right now is to
        // not create the UIDatePicker object until the user expands the date picker.  That will save us a
        // few megabytes whenever the user edits an event without adjusting the start or end times.

        protected void InitializeStartDatePicker ()
        {
            if (null != startDatePicker) {
                return;
            }
            startDatePicker = new UIDatePicker (new CGRect (0, 44, SCREEN_WIDTH, PICKER_HEIGHT));
            startDatePicker.Hidden = true;
            startDatePicker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
            startDatePicker.ValueChanged += StartDatePickerValueChanged;
            startDatePicker.Mode = allDaySwitch.On ? UIDatePickerMode.Date : UIDatePickerMode.DateAndTime;
            startDatePicker.Date = startDate.ToNSDate ();
            startDatePicker.MinuteInterval = 5;
            Util.ConstrainDatePicker (startDatePicker, startDate);
            startView.AddSubview (startDatePicker);
        }

        protected void InitializeEndDatePicker ()
        {
            if (null != endDatePicker) {
                return;
            }
            endDatePicker = new UIDatePicker (new CGRect (0, CELL_HEIGHT, SCREEN_WIDTH, PICKER_HEIGHT));
            endDatePicker.Hidden = true;
            endDatePicker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
            endDatePicker.ValueChanged += EndDatePickerValueChanged;
            endDatePicker.Mode = allDaySwitch.On ? UIDatePickerMode.Date : UIDatePickerMode.DateAndTime;
            endDatePicker.Date = endDate.ToNSDate ();
            endDatePicker.MinuteInterval = 5;
            Util.ConstrainDatePicker (endDatePicker, endDate);
            endView.AddSubview (endDatePicker);
        }

        protected McFolder GetCalendarFolderForItem ()
        {
            if (null == item) {
                return McFolder.GetDeviceCalendarsFolder ();
            }
            return McFolder.QueryByFolderEntryId<McCalendar> (item.AccountId, item.Id).FirstOrDefault ();
        }

        protected List<Tuple<McAccount, NachoFolders>> GetChoosableCalendars ()
        {
            var result = new List<Tuple<McAccount, NachoFolders>> ();
            IEnumerable<McAccount> candidateAccounts;
            // If the user has added any attachments to the event, there is likely an attachment body
            // sitting in a file in an account-specific location.  Switching accounts at this point
            // would be complicated.  So don't allow changing accounts if the user has already added
            // an attachment.
            if (CalendarItemEditorAction.create == action && 0 == attachmentView.AttachmentCount) {
                candidateAccounts = McAccount.GetAllAccounts ();
            } else {
                candidateAccounts = new McAccount[] { account };
            }
            foreach (var account in candidateAccounts) {
                if (account.HasCapability(McAccount.AccountCapabilityEnum.CalWriter)) {
                    var calendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                    if (0 < calendars.Count ()) {
                        result.Add (new Tuple<McAccount, NachoFolders> (account, calendars));
                    }
                }
            }
            if (0 == result.Count) {
                Log.Error (Log.LOG_CALENDAR, "Couldn't find any calendars for the event editor's calendar chooser.");
                if (null == item) {
                    result.Add (new Tuple<McAccount, NachoFolders> (McAccount.GetDeviceAccount (), new NachoFolders (McFolder.GetDeviceCalendarsFolder ())));
                } else {
                    result.Add (new Tuple<McAccount, NachoFolders> (account, new NachoFolders (GetCalendarFolderForItem ())));
                }
            }
            return result;
        }

        /// <summary>
        /// Scroll the view so that the text field's caret, plus some extra space above and below, is visible.
        /// </summary>
        protected void MakeCaretVisible (UITextView textView, nfloat topMargin, nfloat bottomMargin, bool animate)
        {
            var caretFrame = textView.ConvertRectToView (textView.GetCaretRectForPosition (textView.SelectedTextRange.End), contentView);
            caretFrame.Y -= topMargin;
            caretFrame.Height += topMargin + bottomMargin;
            scrollView.ScrollRectToVisible (caretFrame, animate);
        }

        public void DismissView ()
        {
            DismissViewController (true, null);
        }

        protected void LayoutView ()
        {
            if (suppressLayout) {
                return;
            }

            nfloat yOffset = 0f;

            yOffset += 74;
            //descriptionView.Frame = new CGRect (0, yOffset, SCREEN_WIDTH, /*CELL_HEIGHT +*/ DESCRIPTION_OFFSET);
            ViewFramer.Create(descriptionView).Height(descriptionTextView.Frame.Bottom);
            yOffset += descriptionView.Frame.Height;

            AdjustY (line3, yOffset);

            yOffset += LINE_OFFSET;
            AdjustY (line4, yOffset);
            AdjustY (allDayView, yOffset);
            yOffset += CELL_HEIGHT;
            AdjustY (line5, yOffset);

            startView.Frame = new CGRect (0, yOffset, SCREEN_WIDTH, CELL_HEIGHT + currentStartPickerHeight);
            yOffset += startView.Frame.Height;
            AdjustY (line6, yOffset);
            endView.Frame = new CGRect (0, yOffset, SCREEN_WIDTH, CELL_HEIGHT + currentEndPickerHeight);
            yOffset += endView.Frame.Height;
            AdjustY (line7, yOffset);
            AdjustY (separator3, line7.Frame.Bottom);

            yOffset += LINE_OFFSET;
            AdjustY (line8, yOffset);
            AdjustY (locationView, yOffset);
            yOffset += locationView.Frame.Height;

            line9.Hidden = isSimpleEvent;
            attachmentView.Hidden = isSimpleEvent;
            attachmentBGView.Hidden = isSimpleEvent;
            if (!isSimpleEvent) {
                AdjustY (line9, yOffset);
                AdjustY (attachmentView, yOffset);
                AdjustY (attachmentBGView, yOffset);
                attachmentView.Layout ();
                yOffset += attachmentView.Frame.Height;
            }

            line11.Hidden = isSimpleEvent;
            peopleView.Hidden = isSimpleEvent;
            if (!isSimpleEvent) {
                AdjustY (line11, yOffset);
                AdjustY (peopleView, yOffset);
                yOffset += peopleView.Frame.Height;
            }
            AdjustY (line12, yOffset);
            AdjustY (separator4, line12.Frame.Bottom);

            yOffset += LINE_OFFSET;
            AdjustY (line13, yOffset);
            AdjustY (alertsView, yOffset);
            yOffset += alertsView.Frame.Height;
            AdjustY (line14, yOffset);

            yOffset += LINE_OFFSET;
            AdjustY (line15, yOffset);
            AdjustY (calendarView, yOffset);
            yOffset += calendarView.Frame.Height;
            AdjustY (line16, yOffset);
            yOffset += LINE_OFFSET;

            if (action == CalendarItemEditorAction.edit) {
                AdjustY (deleteView, yOffset);
                yOffset += deleteView.Frame.Height + LINE_OFFSET;
            }
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            contentView.Frame = new CGRect (0, 0, SCREEN_WIDTH, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;
        }

        protected void LayoutWithAnimation (Action completion = null)
        {
            if (suppressLayout) {
                return;
            }
            UIView.Animate (0.2, LayoutView, completion);
        }

        protected void AdjustY (UIView view, nfloat yOffset)
        {
            ViewFramer.Create (view).Y (yOffset);
        }

        /// <summary>
        /// Check if the data that the user entered is valid and can be saved as an event.  If not,
        /// throw up a dialog explaining the problem to the user.
        /// </summary>
        /// <returns><c>true</c> if this instance can be saved; otherwise, <c>false</c>.</returns>
        protected bool CanBeSaved ()
        {
            if (string.IsNullOrEmpty (titleField.Text)) {
                NcAlertView.ShowMessage (this, "Cannot Save Event", "The title of the event must not be empty.");
                return false;
            }
            if (startDate > endDate) {
                NcAlertView.ShowMessage (this, "Cannot Save Event",
                    "The starting time must be no later than the ending time.");
                return false;
            }
            return true;
        }

        protected void ExtractValues ()
        {
            c.AccountId = account.Id;
            c.Subject = titleField.Text;
            if (descriptionWasEdited) {
                // Non-break space characters can work their way into the description, especially if it originated
                // as HTML.  But some clients don't like those characters when they are part of plain text.
                // So convert non-break spaces to ordinary spaces.
                c.SetDescription (descriptionTextView.Text.Replace ('\x00A0', ' '), McAbstrFileDesc.BodyTypeEnum.PlainText_1);
            }
            var allDayEvent = allDaySwitch.On;
            c.AllDayEvent = allDayEvent;
            if (allDayEvent) {
                // An all-day event is supposed to run midnight to midnight in the local time zone.
                c.StartTime = startDate.ToLocalTime ().Date.ToUniversalTime ();
                c.EndTime = endDate.ToLocalTime ().AddDays (1.0).Date.ToUniversalTime ();
            } else {
                c.StartTime = startDate;
                c.EndTime = endDate;
            }
            // c.attendees is already set via PullAttendees
            c.Location = locationField.Text;
            c.attachments = attachmentView.AttachmentList;
                
            // Extras
            // The app does not keep track of the account owner's name.  Use the e-mail address instead.
            c.OrganizerName = account.EmailAddr; //Pretty.UserNameForAccount (account);
            c.OrganizerEmail = account.EmailAddr;
            c.DtStamp = DateTime.UtcNow;
            if (0 == c.attendees.Count) {
                c.MeetingStatusIsSet = true;
                c.MeetingStatus = NcMeetingStatus.Appointment;
                c.ResponseRequested = false;
                c.ResponseRequestedIsSet = true;
            } else {
                c.MeetingStatusIsSet = true;
                c.MeetingStatus = NcMeetingStatus.MeetingOrganizer;
                c.ResponseRequested = true;
                c.ResponseRequestedIsSet = true;
            }

            // There is no UI for setting the BusyStatus.  For new events, set it to Free for
            // all-day events and Busy for other events.  If we don't explicitly set BusyStatus,
            // some servers will treat it as if it were Free, while others will act as if it
            // were Busy.
            if (!c.BusyStatusIsSet) {
                c.BusyStatus = allDayEvent ? NcBusyStatus.Free : NcBusyStatus.Busy;
                c.BusyStatusIsSet = true;
            }

            // The event always uses the local time zone.
            c.TimeZone = new AsTimeZone (CalendarHelper.SimplifiedLocalTimeZone (), c.StartTime).toEncodedTimeZone ();

            if (String.IsNullOrEmpty (c.UID)) {
                c.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpperInvariant ();
            }
        }

        protected void SyncMeetingRequest ()
        {
            if (0 == c.Id) {
                c.Insert (); // new entry
                calendarFolder.Link (c);
                BackEnd.Instance.CreateCalCmd (account.Id, c.Id, calendarFolder.Id);
            } else {
                c.RecurrencesGeneratedUntil = DateTime.MinValue; // Force regeneration of events
                c.Update ();
                var oldFolder = GetCalendarFolderForItem ();
                var newFolder = calendarFolder;
                if (newFolder.Id != oldFolder.Id) {
                    BackEnd.Instance.MoveCalCmd (account.Id, c.Id, newFolder.Id);
                    oldFolder.Unlink (c);
                    newFolder.Link (c);
                }
                BackEnd.Instance.UpdateCalCmd (account.Id, c.Id, descriptionWasEdited);
            }
            c = McCalendar.QueryById<McCalendar> (c.Id);
        }

        protected void DeleteEvent ()
        {
            if (0 != c.attendees.Count) {
                PrepareCancelationNotices ();
            }

            BackEnd.Instance.DeleteCalCmd (account.Id, c.Id);

            if (null == owner) {
                DismissView ();
            } else {
                owner.EventWasDeleted (this);
            }
        }

        protected void PrepareCancelationNotices ()
        {
            var iCalCancelPart = CalendarHelper.MimeCancelFromCalendar (c);
            var mimeBody = CalendarHelper.CreateMime ("", iCalCancelPart, new List<McAttachment> ());

            CalendarHelper.SendMeetingCancelations (account, c, null, mimeBody);
        }

        /// <summary>
        /// Sends the message. Message (UID) must already exist in EAS.
        /// </summary>
        protected void PrepareInvites ()
        {
            if (0 == c.attendees.Count) {
                // No attendees to invite.
                return;
            }
            string plainTextDescription;
            switch (c.DescriptionType) {
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                plainTextDescription = Util.ConvertToPlainText (c.Description, NSDocumentType.HTML);
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                plainTextDescription = Util.ConvertToPlainText (c.Description, NSDocumentType.RTF);
                break;
            default:
                plainTextDescription = c.Description;
                break;
            }
            var iCalPart = CalendarHelper.MimeRequestFromCalendar (c);
            var mimeBody = CalendarHelper.CreateMime (plainTextDescription, iCalPart, c.attachments);

            CalendarHelper.SendInvites (account, c, null, null, mimeBody, null);
        }

        /// IUcAttachmentBlock delegate
        public void AttachmentBlockNeedsLayout (UcAttachmentBlock view)
        {
            LayoutWithAnimation ();
        }

        /// IUcAttachmentBlock delegate
        public void ShowChooserForAttachmentBlock ()
        {
            var helper = new AddAttachmentViewController.MenuHelper (this, account, attachmentView);
            PresentViewController (helper.MenuViewController, true, null);
        }

        /// IUcAttachmentBlock delegate
        public void ToggleCompactForAttachmentBlock ()
        {
            attachmentView.ToggleCompact ();
        }

        /// IUcAttachmentBlock delegate
        public void DisplayAttachmentForAttachmentBlock (McAttachment attachment)
        {
            PlatformHelpers.DisplayAttachment (this, attachment);
        }

        /// IUcAttachmentBlock delegate
        public void RemoveAttachmentForAttachmentBlock (McAttachment attachment)
        {
        }

        public void UpdateAttendeeList (IList<McAttendee> attendees)
        {
            c.attendees = attendees;
        }

        public void DismissINachoAttendeeListChooser (INachoAttendeeListChooser vc)
        {
            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            var a = obj as McAttachment;
            if (null != a) {
                attachmentView.Append (a);
                this.DismissViewController (true, null);
                return;
            }

            var file = obj as McDocument;
            if (null != file) {
                var attachment = McAttachment.InsertSaveStart (account.Id);
                attachment.SetDisplayName (file.DisplayName);
                attachment.IsInline = true;
                attachment.UpdateFileCopy (file.GetFilePath ());
                attachmentView.Append (attachment);
                this.DismissViewController (true, null);
                return;
            }

            var note = obj as McNote;
            if (null != note) {
                var attachment = McAttachment.InsertSaveStart (account.Id);
                attachment.SetDisplayName (note.DisplayName + ".txt");
                attachment.IsInline = true;
                attachment.UpdateData (note.noteContent);
                attachmentView.Append (attachment);
                this.DismissViewController (true, null);
                return;
            }

            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            vc.DismissFileChooser (true, null);
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void Append (McAttachment attachment)
        {
            attachmentView.Append (attachment);
        }

        public void AttachmentUpdated (McAttachment attachment)
        {
            attachmentView.UpdateAttachment (attachment);
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissPhotoPicker ()
        {
            this.DismissViewController (true, null);
        }

        public void PresentFileChooserViewController (UIViewController vc)
        {
            PresentViewController (vc, true, null);
        }

        // Event handlers

        private void EditingDidBegin (object sender, EventArgs args)
        {
            eventEditStarted = true;
        }

        private void CancelButtonClicked (object sender, EventArgs args)
        {
            View.EndEditing (true);
            if (eventEditStarted) {
                NcAlertView.Show (this, "Are you sure?", "This event will not be saved.",
                    new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null),
                    new NcAlertAction ("Yes", NcAlertActionStyle.Destructive, () => {
                        DismissView ();
                    }));
            } else {
                DismissView ();
            }
        }

        private void DoneButtonClicked (object sender, EventArgs args)
        {
            if (CalendarItemEditorAction.edit == action && null != item && 0 != item.Id) {
                // Make sure the item wasn't deleted while it was being edited.
                var dbItem = McCalendar.QueryById<McCalendar> (item.Id);
                if (null == dbItem || dbItem.IsAwaitingDelete) {
                    NcAlertView.Show (this, "Deleted Event",
                        "The changes to the event cannot be saved because the event has been deleted.",
                        new NcAlertAction ("OK", NcAlertActionStyle.Cancel, () => {
                            DismissView ();
                        }));
                    return;
                }
            }
            if (CanBeSaved ()) {
                ExtractValues ();
                SyncMeetingRequest ();
                PrepareInvites ();
                DismissView ();
            }
        }

        private bool TextFieldResignFirstResponder (UITextField textField)
        {
            textField.ResignFirstResponder ();
            return true;
        }

        private void DescriptionTextViewChanged (object sender, EventArgs args)
        {
            descriptionWasEdited = true;
            eventEditStarted = true;
            descriptionPlaceHolder.Hidden = true;

            var oldHeight = descriptionTextView.Frame.Height;
            descriptionTextView.SizeToFit ();
            var newheight = descriptionTextView.Frame.Height;
            ViewFramer.Create (descriptionTextView).Width (SCREEN_WIDTH - 30);
            if (oldHeight != newheight) {
                // The height of the description field has changed, so the entire view layout needs to be redone.
                LayoutView ();
                // Scroll the view if necessary to make the caret is visible, plus a little extra space below
                // the caret and three lines of text above the caret.  If the view got smaller, animate the
                // scrolling.  If the view got bigger, don't animate the scrolling.  (I experimented, and that
                // animation strategy looks best.)
                MakeCaretVisible (descriptionTextView, TEXT_LINE_HEIGHT * 3,
                    descriptionTextView.TextContainerInset.Bottom, oldHeight > newheight);
            }
        }

        /// <summary>
        /// When the user moves the caret around with changing the text, make sure the caret stays visible.
        /// </summary>
        private void DescriptionTextViewSelectionChanged (object sender, EventArgs args)
        {
            MakeCaretVisible (descriptionTextView, TEXT_LINE_HEIGHT * 3, descriptionTextView.TextContainerInset.Bottom, true);
        }

        private void DescriptionTextViewEnded (object sender, EventArgs args)
        {
            descriptionPlaceHolder.Hidden = descriptionTextView.HasText;
        }

        private void StartDatePickerValueChanged (object sender, EventArgs args)
        {
            eventEditStarted = true;
            DateTime date = startDatePicker.Date.ToDateTime ();
            if (allDaySwitch.On) {
                startDateLabel.Text = Pretty.MediumFullDate (date);
            } else {
                startDateLabel.Text = Pretty.MediumFullDateTime (date);
            }
            startDate = date;
            if (!endChanged && !allDaySwitch.On) {
                endDate = date.AddHours (1);
                if (null != endDatePicker) {
                    endDatePicker.Date = endDate.ToNSDate ();
                }
                endDateLabel.Text = Pretty.Time (endDate);
                endDateLabel.SizeToFit ();
                endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                endDateLabel.TextColor = A.Color_NachoTeal;
            }
            startDateLabel.SizeToFit ();
            startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            startDateLabel.TextColor = A.Color_NachoTeal;
            if (startDate > endDate) {
                strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
                strikethrough.Hidden = false;
                endDateLabel.TextColor = A.Color_NachoRed;
            } else {
                strikethrough.Hidden = true;
                endDateLabel.TextColor = A.Color_808080;
            }
        }

        private void EndDatePickerValueChanged (object sender, EventArgs args)
        {
            eventEditStarted = true;
            endChanged = true;
            DateTime date = endDatePicker.Date.ToDateTime ();
            if (allDaySwitch.On) {
                endDateLabel.Text = Pretty.MediumFullDate (date);
            } else {
                endDateLabel.Text = Pretty.MediumFullDateTime (date);
            }
            endDate = date;
            endDateLabel.SizeToFit ();
            endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            if (startDate > endDate) {
                strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
                strikethrough.Hidden = false;
                endDateLabel.TextColor = A.Color_NachoRed;
            } else {
                strikethrough.Hidden = true;
                endDateLabel.TextColor = A.Color_NachoTeal;
            }
        }

        private void AllDaySwitchValueChanged (object sender, EventArgs args)
        {
            eventEditStarted = true;
            if (allDaySwitch.On) {
                if (!endChanged && startDate.ToLocalTime ().Date != endDate.ToLocalTime ().Date && TimeSpan.FromHours (1) >= endDate - startDate) {
                    // The user changed the event be an all-day event.  The event is no more than
                    // an hour long, its start and end times are on different days, and the user
                    // hasn't explicitly changed the end time.  It is more likely that the user
                    // wants the event to be a single day rather than an multi-day all-day event.
                    // If the app is guessing incorrectly, the user can still correct the times
                    // before saving the event.
                    endDate = startDate;
                }
                startDateLabel.Text = Pretty.MediumFullDate (startDate);
                startDateLabel.SizeToFit ();
                startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                endDateLabel.Text = Pretty.MediumFullDate (endDate);
                endDateLabel.SizeToFit ();
                endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
                if (null != startDatePicker) {
                    startDatePicker.Mode = UIDatePickerMode.Date;
                }
                if (null != endDatePicker) {
                    endDatePicker.Mode = UIDatePickerMode.Date;
                }
            } else {
                if (!timesAreSet) {
                    // Special case in which the user changes an all day event to an event with a start and end time
                    var tempC = CalendarHelper.DefaultMeeting(startDate, endDate);
                    startDate = tempC.StartTime;
                    endDate = tempC.EndTime;
                    timesAreSet = true;
                }
                if (null != startDatePicker) {
                    startDatePicker.Date = startDate.ToNSDate ();
                    startDatePicker.Mode = UIDatePickerMode.DateAndTime;
                    startDatePicker.MinuteInterval = 5;
                }
                startDateLabel.Text = Pretty.MediumFullDateTime (startDate);
                startDateLabel.SizeToFit ();
                startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                if (null != endDatePicker) {
                    endDatePicker.Date = endDate.ToNSDate ();
                    endDatePicker.Mode = UIDatePickerMode.DateAndTime;
                    endDatePicker.MinuteInterval = 5;
                }
                endDateLabel.Text = Pretty.MediumFullDateTime (endDate);
                endDateLabel.SizeToFit ();
                endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);

                strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
            }
        }

        private void ScrollLocationVisible ()
        {
            scrollView.ScrollRectToVisible (locationView.Frame, true);
        }

        private void LocationEditingDidBegin (object sender, EventArgs args)
        {
            eventEditStarted = true;
            if (0 != keyboardHeight) {
                // The keyboard is already open, which means the OpenKeyboardAction will not get called.
                // (This can happen when transfering control directly from one text field to another.)
                // So go ahead and scroll right now.
                ScrollLocationVisible ();
            }
            OpenKeyboardAction += ScrollLocationVisible;
        }

        private void LocationEditingDidEnd (object sender, EventArgs args)
        {
            OpenKeyboardAction -= ScrollLocationVisible;
        }

        // Gesture recognizer actions

        private void DismissKeyboard ()
        {
            View.EndEditing (true);
        }

        private void StartDatePickerTapAction ()
        {
            View.EndEditing (true);
            if (startDateOpen) {
                // Close the start date picker.
                startDateOpen = false;
                currentStartPickerHeight = 0;
                startDateLabel.TextColor = A.Color_808080;
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    startDatePicker.Hidden = true;
                    startDivider.Hidden = true;
                });
                startDate = startDatePicker.Date.ToDateTime ();
            } else {
                // Open the start date picker and close the end date picker.  If the end date picker is already
                // closed, closing it again will have no effect.
                InitializeStartDatePicker ();
                startDateOpen = true;
                currentStartPickerHeight = PICKER_HEIGHT;
                startDatePicker.Hidden = false;
                startDivider.Hidden = false;
                endDateOpen = false;
                currentEndPickerHeight = 0;
                endDateLabel.TextColor = A.Color_808080;
                scrollView.ScrollRectToVisible (new CGRect (0, startView.Frame.Y, 1, CELL_HEIGHT + currentStartPickerHeight), true);
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    if (null != endDatePicker) {
                        endDatePicker.Hidden = true;
                    }
                    endDivider.Hidden = true;
                });
            }
        }

        private void EndDatePickerTapAction ()
        {
            View.EndEditing (true);
            if (endDateOpen) {
                // Close the end date picker.
                endDateOpen = false;
                currentEndPickerHeight = 0;
                endDateLabel.TextColor = A.Color_808080;
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    endDatePicker.Hidden = true;
                    endDivider.Hidden = true;
                });
                endDate = endDatePicker.Date.ToDateTime ();
                if (startDate > endDate) {
                    strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
                    strikethrough.Hidden = false;
                    endDateLabel.TextColor = A.Color_NachoRed;
                } else {
                    strikethrough.Hidden = true;
                    endDateLabel.TextColor = A.Color_808080;
                }
            } else {
                // Open the end date picker and close the start date picker.  If the start date picker is already
                // closed, closing it again will have no effect.
                InitializeEndDatePicker ();
                endDateOpen = true;
                currentEndPickerHeight = PICKER_HEIGHT;
                endDatePicker.Hidden = false;
                endDivider.Hidden = false;
                startDateOpen = false;
                currentStartPickerHeight = 0;
                startDateLabel.TextColor = A.Color_808080;
                // We might be in the process of closing the start date picker, so the location of the end date
                // picker may be about to change.  Create a rectangle that represents where the end date picker
                // will be after all the adjustments have been made.
                scrollView.ScrollRectToVisible (new CGRect (0, startView.Frame.Y + CELL_HEIGHT, 1, CELL_HEIGHT + currentEndPickerHeight), true);
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    if (null != startDatePicker) {
                        startDatePicker.Hidden = true;
                    }
                    startDivider.Hidden = true;
                });
            }
        }

        private void PeopleTapAction ()
        {
            View.EndEditing (true);
            ShowAttendees ();
        }

        private void AlertTapAction ()
        {
            View.EndEditing (true);
            ShowAlert ();
        }

        private void CalendarTapAction ()
        {
            View.EndEditing (true);
            ShowCalendarChooser ();
        }

        private void DeleteTapAction ()
        {
            NcActionSheet.Show (deleteView, this,
                new NcAlertAction ("Delete Event", NcAlertActionStyle.Destructive, () => {
                    DeleteEvent ();
                }),
                new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null));
        }
    }
}
