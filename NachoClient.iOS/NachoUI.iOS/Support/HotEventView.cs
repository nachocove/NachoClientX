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
    public class HotEventView : UIView, IUIGestureRecognizerDelegate
    {

        static public readonly nfloat PreferredHeight = 69.0f;

        private McEvent _Event;
        public McEvent Event {
            get {
                return _Event;
            }
            set {
                _Event = value;
                EventPressGestureRecognizer.Enabled = _Event != null;
                Update ();
            }
        }
        public readonly SwipeActionsView SwipeView;
        public Action Action;
        UIEdgeInsets ContentInsets;
        UIEdgeInsets TextInsets;
        UILabel TitleLabel;
        UILabel DetailLabel;
        UILabel DateLabel;
        UILabel EmptyLabel;
        UIImageView IconView;
        nfloat IconSize = 16.0f;
        NcTimer ChangeDateLabelTimer;
        UIView BottomBorder;

        PressGestureRecognizer EventPressGestureRecognizer;

        nfloat BorderWidth = 0.5f;

        private bool _Selected;
        public bool Selected {
            get {
                return _Selected;
            }
            set {
                SetSelected (value, false);
            }
        }

        private UIView ContentView {
            get {
                return SwipeView.ContentView;
            }
        }

        public HotEventView (CGRect rect) : base (rect)
        {
            ContentInsets = new UIEdgeInsets (0.0f, 10.0f, 0.0f, 10.0f);
            TextInsets = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 10.0f);

            SwipeView = new SwipeActionsView (Bounds);
            SwipeView.BackgroundColor = UIColor.White;
            SwipeView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            AddSubview (SwipeView);

            IconView = new UIImageView (UIImage.FromBundle ("nav-calendar-active").ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate));
            IconView.Frame = new CGRect (0.0f, 0.0f, IconSize, IconSize);
            ContentView.AddSubview (IconView);

            EmptyLabel = new UILabel (Bounds);
            EmptyLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            EmptyLabel.Lines = 1;
            EmptyLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            EmptyLabel.Font = A.Font_AvenirNextDemiBold17;
            EmptyLabel.TextColor = A.Color_NachoTextGray;
            EmptyLabel.TextAlignment = UITextAlignment.Center;
            EmptyLabel.Hidden = true;
            EmptyLabel.Text = "No upcoming events";
            ContentView.AddSubview (EmptyLabel);

            TitleLabel = new UILabel ();
            TitleLabel.Lines = 1;
            TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            TitleLabel.TextColor = A.Color_NachoGreen;
            ContentView.AddSubview (TitleLabel);

            DetailLabel = new UILabel ();
            DetailLabel.Lines = 1;
            DetailLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            DetailLabel.Font = A.Font_AvenirNextRegular14;
            DetailLabel.TextColor = A.Color_NachoTextGray;
            ContentView.AddSubview (DetailLabel);

            DateLabel = new UILabel ();
            DateLabel.Lines = 1;
            DateLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            DateLabel.Font = A.Font_AvenirNextRegular14;
            DateLabel.TextColor = A.Color_NachoTextGray;
            ContentView.AddSubview (DateLabel);

            BottomBorder = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, BorderWidth));
            BottomBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            ContentView.AddSubview (BottomBorder);

            EventPressGestureRecognizer = new PressGestureRecognizer (EventPressed);
            EventPressGestureRecognizer.IsCanceledByPanning = true;
            EventPressGestureRecognizer.DelaysStart = true;
            EventPressGestureRecognizer.Delegate = this;
            ContentView.AddGestureRecognizer (EventPressGestureRecognizer);

        }

        public void CancelAutomaticDateUpdate ()
        {
            if (ChangeDateLabelTimer != null) {
                ChangeDateLabelTimer.Dispose ();
                ChangeDateLabelTimer = null;
            }
        }

        void EventPressed ()
        {
            if (EventPressGestureRecognizer.State == UIGestureRecognizerState.Began) {
                SetSelected (true, animated: false);
            } else if (EventPressGestureRecognizer.State == UIGestureRecognizerState.Ended) {
                SetSelected (true, animated: false);
                Action ();
            }else if (EventPressGestureRecognizer.State == UIGestureRecognizerState.Changed) {
                SetSelected (EventPressGestureRecognizer.IsInsideView, animated: false);
            } else if (EventPressGestureRecognizer.State == UIGestureRecognizerState.Failed) {
                SetSelected (false, animated: true);
            } else if (EventPressGestureRecognizer.State == UIGestureRecognizerState.Cancelled) {
                SetSelected (false, animated: false);
            }
        }

        [Foundation.Export ("gestureRecognizerShouldBegin:")]
        public bool ShouldBegin (UIKit.UIGestureRecognizer recognizer)
        {
            if (recognizer == EventPressGestureRecognizer) {
                return !SwipeView.IsEditing ();
            }
            return false;
        }

        public void SetSelected (bool selected, bool animated)
        {
            _Selected = selected;
            if (animated) {
                UIView.BeginAnimations (null, IntPtr.Zero);
                UIView.SetAnimationDuration (0.25f);
            }
            if (selected) {
                ContentView.BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);
            } else {
                ContentView.BackgroundColor = UIColor.White;
            }
            if (animated) {
                UIView.CommitAnimations ();
            }
        }

        void Update ()
        {
            var enabled = Event != null;
            IconView.Hidden = !enabled;
            TitleLabel.Hidden = !enabled;
            DetailLabel.Hidden = !enabled;
            DateLabel.Hidden = !enabled;
            EmptyLabel.Hidden = enabled;
            SwipeView.Enabled = enabled;

            if (ChangeDateLabelTimer != null) {
                ChangeDateLabelTimer.Dispose ();
            }

            if (Event != null) {
                TitleLabel.Text = Pretty.SubjectString (Event.Subject);
                DetailLabel.Text = Event.Location ?? "";
                DetailLabel.Hidden = String.IsNullOrWhiteSpace (DetailLabel.Text);

                UIColor iconColor = A.Color_NachoYellow;
                var colorIndex = Event.GetColorIndex ();
                if (colorIndex > 0) {
                    iconColor = Util.CalendarColor (colorIndex);
                }
                IconView.TintColor = iconColor;
                TimeSpan labelValidSpan = TimeSpan.FromSeconds(-2);
                var start = Event.GetStartTimeUtc ();
                if (Event.AllDayEvent) {
                    DateLabel.Text = Pretty.EventDay (start, out labelValidSpan);
                } else {
                    DateLabel.Text = Pretty.EventTime (start, out labelValidSpan);
                }
                // adjust by one second so we're always on the under side of a Ceiling operation
                labelValidSpan = labelValidSpan + TimeSpan.FromSeconds (1);
                if (labelValidSpan.TotalSeconds > 0.0) {
                    ChangeDateLabelTimer = new NcTimer ("HotEventView_UpdateDateLabel", ChangeDateLabelTimerFired, null, labelValidSpan, TimeSpan.Zero);
                }
            }
            SetNeedsLayout ();
        }

        void ChangeDateLabelTimerFired (object state)
        {
            BeginInvokeOnMainThread (Update);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            if (Event != null) {
                var dateSize = DateLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
                var titleHeight = TitleLabel.Font.RoundedLineHeight (1.0f);
                nfloat detailHeight = 0.0f;
                if (!DetailLabel.Hidden) {
                    detailHeight = DetailLabel.Font.RoundedLineHeight (1.0f);
                }
                var textHeight = titleHeight + detailHeight;
                var textTop = (ContentView.Bounds.Height - textHeight) / 2.0f;

                CGRect frame;

                frame = DateLabel.Frame;
                frame.X = ContentView.Bounds.Width - ContentInsets.Right - TextInsets.Right - dateSize.Width;
                frame.Width = dateSize.Width;
                frame.Height = DateLabel.Font.RoundedLineHeight (1.0f);
                frame.Y = (ContentView.Bounds.Height - frame.Height) / 2.0f;
                DateLabel.Frame = frame;

                frame = TitleLabel.Frame;
                frame.X = ContentInsets.Left + TextInsets.Left;
                frame.Y = textTop;
                frame.Width = DateLabel.Frame.X - frame.X;
                frame.Height = titleHeight;
                TitleLabel.Frame = frame;

                if (!DetailLabel.Hidden) {
                    frame = DetailLabel.Frame;
                    frame.X = TitleLabel.Frame.X;
                    frame.Y = TitleLabel.Frame.Y + TitleLabel.Frame.Height;
                    frame.Width = TitleLabel.Frame.Width;
                    frame.Height = detailHeight;
                    DetailLabel.Frame = frame;
                }

                IconView.Center = new CGPoint (ContentInsets.Left + TextInsets.Left / 2.0f, ContentView.Bounds.Height / 2.0f);
            }

            BottomBorder.Frame = new CGRect (0.0f, ContentView.Bounds.Height - BorderWidth, ContentView.Bounds.Width, BorderWidth);
        }

    }
}

