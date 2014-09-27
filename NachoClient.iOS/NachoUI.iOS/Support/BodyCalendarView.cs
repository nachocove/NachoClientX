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

namespace NachoClient.iOS
{
    public class BodyCalendarView : UIView
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

        protected BodyView parentView;
        protected bool rendered;

        public BodyCalendarView (BodyView parentView) : base (parentView.Frame)
        {
            this.parentView = parentView;
            ViewFramer.Create (this).Height (1);
            Tag = CALENDAR_PART_TAG;
        }

        public void Configure (MimePart part)
        {
            // Decode iCal
            var textPart = part as TextPart;
            var decodedText = GetText (textPart);
            var stringReader = new StringReader (decodedText);
            IICalendar iCal = iCalendar.LoadFromStream (stringReader) [0];
            var evt = iCal.Events.First () as DDay.iCal.Event;
            NachoCore.Utils.CalendarHelper.ExtrapolateTimes (ref evt);

            // Render the event description
            if (null != evt.Description) {
                parentView.RenderTextString (evt.Description);
            }

            // Display the event with an accept bar
            MakeStyledCalendarInvite (evt);

            rendered = true;

            // Layout all the subviews
            ViewFramer.Create (this).Height (150);
        }

        /// Gets the decoded text content.
        protected string GetText (TextPart text)
        {
            return text.Text;
        }

        public void MakeStyledCalendarInvite (DDay.iCal.Event evt)
        {
            string UID = evt.UID;
            string subject = evt.Summary;
            bool isAllDay = evt.IsAllDay;
            DateTime start = evt.Start.Value;
            DateTime end = evt.End.Value;
            string location = evt.Location;
            float viewWidth = parentView.Frame.Width;

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
                if (start.DayOfYear == end.DayOfYear) {
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

            MakeResponseBar (UID);

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

        public void MakeResponseBar (string UID)
        {
            float viewWidth = Frame.Width;
            UIView responseView = new UIView (new RectangleF (0, 86, viewWidth, 54));
            responseView.BackgroundColor = UIColor.Clear;

            eventDoesNotExistLabel = new UILabel (new RectangleF (25, 15, viewWidth, 24));
            eventDoesNotExistLabel.TextColor = A.Color_NachoBlack;
            eventDoesNotExistLabel.TextAlignment = UITextAlignment.Left;
            eventDoesNotExistLabel.Text = "This event has either been cancelled or removed";
            eventDoesNotExistLabel.Font = A.Font_AvenirNextRegular12;
            eventDoesNotExistLabel.Hidden = true;
            responseView.Add (eventDoesNotExistLabel);

            acceptButton = new UIButton (UIButtonType.RoundedRect);
            tentativeButton = new UIButton (UIButtonType.RoundedRect);
            declineButton = new UIButton (UIButtonType.RoundedRect);

            //acceptButton
            using (var acceptButtonImage = UIImage.FromBundle ("btn-mtng-accept")) {
                acceptButton.SetImage (acceptButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
            }
            using (var acceptPressedButtonImage = UIImage.FromBundle ("btn-mtng-accept-pressed")) {
                acceptButton.SetImage (acceptPressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            acceptButton.SetTitle ("", UIControlState.Normal);
            acceptButton.Frame = new RectangleF (25, 10, 24, 24);
            acceptButton.TintColor = UIColor.Clear;

            //tentativeButton
            using (var tentativeButtonImage = UIImage.FromBundle ("btn-mtng-tenative")) {
                tentativeButton.SetImage (tentativeButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);
            }
            using (var tentativePressedButtonImage = UIImage.FromBundle ("btn-mtng-tenative-pressed")) {
                tentativeButton.SetImage (tentativePressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            tentativeButton.SetTitle ("", UIControlState.Normal);
            tentativeButton.Frame = new RectangleF ((viewWidth / 2) - 12, 10, 24, 24);
            tentativeButton.TintColor = UIColor.Clear;

            //declineButton
            using (var declineButtonImage = UIImage.FromBundle ("btn-mtng-decline")) {
                declineButton.SetImage (declineButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Normal);

            }
            using (var declinePressedButtonImage = UIImage.FromBundle ("btn-mtng-decline-pressed")) {
                declineButton.SetImage (declinePressedButtonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), UIControlState.Selected);
            }
            declineButton.SetTitle ("", UIControlState.Normal);
            declineButton.Frame = new RectangleF (viewWidth - 24 - 25, 10, 24, 24);
            declineButton.TintColor = UIColor.Clear;

            responseView.Add (acceptButton);
            responseView.Add (tentativeButton);
            responseView.Add (declineButton);

            acceptLabel = new UILabel (new RectangleF (15, 36, 44, 10));
            acceptLabel.TextColor = A.Color_NachoBlack;
            acceptLabel.TextAlignment = UITextAlignment.Center;
            acceptLabel.Font = A.Font_AvenirNextRegular10;
            acceptLabel.Text = "Accept";
            responseView.Add (acceptLabel);

            tentativeLabel = new UILabel (new RectangleF ((viewWidth / 2) - 22, 36, 44, 10));
            tentativeLabel.TextColor = A.Color_NachoBlack;
            tentativeLabel.TextAlignment = UITextAlignment.Center;
            tentativeLabel.Font = A.Font_AvenirNextRegular10;
            tentativeLabel.Text = "Tentative";
            responseView.Add (tentativeLabel);

            declineLabel = new UILabel (new RectangleF (viewWidth - 24 - 35, 36, 44, 10));
            declineLabel.TextColor = A.Color_NachoBlack;
            declineLabel.TextAlignment = UITextAlignment.Center;
            declineLabel.Font = A.Font_AvenirNextRegular10;
            declineLabel.Text = "Decline";
            responseView.Add (declineLabel);

            messageLabel = new UILabel (new RectangleF (25 + 24 + 10, 15, 100, 24));
            messageLabel.TextColor = A.Color_NachoBlack;
            messageLabel.TextAlignment = UITextAlignment.Left;
            messageLabel.Font = A.Font_AvenirNextRegular12;
            messageLabel.Hidden = true;
            responseView.Add (messageLabel);

            changeResponseButton = new UIButton (UIButtonType.RoundedRect);

            changeResponseButton.SetTitle ("Change response", UIControlState.Normal);
            changeResponseButton.Font = A.Font_AvenirNextRegular12;
            changeResponseButton.SizeToFit ();
            changeResponseButton.Frame = new RectangleF (viewWidth - changeResponseButton.Frame.Width - 25, 16, changeResponseButton.Frame.Width, 24);
            changeResponseButton.SetTitleColor (A.Color_NachoBlue, UIControlState.Normal);
            changeResponseButton.Hidden = true;
            changeResponseButton.TouchUpInside += (object sender, EventArgs e) => {
                RestoreButtons ();
            };
            responseView.Add (changeResponseButton);

            McCalendar calendarItem;
            if (null != McCalendar.QueryByUID (UID)) {
                calendarItem = McCalendar.QueryByUID (UID);

                acceptButton.TouchUpInside += (object sender, EventArgs e) => {
                    ToggleButtons (NcResponseType.Accepted);
                    acceptButton.Selected = true;
                    UpdateMeetingStatus (calendarItem, NcResponseType.Accepted);
                };

                tentativeButton.TouchUpInside += (object sender, EventArgs e) => {
                    ToggleButtons (NcResponseType.Tentative);
                    tentativeButton.Selected = true;
                    UpdateMeetingStatus (calendarItem, NcResponseType.Tentative);
                };

                declineButton.TouchUpInside += (object sender, EventArgs e) => {
                    ToggleButtons (NcResponseType.Declined);
                    declineButton.Selected = true;
                    UpdateMeetingStatus (calendarItem, NcResponseType.Declined);
                };
            } else {
                eventDoesNotExistLabel.Hidden = false;
                acceptButton.Hidden = true;
                acceptLabel.Hidden = true;
                tentativeButton.Hidden = true;
                tentativeLabel.Hidden = true;
                declineButton.Hidden = true;
                declineLabel.Hidden = true;
            }

            Add (responseView);
        }

        protected void ToggleButtons (NcResponseType r)
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

        protected void RestoreButtons ()
        {
            float viewWidth = Frame.Width;

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
        /// Map meeting uid to calendar record.
        /// </summary>
        void UpdateMeetingStatus (McCalendar c, NcResponseType status)
        {
            BackEnd.Instance.RespondCalCmd (c.AccountId, c.Id, status);
        }
    }
}

