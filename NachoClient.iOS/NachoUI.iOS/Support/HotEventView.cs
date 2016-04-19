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

        static public readonly nfloat PreferredHeight = 69.0f;

        private McEvent _Event;
        public McEvent Event {
            get {
                return _Event;
            }
            set {
                _Event = value;
                Update ();
            }
        }
        public readonly SwipeActionsView SwipeView;
        public Action Action;
        UIEdgeInsets ContentInsets;
        UILabel TitleLabel;
        UILabel DetailLabel;
        UILabel DateLabel;
        UILabel EmptyLabel;
        UIImageView IconView;
        nfloat IconSize = 16.0f;

        private UIView ContentView {
            get {
                return SwipeView.ContentView;
            }
        }

        public HotEventView (CGRect rect) : base (rect)
        {
            ContentInsets = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 10.0f);

            SwipeView = new SwipeActionsView (Bounds);
            SwipeView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            AddSubview (SwipeView);

            using (var image = UIImage.FromBundle ("nav-calendar-active").ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                IconView = new UIImageView (image);
                IconView.Frame = new CGRect (0.0f, 0.0f, IconSize, IconSize);
            }
            ContentView.AddSubview (IconView);

            EmptyLabel = new UILabel (Bounds);
            EmptyLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            EmptyLabel.Lines = 1;
            EmptyLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            EmptyLabel.Font = A.Font_AvenirNextDemiBold17;
            EmptyLabel.TextColor = A.Color_NachoTextGray;
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

            ContentView.AddGestureRecognizer (new UITapGestureRecognizer (Tap));
        }

        void Tap ()
        {
            if (Event != null) {
                Action ();
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

                var start = Event.GetStartTimeUtc ();
                if (Event.AllDayEvent) {
                    DateLabel.Text = Pretty.LongFullDate (start);
                } else {
                    var diff = start - DateTime.UtcNow;
                    if (diff.TotalHours < 12) {
                        DateLabel.Text = Pretty.Time (start);
                    } else {
                        DateLabel.Text = Pretty.LongDayTime (start);
                    }
                }
            }
            SetNeedsLayout ();
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
                frame.X = ContentView.Bounds.Width - ContentInsets.Right - dateSize.Width;
                frame.Width = dateSize.Width;
                frame.Height = DateLabel.Font.RoundedLineHeight (1.0f);
                frame.Y = (ContentView.Bounds.Height - frame.Height) / 2.0f;
                DateLabel.Frame = frame;

                frame = TitleLabel.Frame;
                frame.X = ContentInsets.Left;
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
                }

                IconView.Center = new CGPoint ((ContentInsets.Left - IconView.Frame.Width) / 2.0f, ContentView.Bounds.Height / 2.0f);
            }
        }

    }
}

