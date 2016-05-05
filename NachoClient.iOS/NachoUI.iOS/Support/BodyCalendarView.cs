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

        private McMeetingRequest meetingInfo;
        private McEmailMessage parentMessage;
        private McCalendar calendarItem;
        private bool requestActions = false;
        private bool cancelActions = false;
        private string organizerEmail;
        private Action dismissView;
        private BodyWebView.LinkSelectedCallback onLinkSelected;

        private UIView topLineView;
        private UIView whenHeadingView;
        private UILabel whenLabel;
        private UILabel durationLabel;
        private UIView locationHeadingView;
        private UcLocationView locationView;
        private UIView bottomLineView;
        private UIView organizerView;
        private UILabel organizerNameLabel;
        private UILabel organizerEmailLabel;
        private UIImageView organizerPhotoView;
        private UILabel organizerPhotoFallbackView;
        private UIView attendeesView;
        private UIView attendessListView;

        private nfloat preferredHeight;

        public UIEdgeInsets SeparatorInsets;
        public UIColor SeparatorColor;

        public BodyCalendarView (nfloat Y, nfloat width, McEmailMessage parentMessage, bool isOnHot, Action dismissView, BodyWebView.LinkSelectedCallback onLinkSelected, UIEdgeInsets? separatorInsets = null, UIColor separatorColor = null)
            : base (new CGRect (0, Y, width, 150))
        {

            SeparatorInsets = separatorInsets.HasValue ? separatorInsets.Value : new UIEdgeInsets (0.0f, 0.0f, 0.0f, 0.0f);
            SeparatorColor = separatorColor == null ? A.Color_NachoBorderGray : separatorColor;

            this.parentMessage = parentMessage;
            meetingInfo = parentMessage.MeetingRequest;
            NcAssert.NotNull (meetingInfo, "BodyCalendarView was given a message without a MeetingRequest.");
            calendarItem = McCalendar.QueryByUID (parentMessage.AccountId, meetingInfo.GetUID ());
            this.dismissView = dismissView;
            this.onLinkSelected = onLinkSelected;
            preferredHeight = Bounds.Height;

            Tag = CALENDAR_PART_TAG;

            // The contents of the action/info bar depends on whether this is a request,
            // response, or cancellation.
            if (parentMessage.IsMeetingResponse) {
                ShowAttendeeResponseBar ();
            } else if (parentMessage.IsMeetingCancelation) {
                ShowCancellationBar (isOnHot);
            } else {
                if (!parentMessage.IsMeetingRequest) {
                    Log.Warn (Log.LOG_CALENDAR, "Unexpected calendar kind: {0}. It will be treated as a meeting request.", parentMessage.MessageClass);
                }
                ShowRequestChoicesBar (isOnHot);
            }

            CreateSubviews ();
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
            // The calendar section does not scroll or resize vertically.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y).Width(frame.Width);
        }

        private void CreateSubviews ()
        {
            topLineView = Util.AddHorizontalLine (SeparatorInsets.Left, 0, Bounds.Width - SeparatorInsets.Left - SeparatorInsets.Right, SeparatorColor, this);
            topLineView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            whenHeadingView = Util.AddTextLabelWithImageView (0, "WHEN", "event-when", 0, this);
            whenHeadingView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            whenLabel = Util.AddDetailTextLabel (42, 0, Bounds.Width - 90, 20, 0, this);
            whenLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            durationLabel = Util.AddDetailTextLabel (42, 0, Bounds.Width - 90, 20, 0, this);
            durationLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            durationLabel.Lines = 0;
            durationLabel.LineBreakMode = UILineBreakMode.WordWrap;

            locationHeadingView = Util.AddTextLabelWithImageView (0, "LOCATION", "event-location", 0, this);
            locationHeadingView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            locationView = new UcLocationView (37, 0, Bounds.Width - 37, onLinkSelected);
            locationView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            locationView.Font = A.Font_AvenirNextRegular14;
            locationView.TextColor = A.Color_NachoDarkText;
            this.AddSubview (locationView);

            // The organizer view should probably be its own simple UIView subclass
            CreateOrganizerSubview ();

            attendeesView = new UIView (new CGRect (0, 0, Bounds.Width, 96 + 16));
            attendeesView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            Util.AddTextLabelWithImageView (0, "ATTENDEES", "event-attendees", 0, attendeesView);
            attendeesView.BackgroundColor = UIColor.White;
            this.AddSubview (attendeesView);
            attendessListView = new UIView (new CGRect(0.0, 16f, attendeesView.Bounds.Width, attendeesView.Bounds.Height - 16f));
            attendessListView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            attendessListView.BackgroundColor = UIColor.Clear;
            attendeesView.AddSubview (attendessListView);

            bottomLineView = Util.AddHorizontalLine (SeparatorInsets.Left, 0, Bounds.Width - SeparatorInsets.Left - SeparatorInsets.Right, SeparatorColor, this);
            bottomLineView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
        }

        private void CreateOrganizerSubview ()
        {
            organizerView = new UIView (new CGRect (0, 0, Bounds.Width, 44 + 16 + 16));
            organizerView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            organizerView.BackgroundColor = UIColor.White;
            this.AddSubview (organizerView);
            Util.AddTextLabelWithImageView (0, "ORGANIZER", "event-organizer", 0, organizerView);

            organizerNameLabel = new UILabel (new CGRect (92, 16 + 10, organizerView.Frame.Width - 92 - 18, 20));
            organizerNameLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            organizerNameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            organizerNameLabel.TextColor = UIColor.LightGray;
            organizerNameLabel.Font = A.Font_AvenirNextRegular14;
            organizerView.AddSubview (organizerNameLabel);

            organizerEmailLabel = new UILabel (new CGRect (92, 46f, organizerView.Frame.Width - 92 - 18, 20));
            organizerEmailLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            organizerEmailLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            organizerEmailLabel.TextColor = UIColor.LightGray;
            organizerEmailLabel.Font = A.Font_AvenirNextRegular14;
            organizerEmailLabel.Tag = (int)EventViewController.TagType.EVENT_ORGANIZER_EMAIL_LABEL;
            organizerView.AddSubview (organizerEmailLabel);

            // The photo view and fallback should probably be combined in a subclass that can automatically switch display
            organizerPhotoView = new UIImageView (new CGRect (42, 10 + 16, 40, 40));
            organizerPhotoView.Layer.CornerRadius = (40.0f / 2.0f);
            organizerPhotoView.Layer.MasksToBounds = true;
            organizerPhotoView.Layer.BorderWidth = .25f;
            organizerPhotoView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            organizerView.AddSubview (organizerPhotoView);

            organizerPhotoFallbackView = new UILabel (organizerPhotoView.Frame);
            organizerPhotoFallbackView.Font = A.Font_AvenirNextRegular17;
            organizerPhotoFallbackView.TextColor = UIColor.White;
            organizerPhotoFallbackView.TextAlignment = UITextAlignment.Center;
            organizerPhotoFallbackView.LineBreakMode = UILineBreakMode.Clip;
            organizerPhotoFallbackView.Layer.CornerRadius = (40 / 2);
            organizerPhotoFallbackView.Layer.MasksToBounds = true;
            organizerView.AddSubview (organizerPhotoFallbackView);
        }

        /// <summary>
        /// Display the basic information about the calendar event.
        /// </summary>
        private void ShowEventInfo ()
        {
            whenLabel.Text = NcEventDetail.GetDateString (meetingInfo);
            durationLabel.Text = NcEventDetail.GetDurationString (meetingInfo);
            if (0 == meetingInfo.recurrences.Count) {
                durationLabel.Text = NcEventDetail.GetDurationString (meetingInfo);
            } else {
                durationLabel.Text = string.Format ("{0}\n{1}", NcEventDetail.GetDurationString (meetingInfo), NcEventDetail.GetRecurrenceString (meetingInfo));
            }

            string location = meetingInfo.Location;
            if (!string.IsNullOrEmpty (location)) {
                locationHeadingView.Hidden = false;
                locationView.Hidden = false;
                locationView.SetText (location);
            } else {
                locationHeadingView.Hidden = true;
                locationView.Hidden = true;
            }

            var accountId = parentMessage.AccountId;

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meetingInfo.Organizer);
            if (null != organizerAddress) {
                organizerView.Hidden = false;
                organizerEmail = organizerAddress.Address;
                var organizerName = organizerAddress.Name;
                if (null != organizerEmail) {
                    if (null != organizerName) {
                        organizerNameLabel.Text = organizerName;
                    } else {
                        organizerNameLabel.Text = "";
                    }
                    organizerEmailLabel.Text = organizerEmail;

                    var userImage = Util.ImageOfSender (accountId, organizerEmail);

                    if (null != userImage) {
                        organizerPhotoView.Hidden = false;
                        organizerPhotoFallbackView.Hidden = true;
                        using (var rawImage = userImage) {
                            using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                                organizerPhotoView.Image = originalImage;
                            }
                        }
                    } else {
                        // User userLabelView view, if no image
                        organizerPhotoView.Hidden = true;
                        organizerPhotoFallbackView.Hidden = false;
                        organizerPhotoFallbackView.BackgroundColor = Util.GetCircleColorForEmail (organizerEmail, accountId);
                        var nameString = (null != organizerName ? organizerName : organizerEmail);
                        organizerPhotoFallbackView.Text = ContactsHelper.NameToLetters (nameString);
                    }

                }
            } else {
                organizerView.Hidden = true;
            }

            // Only display the attendees when it is a meeting request
            if (parentMessage.IsMeetingRequest) {
                attendeesView.Hidden = false;
                for (int i = attendessListView.Subviews.Length - 1; i >= 0; --i) {
                    attendeesView.Subviews [i].RemoveFromSuperview ();
                }
                var attendeeImageDiameter = 40;
                var iconSpace = Bounds.Width - 60;
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
                    Util.CreateAttendeeButton (attendeeImageDiameter, spacing, 0, attendee, attendeeNum, false, attendessListView);

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
                    extraAttendeesButton.Frame = new CGRect (42 + iconSpace - 39, 10, attendeeImageDiameter, attendeeImageDiameter);
                    extraAttendeesButton.Font = A.Font_AvenirNextRegular14;
                    extraAttendeesButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
                    extraAttendeesButton.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_DETAIL_TAG;
                    extraAttendeesButton.SetTitle (string.Format ("+{0}", allAttendees.Count - 4), UIControlState.Normal);
                    extraAttendeesButton.AccessibilityLabel = "More";
                    attendessListView.AddSubview (extraAttendeesButton);
                }
            } else {
                attendeesView.Hidden = true;
            }

            SetNeedsLayout ();
            LayoutIfNeeded ();
            this.Frame = new CGRect (this.Frame.X, this.Frame.Y, this.Frame.Width, preferredHeight);
        }

        public override void LayoutSubviews ()
        {
            // First have the base class handle any autoresizing mask layouts
            base.LayoutSubviews ();

            // Then layout our known views in a vertical stack
            nfloat y = 60f;
            nfloat generalMaxHeight = 100000f;
            y += LayoutSubviewAtYPosition (topLineView, y, 17f);
            y += LayoutSubviewAtYPosition (whenHeadingView, y, 6f);
            y += LayoutSubviewAtYPosition (whenLabel, y);
            y += LayoutSubviewAtYPosition (durationLabel, y, 20f, generalMaxHeight);
            y += LayoutSubviewAtYPosition (locationHeadingView, y, 6f);
            y += LayoutSubviewAtYPosition (locationView, y, 20f);
            y += LayoutSubviewAtYPosition (organizerView, y, 4f);
            y += LayoutSubviewAtYPosition (attendeesView, y, 4f);
            y += LayoutSubviewAtYPosition (bottomLineView, y, 20f);

            preferredHeight = y;

            // Finally do a little bit of layout without certain subviews
            // Ideally the subview would be made into a class and take care of its own layout, but this works for now
            if (!organizerView.Hidden) {
                nfloat emailOffset = 46f;
                if (string.IsNullOrEmpty (organizerNameLabel.Text)) {
                    emailOffset = (organizerView.Bounds.Height / 2) - 3;
                }
                if (emailOffset != organizerEmailLabel.Frame.Y) {
                    organizerEmailLabel.Frame = new CGRect (organizerEmailLabel.Frame.X, emailOffset, organizerEmailLabel.Frame.Width, organizerEmailLabel.Frame.Height);
                }
            }
        }

        private nfloat LayoutSubviewAtYPosition(UIView subview, nfloat y, float padding = 0f, nfloat? maxHeight = null, float minHeight = 0f)
        {
            if (subview.Hidden){
                return 0f;
            }
            var layoutHeight = subview.Frame.Height;
            if (maxHeight.HasValue) {
                layoutHeight = subview.SizeThatFits (new CGSize (subview.Frame.Width, maxHeight.Value)).Height;
            }
            if (layoutHeight < minHeight) {
                layoutHeight = minHeight;
            }
            subview.Frame = new CGRect (subview.Frame.X, y, subview.Frame.Width, layoutHeight);
            return layoutHeight + padding;
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
            messageLabel = new UILabel (new CGRect (messageX, 18, Bounds.Width - messageX, 24));
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
            UIView responseView = new UIView (new CGRect (0, 0, Bounds.Width, 60));
            responseView.BackgroundColor = UIColor.White;

            CreateActionBarViews (responseView);

            if (isOnHot) {

                // This is something other than the message detail view, probably the Hot view.
                // Don't show all three buttons.  Instead, show a message with either a
                // non-clickable button or a dot.

                string message = "You have not responded to this invitation.";
                UIButton displayedButton = null;
                if (DateTime.MinValue == meetingInfo.RecurrenceId && null != calendarItem && calendarItem.ResponseTypeIsSet) {
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

                // If this is a meeting request for a particular occurrence of a recurring meeting,
                // don't even try to figure whether or not the user has responded.  It's not worth
                // the effort.
                if (DateTime.MinValue == meetingInfo.RecurrenceId && null != calendarItem && calendarItem.ResponseTypeIsSet) {
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
            UIView responseView = new UIView (new CGRect (0, 0, Bounds.Width, 60));
            responseView.BackgroundColor = UIColor.Clear;

            CreateActionBarViews (responseView);

            UIButton displayedButton = null;
            string messageFormat;
            switch (parentMessage.MeetingResponseValue) {
            case NcResponseType.Accepted:
                displayedButton = acceptButton;
                messageFormat = "{0} has accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                displayedButton = tentativeButton;
                messageFormat = "{0} has tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
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
            UIView responseView = new UIView (new CGRect (0, 0, Bounds.Width, 60));
            responseView.BackgroundColor = UIColor.Clear;

            CreateActionBarViews (responseView);

            bool eventExists = (null != calendarItem);
            if (eventExists && DateTime.MinValue != meetingInfo.RecurrenceId) {
                // This is the cancelation notice for a particular occurrence.  See if that occurrence exists.
                var exceptions = McException.QueryForExceptionId (calendarItem.Id, meetingInfo.RecurrenceId);
                eventExists = (0 == exceptions.Count || 0 == exceptions [0].Deleted);
            }

            if (isOnHot || !eventExists) {

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
            return new CGRect (42, 18, Bounds.Width - 42, 24);
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
                    // Dismiss the entire view once the animation is done.
                    dismissView ();
                });

            // Remove the item from the calendar.
            if (DateTime.MinValue == meetingInfo.RecurrenceId) {
                BackEnd.Instance.DeleteCalCmd (calendarItem.AccountId, calendarItem.Id);
            } else {
                CalendarHelper.CancelOccurrence (calendarItem, meetingInfo.RecurrenceId);
                BackEnd.Instance.UpdateCalCmd (calendarItem.AccountId, calendarItem.Id, sendBody: false);
            }
        }

        protected override void Dispose (bool disposing)
        {
            Cleanup ();
            base.Dispose (disposing);
        }

        public void Cleanup ()
        {
            onLinkSelected = null;
            dismissView = null;
            if (requestActions) {
                acceptButton.TouchUpInside -= AcceptButtonClicked;
                tentativeButton.TouchUpInside -= TentativeButtonClicked;
                declineButton.TouchUpInside -= DeclineButtonClicked;
                requestActions = false;
            }
            if (cancelActions) {
                declineButton.TouchUpInside -= RemoveFromCalendarClicked;
                removeFromCalendarButton.TouchUpInside -= RemoveFromCalendarClicked;
                cancelActions = false;
            }
        }

        private void AcceptButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Accepted);
            UpdateMeetingStatus (NcResponseType.Accepted);
            dismissView ();
        }

        private void TentativeButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Tentative);
            UpdateMeetingStatus (NcResponseType.Tentative);
            dismissView ();
        }

        private void DeclineButtonClicked (object sender, EventArgs e)
        {
            MarkSelectedButton (NcResponseType.Declined);
            UpdateMeetingStatus (NcResponseType.Declined);
            dismissView ();
        }
    }
}

