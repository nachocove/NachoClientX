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
        void CalendarInviteViewDidRespond (CalendarInviteView view, NcResponseType response);
        void CalendarInviteViewDidRemove (CalendarInviteView view);
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
        NcSimpleColorButton AcceptButton;
        NcSimpleColorButton TentativeButton;
        NcSimpleColorButton DeclineButton;
        NcSimpleColorButton RemoveButton;
        UIEdgeInsets ContentInsets = new UIEdgeInsets (10.0f, 0.0f, 10.0f, 14.0f);
        PressGestureRecognizer HeaderPressRecognizer;
        nfloat BorderWidth = 0.5f;
        nfloat IconMargin = 5.0f;
        nfloat ButtonHeight = 44.0f;
        CGSize IconSize = new CGSize (24.0f, 24.0f);

        public CalendarInviteView (CGRect frame) : base (frame)
        {

            HeaderView = new UIView ();
            AddSubview (HeaderView);

            ActionsView = new UIView (Bounds);
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

            AcceptButton = new NcSimpleColorButton ();
            AcceptButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            AcceptButton.SetTitle ("Accept and add to calendar", UIControlState.Normal);
            using (var image = UIImage.FromBundle ("calendar-invite-action-accept")) {
                AcceptButton.SetImage (image, UIControlState.Normal);
            }
            AcceptButton.TitleEdgeInsets = new UIEdgeInsets (0, 4.0f + IconMargin, 0, 0);
            ActionsView.AddSubview (AcceptButton);

            TentativeButton = new NcSimpleColorButton ();
            TentativeButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            TentativeButton.SetTitle ("Tentatively accept", UIControlState.Normal);
            using (var image = UIImage.FromBundle ("calendar-invite-action-maybe")) {
                TentativeButton.SetImage (image, UIControlState.Normal);
            }
            TentativeButton.TitleEdgeInsets = new UIEdgeInsets (0, 4.0f + IconMargin, 0, 0);
            ActionsView.AddSubview (TentativeButton);

            DeclineButton = new NcSimpleColorButton ();
            DeclineButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            DeclineButton.SetTitle ("Decline", UIControlState.Normal);
            using (var image = UIImage.FromBundle ("calendar-invite-action-decline")) {
                DeclineButton.SetImage (image, UIControlState.Normal);
            }
            DeclineButton.TitleEdgeInsets = new UIEdgeInsets (0, 4.0f + IconMargin, 0, 0);
            ActionsView.AddSubview (DeclineButton);

            RemoveButton = new NcSimpleColorButton ();
            RemoveButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            RemoveButton.SetTitle ("Remove from calendar", UIControlState.Normal);
            using (var image = UIImage.FromBundle ("calendar-invite-action-decline")) {
                RemoveButton.SetImage (image, UIControlState.Normal);
            }
            RemoveButton.TitleEdgeInsets = new UIEdgeInsets (0, 4.0f + IconMargin, 0, 0);
            ActionsView.AddSubview (RemoveButton);

            AcceptButton.TouchUpInside += Accept;
            TentativeButton.TouchUpInside += Tentative;
            DeclineButton.TouchUpInside += Decline;
            RemoveButton.TouchUpInside += Remove;

            SeparatorView = new UIView ();
            AddSubview (SeparatorView);

            //HeaderPressRecognizer = new PressGestureRecognizer (HeaderPressed);
            //HeaderPressRecognizer.IsCanceledByPanning = true;
            //HeaderView.AddGestureRecognizer (HeaderPressRecognizer);
        }

        public void Cleanup ()
        {
            AcceptButton.TouchUpInside -= Accept;
            TentativeButton.TouchUpInside -= Tentative;
            DeclineButton.TouchUpInside -= Decline;
            RemoveButton.TouchUpInside -= Remove;
        }

        public void AdoptTheme (Theme theme)
        {
            SeparatorView.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
            TextLabel.TextColor = theme.DefaultTextColor;
            DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
            TintColor = theme.TableViewTintColor;

            AcceptButton.BackgroundColor = UIColor.White;
            AcceptButton.HighlightedColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            TentativeButton.BackgroundColor = UIColor.White;
            TentativeButton.HighlightedColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            DeclineButton.BackgroundColor = UIColor.White;
            DeclineButton.HighlightedColor = UIColor.White.ColorDarkenedByAmount (0.15f);
            RemoveButton.BackgroundColor = UIColor.White;
            RemoveButton.HighlightedColor = UIColor.White.ColorDarkenedByAmount (0.15f);

            AcceptButton.SetTitleColor (theme.TableViewTintColor, UIControlState.Normal);
            TentativeButton.SetTitleColor (theme.TableViewTintColor, UIControlState.Normal);
            DeclineButton.SetTitleColor (theme.TableViewTintColor, UIControlState.Normal);
            RemoveButton.SetTitleColor (theme.TableViewTintColor, UIControlState.Normal);

            AcceptButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
            TentativeButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
            DeclineButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
            RemoveButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
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
                ActionsView.Hidden = !MeetingRequest.IsOnCalendar ();
                AcceptButton.Hidden = true;
                TentativeButton.Hidden = true;
                DeclineButton.Hidden = true;
                RemoveButton.Hidden = false;
                using (var image = UIImage.FromBundle ("calendar-invite-cancel")) {
                    IconView.Image = image;
                }
                DetailTextLabel.Text = "This event has been canceled";
            } else {
                ActionsView.Hidden = false;
                AcceptButton.Hidden = false;
                TentativeButton.Hidden = false;
                DeclineButton.Hidden = false;
                RemoveButton.Hidden = true;
                using (var image = UIImage.FromBundle ("calendar-invite-request")) {
                    IconView.Image = image;
                }
                if (!String.IsNullOrEmpty (MeetingRequest.Location)) {
                    DetailTextLabel.Text = MeetingRequest.Location;
                } else {
                    DetailTextLabel.Text = "";
                }
            }
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
                var actionOrigin = new CGPoint (0.0f, 0.0f);
                foreach (var button in ActionsView.Subviews) {
                    if (!button.Hidden) {
                        button.Frame = new CGRect (actionOrigin, new CGSize (Bounds.Width, ButtonHeight));
                        actionOrigin.Y += button.Frame.Height;
                    }
                }
                ActionsView.Frame = new CGRect (origin, new CGSize (Bounds.Width, actionOrigin.Y));
                origin.Y += ActionsView.Frame.Height;
                AcceptButton.ContentEdgeInsets = new UIEdgeInsets (0, SeparatorInsets.Left, 0, 0);
                TentativeButton.ContentEdgeInsets = new UIEdgeInsets (0, SeparatorInsets.Left, 0, 0);
                DeclineButton.ContentEdgeInsets = new UIEdgeInsets (0, SeparatorInsets.Left, 0, 0);
                RemoveButton.ContentEdgeInsets = new UIEdgeInsets (0, SeparatorInsets.Left, 0, 0);
            }
            SeparatorView.Frame = new CGRect (SeparatorInsets.Left, origin.Y, Bounds.Width - SeparatorInsets.Left, BorderWidth);
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
                //InviteDelegate?.CalendarInviteViewDidSelectCalendar (this);
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

        void Remove (object sender, EventArgs e)
        {
            InviteDelegate?.CalendarInviteViewDidRemove (this);
        }
    }
}
