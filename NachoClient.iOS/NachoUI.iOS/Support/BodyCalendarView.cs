//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using CoreGraphics;
using UIKit;

using MimeKit;
using DDay.iCal;
using DDay.iCal.Serialization;
using DDay.iCal.Serialization.iCalendar;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class BodyCalendarView : UIView, IBodyRender
    {
        public const int CALENDAR_PART_TAG = 400;

        public enum TagType
        {
            CALENDAR_PART_TAG = 400,
            CALENDAR_MONTH_TAG = 401,
            CALENDAR_DATE_TAG = 402,
            CALENDAR_TITLE_TAG = 403,
            CALENDAR_DURATION_TAG = 404,
            CALENDAR_LOCATION_TAG = 405,
            CALENDAR_LINE_TAG = 406
        }

        private McMeetingRequest meetingInfo;
        private McEmailMessage parentMessage;
        private McCalendar calendarItem;
        private bool requestActions = false;
        private bool cancelActions = false;
        private nfloat viewWidth;
        private string organizerEmail;

        public BodyCalendarView (nfloat Y, nfloat width, McEmailMessage parentMessage, bool isOnHot)
            : base (new CGRect (0, Y, width, 150))
        {
            this.parentMessage = parentMessage;
            meetingInfo = parentMessage.MeetingRequest;
            NcAssert.NotNull (meetingInfo, "BodyCalendarView was given a message without a MeetingRequest.");
            calendarItem = McCalendar.QueryByUID (parentMessage.AccountId, meetingInfo.GetUID ());

            viewWidth = width;
            Tag = CALENDAR_PART_TAG;

            // The contents of the action/info bar depends on whether this is a request,
            // response, or cancellation.
            string messageKind = parentMessage.MessageClass;
            if (null != messageKind && messageKind.StartsWith ("IPM.Schedule.Meeting.Resp.")) {
                ShowAttendeeResponseBar ();
            } else if ("IPM.Schedule.Meeting.Canceled" == messageKind) {
                ShowCancellationBar (isOnHot);
            } else {
                if ("IPM.Schedule.Meeting.Request" != messageKind) {
                    Log.Warn (Log.LOG_CALENDAR, "Unexpected calendar kind: {0}. It will be treated as a meeting request.", messageKind);
                }
                ShowRequestChoicesBar (isOnHot);
            }

            ShowEventInfo ();
        }

        public UIView uiView ()
        {
            return this;
        }

        public CGSize ContentSize {
            get {
                return Frame.Size;
            }
        }

        public void ScrollingAdjustment (CGRect frame, CGPoint contentOffset)
        {
            // The calendar section does not scroll or resize.
            // The only thing that can be adjusted is the view's origin.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y);
        }

        /// <summary>
        /// Display the basic information about the calendar event.
        /// </summary>
        private void ShowEventInfo ()
        {
            DateTime start = meetingInfo.StartTime;
            DateTime end = meetingInfo.EndTime;
            string location = meetingInfo.Location;
            nfloat yOffset = 60f + 18f;

            Util.AddHorizontalLine (0, 60, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;

            // When label, image, and detail
            Util.AddTextLabelWithImageView (yOffset, "WHEN", "event-when", EventViewController.TagType.EVENT_WHEN_TITLE_TAG, this);
            yOffset += 16 + 6;
            Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_WHEN_DETAIL_LABEL_TAG, this);
            yOffset += 20;
            Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_WHEN_DURATION_TAG, this);

            var whenLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_WHEN_DETAIL_LABEL_TAG) as UILabel;
            whenLabel.Text = Pretty.ExtendedDateString (start);
            var durationLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_WHEN_DURATION_TAG) as UILabel;
            if (meetingInfo.AllDayEvent) {
                durationLabel.Text = "all day event";
                if ((start.LocalT ().DayOfYear) + 1 != end.LocalT ().DayOfYear) {
                    durationLabel.Text = string.Format ("All day from {0} \nuntil {1}",
                        Pretty.FullDateYearString (start), Pretty.FullDateYearString (end));
                }
            } else {
                if (start.LocalT ().DayOfYear == end.LocalT ().DayOfYear) {
                    durationLabel.Text = string.Format ("from {0} until {1}",
                        Pretty.FullTimeString (start), Pretty.FullTimeString (end));
                } else {
                    durationLabel.Text = string.Format ("from {0} until {1}",
                        Pretty.FullTimeString (start), Pretty.FullDateTimeString (end));
                }
            }
            durationLabel.Lines = 0;
            durationLabel.LineBreakMode = UILineBreakMode.WordWrap;
            durationLabel.SizeToFit ();

            yOffset += NMath.Max (20, durationLabel.Frame.Height);
            yOffset += 20;

            if (!string.IsNullOrEmpty (location)) {
                // Location label, image, and detail
                Util.AddTextLabelWithImageView (yOffset, "LOCATION", "event-location", EventViewController.TagType.EVENT_LOCATION_TITLE_TAG, this);
                yOffset += 16 + 6;
                Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_LOCATION_DETAIL_LABEL_TAG, this);
                yOffset += 20 + 20;
                this.ViewWithTag ((int)EventViewController.TagType.EVENT_LOCATION_TITLE_TAG).Hidden = false;
                var locationLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_LOCATION_DETAIL_LABEL_TAG) as UILabel;
                locationLabel.Hidden = false;
                locationLabel.Text = location;
                locationLabel.Lines = 0;
                locationLabel.LineBreakMode = UILineBreakMode.WordWrap;
                locationLabel.SizeToFit ();
            }

            var accountId = parentMessage.AccountId;

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meetingInfo.Organizer);
            if (null != organizerAddress) {

                organizerEmail = organizerAddress.Address;
                var organizerName = organizerAddress.Name;

                if (null != organizerEmail) {
                    // Organizer
                    var eventOrganizerView = new UIView (new CGRect (0, yOffset, viewWidth, 44 + 16 + 16));
                    eventOrganizerView.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_VIEW_TAG;
                    eventOrganizerView.BackgroundColor = UIColor.White;
                    this.AddSubview (eventOrganizerView);

                    Util.AddTextLabelWithImageView (0, "ORGANIZER", "event-organizer", EventViewController.TagType.EVENT_ORGANIZER_TITLE_TAG, eventOrganizerView);

                    nfloat emailOffset = 46f;

                    if (null != organizerName) {
                        // Organizer Name
                        var userNameLabel = new UILabel (new CGRect (92, 16 + 10, eventOrganizerView.Frame.Width - 92 - 18, 20));
                        userNameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
                        userNameLabel.TextColor = UIColor.LightGray;
                        userNameLabel.Font = A.Font_AvenirNextRegular14;
                        userNameLabel.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_NAME_LABEL;
                        userNameLabel.Text = organizerName;
                        eventOrganizerView.AddSubview (userNameLabel);
                    } else {
                        emailOffset = (eventOrganizerView.Frame.Height / 2) - 3;
                    }

                    var userEmailLabel = new UILabel (new CGRect (92, emailOffset, eventOrganizerView.Frame.Width - 92 - 18, 20));
                    userEmailLabel.LineBreakMode = UILineBreakMode.TailTruncation;
                    userEmailLabel.TextColor = UIColor.LightGray;
                    userEmailLabel.Font = A.Font_AvenirNextRegular14;
                    userEmailLabel.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_EMAIL_LABEL;
                    userEmailLabel.Text = organizerEmail;
                    eventOrganizerView.AddSubview (userEmailLabel);

                    var userImage = Util.ImageOfSender (accountId, organizerEmail);

                    if (null != userImage) {
                        using (var rawImage = userImage) {
                            using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                                // User image
                                var userImageView = new UIImageView (new CGRect (42, 10 + 16, 40, 40));
                                userImageView.Layer.CornerRadius = (40.0f / 2.0f);
                                userImageView.Layer.MasksToBounds = true;
                                userImageView.Image = originalImage;
                                userImageView.Layer.BorderWidth = .25f;
                                userImageView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                                eventOrganizerView.AddSubview (userImageView);
                            }
                        }
                    } else {

                        // User userLabelView view, if no image
                        var userLabelView = new UILabel (new CGRect (42, 10 + 16, 40, 40));
                        userLabelView.Font = A.Font_AvenirNextRegular17;
                        userLabelView.BackgroundColor = Util.GetCircleColorForEmail (organizerEmail, accountId);
                        userLabelView.TextColor = UIColor.White;
                        userLabelView.TextAlignment = UITextAlignment.Center;
                        userLabelView.LineBreakMode = UILineBreakMode.Clip;
                        userLabelView.Layer.CornerRadius = (40 / 2);
                        userLabelView.Layer.MasksToBounds = true;
                        var nameString = (null != organizerName ? organizerName : organizerEmail);
                        userLabelView.Text = Util.NameToLetters (nameString);
                        eventOrganizerView.AddSubview (userLabelView);
                    }

                    yOffset += 44 + 20 + 16;
                }
            }

            // Only display the attendees when it is a meeting request
            if ("IPM.Schedule.Meeting.Request" == parentMessage.MessageClass) {

                // Attendees label, image, and detail
                var eventAttendeeView = new UIView (new CGRect (0, yOffset, viewWidth, 96 + 16));
                eventAttendeeView.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_VIEW_TAG;
                Util.AddTextLabelWithImageView (0, "ATTENDEES", "event-attendees", EventViewController.TagType.EVENT_ATTENDEES_TITLE_TAG, eventAttendeeView);
                this.AddSubview (eventAttendeeView);

                yOffset += 96 + 20;

                eventAttendeeView.BackgroundColor = UIColor.White;
                Util.AddTextLabelWithImageView (0, "ATTENDEES", "event-attendees", EventViewController.TagType.EVENT_ATTENDEES_TITLE_TAG, eventAttendeeView);
                var titleOffset = 16;
                var attendeeImageDiameter = 40;
                var iconSpace = viewWidth - 60;
                var iconPadding = (iconSpace - (attendeeImageDiameter * 5)) / 4;
                nfloat spacing = 0;
                int attendeeNum = 0;
                var allAttendees = NcEmailAddress.ParseAddressListString (Pretty.Join (parentMessage.To, parentMessage.Cc, ", "));
                foreach (var attendeeAddress in allAttendees) {
                    var attendeeMailbox = attendeeAddress as MailboxAddress;
                    var attendee = new McAttendee ();
                    attendee.AccountId = accountId;
                    attendee.Name = attendeeAddress.Name;
                    attendee.Email = null == attendeeMailbox ? null : attendeeMailbox.Address;
                    Util.CreateAttendeeButton (attendeeImageDiameter, spacing, titleOffset, attendee, attendeeNum, false, eventAttendeeView);

                    spacing += (attendeeImageDiameter + iconPadding);
                    if (4 <= ++attendeeNum && 5 < allAttendees.Count) {
                        // There is room for four attendees in the view.  If the meeting
                        // has more than five attendees, only show four of them and save
                        // the last slot for showing the number of extra attendees.
                        break;
                    }
                }
                if (5 < allAttendees.Count) {
                    var extraAttendeesButton = new UIButton (UIButtonType.RoundedRect);
                    extraAttendeesButton.Layer.CornerRadius = attendeeImageDiameter / 2;
                    extraAttendeesButton.Layer.MasksToBounds = true;
                    extraAttendeesButton.Layer.BorderColor = A.Color_NachoGreen.CGColor;
                    extraAttendeesButton.Layer.BorderWidth = 1;
                    extraAttendeesButton.Frame = new CGRect (42 + iconSpace - 39, 10 + titleOffset, attendeeImageDiameter, attendeeImageDiameter);
                    extraAttendeesButton.Font = A.Font_AvenirNextRegular14;
                    extraAttendeesButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
                    extraAttendeesButton.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_DETAIL_TAG;
                    extraAttendeesButton.SetTitle (string.Format ("+{0}", allAttendees.Count - 4), UIControlState.Normal);
                    extraAttendeesButton.AccessibilityLabel = "More";
                    eventAttendeeView.AddSubview (extraAttendeesButton);
                }
            }

            Util.AddHorizontalLine (0, yOffset, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
            this.Frame = new CGRect (this.Frame.X, this.Frame.Y, this.Frame.Width, yOffset + 20);
        }

        UIButton acceptButton;
        UIButton tentativeButton;
        UIButton declineButton;

        UILabel acceptLabel;
        UILabel tentativeLabel;
        UILabel declineLabel;

        UILabel messageLabel;
        UIButton removeFromCalendarButton;
        UIImageView dotView;


        /// <summary>
        /// Create all of the UI elements that might appear on the action bar.
        /// All of them are marked hidden for now.
        /// </summary>
        private void CreateActionBarViews (UIView responseView)
        {
            acceptButton = new UIButton (UIButtonType.RoundedRect);
            acceptButton.AccessibilityLabel = "Attend";
            Util.AddButtonImage (acceptButton, "event-attend", UIControlState.Normal);
            Util.AddButtonImage (acceptButton, "event-attend-active", UIControlState.Selected);
            acceptButton.Frame = new CGRect (responseView.Frame.X + 18, 18, 24, 24);
            acceptButton.TintColor = UIColor.Clear;
            acceptButton.Hidden = true;
            responseView.AddSubview (acceptButton);

            tentativeButton = new UIButton (UIButtonType.RoundedRect);
            tentativeButton.AccessibilityLabel = "Maybe";
            Util.AddButtonImage (tentativeButton, "event-maybe", UIControlState.Normal);
            Util.AddButtonImage (tentativeButton, "event-maybe-active", UIControlState.Selected);
            tentativeButton.Frame = new CGRect (responseView.Center.X - 37.5f, 18, 24, 24);
            tentativeButton.TintColor = UIColor.Clear;
            tentativeButton.Hidden = true;
            responseView.AddSubview (tentativeButton);

            declineButton = new UIButton (UIButtonType.RoundedRect);
            declineButton.AccessibilityLabel = "Decline";
            Util.AddButtonImage (declineButton, "event-decline", UIControlState.Normal);
            Util.AddButtonImage (declineButton, "event-decline-active", UIControlState.Selected);
            declineButton.Frame = new CGRect (responseView.Frame.Width - 96.5f, 18, 24, 24);
            declineButton.TintColor = UIColor.Clear;
            declineButton.Hidden = true;
            responseView.AddSubview (declineButton);

            acceptLabel = new UILabel (new CGRect (acceptButton.Frame.X + 24 + 6, 20, 45, 20));
            acceptLabel.TextColor = A.Color_NachoDarkText;
            acceptLabel.Font = A.Font_AvenirNextMedium14;
            acceptLabel.Text = "Attend";
            acceptLabel.Hidden = true;
            responseView.AddSubview (acceptLabel);

            tentativeLabel = new UILabel (new CGRect (tentativeButton.Frame.X + 24 + 6, 20, 45, 20));
            tentativeLabel.TextColor = A.Color_NachoDarkText;
            tentativeLabel.Font = A.Font_AvenirNextMedium14;
            tentativeLabel.Text = "Maybe";
            tentativeLabel.Hidden = true;
            responseView.AddSubview (tentativeLabel);

            declineLabel = new UILabel (new CGRect (declineButton.Frame.X + 24 + 6, 20, 50, 20));
            declineLabel.TextColor = A.Color_NachoDarkText;
            declineLabel.Font = A.Font_AvenirNextMedium14;
            declineLabel.Text = "Decline";
            declineLabel.Hidden = true;
            responseView.AddSubview (declineLabel);

            nfloat messageX = 18 + 24 + 10;
            messageLabel = new UILabel (new CGRect (messageX, 18, viewWidth - messageX, 24));
            messageLabel.TextColor = A.Color_NachoBlack;
            messageLabel.TextAlignment = UITextAlignment.Left;
            messageLabel.Font = A.Font_AvenirNextRegular12;
            messageLabel.Hidden = true;
            responseView.AddSubview (messageLabel);

            dotView = new UIImageView ();
            dotView.Frame = new CGRect (21, 25, 10, 10);
            dotView.Hidden = true;
            responseView.Add (dotView);

            removeFromCalendarButton = new UIButton (UIButtonType.RoundedRect);
            removeFromCalendarButton.SetTitle ("Remove from calendar", UIControlState.Normal);
            removeFromCalendarButton.AccessibilityLabel = "Remove from calendar";
            removeFromCalendarButton.Font = A.Font_AvenirNextRegular12;
            removeFromCalendarButton.SizeToFit ();
            removeFromCalendarButton.Frame = new CGRect (18 + 24 + 10, 19, removeFromCalendarButton.Frame.Width, 24);
            removeFromCalendarButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            removeFromCalendarButton.Hidden = true;
            responseView.Add (removeFromCalendarButton);
        }

        /// <summary>
        /// Show the action bar for a meeting request, with the "Accept", "Tentative", and "Decline" buttons.
        /// </summary>
        private void ShowRequestChoicesBar (bool isOnHot)
        {
            UIView responseView = new UIView (new CGRect (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.White;

            CreateActionBarViews (responseView);

            if (isOnHot) {

                // This is something other than the message detail view, probably the Hot view.
                // Don't show all three buttons.  Instead, show a message with either a
                // non-clickable button or a dot.

                string message = "You have not responded to this invitation.";
                UIButton displayedButton = null;
                if (null != calendarItem && calendarItem.ResponseTypeIsSet) {
                    switch (calendarItem.ResponseType) {
                    case NcResponseType.Accepted:
                        message = "You accepted the meeting.";
                        displayedButton = acceptButton;
                        break;
                    case NcResponseType.Tentative:
                        message = "You might attend the meeting.";
                        displayedButton = tentativeButton;
                        break;
                    case NcResponseType.Declined:
                        message = "You declined the meeting.";
                        displayedButton = declineButton;
                        break;
                    }
                }
                messageLabel.Text = message;
                messageLabel.Hidden = false;
                if (null == displayedButton) {
                    messageLabel.Frame = MessageFrameWithDot ();
                    dotView.Image = ColoredDotImage (A.Color_NachoSwipeActionGreen);
                    dotView.Hidden = false;
                } else {
                    displayedButton.Frame = SingleButtonFrame ();
                    displayedButton.Selected = true;
                    displayedButton.UserInteractionEnabled = false;
                    displayedButton.Hidden = false;
                }
            } else {

                // Message detail view.  Show all three buttons.  If the user has already responded,
                // one of the buttons will be selected.

                acceptButton.Hidden = false;
                acceptLabel.Hidden = false;
                tentativeButton.Hidden = false;
                tentativeLabel.Hidden = false;
                declineButton.Hidden = false;
                declineLabel.Hidden = false;

                requestActions = true;
                acceptButton.TouchUpInside += AcceptButtonClicked;
                tentativeButton.TouchUpInside += TentativeButtonClicked;
                declineButton.TouchUpInside += DeclineButtonClicked;

                if (null != calendarItem && calendarItem.ResponseTypeIsSet) {
                    MarkSelectedButton (calendarItem.ResponseType);
                }
            }

            this.AddSubview (responseView);
        }

        /// <summary>
        /// One of the "Accept", "Tentative", or "Decline" buttons has been touched.
        /// Adjust the UI accordingly.
        /// </summary>
        protected void MarkSelectedButton (NcResponseType r)
        {
            bool isAccepted = NcResponseType.Accepted == r;
            acceptButton.Selected = isAccepted;
            acceptButton.UserInteractionEnabled = !isAccepted;

            bool isTentative = NcResponseType.Tentative == r;
            tentativeButton.Selected = isTentative;
            tentativeButton.UserInteractionEnabled = !isTentative;

            bool isDeclined = NcResponseType.Declined == r;
            declineButton.Selected = isDeclined;
            declineButton.UserInteractionEnabled = !isDeclined;
        }

        /// <summary>
        /// Update the user's status for the meeting and send a meeting response
        /// message to the organizer.
        /// </summary>
        private void UpdateMeetingStatus (NcResponseType status)
        {
            BackEnd.Instance.RespondEmailCmd (parentMessage.AccountId, parentMessage.Id, status);

            McAccount account = McAccount.QueryById<McAccount> (parentMessage.AccountId);

            if (null != organizerEmail && meetingInfo.ResponseRequested) {

                var iCalPart = CalendarHelper.MimeResponseFromEmail (meetingInfo, status, parentMessage.Subject, meetingInfo.RecurrenceId);
                // TODO Give the user a chance to enter some text. For now, the message body is empty.
                var mimeBody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                CalendarHelper.SendMeetingResponse (account, NcEmailAddress.ParseMailboxAddressString(meetingInfo.Organizer), parentMessage.Subject, null, mimeBody, status);
            }
        }

        /// <summary>
        /// Show the action bar for a meeting response.  The bar doesn't have any
        /// action; it just shows the status of the person who responded.
        /// </summary>
        private void ShowAttendeeResponseBar ()
        {
            UIView responseView = new UIView (new CGRect (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.Clear;

            CreateActionBarViews (responseView);

            UIButton displayedButton = null;
            string messageFormat;
            switch (parentMessage.MessageClass) {
            case "IPM.Schedule.Meeting.Resp.Pos":
                displayedButton = acceptButton;
                messageFormat = "{0} has accepted the meeting.";
                break;
            case "IPM.Schedule.Meeting.Resp.Tent":
                displayedButton = tentativeButton;
                messageFormat = "{0} has tentatively accepted the meeting.";
                break;
            case "IPM.Schedule.Meeting.Resp.Neg":
                displayedButton = declineButton;
                messageFormat = "{0} has declined the meeting.";
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unkown meeting response status: {0}", parentMessage.MessageClass);
                messageFormat = "The status of {0} is unknown.";
                break;
            }

            string displayName;
            var responder = NcEmailAddress.ParseMailboxAddressString (parentMessage.From);
            if (null == responder) {
                displayName = parentMessage.From;
            } else if (!string.IsNullOrEmpty (responder.Name)) {
                displayName = responder.Name;
            } else {
                displayName = responder.Address;
            }

            if (null != displayedButton) {
                displayedButton.Hidden = false;
                displayedButton.Selected = true;
                displayedButton.UserInteractionEnabled = false;
                displayedButton.Frame = SingleButtonFrame ();
            }

            messageLabel.Hidden = false;
            messageLabel.Text = String.Format (messageFormat, displayName);

            this.AddSubview (responseView);
        }

        /// <summary>
        /// Show the action bar for a meeting cancellation, which has a
        /// "Remove from calendar" button.
        /// </summary>
        private void ShowCancellationBar (bool isOnHot)
        {
            UIView responseView = new UIView (new CGRect (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.Clear;

            CreateActionBarViews (responseView);

            if (isOnHot || null == calendarItem) {

                messageLabel.Text = "The meeting has been canceled.";
                messageLabel.Frame = MessageFrameWithDot ();
                messageLabel.Hidden = false;
                dotView.Hidden = false;
                dotView.Image = ColoredDotImage (A.Color_NachoSwipeEmailDelete);

            } else {

                // Let the user click either the red cirle X or the words "Remove from calendar."
                // They both do the same thing.

                cancelActions = true;

                declineButton.Hidden = false;
                declineButton.Selected = false;
                declineButton.Frame = SingleButtonFrame ();
                declineButton.TouchUpInside += RemoveFromCalendarClicked;

                removeFromCalendarButton.Hidden = false;
                removeFromCalendarButton.TouchUpInside += RemoveFromCalendarClicked;
            }

            this.AddSubview (responseView);
        }

        /// <summary>
        /// The location of the message when it is next to a dot rather than a full button.
        /// </summary>
        private CGRect MessageFrameWithDot ()
        {
            return new CGRect (42, 18, viewWidth - 42, 24);
        }

        /// <summary>
        /// The location for a button when only one button is being shown.
        /// </summary>
        private CGRect SingleButtonFrame ()
        {
            return new CGRect (18, 18, 24, 24);
        }

        /// <summary>
        /// Create a 10-point dot with the given color.
        /// </summary>
        private UIImage ColoredDotImage (UIColor color)
        {
            return Util.DrawCalDot (color, new CGSize (10, 10));
        }

        /// <summary>
        /// The "Remove from calendar" button has been touched. Adjust the UI, and remove
        /// the calendar entry.
        /// </summary>
        private void RemoveFromCalendarClicked (object sender, EventArgs e)
        {
            // Handle the UI changes.
            declineButton.UserInteractionEnabled = false;
            removeFromCalendarButton.UserInteractionEnabled = false;

            messageLabel.Text = "The meeting has been canceled.";
            messageLabel.Frame = MessageFrameWithDot ();
            messageLabel.Hidden = false;
            dotView.Image = ColoredDotImage (A.Color_NachoSwipeEmailDelete);

            messageLabel.Alpha = 0;
            messageLabel.Hidden = false;
            dotView.Alpha = 0;
            dotView.Hidden = false;

            UIView.Animate (0.2, 0, UIViewAnimationOptions.CurveLinear,
                () => {
                    declineButton.Alpha = 0;
                    removeFromCalendarButton.Alpha = 0;
                    messageLabel.Alpha = 1;
                    dotView.Alpha = 1;
                },
                () => {
                    declineButton.Hidden = true;
                    removeFromCalendarButton.Hidden = true;
                });

            // Remove the item from the calendar.
            BackEnd.Instance.DeleteCalCmd (calendarItem.AccountId, calendarItem.Id);
        }

        protected override void Dispose (bool disposing)
        {
            if (requestActions) {
                acceptButton.TouchUpInside -= AcceptButtonClicked;
                tentativeButton.TouchUpInside -= TentativeButtonClicked;
                declineButton.TouchUpInside -= DeclineButtonClicked;
            }
            if (cancelActions) {
                declineButton.TouchUpInside -= RemoveFromCalendarClicked;
                removeFromCalendarButton.TouchUpInside -= RemoveFromCalendarClicked;
            }
            base.Dispose (disposing);
        }

        private void AcceptButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Accepted);
            UpdateMeetingStatus (NcResponseType.Accepted);
        }

        private void TentativeButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Tentative);
            UpdateMeetingStatus (NcResponseType.Tentative);
        }

        private void DeclineButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Declined);
            UpdateMeetingStatus (NcResponseType.Declined);
        }
    }
}

