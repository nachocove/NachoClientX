//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;

using Foundation;
using UIKit;
using CoreGraphics;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface CalendarInviteViewDelegate
    {
        void CalendarInviteViewDidChangeSize (CalendarInviteView view);
        void CalendarInviteViewDidRespond (CalendarInviteView view, NcResponseType repsonse);
        void CalendarInviteViewDidSelectCalendar (CalendarInviteView view);
    }

    public class CalendarInviteView : UIView, ThemeAdopter
    {

        public McEmailMessage Message;

        private McMeetingRequest _MeetingRequest;
        public McMeetingRequest MeetingRequest {
            get {
                return _MeetingRequest;
            }
            set {
                bool hadValue = _MeetingRequest != null;
                _MeetingRequest = value;
                Update ();
                if (hadValue) {
                    InviteDelegate?.CalendarInviteViewDidChangeSize (this);
                }
            }
        }

        private UIEdgeInsets _SeparatorInsets;
        public UIEdgeInsets SeparatorInsets {
            get {
                return _SeparatorInsets;
            }
            set {
                _SeparatorInsets = value;
                SetNeedsLayout ();
            }
        }

        public CalendarInviteViewDelegate InviteDelegate;

        UIView HeaderView;
        UIView ActionsView;
        UIImageView IconView;
        UILabel TextLabel;
        UILabel DetailTextLabel;
        UIView SeparatorView;
        UIButton AcceptButton;
        UIButton TentativeButton;
        UIButton DeclineButton;
        UIEdgeInsets ContentInsets = new UIEdgeInsets (10.0f, 0.0f, 10.0f, 14.0f);
        PressGestureRecognizer HeaderPressRecognizer;
        nfloat BorderWidth = 0.5f;
        nfloat IconMargin = 5.0f;
        CGSize IconSize = new CGSize (24.0f, 24.0f);

        public CalendarInviteView (CGRect frame) : base (frame)
        {

            HeaderView = new UIView ();
            AddSubview (HeaderView);

            ActionsView = new UIView ();
            AddSubview (ActionsView);

            IconView = new UIImageView ();
            HeaderView.AddSubview (IconView);

            TextLabel = new UILabel ();
            TextLabel.LineBreakMode = UILineBreakMode.WordWrap;
            TextLabel.Lines = 0;
            HeaderView.AddSubview (TextLabel);

            DetailTextLabel = new UILabel ();
            DetailTextLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            DetailTextLabel.Lines = 1;
            HeaderView.AddSubview (DetailTextLabel);

            AcceptButton = new UIButton (UIButtonType.Custom);
            TentativeButton = new UIButton (UIButtonType.Custom);
            DeclineButton = new UIButton (UIButtonType.Custom);
            ActionsView.AddSubview (AcceptButton);
            ActionsView.AddSubview (TentativeButton);
            ActionsView.AddSubview (DeclineButton);

            AcceptButton.TouchUpInside += Accept;
            TentativeButton.TouchUpInside += Tentative;
            DeclineButton.TouchUpInside += Decline;

            SeparatorView = new UIView ();
            AddSubview (SeparatorView);

            HeaderPressRecognizer = new PressGestureRecognizer (HeaderPressed);
            HeaderPressRecognizer.IsCanceledByPanning = true;
            HeaderView.AddGestureRecognizer (HeaderPressRecognizer);
        }

        public void Cleanup ()
        {
            AcceptButton.TouchUpInside -= Accept;
            TentativeButton.TouchUpInside -= Tentative;
            DeclineButton.TouchUpInside -= Decline;
        }

        public void AdoptTheme (Theme theme)
        {
            SeparatorView.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
            TextLabel.TextColor = theme.DefaultTextColor;
            DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
        }

        void Update ()
        {
            TextLabel.Text = Pretty.MeetingRequestTime (MeetingRequest);
            if (Message.IsMeetingResponse) {
                ActionsView.Hidden = true;
                using (var image = UIImage.FromBundle ("calendar-invite-response")) {
                    IconView.Image = image;
                }
                DetailTextLabel.Text = Pretty.MeetingResponse (Message);
            } else if (Message.IsMeetingCancelation) {
                ActionsView.Hidden = true;
                using (var image = UIImage.FromBundle ("calendar-invite-cancel")) {
                    IconView.Image = image;
                }
                DetailTextLabel.Text = "This event has been canceled";
            } else {
                ActionsView.Hidden = false;
                using (var image = UIImage.FromBundle ("calendar-invite-request")) {
                    IconView.Image = image;
                }
                if (!String.IsNullOrEmpty (MeetingRequest.Location)) {
                    DetailTextLabel.Text = MeetingRequest.Location;
                } else {
                    DetailTextLabel.Text = "";
                }
                // TODO: show response buttons
            }
            HeaderPressRecognizer.Enabled = MeetingRequest.Calendar != null;
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            var origin = new CGPoint (0.0f, 0.0f);
            var headerSubviewOrigin = new CGPoint (SeparatorInsets.Left + ContentInsets.Left, ContentInsets.Top);
            IconView.Frame = new CGRect (headerSubviewOrigin, IconSize);
            headerSubviewOrigin.X += IconView.Frame.Width + IconMargin;
            var width = Bounds.Width - ContentInsets.Right - headerSubviewOrigin.X;
            var textSize = TextLabel.SizeThatFits (new CGSize (width, 0.0f));
            TextLabel.Frame = new CGRect (headerSubviewOrigin, textSize);
            headerSubviewOrigin.Y += TextLabel.Frame.Height;
            if (String.IsNullOrEmpty (DetailTextLabel.Text)) {
                DetailTextLabel.Hidden = true;
            } else {
                headerSubviewOrigin.Y += 2.0f;
                DetailTextLabel.Hidden = false;
                DetailTextLabel.Frame = new CGRect (headerSubviewOrigin, new CGSize (width, DetailTextLabel.Font.RoundedLineHeight (1.0f)));
                headerSubviewOrigin.Y += DetailTextLabel.Frame.Height;
            }
            headerSubviewOrigin.Y += ContentInsets.Bottom;
            HeaderView.Frame = new CGRect (origin, new CGSize (Bounds.Width, headerSubviewOrigin.Y));
            origin.Y += HeaderView.Frame.Height;
            if (!ActionsView.Hidden) {
                ActionsView.Frame = new CGRect (origin, new CGSize (Bounds.Width, ActionsView.Frame.Height));
                origin.Y += ActionsView.Frame.Height;
            }
            SeparatorView.Frame = new CGRect (SeparatorInsets.Left, headerSubviewOrigin.Y, Bounds.Width - SeparatorInsets.Left, BorderWidth);
        }

        public override void SizeToFit ()
        {
            LayoutIfNeeded ();
            var frame = Frame;
            frame.Height = SeparatorView.Frame.Y + SeparatorView.Frame.Height;
            Frame = frame;
        }

        void SetHeaderSelected (bool selected, bool animated)
        {
            if (animated) {
                UIView.BeginAnimations (null, IntPtr.Zero);
                UIView.SetAnimationDuration (0.25f);
            }
            if (selected) {
                HeaderView.BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);
            } else {
                HeaderView.BackgroundColor = UIColor.White;
            }
            if (animated) {
                UIView.CommitAnimations ();
            }
        }

        void HeaderPressed ()
        {
            if (HeaderPressRecognizer.State == UIGestureRecognizerState.Began) {
                SetHeaderSelected (true, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Ended) {
                InviteDelegate?.CalendarInviteViewDidSelectCalendar (this);
                SetHeaderSelected (false, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Failed) {
                SetHeaderSelected (false, animated: true);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                SetHeaderSelected (false, animated: false);
            }
        }

        void Accept (object sender, EventArgs e)
        {
            InviteDelegate?.CalendarInviteViewDidRespond (this, NcResponseType.Accepted);
        }

        void Tentative (object sender, EventArgs e)
        {
            InviteDelegate?.CalendarInviteViewDidRespond (this, NcResponseType.Tentative);
        }

        void Decline (object sender, EventArgs e)
        {
            InviteDelegate?.CalendarInviteViewDidRespond (this, NcResponseType.Declined);
        }
    }
}
