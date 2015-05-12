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
    public partial class EditEventViewController
        : NcUIViewControllerNoLeaks, INachoAttendeeListChooserDelegate, IUcAttachmentBlockDelegate, INachoFileChooserParent
    {
        protected INachoCalendarItemEditorParent owner;
        protected CalendarItemEditorAction action;
        protected McCalendar item;
        protected McCalendar c;
        protected DateTime startingDate;
        protected McFolder folder;
        protected McAccount account;
        protected NachoFolders calendars;
        protected bool calendarChanged;
        protected string TempPhone = "";
        protected int calendarIndex = 0;

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
        UIView endView;

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
        protected int START_PICKER_HEIGHT = 0;
        protected int END_PICKER_HEIGHT = 0;
        protected nfloat TEXT_LINE_HEIGHT = 19.124f;
        protected nfloat DESCRIPTION_OFFSET = 0f;
        protected nfloat DELETE_BUTTON_OFFSET = 0f;
        protected UIFont labelFont = A.Font_AvenirNextMedium14;

        protected bool startDateOpen = false;
        protected bool endDateOpen = false;
        protected bool endChanged = false;
        protected bool eventEditStarted = false;
        protected bool attachmentsInitialized = false;
        protected bool timesAreSet = true;
        protected bool descriptionWasEdited = false;
        protected bool suppressLayout = false;

        protected bool calendarItemIsMissing = false;

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

            base.ViewDidLoad ();
        }

        public void SetCalendarEvent (McEvent e, CalendarItemEditorAction action)
        {
            if (null == e) {
                this.item = null;
            } else {
                this.item = McCalendar.QueryById<McCalendar> (e.CalendarId);
            }
            this.action = action;
        }

        public void SetCalendarItem (McCalendar c)
        {
            if (null == c) {
                this.item = null;
                this.action = CalendarItemEditorAction.create;
                return;
            }

            if (0 == c.Id) {
                this.item = c;
                this.action = CalendarItemEditorAction.create;
                return;
            }

            this.item = c;
            this.action = CalendarItemEditorAction.edit;
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
                        NavigationController.PopViewController (true);
                    }));
            }
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
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
        /// <param name="animated">If set to <c>true</c> animated.</param>
        /// <param name="action">Action.</param>
        public void DismissCalendarItemEditor (bool animated, Action action)
        {
            owner = null;
            NavigationController.PopViewController (true);
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

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("EditEventToEventAttendees")) {
                var dc = (EventAttendeeViewController)segue.DestinationViewController;
                ExtractValues ();
                dc.Setup (this, c.attendees, c, true, CalendarHelper.IsOrganizer (c.OrganizerEmail, account.EmailAddr));
                return;
            }

            if (segue.Identifier.Equals ("EditEventToAlert")) {
                var dc = (AlertChooserViewController)segue.DestinationViewController;
                dc.SetReminder (c.ReminderIsSet, c.Reminder);
                ExtractValues ();
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    uint reminder;
                    c.ReminderIsSet = dc.GetReminder (out reminder);
                    if (c.ReminderIsSet) {
                        c.Reminder = reminder;
                    }
                };
                return;
            }

            if (segue.Identifier.Equals ("SegueToAddAttachment")) {
                var dc = (AddAttachmentViewController)segue.DestinationViewController;
                ExtractValues ();
                dc.SetOwner (this);
                return;
            }

            if (segue.Identifier.Equals ("EventToPhone")) {
                var dc = (PhoneViewController)segue.DestinationViewController;
                dc.SetPhone (TempPhone);
                ExtractValues ();
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    TempPhone = dc.GetPhone ();
                };
                return;
            }

            if (segue.Identifier.Equals ("EditEventToCalendarChooser")) {
                var dc = (ChooseCalendarViewController)segue.DestinationViewController;
                ExtractValues ();
                dc.SetCalendars (calendars);
                dc.SetSelectedCalIndex (calendarIndex);
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    calendarIndex = dc.GetCalIndex ();
                    calendarChanged = true;
                };
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected override void CreateViewHierarchy ()
        {
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            backgroundTapGesture = new UITapGestureRecognizer ();
            backgroundTapGestureToken = backgroundTapGesture.AddTarget (DismissKeyboard);
            contentView.AddGestureRecognizer (backgroundTapGesture);

            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);

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

            startDatePicker.Frame = new CGRect (0, 44, SCREEN_WIDTH, START_PICKER_HEIGHT);
            startDatePicker.Hidden = true;
            startDatePicker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
            startView.AddSubview (startDatePicker);

            startDatePicker.ValueChanged += StartDatePickerValueChanged;
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

            endDatePicker.Frame = new CGRect (0, CELL_HEIGHT, SCREEN_WIDTH, END_PICKER_HEIGHT);
            endDatePicker.Hidden = true;
            endDatePicker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
            endView.AddSubview (endDatePicker);

            endDatePicker.ValueChanged += EndDatePickerValueChanged;

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
            attachmentView = new UcAttachmentBlock (this, account.Id, SCREEN_WIDTH, 44, true);
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

            calendars = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);

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
                descriptionTextView.Text = ConvertToPlainText (c.Description, NSDocumentType.HTML);
                descriptionPlaceHolder.Hidden = descriptionTextView.HasText;
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                descriptionTextView.Text = ConvertToPlainText (c.Description, NSDocumentType.RTF);
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
                startDateLabel.Text = Pretty.FullDateString (c.StartTime);
                startDatePicker.Mode = UIDatePickerMode.Date;
            } else {
                startDateLabel.Text = Pretty.FullDateTimeString (c.StartTime);
                startDatePicker.Mode = UIDatePickerMode.DateAndTime;
            }
            startDate = c.StartTime;
            startDatePicker.Date = c.StartTime.ToNSDate ();
            Util.ConstrainDatePicker (startDatePicker, startDate);
            startDateLabel.SizeToFit ();
            startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);

            //end date
            if (c.AllDayEvent) {
                var endDay = CalendarHelper.ReturnAllDayEventEndTime (c.EndTime);
                endDateLabel.Text = Pretty.FullDateString (endDay);
                endDatePicker.Mode = UIDatePickerMode.Date;
                endDate = endDay;
                endDatePicker.Date = endDay.ToNSDate ();
            } else {
                endDateLabel.Text = Pretty.FullDateTimeString (c.EndTime);
                endDatePicker.Mode = UIDatePickerMode.DateAndTime;
                endDate = c.EndTime;
                endDatePicker.Date = c.EndTime.ToNSDate ();
            }
            Util.ConstrainDatePicker (endDatePicker, endDate);
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
            var calFolder = new McFolder ();
            if (!calendarChanged) {
                if (action == CalendarItemEditorAction.create) {
                    // The initial setting of the calendar picker should be the default calendar folder.
                    // (In most cases, there is only one calendar folder.  But Hotmail does things
                    // differently, and choosing the correct folder is vital.)  Start with the first
                    // calendar in the list, regardless of its type.  But then look for a default
                    // calendar folder elsewhere in the calendar list.
                    calFolder = calendars.GetFolder (0);
                    for (int i = 1; i < calendars.Count (); ++i) {
                        var cal = calendars.GetFolder (i);
                        if (Xml.FolderHierarchy.TypeCode.DefaultCal_8 == cal.Type) {
                            calFolder = cal;
                            break;
                        }
                    }
                } else {
                    calFolder = GetCalendarFolder ();
                    if (null == calFolder) {
                        calFolder = calendars.GetFolder (0);
                    } 
                }
            } else {
                calFolder = calendars.GetFolder (calendarIndex);
            }
            SetCalIndex (calFolder);

            var calendarDetailLabelView = contentView.ViewWithTag (CAL_DETAIL_TAG) as UILabel;
            calendarDetailLabelView.Text = calendars.GetFolder (calendarIndex).DisplayName;
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
            startDatePicker.ValueChanged -= StartDatePickerValueChanged;
            endDatePicker.ValueChanged -= EndDatePickerValueChanged;
            allDaySwitch.ValueChanged -= AllDaySwitchValueChanged;
            locationField.EditingDidBegin -= LocationEditingDidBegin;
            locationField.EditingDidEnd -= LocationEditingDidEnd;
            locationField.ShouldReturn -= TextFieldResignFirstResponder;

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
            endView = null;
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

        protected string ConvertToPlainText (string formattedText, NSDocumentType type)
        {
            NSError error = null;
            var descriptionData = NSData.FromString (formattedText);
            var descriptionAttributed = new NSAttributedString (descriptionData, new NSAttributedStringDocumentAttributes {
                DocumentType = type
            }, ref error);
            return descriptionAttributed.Value;
        }

        protected McFolder GetCalendarFolder ()
        {
            for (var i = 0; i < calendars.Count (); i++) {
                var calFolderMap = McMapFolderFolderEntry.QueryByFolderIdFolderEntryIdClassCode (account.Id, (calendars.GetFolder (i)).Id, c.Id, c.GetClassCode ());
                if (null != calFolderMap) {
                    return calendars.GetFolderByFolderID (calFolderMap.FolderId);
                }
            }
            return null;
        }

        protected void SetCalIndex (McFolder folder)
        {
            for (var i = 0; i < calendars.Count (); i++) {
                if (folder.Id == (calendars.GetFolder (i)).Id) {
                    calendarIndex = i;
                    return;
                }
            }
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
            NavigationController.PopViewController (true);
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

            startView.Frame = new CGRect (0, yOffset, SCREEN_WIDTH, CELL_HEIGHT + START_PICKER_HEIGHT);
            yOffset += startView.Frame.Height;
            AdjustY (line6, yOffset);
            endView.Frame = new CGRect (0, yOffset, SCREEN_WIDTH, CELL_HEIGHT + END_PICKER_HEIGHT);
            yOffset += endView.Frame.Height;
            AdjustY (line7, yOffset);
            AdjustY (separator3, line7.Frame.Bottom);

            yOffset += LINE_OFFSET;
            AdjustY (line8, yOffset);
            AdjustY (locationView, yOffset);
            yOffset += locationView.Frame.Height;

            AdjustY (line9, yOffset);
            AdjustY (attachmentView, yOffset);
            AdjustY (attachmentBGView, yOffset);
            attachmentView.Layout ();
            yOffset += attachmentView.Frame.Height;

            AdjustY (line11, yOffset);
            AdjustY (peopleView, yOffset);
            yOffset += peopleView.Frame.Height;
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
            //c.attendees is already set via PullAttendees
            c.Location = locationField.Text;
            c.attachments = attachmentView.AttachmentList;
                
            // Extras
            c.OrganizerName = Pretty.UserNameForAccount (account);
            c.OrganizerEmail = account.EmailAddr;
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
                c.UID = System.Guid.NewGuid ().ToString ().Replace ("-", null).ToUpper ();
            }
        }

        protected void SyncMeetingRequest ()
        {
            if (0 == c.Id) {
                c.Insert (); // new entry
                folder = calendars.GetFolder (calendarIndex);
                folder.Link (c);
                BackEnd.Instance.CreateCalCmd (account.Id, c.Id, folder.Id);
            } else {
                c.RecurrencesGeneratedUntil = DateTime.MinValue; // Force regeneration of events
                c.Update ();
                var oldFolder = GetCalendarFolder ();
                var newFolder = calendars.GetFolder (calendarIndex);
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
            //remove item from db
            if (0 != c.attendees.Count) {
                PrepareCancelationNotices ();
            }
            BackEnd.Instance.DeleteCalCmd (account.Id, c.Id);
            var controllers = this.NavigationController.ViewControllers;
            int currentVC = controllers.Count () - 1; // take 0 indexing into account
            NavigationController.PopToViewController (controllers [currentVC - 2], true);
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
            var iCalPart = CalendarHelper.MimeRequestFromCalendar (c);
            var mimeBody = CalendarHelper.CreateMime (c.Description, iCalPart, c.attachments);

            CalendarHelper.SendInvites (account, c, null, null, mimeBody, null);
        }

        /// IUcAttachmentBlock delegate
        public void AttachmentBlockNeedsLayout (UcAttachmentBlock view)
        {
            LayoutWithAnimation ();
        }

        /// IUcAttachmentBlock delegate
        public void PerformSegueForAttachmentBlock (string identifier, SegueHolder segueHolder)
        {
            PerformSegue (identifier, segueHolder);
        }

        /// IUcAttachmentBlock delegate
        public void DisplayAttachmentForAttachmentBlock (McAttachment attachment)
        {
            PlatformHelpers.DisplayAttachment (this, attachment);
        }

        /// IUcAttachmentBlock delegate
        public void PresentViewControllerForAttachmentBlock (UIViewController viewControllerToPresent, bool animated, Action completionHandler)
        {
            this.PresentViewController (viewControllerToPresent, animated, completionHandler);
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

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissPhotoPicker ()
        {
            this.DismissViewController (true, null);
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
                startDateLabel.Text = Pretty.FullDateString (date);
            } else {
                startDateLabel.Text = Pretty.FullDateTimeString (date);
            }
            startDate = date;
            if (!endChanged && !allDaySwitch.On) {
                endDate = date.AddHours (1);
                endDatePicker.Date = endDate.ToNSDate ();
                endDateLabel.Text = Pretty.FullTimeString (endDate);
                endDateLabel.SizeToFit ();
                endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                endDateLabel.TextColor = A.Color_NachoTeal;
            }
            startDateLabel.SizeToFit ();
            startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            startDateLabel.TextColor = A.Color_NachoTeal;
            if (0 > endDate.CompareTo(startDate)) {
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
                endDateLabel.Text = Pretty.FullDateString (date);
            } else {
                endDateLabel.Text = Pretty.FullDateTimeString (date);
            }
            endDate = date;
            endDateLabel.SizeToFit ();
            endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
            if (0 > endDate.CompareTo (startDate)) {
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
                startDateLabel.Text = Pretty.FullDateString (startDate);
                startDateLabel.SizeToFit ();
                startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                endDateLabel.Text = Pretty.FullDateString (endDate);
                endDateLabel.SizeToFit ();
                endDateLabel.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, 12.438f, endDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                strikethrough.Frame = new CGRect (SCREEN_WIDTH - endDateLabel.Frame.Width - 15, CELL_HEIGHT / 2, endDateLabel.Frame.Width, 1);
                startDatePicker.Mode = UIDatePickerMode.Date;
                endDatePicker.Mode = UIDatePickerMode.Date;
            } else {
                if (!timesAreSet) {
                    // Special case in which the user changes an all day event to an event with a start and end time
                    var tempC = CalendarHelper.DefaultMeeting(startDate, endDate);
                    startDate = tempC.StartTime;
                    endDate = tempC.EndTime;
                    timesAreSet = true;
                }
                startDatePicker.Date = startDate.ToNSDate ();
                startDatePicker.Mode = UIDatePickerMode.DateAndTime;
                startDateLabel.Text = Pretty.FullDateTimeString (startDate);
                startDateLabel.SizeToFit ();
                startDateLabel.Frame = new CGRect (SCREEN_WIDTH - startDateLabel.Frame.Width - 15, 12.438f, startDateLabel.Frame.Width, TEXT_LINE_HEIGHT);
                endDatePicker.Date = endDate.ToNSDate ();
                endDatePicker.Mode = UIDatePickerMode.DateAndTime;
                endDateLabel.Text = Pretty.FullDateTimeString (endDate);
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
                START_PICKER_HEIGHT = 0;
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
                startDateOpen = true;
                START_PICKER_HEIGHT = 216;
                startDatePicker.Hidden = false;
                startDivider.Hidden = false;
                endDateOpen = false;
                END_PICKER_HEIGHT = 0;
                endDateLabel.TextColor = A.Color_808080;
                scrollView.ScrollRectToVisible (new CGRect (0, startView.Frame.Y, 1, CELL_HEIGHT + START_PICKER_HEIGHT), true);
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    endDatePicker.Hidden = true;
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
                END_PICKER_HEIGHT = 0;
                endDateLabel.TextColor = A.Color_808080;
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    endDatePicker.Hidden = true;
                    endDivider.Hidden = true;
                });
                endDate = endDatePicker.Date.ToDateTime ();
                if (0 > endDate.CompareTo (startDate)) {
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
                endDateOpen = true;
                END_PICKER_HEIGHT = 216;
                endDateOpen = true;
                endDatePicker.Hidden = false;
                endDivider.Hidden = false;
                startDateOpen = false;
                START_PICKER_HEIGHT = 0;
                startDateLabel.TextColor = A.Color_808080;
                // We might be in the process of closing the start date picker, so the location of the end date
                // picker may be about to change.  Create a rectangle that represents where the end date picker
                // will be after all the adjustments have been made.
                scrollView.ScrollRectToVisible (new CGRect (0, startView.Frame.Y + CELL_HEIGHT, 1, CELL_HEIGHT + END_PICKER_HEIGHT), true);
                LayoutWithAnimation (() => {
                    // The views can't be marked as hidden until after the animation has completed.
                    startDatePicker.Hidden = true;
                    startDivider.Hidden = true;
                });
            }
        }

        private void PeopleTapAction ()
        {
            View.EndEditing (true);
            PerformSegue ("EditEventToEventAttendees", this);
        }

        private void AlertTapAction ()
        {
            View.EndEditing (true);
            PerformSegue ("EditEventToAlert", this);
        }

        private void CalendarTapAction ()
        {
            View.EndEditing (true);
            PerformSegue ("EditEventToCalendarChooser", this);
        }

        private void DeleteTapAction ()
        {
            NcActionSheet.Show (View, this,
                new NcAlertAction ("Delete Event", NcAlertActionStyle.Destructive, () => {
                    DeleteEvent ();
                }),
                new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null));
        }
    }
}
