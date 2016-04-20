//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using System.Linq;
using System.Collections.Generic;

using UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class HotEventView : UIView
    {
        private const int SWIPE_TAG = 3900;
        private const int DOT_TAG = 3901;
        private const int SUBJECT_TAG = 3902;
        private const int ICON_TAG = 3903;
        private const int TEXT_TAG = 3904;
        private const int NO_MESSAGES_TAG = 3905;

        public const int DIAL_IN_TAG = 1;
        public const int NAVIGATE_TO_TAG = 2;
        public const int LATE_TAG = 3;
        public const int FORWARD_TAG = 4;
        public const int OPEN_TAG = 5;

        public delegate void ButtonCallback (int tag, int eventId);

        public ButtonCallback OnClick;

        protected McEvent currentEvent;
        protected UITapGestureRecognizer tapRecognizer;
        protected NcTimer eventEndTimer = null;

        // Pre-made swipe action descriptors
//        private static SwipeActionDescriptor DIAL_IN_BUTTON =
//            new SwipeActionDescriptor (DIAL_IN_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeDialIn),
//                "Dial In", A.Color_NachoSwipeDialIn);
//        private static SwipeActionDescriptor NAVIGATE_BUTTON =
//            new SwipeActionDescriptor (NAVIGATE_TO_TAG, 0.25f, UIImage.FromBundle (A.File_NachoSwipeNavigate),
//                "Navigate To", A.Color_NachoSwipeNavigate);
        private static SwipeActionDescriptor LATE_BUTTON =
            new SwipeActionDescriptor (LATE_TAG, 0.5f, UIImage.FromBundle (A.File_NachoSwipeLate),
                "I'm Late", A.Color_NachoSwipeLate);
        private static SwipeActionDescriptor FORWARD_BUTTON =
            new SwipeActionDescriptor (FORWARD_TAG, 0.5f, UIImage.FromBundle (A.File_NachoSwipeForward),
                "Forward", A.Color_NachoeSwipeForward);

        public HotEventView (CGRect rect) : base (rect)
        {
            var cellWidth = rect.Width;

            var view = new SwipeActionView (rect);
            view.Tag = SWIPE_TAG;

            this.AddSubview (view);

//            view.SetAction (NAVIGATE_BUTTON, SwipeSide.LEFT);
//            view.SetAction (DIAL_IN_BUTTON, SwipeSide.LEFT);
            view.SetAction (LATE_BUTTON, SwipeSide.LEFT);
            view.SetAction (FORWARD_BUTTON, SwipeSide.RIGHT);

            // Dot image view
            var dotView = new UIImageView (new CGRect (30, 20, 9, 9));
            dotView.Tag = DOT_TAG;
            view.AddSubview (dotView);

            // Subject label view
            var subjectLabelView = new UILabel (new CGRect (56, 15, cellWidth - 56, 20));
            subjectLabelView.Font = A.Font_AvenirNextDemiBold17;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            // No messages label view
            var noMessagesLabelView = new UILabel (rect);
            noMessagesLabelView.Font = A.Font_AvenirNextDemiBold17;
            noMessagesLabelView.TextColor = A.Color_0F424C;
            noMessagesLabelView.Tag = NO_MESSAGES_TAG;
            noMessagesLabelView.TextAlignment = UITextAlignment.Center;
            view.AddSubview (noMessagesLabelView);

            // Location image view
            var iconView = new UIImageView (new CGRect (30, 40, 12, 12));
            iconView.Tag = ICON_TAG;
            iconView.Image = UIImage.FromBundle ("cal-icn-pin");
            view.AddSubview (iconView);

            // Location label view
            var labelView = new UILabel (new CGRect (56, 37, cellWidth - 56, 20));
            labelView.Font = A.Font_AvenirNextRegular14;
            labelView.TextColor = A.Color_0F424C;
            labelView.Tag = TEXT_TAG;
            view.AddSubview (labelView);

            var bottomLine = new UIView (new CGRect (0, this.Frame.Height - 1, this.Frame.Width, 1));
            bottomLine.BackgroundColor = A.Color_NachoBackgroundGray;
            view.AddSubview (bottomLine);

            tapRecognizer = new UITapGestureRecognizer ((UITapGestureRecognizer tap) => {
                SendClick (OPEN_TAG);
            });
            this.AddGestureRecognizer (tapRecognizer);

            // Have the event manager keep the McEvents accurate for at least the next seven days.
            NcEventManager.AddEventWindow (this, new TimeSpan (7, 0, 0, 0));
        }

        public void ViewWillAppear ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            Configure ();
        }

        public void ViewWillDisappear ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            if (null != eventEndTimer) {
                eventEndTimer.Dispose ();
                eventEndTimer = null;
            }
        }

        public void Configure ()
        {
            DateTime timerFireTime;
            currentEvent = CalendarHelper.CurrentOrNextEvent (out timerFireTime);
            ConfigureCurrentEvent ();

            if (null != eventEndTimer) {
                eventEndTimer.Dispose ();
                eventEndTimer = null;
            }

            // Set a timer to fire at the end of the currently displayed event, so the view can
            // be reconfigured to show the next event.
            if (null != currentEvent) {
                TimeSpan timerDuration = timerFireTime - DateTime.UtcNow;
                if (timerDuration < TimeSpan.Zero) {
                    // The time to reevaluate the current event was in the very near future, and that time was reached in between
                    // CurrentOrNextEvent() and now.  Configure the timer to fire immediately.
                    timerDuration = TimeSpan.Zero;
                }
                eventEndTimer = new NcTimer ("HotEventView", (state) => {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        Configure ();
                    });
                }, null, timerDuration, TimeSpan.Zero);
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;

            switch (statusEvent.Status.SubKind) {

            case NcResult.SubKindEnum.Info_EventSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                Configure ();
                break;

            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                // When the app goes into the background, eventEndTimer might get cancelled, but ViewWillAppear
                // won't get called when the app returns to the foreground.  That might leave the view displaying
                // an old event.  Watch for foreground events and refresh the view.
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    Configure ();
                }
                break;
            }
        }

        private void SendClick (int tag)
        {
            if ((null != currentEvent) && (null != OnClick)) {
                OnClick (tag, currentEvent.Id);
            }
        }

        public void ConfigureCurrentEvent ()
        {
            McAbstrCalendarRoot c = null;
            McCalendar cRoot = null;

            if (null != currentEvent) {
                c = currentEvent.GetCalendarItemforEvent ();
                cRoot = McCalendar.QueryById<McCalendar> (currentEvent.CalendarId);
            }

            var view = (SwipeActionView)this.ViewWithTag (SWIPE_TAG);
            var noMessagesLabelView = (UILabel)this.ViewWithTag (NO_MESSAGES_TAG);
            var subjectLabelView = (UILabel)this.ViewWithTag (SUBJECT_TAG);
            var labelView = (UILabel)this.ViewWithTag (TEXT_TAG);
            var dotView = (UIImageView)this.ViewWithTag (DOT_TAG);
            var iconView = (UIImageView)this.ViewWithTag (ICON_TAG);

            if (null == c || null == cRoot) {
                noMessagesLabelView.Text = "No upcoming events";
                noMessagesLabelView.Hidden = false;
                subjectLabelView.Hidden = true;
                labelView.Hidden = true;
                iconView.Hidden = true;
                dotView.Hidden = true;
                view.OnSwipe = null;
                view.OnClick = null;
                view.DisableSwipe ();
                return;
            }

            view.EnableSwipe (!string.IsNullOrEmpty (cRoot.OrganizerEmail));

            noMessagesLabelView.Hidden = true;

            // Subject label view
            var subject = Pretty.SubjectString (c.GetSubject ());
            subjectLabelView.Text = subject;
            subjectLabelView.Hidden = false;

            int colorIndex = 0;
            var folder = McFolder.QueryByFolderEntryId<McCalendar> (cRoot.AccountId, cRoot.Id).FirstOrDefault ();
            if (null != folder) {
                colorIndex = folder.DisplayColor;
            }
            dotView.Image = Util.DrawCalDot (Util.CalendarColor (colorIndex), new CGSize (10, 10));
            dotView.Hidden = false;

            var startString = "";
            if (c.AllDayEvent) {
                startString = "ALL DAY " + Pretty.LongFullDate (currentEvent.GetStartTimeUtc ());
            } else {
                if ((currentEvent.GetStartTimeUtc () - DateTime.UtcNow).TotalHours < 12) {
                    startString = Pretty.Time (currentEvent.GetStartTimeUtc ());
                } else {
                    startString = Pretty.LongDayTime (currentEvent.GetStartTimeUtc ());
                }
            }

            var locationString = Pretty.SubjectString (c.GetLocation ());
            var eventString = Pretty.Join (startString, locationString, " : ");

            iconView.Hidden = String.IsNullOrEmpty (eventString);
            labelView.Text = eventString;
            labelView.Hidden = false;

            view.OnClick = (int tag) => {
                switch (tag) {
                case NAVIGATE_TO_TAG:
                    SendClick (NAVIGATE_TO_TAG);
                    break;
                case FORWARD_TAG:
                    SendClick (FORWARD_TAG);
                    break;
                case DIAL_IN_TAG:
                    SendClick (DIAL_IN_TAG);
                    break;
                case LATE_TAG:
                    SendClick (LATE_TAG);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };
            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };
        }
    }
}

