﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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

        private SizeF _contentSize;
        public SizeF ContentSize {
            get {
                return _contentSize;
            }
            protected set {
                _contentSize = value;
            }
        }

        public void ScrollTo (PointF upperLeft)
        {
            // Calendar view is not scrollable
        }

        public BodyCalendarView (IntPtr ptr) : base (ptr)
        {
        }

        public BodyCalendarView (float width) : base (new RectangleF (0, 0, width, 150))
        {
            ViewHelper.SetDebugBorder (this, UIColor.Magenta);

            viewWidth = width;
            Tag = CALENDAR_PART_TAG;
        }

        public void Configure (MimePart part)
        {
            // Decode iCal
            var textPart = part as TextPart;
            IICalendar iCal;
            using (var stringReader = new StringReader (textPart.Text)) {
                iCal = iCalendar.LoadFromStream (stringReader) [0];
            }
            var evt = iCal.Events.First () as DDay.iCal.Event;

            if (null == evt) {
                // The text/calendar part doesn't contain any events. There is nothing to show.
                return;
            }

            if (null != evt.UID) {
                calendarItem = McCalendar.QueryByUID (evt.UID);
            }

            ShowEventInfo (evt);

            // The contents of the action/info bar depends on whether this is a request,
            // response, or cancellation.
            if (iCal.Method.Equals (DDay.iCal.CalendarMethods.Reply)) {
                ShowAttendeeResponseBar (evt);
            } else if (iCal.Method.Equals (DDay.iCal.CalendarMethods.Cancel)) {
                ShowCancellationBar (evt);
            } else {
                if (!iCal.Method.Equals (DDay.iCal.CalendarMethods.Request)) {
                    Log.Warn (Log.LOG_CALENDAR, "Unexpected calendar method: {0}. It will be treated as a {1}.",
                        iCal.Method, DDay.iCal.CalendarMethods.Request);
                }
                ShowRequestChoicesBar (evt);
            }

            // Save the content size
            _contentSize = Frame.Size;
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

            UILabel monthLabel = new UILabel (new RectangleF (19, 23, 36, 20));
            monthLabel.Tag = (int)TagType.CALENDAR_MONTH_TAG;
            monthLabel.Font = A.Font_AvenirNextRegular12;
            monthLabel.TextColor = A.Color_NachoBlack;
            monthLabel.TextAlignment = UITextAlignment.Center;
            monthLabel.Text = start.ToString ("MMM");

            UIImageView dateImage = new UIImageView (new RectangleF (19, 43, 36, 36));
            var size = new SizeF (40, 40);
            dateImage.Image = NachoClient.Util.DrawCalDot (A.Color_FEBA32, size);

            UILabel dateLabel = new UILabel (new RectangleF (19, 43, 36, 36));
            dateLabel.Tag = (int)TagType.CALENDAR_DATE_TAG;
            dateLabel.Font = A.Font_AvenirNextDemiBold17;
            dateLabel.TextColor = UIColor.White;
            dateLabel.TextAlignment = UITextAlignment.Center;
            dateLabel.Text = start.ToString ("%d");

            UILabel titleLabel = new UILabel (new RectangleF (74, 27, viewWidth - 89, 20));
            titleLabel.Tag = (int)TagType.CALENDAR_TITLE_TAG;
            titleLabel.Font = A.Font_AvenirNextDemiBold14;
            titleLabel.TextColor = A.Color_NachoBlack;
            titleLabel.TextAlignment = UITextAlignment.Left;
            titleLabel.Text = subject;
            titleLabel.SizeToFit ();

            UILabel durationLabel = new UILabel (new RectangleF (74, 47, viewWidth - 89, 20));
            durationLabel.Tag = (int)TagType.CALENDAR_DURATION_TAG;
            durationLabel.Font = A.Font_AvenirNextRegular12;
            durationLabel.TextColor = A.Color_NachoBlack;
            durationLabel.TextAlignment = UITextAlignment.Left;
            durationLabel.Text = start.ToString ("dd");
            if (!isAllDay) {
                if (start.LocalT ().DayOfYear == end.LocalT ().DayOfYear) {
                    durationLabel.Text = "from " + Pretty.FullTimeString (start) + " until " + Pretty.FullTimeString (end);
                } else {
                    durationLabel.Text = "from " + Pretty.FullTimeString (start) + " until " + Pretty.FullDateTimeString (end);
                }
            } else {
                if (start.DayOfYear == end.DayOfYear) {
                    durationLabel.Text = "all day event";
                } else {
                    durationLabel.Text = "from " + Pretty.FullDateString (start) + " until " + Pretty.FullDateString (end);
                }
            }
            durationLabel.SizeToFit ();

            UILabel locationLabel = new UILabel (new RectangleF (74, 65, viewWidth - 89, 20));
            locationLabel.Tag = (int)TagType.CALENDAR_LOCATION_TAG;
            locationLabel.Font = A.Font_AvenirNextRegular12;
            locationLabel.TextColor = A.Color_NachoBlack;
            locationLabel.TextAlignment = UITextAlignment.Left;
            locationLabel.Text = location;
            locationLabel.SizeToFit ();

            AddSubview (monthLabel);
            AddSubview (dateImage);
            AddSubview (dateLabel);
            AddSubview (titleLabel);
            AddSubview (durationLabel);
            AddSubview (locationLabel);

            Util.AddHorizontalLine (0, 20, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
            Util.AddHorizontalLine (0, 86, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
            Util.AddHorizontalLine (0, 140, viewWidth, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
            Util.AddVerticalLine (65, 20, 66, A.Color_NachoBorderGray, this).Tag =
                (int)TagType.CALENDAR_LINE_TAG;
        }

        UILabel eventDoesNotExistLabel;

        UIButton acceptButton;
        UIButton tentativeButton;
        UIButton declineButton;

        UILabel acceptLabel;
        UILabel tentativeLabel;
        UILabel declineLabel;

        UILabel messageLabel;
        UIButton changeResponseButton;
        UIButton removeFromCalendarButton;

        /// <summary>
        /// Configuration of some of the subview elements of the action bar that are
        /// common to at least two of the forms of the action bar.
        /// </summary>
        private void ActionBarCommon (UIView responseView)
        {
            acceptButton = new UIButton (UIButtonType.RoundedRect);
            using (var acceptButtonImage = UIImage.FromBundle ("btn-mtng-accept")) {
                acceptButton.SetImage (acceptButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
            }
            using (var acceptPressedButtonImage = UIImage.FromBundle ("btn-mtng-accept-pressed")) {
                acceptButton.SetImage (acceptPressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            acceptButton.SetTitle ("", UIControlState.Normal);
            acceptButton.Frame = new RectangleF (25, 10, 24, 24);
            acceptButton.TintColor = UIColor.Clear;
            responseView.AddSubview (acceptButton);

            tentativeButton = new UIButton (UIButtonType.RoundedRect);
            using (var tentativeButtonImage = UIImage.FromBundle ("btn-mtng-tenative")) {
                tentativeButton.SetImage (tentativeButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
            }
            using (var tentativePressedButtonImage = UIImage.FromBundle ("btn-mtng-tenative-pressed")) {
                tentativeButton.SetImage (tentativePressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            tentativeButton.SetTitle ("", UIControlState.Normal);
            tentativeButton.Frame = new RectangleF ((viewWidth / 2) - 12, 10, 24, 24);
            tentativeButton.TintColor = UIColor.Clear;
            responseView.AddSubview (tentativeButton);

            declineButton = new UIButton (UIButtonType.RoundedRect);
            using (var declineButtonImage = UIImage.FromBundle ("btn-mtng-decline")) {
                declineButton.SetImage (declineButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
            }
            using (var declinePressedButtonImage = UIImage.FromBundle ("btn-mtng-decline-pressed")) {
                declineButton.SetImage (declinePressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            declineButton.SetTitle ("", UIControlState.Normal);
            declineButton.Frame = new RectangleF (viewWidth - 24 - 25, 10, 24, 24);
            declineButton.TintColor = UIColor.Clear;
            responseView.AddSubview (declineButton);

            acceptLabel = new UILabel (new RectangleF (15, 36, 44, 10));
            acceptLabel.TextColor = A.Color_NachoBlack;
            acceptLabel.TextAlignment = UITextAlignment.Center;
            acceptLabel.Font = A.Font_AvenirNextRegular10;
            acceptLabel.Text = "Accept";
            responseView.AddSubview (acceptLabel);

            tentativeLabel = new UILabel (new RectangleF ((viewWidth / 2) - 22, 36, 44, 10));
            tentativeLabel.TextColor = A.Color_NachoBlack;
            tentativeLabel.TextAlignment = UITextAlignment.Center;
            tentativeLabel.Font = A.Font_AvenirNextRegular10;
            tentativeLabel.Text = "Tentative";
            responseView.AddSubview (tentativeLabel);

            declineLabel = new UILabel (new RectangleF (viewWidth - 24 - 35, 36, 44, 10));
            declineLabel.TextColor = A.Color_NachoBlack;
            declineLabel.TextAlignment = UITextAlignment.Center;
            declineLabel.Font = A.Font_AvenirNextRegular10;
            declineLabel.Text = "Decline";
            responseView.AddSubview (declineLabel);

            float messageX = 25 + 24 + 10;
            messageLabel = new UILabel (new RectangleF (messageX, 15, viewWidth - messageX, 24));
            messageLabel.TextColor = A.Color_NachoBlack;
            messageLabel.TextAlignment = UITextAlignment.Left;
            messageLabel.Font = A.Font_AvenirNextRegular12;
            messageLabel.Hidden = true;
            responseView.AddSubview (messageLabel);

            eventDoesNotExistLabel = new UILabel (new RectangleF (25, 15, viewWidth - 25, 24));
            eventDoesNotExistLabel.TextColor = A.Color_NachoBlack;
            eventDoesNotExistLabel.TextAlignment = UITextAlignment.Left;
            eventDoesNotExistLabel.Text = "This event has been removed from your calendar";
            eventDoesNotExistLabel.Font = A.Font_AvenirNextRegular12;
            eventDoesNotExistLabel.Hidden = true;
            responseView.Add (eventDoesNotExistLabel);
        }

        /// <summary>
        /// Show the action bar for a meeting request, with the "Accept", "Tentative", and "Decline" buttons.
        /// </summary>
        private void ShowRequestChoicesBar (DDay.iCal.Event evt)
        {
            UIView responseView = new UIView (new RectangleF (0, 86, viewWidth, 54));
            responseView.BackgroundColor = UIColor.Clear;

            ActionBarCommon (responseView);

            requestActions = true;

            changeResponseButton = new UIButton (UIButtonType.RoundedRect);
            changeResponseButton.SetTitle ("Change response", UIControlState.Normal);
            changeResponseButton.Font = A.Font_AvenirNextRegular12;
            changeResponseButton.SizeToFit ();
            changeResponseButton.Frame = new RectangleF (viewWidth - changeResponseButton.Frame.Width - 25, 16, changeResponseButton.Frame.Width, 24);
            changeResponseButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            changeResponseButton.Hidden = true;
            changeResponseButton.TouchUpInside += ChangeResponseButtonClicked;
            responseView.Add (changeResponseButton);

            acceptButton.TouchUpInside += AcceptButtonClicked;
            tentativeButton.TouchUpInside += TentativeButtonClicked;
            declineButton.TouchUpInside += DeclineButtonClicked;

            if (null != calendarItem) {

                if (calendarItem.ResponseTypeIsSet && NcResponseType.Organizer == calendarItem.ResponseType) {
                    // The organizer doesn't normally get an meeting request.
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
                        acceptButton.Frame = new RectangleF (25, 15, 24, 24);
                        messageLabel.Text = "You are going";
                        messageLabel.Hidden = false;
                        changeResponseButton.Hidden = false;
                        acceptLabel.Hidden = true;
                        tentativeButton.Hidden = true;
                        declineButton.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineLabel.Hidden = true;
                        acceptButton.UserInteractionEnabled = false;
                        break;

                    case NcResponseType.Tentative:
                        tentativeButton.Selected = true;
                        tentativeButton.Frame = new RectangleF (25, 15, 24, 24);
                        messageLabel.Text = "Tentative";
                        messageLabel.Hidden = false;
                        changeResponseButton.Hidden = false;
                        acceptButton.Hidden = true;
                        acceptLabel.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineButton.Hidden = true;
                        declineLabel.Hidden = true;
                        tentativeButton.UserInteractionEnabled = false;
                        break;

                    case NcResponseType.Declined:
                        declineButton.Selected = true;
                        declineButton.Frame = new RectangleF (25, 15, 24, 24);
                        messageLabel.Text = "You are not going to this meeting";
                        messageLabel.Hidden = false;
                        changeResponseButton.Hidden = false;
                        acceptButton.Hidden = true;
                        acceptLabel.Hidden = true;
                        tentativeButton.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineLabel.Hidden = true;
                        declineButton.UserInteractionEnabled = false;
                        break;
                    }
                }
            } else {
                eventDoesNotExistLabel.Hidden = false;
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
        private void ToggleButtons (NcResponseType r)
        {
            if (NcResponseType.Accepted == r) {
                acceptButton.Selected = true;
                tentativeButton.Selected = false;
                declineButton.Selected = false;
                messageLabel.Text = "You are going";
                messageLabel.Hidden = false;
                messageLabel.Alpha = 0;
                changeResponseButton.Hidden = false;
                changeResponseButton.Alpha = 0;

                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        acceptLabel.Alpha = 0;
                        tentativeButton.Alpha = 0;
                        declineButton.Alpha = 0;
                        tentativeLabel.Alpha = 0;
                        declineLabel.Alpha = 0;
                        messageLabel.Alpha = 1;
                        changeResponseButton.Alpha = 1;

                        acceptButton.Frame = new RectangleF (25, 15, 24, 24);
                    },
                    () => {
                        acceptLabel.Hidden = true;
                        tentativeButton.Hidden = true;
                        declineButton.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineLabel.Hidden = true;
                        acceptButton.UserInteractionEnabled = false;
                    }
                );
            } else if (NcResponseType.Tentative == r) {
                acceptButton.Selected = false;
                tentativeButton.Selected = true;
                declineButton.Selected = false;
                messageLabel.Text = "Tentative";
                messageLabel.Hidden = false;
                messageLabel.Alpha = 0;
                changeResponseButton.Hidden = false;
                changeResponseButton.Alpha = 0;

                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        acceptLabel.Alpha = 0;
                        acceptButton.Alpha = 0;
                        declineButton.Alpha = 0;
                        tentativeLabel.Alpha = 0;
                        declineLabel.Alpha = 0;
                        messageLabel.Alpha = 1;
                        changeResponseButton.Alpha = 1;

                        tentativeButton.Frame = new RectangleF (25, 15, 24, 24);
                    },
                    () => {
                        acceptLabel.Hidden = true;
                        acceptButton.Hidden = true;
                        declineButton.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineLabel.Hidden = true;
                        tentativeButton.UserInteractionEnabled = false;
                    }
                );
            } else {
                acceptButton.Selected = false;
                tentativeButton.Selected = false;
                declineButton.Selected = true;
                messageLabel.Text = "You are not going";
                messageLabel.Hidden = false;
                messageLabel.Alpha = 0;
                changeResponseButton.Hidden = false;
                changeResponseButton.Alpha = 0;

                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        acceptLabel.Alpha = 0;
                        acceptButton.Alpha = 0;
                        tentativeButton.Alpha = 0;
                        tentativeLabel.Alpha = 0;
                        declineLabel.Alpha = 0;
                        messageLabel.Alpha = 1;
                        changeResponseButton.Alpha = 1;

                        declineButton.Frame = new RectangleF (25, 15, 24, 24);
                    },
                    () => {
                        acceptLabel.Hidden = true;
                        acceptButton.Hidden = true;
                        tentativeButton.Hidden = true;
                        tentativeLabel.Hidden = true;
                        declineLabel.Hidden = true;
                        declineButton.UserInteractionEnabled = false;

                    }
                );
            }
        }

        /// <summary>
        /// The "Change my response" button has been touched. Restore the
        /// "Accept", "Tentative", and "Decline" buttons.
        /// </summary>
        private void RestoreButtons ()
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

            UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                () => {

                    acceptButton.Alpha = 1;
                    tentativeButton.Alpha = 1;
                    declineButton.Alpha = 1;
                    acceptLabel.Alpha = 1;
                    tentativeLabel.Alpha = 1;
                    declineLabel.Alpha = 1;

                    messageLabel.Alpha = 0;
                    changeResponseButton.Alpha = 0;

                    acceptButton.Frame = new RectangleF (25, 10, 24, 24);
                    tentativeButton.Frame = new RectangleF ((viewWidth / 2) - 12, 10, 24, 24);
                    declineButton.Frame = new RectangleF (viewWidth - 24 - 25, 10, 24, 24);
                },
                () => {
                    messageLabel.Hidden = true;
                    changeResponseButton.Hidden = true;
                }
            );
        }

        /// <summary>
        /// Update the user's status for the meeting and send a meeting response
        /// message to the organizer.
        /// </summary>
        private void UpdateMeetingStatus (NcResponseType status)
        {
            BackEnd.Instance.RespondCalCmd (calendarItem.AccountId, calendarItem.Id, status);

            if (calendarItem.ResponseRequestedIsSet && calendarItem.ResponseRequested) {
                // Send an e-mail message to the organizer with the response.
                McAccount account = McAccount.QueryById<McAccount> (calendarItem.AccountId);
                var iCalPart = CalendarHelper.iCalResponseToMimePart (account, (McCalendar)calendarItem, status);
                // TODO Give the user a chance to enter some text. For now, the message body is empty.
                var mimeBody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                CalendarHelper.SendMeetingResponse (account, (McCalendar)calendarItem, mimeBody, status);
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

            UIView responseView = new UIView (new RectangleF (0, 86, viewWidth, 54));
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
                displayedButton.Frame = new RectangleF (25, 15, 24, 24);
            }

            messageLabel.Hidden = false;
            messageLabel.Text = String.Format (messageFormat, displayName);

            this.AddSubview (responseView);
        }

        /// <summary>
        /// Show the action bar for a meeting cancellation, which has a
        /// "Remove from calendar" button.
        /// </summary>
        private void ShowCancellationBar (DDay.iCal.Event evt)
        {
            UIView responseView = new UIView (new RectangleF (0, 86, viewWidth, 54));
            responseView.BackgroundColor = UIColor.Clear;

            ActionBarCommon (responseView);

            acceptButton.Hidden = true;
            tentativeButton.Hidden = true;
            declineButton.Hidden = true;
            acceptLabel.Hidden = true;
            tentativeLabel.Hidden = true;
            declineLabel.Hidden = true;

            if (null == calendarItem) {

                eventDoesNotExistLabel.Hidden = false;

            } else {

                // Let the user click either the red cirle X or the words "Remove from calendar."
                // They both do the same thing.

                cancelActions = true;

                declineButton.Hidden = false;
                declineButton.Selected = false;
                declineButton.Frame = new RectangleF (25, 15, 24, 24);
                declineButton.TouchUpInside += RemoveFromCalendarClicked;

                removeFromCalendarButton = new UIButton (UIButtonType.RoundedRect);
                removeFromCalendarButton.SetTitle ("Remove from calendar", UIControlState.Normal);
                removeFromCalendarButton.Font = A.Font_AvenirNextRegular12;
                removeFromCalendarButton.SizeToFit ();
                removeFromCalendarButton.Frame = new RectangleF (25 + 24 + 10, 16, removeFromCalendarButton.Frame.Width, 24);
                removeFromCalendarButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
                removeFromCalendarButton.Hidden = false;
                removeFromCalendarButton.TouchUpInside += RemoveFromCalendarClicked;
                responseView.Add (removeFromCalendarButton);
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
            eventDoesNotExistLabel.Alpha = 0;
            eventDoesNotExistLabel.Hidden = false;
            UIView.Animate (0.2, 0, UIViewAnimationOptions.CurveLinear,
                () => {
                    declineButton.Alpha = 0;
                    removeFromCalendarButton.Alpha = 0;
                    eventDoesNotExistLabel.Alpha = 1;
                },
                () => {
                    declineButton.Hidden = true;
                    removeFromCalendarButton.Hidden = true;
                });

            // Remove the item from the calendar.
            BackEnd.Instance.DeleteCalCmd (calendarItem.AccountId, calendarItem.Id);
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyCalendarView: offset={0}  frame={1}  content={2}",
                Pretty.PointF (Frame.Location), Pretty.SizeF (Frame.Size), Pretty.SizeF (ContentSize));
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

