//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MonoTouch.UIKit;

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

        private McCalendar calendarItem;
        private bool requestActions = false;
        private bool cancelActions = false;
        private float viewWidth;
        private string organizerEmail;

        public BodyCalendarView (float Y, float width, MimePart part, bool isOnNow)
            : base (new RectangleF (0, Y, width, 150))
        {
            viewWidth = width;
            Tag = CALENDAR_PART_TAG;

            var textPart = part as TextPart;
            IICalendar iCal;
            using (var stringReader = new StringReader (textPart.Text)) {
                iCal = iCalendar.LoadFromStream (stringReader) [0];
            }
            var evt = iCal.Events.First () as DDay.iCal.Event;

            if (null == evt) {
                // The text/calendar part doesn't contain any events. There is nothing to show.
                ViewFramer.Create (this).Height (1);
                return;
            }

            if (null != evt.UID) {
                // TODO: Do McExceptions have their own status?
                calendarItem = McCalendar.QueryByUID (evt.UID);
            }

            // The contents of the action/info bar depends on whether this is a request,
            // response, or cancellation.
            if (iCal.Method.Equals (DDay.iCal.CalendarMethods.Reply)) {
                ShowAttendeeResponseBar (evt);
            } else if (iCal.Method.Equals (DDay.iCal.CalendarMethods.Cancel)) {
                ShowCancellationBar (evt, isOnNow);
            } else {
                if (!iCal.Method.Equals (DDay.iCal.CalendarMethods.Request)) {
                    Log.Warn (Log.LOG_CALENDAR, "Unexpected calendar method: {0}. It will be treated as a {1}.",
                        iCal.Method, DDay.iCal.CalendarMethods.Request);
                }
                ShowRequestChoicesBar (evt, isOnNow);
            }

            ShowEventInfo (evt);
        }

        public UIView uiView ()
        {
            return this;
        }

        public SizeF ContentSize {
            get {
                return Frame.Size;
            }
        }

        public void ScrollingAdjustment (RectangleF frame, PointF contentOffset)
        {
            // The calendar section does not scroll or resize.
            // The only thing that can be adjusted is the view's origin.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y);
        }

        /// <summary>
        /// Display the basic information about the calendar event.
        /// </summary>
        private void ShowEventInfo (DDay.iCal.Event evt)
        {
            string subject = evt.Summary;
            bool isAllDay = evt.IsAllDay;
            DateTime start = CalendarHelper.EventStartTime (evt, calendarItem);
            DateTime end = CalendarHelper.EventEndTime (evt, calendarItem);
            string location = evt.Location;
            var yOffset = 60f + 18f;

            Util.AddHorizontalLine (0, 60, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;

            UILabel titleLabel = new UILabel (new RectangleF (74, 27, viewWidth - 89, 20));
            titleLabel.Tag = (int)TagType.CALENDAR_TITLE_TAG;
            titleLabel.Font = A.Font_AvenirNextDemiBold14;
            titleLabel.TextColor = A.Color_NachoBlack;
            titleLabel.TextAlignment = UITextAlignment.Left;
            titleLabel.Text = subject;
            titleLabel.SizeToFit ();

            // When label, image, and detail
            Util.AddTextLabelWithImageView (yOffset, "WHEN", "event-when", EventViewController.TagType.EVENT_WHEN_TITLE_TAG, this);
            yOffset += 16 + 6;
            Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_WHEN_DETAIL_LABEL_TAG, this);
            yOffset += 20;
            Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_WHEN_DURATION_TAG, this);

            var whenLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_WHEN_DETAIL_LABEL_TAG) as UILabel;
            whenLabel.Text = Pretty.ExtendedDateString (start);
            var durationLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_WHEN_DURATION_TAG) as UILabel;
            if (isAllDay) {
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

            yOffset += Math.Max (20, durationLabel.Frame.Height);
            //            Util.AddDetailTextLabel (42, yOffset, viewWidth - 90, 20, EventViewController.TagType.EVENT_WHEN_RECURRENCE_TAG, this);
            //            yOffset += 20 + 20;
            yOffset += 20;

            //            if (evt.re) {
            //                var recurrenceLabel = View.ViewWithTag ((int)EventViewController.TagType.EVENT_WHEN_RECURRENCE_TAG) as UILabel;
            //                recurrenceLabel.Text = Pretty.MakeRecurrenceString (root.recurrences);
            //                recurrenceLabel.Lines = 0;
            //                recurrenceLabel.LineBreakMode = UILineBreakMode.WordWrap;
            //                recurrenceLabel.SizeToFit ();
            //yOffset += Math.Max (20, recurrenceLabel.Frame.Height);
            //            }

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

            //            Util.AddTextLabelWithImageView (yOffset, "REMINDER", "event-reminder", EventViewController.TagType.EVENT_ALERT_TITLE_TAG, this);
            //            yOffset += 16 + 6;
            //            Util.AddDetailTextLabel (42, yOffset, viewWidth - 84 - 18, 20, EventViewController.TagType.EVENT_ALERT_DETAIL_TAG, this);
            //            yOffset += 20 + 20;
            //
            //            var alertDetailLabel = this.ViewWithTag ((int)EventViewController.TagType.EVENT_ALERT_DETAIL_TAG) as UILabel;
            //            alertDetailLabel.Text = Pretty.ReminderString (true, evt.RecurrenceDates);
            //            alertDetailLabel.SizeToFit ();

            var accountId = LoginHelpers.GetCurrentAccountId ();

            if (null != evt.Organizer) {
                if (Uri.UriSchemeMailto == evt.Organizer.Value.Scheme) {
                    organizerEmail = evt.Organizer.Value.AbsoluteUri.Substring (Uri.UriSchemeMailto.Length + 1);
                } else {
                    organizerEmail = evt.Organizer.Value.ToString ();
                }
                var organizerName = evt.Organizer.CommonName.ToString ();

                if (null != organizerEmail) {
                    // Organizer
                    var eventOrganizerView = new UIView (new RectangleF (0, yOffset, viewWidth, 44 + 16 + 16));
                    eventOrganizerView.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_VIEW_TAG;
                    eventOrganizerView.BackgroundColor = UIColor.White;
                    this.AddSubview (eventOrganizerView);

                    //Util.AddArrowAccessory (eventOrganizerView.Frame.Width - 18 - 12, 16 + 20, 12, eventOrganizerView);

                    Util.AddTextLabelWithImageView (0, "ORGANIZER", "event-organizer", EventViewController.TagType.EVENT_ORGANIZER_TITLE_TAG, eventOrganizerView);

                    var emailOffset = 46f;

                    if (null != organizerName) {
                        // Organizer Name
                        var userNameLabel = new UILabel (new RectangleF (92, 16 + 10, eventOrganizerView.Frame.Width - 92 - 18, 20));
                        userNameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
                        userNameLabel.TextColor = UIColor.LightGray;
                        userNameLabel.Font = A.Font_AvenirNextRegular14;
                        userNameLabel.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_NAME_LABEL;
                        userNameLabel.Text = organizerName;
                        eventOrganizerView.AddSubview (userNameLabel);
                    } else {
                        emailOffset = (eventOrganizerView.Frame.Height / 2) - 3;
                    }

                    var userEmailLabel = new UILabel (new RectangleF (92, emailOffset, eventOrganizerView.Frame.Width - 92 - 18, 20));
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
                                var userImageView = new UIImageView (new RectangleF (42, 10 + 16, 40, 40));
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
                        var userLabelView = new UILabel (new RectangleF (42, 10 + 16, 40, 40));
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

                    //            organizerTapGestureRecognizer = new UITapGestureRecognizer ();
                    //            organizerTapGestureRecognizerTapToken = organizerTapGestureRecognizer.AddTarget (OrganizerTapGestureRecognizerTap);
                    //            eventOrganizerView.AddGestureRecognizer (organizerTapGestureRecognizer);
                    //            eventCardView.AddSubview (eventOrganizerView);

                    yOffset += 44 + 20 + 16;
                }
            }


            if (0 != evt.Attendees.Count) {
                // Attendees label, image, and detail
                var eventAttendeeView = new UIView (new RectangleF (0, yOffset, viewWidth, 96 + 16));
                eventAttendeeView.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_VIEW_TAG;
                //            attendeeTapGestureRecognizer = new UITapGestureRecognizer ();
                //            attendeeTapGestureRecognizerTapToken = attendeeTapGestureRecognizer.AddTarget (AttendeeTapGestureRecognizerTap);
                //            eventAttendeeView.AddGestureRecognizer (attendeeTapGestureRecognizer);
                Util.AddTextLabelWithImageView (0, "ATTENDEES", "event-attendees", EventViewController.TagType.EVENT_ATTENDEES_TITLE_TAG, eventAttendeeView);
                this.AddSubview (eventAttendeeView);

                yOffset += 96 + 20;

                //            if (null != extraAttendeesButton) {
                //                extraAttendeesButton.TouchUpInside -= ExtraAttendeesTouchUpInside;
                //            }
                eventAttendeeView.BackgroundColor = UIColor.White;
                Util.AddTextLabelWithImageView (0, "ATTENDEES", "event-attendees", EventViewController.TagType.EVENT_ATTENDEES_TITLE_TAG, eventAttendeeView);
                //Util.AddArrowAccessory (eventAttendeeView.Frame.Width - 18 - 12, 16 + 20, 12, eventAttendeeView);
                var titleOffset = 16;
                var attendeeImageDiameter = 40;
                var iconSpace = viewWidth - 60;
                var iconPadding = (iconSpace - (attendeeImageDiameter * 5)) / 4;
                float spacing = 0;
                int attendeeNum = 0;
                foreach (var a in evt.Attendees) {
                    var attendee = new McAttendee ();
                    attendee.AccountId = accountId;
                    attendee.Name = a.CommonName;
                    string aEmail;
                    if (Uri.UriSchemeMailto == a.Value.Scheme) {
                        aEmail = a.Value.AbsoluteUri.Substring (Uri.UriSchemeMailto.Length + 1);
                    } else {
                        aEmail = a.Value.ToString ();
                    }
                    attendee.Email = aEmail;
                    Util.CreateAttendeeButton (attendeeImageDiameter, spacing, titleOffset, attendee, attendeeNum, false, eventAttendeeView);

                    spacing += (attendeeImageDiameter + iconPadding);
                    if (4 <= ++attendeeNum && 5 < evt.Attendees.Count) {
                        // There is room for four attendees in the view.  If the meeting
                        // has more than five attendees, only show four of them and save
                        // the last slot for showing the number of extra attendees.
                        break;
                    }
                }
                if (4 < evt.Attendees.Count) {
                    var extraAttendeesButton = new UIButton (UIButtonType.RoundedRect);
                    extraAttendeesButton.Layer.CornerRadius = attendeeImageDiameter / 2;
                    extraAttendeesButton.Layer.MasksToBounds = true;
                    extraAttendeesButton.Layer.BorderColor = A.Color_NachoGreen.CGColor;
                    extraAttendeesButton.Layer.BorderWidth = 1;
                    extraAttendeesButton.Frame = new RectangleF (42 + iconSpace - 39, 10 + titleOffset, attendeeImageDiameter, attendeeImageDiameter);
                    extraAttendeesButton.Font = A.Font_AvenirNextRegular14;
                    extraAttendeesButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
                    extraAttendeesButton.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_DETAIL_TAG;
                    extraAttendeesButton.SetTitle (string.Format ("+{0}", evt.Attendees.Count - 4), UIControlState.Normal);
                    //extraAttendeesButton.TouchUpInside += ExtraAttendeesTouchUpInside;
                    eventAttendeeView.AddSubview (extraAttendeesButton);
                }
            }

            Util.AddHorizontalLine (0, yOffset, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
            this.Frame = new RectangleF (this.Frame.X, this.Frame.Y, this.Frame.Width, yOffset + 20);
        }

        UIButton acceptButton;
        UIButton tentativeButton;
        UIButton declineButton;

        UILabel acceptLabel;
        UILabel tentativeLabel;
        UILabel declineLabel;

        UILabel messageLabel;
        UIButton changeResponseButton;
        UIButton removeFromCalendarButton;
        UIImageView dotView;


        /// <summary>
        /// Configuration of some of the subview elements of the action bar that are
        /// common to at least two of the forms of the action bar.
        /// </summary>
        private void ActionBarCommon (UIView responseView)
        {
            acceptButton = new UIButton (UIButtonType.RoundedRect);
            Util.AddButtonImage (acceptButton, "event-attend", UIControlState.Normal);
            Util.AddButtonImage (acceptButton, "event-attend-active", UIControlState.Selected);
            acceptButton.Frame = new RectangleF (responseView.Frame.X + 18, 18, 24, 24);
            acceptButton.TintColor = UIColor.Clear;
            responseView.AddSubview (acceptButton);

            tentativeButton = new UIButton (UIButtonType.RoundedRect);
            Util.AddButtonImage (tentativeButton, "event-maybe", UIControlState.Normal);
            Util.AddButtonImage (tentativeButton, "event-maybe-active", UIControlState.Selected);
            tentativeButton.Frame = new RectangleF (responseView.Center.X - 37.5f, 18, 24, 24);
            tentativeButton.TintColor = UIColor.Clear;
            responseView.AddSubview (tentativeButton);

            declineButton = new UIButton (UIButtonType.RoundedRect);
            Util.AddButtonImage (declineButton, "event-decline", UIControlState.Normal);
            Util.AddButtonImage (declineButton, "event-decline-active", UIControlState.Selected);
            declineButton.Frame = new RectangleF (responseView.Frame.Width - 96.5f, 18, 24, 24);
            declineButton.TintColor = UIColor.Clear;
            responseView.AddSubview (declineButton);

            acceptLabel = new UILabel (new RectangleF (acceptButton.Frame.X + 24 + 6, 20, 45, 20));
            acceptLabel.TextColor = A.Color_NachoDarkText;
            acceptLabel.Font = A.Font_AvenirNextMedium14;
            acceptLabel.Text = "Attend";
            responseView.AddSubview (acceptLabel);

            tentativeLabel = new UILabel (new RectangleF (tentativeButton.Frame.X + 24 + 6, 20, 45, 20));
            tentativeLabel.TextColor = A.Color_NachoDarkText;
            tentativeLabel.Font = A.Font_AvenirNextMedium14;
            tentativeLabel.Text = "Maybe";
            responseView.AddSubview (tentativeLabel);

            declineLabel = new UILabel (new RectangleF (declineButton.Frame.X + 24 + 6, 20, 50, 20));
            declineLabel.TextColor = A.Color_NachoDarkText;
            declineLabel.Font = A.Font_AvenirNextMedium14;
            declineLabel.Text = "Decline";
            responseView.AddSubview (declineLabel);

            float messageX = 18 + 24 + 10;
            messageLabel = new UILabel (new RectangleF (messageX, 18, viewWidth - messageX, 24));
            messageLabel.TextColor = A.Color_NachoBlack;
            messageLabel.TextAlignment = UITextAlignment.Left;
            messageLabel.Font = A.Font_AvenirNextRegular12;
            messageLabel.Hidden = true;
            responseView.AddSubview (messageLabel);

            dotView = new UIImageView ();
            dotView.Frame = new RectangleF (21, 25, 10, 10);
            dotView.Hidden = true;
            responseView.Add (dotView);
        }

        /// <summary>
        /// Show the action bar for a meeting request, with the "Accept", "Tentative", and "Decline" buttons.
        /// </summary>
        private void ShowRequestChoicesBar (DDay.iCal.Event evt, bool isOnNow)
        {
            UIView responseView = new UIView (new RectangleF (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.White;

            ActionBarCommon (responseView);

            requestActions = true;

            changeResponseButton = new UIButton (UIButtonType.RoundedRect);
            changeResponseButton.SetTitle ("Change response", UIControlState.Normal);
            changeResponseButton.Font = A.Font_AvenirNextRegular12;
            changeResponseButton.SizeToFit ();
            changeResponseButton.Frame = new RectangleF (viewWidth - changeResponseButton.Frame.Width - 18, 18, changeResponseButton.Frame.Width, 24);
            changeResponseButton.SetTitleColor (UIColor.LightGray, UIControlState.Normal);
            changeResponseButton.Hidden = true;
            changeResponseButton.TouchUpInside += ChangeResponseButtonClicked;
            responseView.Add (changeResponseButton);

            acceptButton.TouchUpInside += AcceptButtonClicked;
            tentativeButton.TouchUpInside += TentativeButtonClicked;
            declineButton.TouchUpInside += DeclineButtonClicked;

            if (null != calendarItem) {
                if (isOnNow && NcResponseType.NotResponded == calendarItem.ResponseType) {
                    messageLabel.Text = "You have not responded to this invite";
                    messageLabel.Frame = new RectangleF (42, 18, viewWidth - 42, 24);
                    messageLabel.Hidden = false;
                    dotView.Hidden = false;
                    dotView.Image = Util.DrawCalDot (A.Color_NachoSwipeActionGreen, new SizeF (10, 10));
                    acceptButton.Hidden = true;
                    acceptLabel.Hidden = true;
                    tentativeButton.Hidden = true;
                    tentativeLabel.Hidden = true;
                    declineButton.Hidden = true;
                    declineLabel.Hidden = true;
                }
            }

            if (null != calendarItem) {

                if (calendarItem.ResponseTypeIsSet && NcResponseType.Organizer == calendarItem.ResponseType
                    || (NcMeetingStatus.Appointment == calendarItem.MeetingStatus) || (NcMeetingStatus.Meeting == calendarItem.MeetingStatus)) {
                    // The organizer doesn't normally get a meeting request.
                    // I'm not sure if this will ever happen.
                    messageLabel.Hidden = false;
                    messageLabel.Text = "You are the organizer";
                    acceptButton.Hidden = false;
                    acceptButton.UserInteractionEnabled = false;
                    acceptButton.Selected = true;
                    acceptLabel.Hidden = true;
                    tentativeButton.Hidden = true;
                    tentativeLabel.Hidden = true;
                    declineButton.Hidden = true;
                    declineLabel.Hidden = true;

                } else if (calendarItem.ResponseTypeIsSet) {

                    switch (calendarItem.ResponseType) {

                    case NcResponseType.Accepted:
                        acceptButton.Selected = true;
                        if (isOnNow) {
                            acceptButton.Frame = new RectangleF (18, 18, 24, 24);
                            messageLabel.Text = "You are going";
                            messageLabel.Hidden = false;
                            acceptLabel.Hidden = true;
                            tentativeButton.Hidden = true;
                            declineButton.Hidden = true;
                            tentativeLabel.Hidden = true;
                            declineLabel.Hidden = true;
                        }
                        acceptButton.UserInteractionEnabled = false;
                        break;

                    case NcResponseType.Tentative:
                        tentativeButton.Selected = true;
                        if (isOnNow) {
                            tentativeButton.Frame = new RectangleF (18, 18, 24, 24);
                            messageLabel.Text = "Tentative";
                            messageLabel.Hidden = false;
                            acceptButton.Hidden = true;
                            acceptLabel.Hidden = true;
                            tentativeLabel.Hidden = true;
                            declineButton.Hidden = true;
                            declineLabel.Hidden = true;
                        }
                        tentativeButton.UserInteractionEnabled = false;
                        break;

                    case NcResponseType.Declined:
                        declineButton.Selected = true;
                        if (isOnNow) {
                            declineButton.Frame = new RectangleF (18, 18, 24, 24);
                            messageLabel.Text = "You are not going to this meeting";
                            messageLabel.Hidden = false;
                            acceptButton.Hidden = true;
                            acceptLabel.Hidden = true;
                            tentativeButton.Hidden = true;
                            tentativeLabel.Hidden = true;
                            declineLabel.Hidden = true;
                        }
                        declineButton.UserInteractionEnabled = false;
                        break;
                    }
                } 
            } else {
                acceptButton.Hidden = true;
                acceptLabel.Hidden = true;
                tentativeButton.Hidden = true;
                tentativeLabel.Hidden = true;
                declineButton.Hidden = true;
                declineLabel.Hidden = true;
            }

            this.AddSubview (responseView);
        }

        /// <summary>
        /// One of the "Accept", "Tentative", or "Decline" buttons has been touched.
        /// Adjust the UI accordingly.
        /// </summary>
        protected void ToggleButtons (NcResponseType r)
        {
            RestoreButtons ();
            if (NcResponseType.Accepted == r) {
                acceptButton.Selected = true;
                tentativeButton.Selected = false;
                declineButton.Selected = false;

                acceptButton.UserInteractionEnabled = false;

                //                messageLabel.Text = "You are going";
                //                messageLabel.Hidden = false;
                //                messageLabel.Alpha = 0;
                //                changeResponseButton.Hidden = false;
                //                changeResponseButton.Alpha = 0;
                //
                //                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                //                    () => {
                //                        acceptLabel.Alpha = 0;
                //                        tentativeButton.Alpha = 0;
                //                        declineButton.Alpha = 0;
                //                        tentativeLabel.Alpha = 0;
                //                        declineLabel.Alpha = 0;
                //                        messageLabel.Alpha = 1;
                //                        changeResponseButton.Alpha = 1;
                //
                //                        acceptButton.Frame = new RectangleF (18, 18, 24, 24);
                //                    },
                //                    () => {
                //                        acceptLabel.Hidden = true;
                //                        tentativeButton.Hidden = true;
                //                        declineButton.Hidden = true;
                //                        tentativeLabel.Hidden = true;
                //                        declineLabel.Hidden = true;
                //                        acceptButton.UserInteractionEnabled = false;
                //                    }
                //                );
            } else if (NcResponseType.Tentative == r) {
                acceptButton.Selected = false;
                tentativeButton.Selected = true;
                declineButton.Selected = false;

                tentativeButton.UserInteractionEnabled = false;

                // Acompli style rsvp UX
                //                messageLabel.Text = "Tentative";
                //                messageLabel.Hidden = false;
                //                messageLabel.Alpha = 0;
                //                changeResponseButton.Hidden = false;
                //                changeResponseButton.Alpha = 0;
                //                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                //                    () => {
                //                        acceptLabel.Alpha = 0;
                //                        acceptButton.Alpha = 0;
                //                        declineButton.Alpha = 0;
                //                        tentativeLabel.Alpha = 0;
                //                        declineLabel.Alpha = 0;
                //                        messageLabel.Alpha = 1;
                //                        changeResponseButton.Alpha = 1;
                //
                //                        tentativeButton.Frame = new RectangleF (18, 18, 24, 24);
                //                    },
                //                    () => {
                //                        acceptLabel.Hidden = true;
                //                        acceptButton.Hidden = true;
                //                        declineButton.Hidden = true;
                //                        tentativeLabel.Hidden = true;
                //                        declineLabel.Hidden = true;
                //                        tentativeButton.UserInteractionEnabled = false;
                //                    }
                //                );
            } else {
                acceptButton.Selected = false;
                tentativeButton.Selected = false;
                declineButton.Selected = true;

                declineButton.UserInteractionEnabled = false;


                //                messageLabel.Text = "You are not going";
                //                messageLabel.Hidden = false;
                //                messageLabel.Alpha = 0;
                //                changeResponseButton.Hidden = false;
                //                changeResponseButton.Alpha = 0;
                //
                //                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                //                    () => {
                //                        acceptLabel.Alpha = 0;
                //                        acceptButton.Alpha = 0;
                //                        tentativeButton.Alpha = 0;
                //                        tentativeLabel.Alpha = 0;
                //                        declineLabel.Alpha = 0;
                //                        messageLabel.Alpha = 1;
                //                        changeResponseButton.Alpha = 1;
                //
                //                        declineButton.Frame = new RectangleF (18, 18, 24, 24);
                //                    },
                //                    () => {
                //                        acceptLabel.Hidden = true;
                //                        acceptButton.Hidden = true;
                //                        tentativeButton.Hidden = true;
                //                        tentativeLabel.Hidden = true;
                //                        declineLabel.Hidden = true;
                //                        declineButton.UserInteractionEnabled = false;
                //
                //                    }
                //                );
            }
        }

        protected void RestoreButtons ()
        {

            acceptButton.Selected = false;
            tentativeButton.Selected = false;
            declineButton.Selected = false;
            acceptButton.Hidden = false;
            acceptLabel.Hidden = false;
            tentativeButton.Hidden = false;
            declineButton.Hidden = false;
            tentativeLabel.Hidden = false;
            declineLabel.Hidden = false;
            acceptButton.UserInteractionEnabled = true;
            tentativeButton.UserInteractionEnabled = true;
            declineButton.UserInteractionEnabled = true;
            //
            //            UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
            //                () => {
            //
            //                    acceptButton.Alpha = 1;
            //                    tentativeButton.Alpha = 1;
            //                    declineButton.Alpha = 1;
            //                    acceptLabel.Alpha = 1;
            //                    tentativeLabel.Alpha = 1;
            //                    declineLabel.Alpha = 1;
            //
            //                    messageLabel.Alpha = 0;
            //                    changeResponseButton.Alpha = 0;
            //
            //                    acceptButton.Frame = new RectangleF (18, 18, 24, 24);
            //                    tentativeButton.Frame = new RectangleF (acceptButton.Frame.X + 42 + 45, 18, 24, 24);
            //                    declineButton.Frame = new RectangleF (tentativeButton.Frame.X + 38 + 45 + 6, 18, 24, 24);
            //                },
            //                () => {
            //                    messageLabel.Hidden = true;
            //                    changeResponseButton.Hidden = true;
            //                }
            //            );

        }

        /// <summary>
        /// Update the user's status for the meeting and send a meeting response
        /// message to the organizer.
        /// </summary>
        private void UpdateMeetingStatus (NcResponseType status)
        {
            BackEnd.Instance.RespondCalCmd (calendarItem.AccountId, calendarItem.Id, status);

            if (calendarItem.ResponseRequestedIsSet && calendarItem.ResponseRequested && null != organizerEmail) {
                // Send an e-mail message to the organizer with the response.
                McAccount account = McAccount.QueryById<McAccount> (calendarItem.AccountId);
                var iCalPart = CalendarHelper.iCalResponseToMimePart (account, (McCalendar)calendarItem, status);
                // TODO Give the user a chance to enter some text. For now, the message body is empty.
                var mimeBody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                CalendarHelper.SendMeetingResponse (account, (McCalendar)calendarItem, mimeBody, status);
            }

            if (0 != calendarItem.BodyId) {
                var body = McBody.QueryById<McBody> (calendarItem.BodyId);
                if (null != body) {
                    body.Touch ();  // KLUDGE: Message display optimization looks a LastModified
                }
            }
        }

        /// <summary>
        /// Show the action bar for a meeting response.  The bar doesn't have any
        /// action; it just shows the status of the person who responded.
        /// </summary>
        private void ShowAttendeeResponseBar (DDay.iCal.Event evt)
        {
            if (0 == evt.Attendees.Count || null == evt.Attendees [0].ParticipationStatus) {
                // Malformed meeting reply.  It doesn't include anyone's status.
                // Leave the action bar blank.
                return;
            }

            UIView responseView = new UIView (new RectangleF (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.Clear;

            ActionBarCommon (responseView);

            acceptButton.Hidden = true;
            tentativeButton.Hidden = true;
            declineButton.Hidden = true;
            acceptLabel.Hidden = true;
            tentativeLabel.Hidden = true;
            declineLabel.Hidden = true;

            var responder = evt.Attendees [0];

            UIButton displayedButton = null;
            string messageFormat;
            switch (responder.ParticipationStatus) {
            case DDay.iCal.ParticipationStatus.Accepted:
                displayedButton = acceptButton;
                messageFormat = "{0} has accepted the meeting.";
                break;
            case DDay.iCal.ParticipationStatus.Tentative:
                displayedButton = tentativeButton;
                messageFormat = "{0} has tentatively accepted the meeting.";
                break;
            case DDay.iCal.ParticipationStatus.Declined:
                displayedButton = declineButton;
                messageFormat = "{0} has declined the meeting.";
                break;
            case DDay.iCal.ParticipationStatus.Delegated:
                messageFormat = "{0} has delegated the meeting.";
                break;
            case DDay.iCal.ParticipationStatus.NeedsAction:
                messageFormat = "{0} has not yet responded.";
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unkown meeting response status: {0}", responder.ParticipationStatus);
                messageFormat = "The status of {0} is unknown.";
                break;
            }

            string displayName;
            if (!string.IsNullOrEmpty (responder.CommonName)) {
                displayName = responder.CommonName;
            } else {
                if (Uri.UriSchemeMailto == responder.Value.Scheme) {
                    // String the "mailto:" off of the URL so the e-mail address is displayed.
                    displayName = responder.Value.AbsoluteUri.Substring (Uri.UriSchemeMailto.Length + 1);
                } else {
                    displayName = responder.Value.ToString ();
                }
            }

            if (null != displayedButton) {
                displayedButton.Hidden = false;
                displayedButton.Selected = true;
                displayedButton.UserInteractionEnabled = false;
                displayedButton.Frame = new RectangleF (18, 18, 24, 24);
            }

            messageLabel.Hidden = false;
            messageLabel.Text = String.Format (messageFormat, displayName);

            this.AddSubview (responseView);
        }

        /// <summary>
        /// Show the action bar for a meeting cancellation, which has a
        /// "Remove from calendar" button.
        /// </summary>
        private void ShowCancellationBar (DDay.iCal.Event evt, bool isOnNow)
        {
            UIView responseView = new UIView (new RectangleF (0, 0, viewWidth, 60));
            responseView.BackgroundColor = UIColor.Clear;

            ActionBarCommon (responseView);

            acceptButton.Hidden = true;
            tentativeButton.Hidden = true;
            declineButton.Hidden = true;
            acceptLabel.Hidden = true;
            tentativeLabel.Hidden = true;
            declineLabel.Hidden = true;

            if (null == calendarItem) {

                messageLabel.Text = "This event is not on your calendar";
                messageLabel.Frame = new RectangleF (42, 18, viewWidth - 42, 24);
                messageLabel.Hidden = false;
                dotView.Hidden = false;
                dotView.Image = Util.DrawCalDot (A.Color_NachoSwipeEmailDefer, new SizeF (10, 10));

            } else {

                // Let the user click either the red cirle X or the words "Remove from calendar."
                // They both do the same thing.


                if (isOnNow) {
                    messageLabel.Text = "This event is no longer occurring";
                    messageLabel.Frame = new RectangleF (42, 18, viewWidth - 42, 24);
                    messageLabel.Hidden = false;
                    dotView.Hidden = false;
                    dotView.Image = Util.DrawCalDot (A.Color_NachoSwipeEmailDelete, new SizeF (10, 10));
                    acceptButton.Hidden = true;
                    acceptLabel.Hidden = true;
                    tentativeButton.Hidden = true;
                    tentativeLabel.Hidden = true;
                    declineButton.Hidden = true;
                    declineLabel.Hidden = true;
                } else {

                    cancelActions = true;

                    declineButton.Hidden = false;
                    declineButton.Selected = false;
                    declineButton.Frame = new RectangleF (18, 18, 24, 24);
                    declineButton.TouchUpInside += RemoveFromCalendarClicked;

                    removeFromCalendarButton = new UIButton (UIButtonType.RoundedRect);
                    removeFromCalendarButton.SetTitle ("Remove from calendar", UIControlState.Normal);
                    removeFromCalendarButton.Font = A.Font_AvenirNextRegular12;
                    removeFromCalendarButton.SizeToFit ();
                    removeFromCalendarButton.Frame = new RectangleF (18 + 24 + 10, 19, removeFromCalendarButton.Frame.Width, 24);
                    removeFromCalendarButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
                    removeFromCalendarButton.Hidden = false;
                    removeFromCalendarButton.TouchUpInside += RemoveFromCalendarClicked;
                    responseView.Add (removeFromCalendarButton);
                }
            }

            this.AddSubview (responseView);
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

            messageLabel.Text = "This event is no longer on your calendar";
            messageLabel.Frame = new RectangleF (42, 18, viewWidth - 42, 24);
            messageLabel.Hidden = false;
            dotView.Image = Util.DrawCalDot (A.Color_NachoSwipeEmailDelete, new SizeF (10, 10));

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
            ToggleButtons (NcResponseType.Accepted);
            UpdateMeetingStatus (NcResponseType.Accepted);
            acceptButton.Selected = true;
        }

        private void TentativeButtonClicked (object sender, EventArgs e)
        {
            ToggleButtons (NcResponseType.Tentative);
            UpdateMeetingStatus (NcResponseType.Tentative);
            tentativeButton.Selected = true;
        }

        private void DeclineButtonClicked (object sender, EventArgs e)
        {
            ToggleButtons (NcResponseType.Declined);
            UpdateMeetingStatus (NcResponseType.Declined);
            declineButton.Selected = true;
        }

        private void ChangeResponseButtonClicked (object sender, EventArgs e)
        {
            RestoreButtons ();
        }
    }
}

