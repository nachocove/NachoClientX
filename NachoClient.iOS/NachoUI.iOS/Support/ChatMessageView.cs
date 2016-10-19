//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using System.Linq;

namespace NachoClient.iOS
{

    public class ChatMessageView : UIView, ThemeAdopter
    {
        public ChatView ChatView;
        public UIView BubbleView;
        public AttachmentView.AttachmentSelectedCallback OnAttachmentSelected;
        McEmailMessage Message;
        McChatParticipant Participant;
        List<McAttachment> Attachments;
        UITextView MessageLabel;
        public int Index;
        UIEdgeInsets MessageInsets;
        UITapGestureRecognizer TapRecognizer;
        nfloat BubbleSideInset = 15.0f;
        nfloat PortraitBubbleSpacing = 7.0f;
        nfloat TimestampRevealRightSpacing = 10.0f;
        nfloat MaxBubbleWidthPercent = 0.75f;
        bool ForceIsLoading;
        bool IsLoaded;
        public bool IsMe {
            get {
                return Participant == null;
            }
        }
        Theme adoptedTheme;
        UILabel TimestampDividerLabel;
        UILabel TimestampRevealLabel;
        UILabel _NameLabel;
        UILabel NameLabel {
            get {
                if (_NameLabel == null) {
                    _NameLabel = new UILabel (new CGRect (0.0f, 0.0f, BubbleView.Frame.Width, 0.0f));
                    _NameLabel.Lines = 1;
                    _NameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
                    ApplyThemeToNameLabel ();
                    AddSubview (_NameLabel);
                }
                return _NameLabel;
            }
        }
        PortraitView _PortraitView = null;
        PortraitView PortraitView {
            get {
                if (_PortraitView == null) {
                    _PortraitView = new PortraitView (new CGRect (0.0f, 0.0f, 20.0f, 20.0f));
                    AddSubview (_PortraitView);
                }
                return _PortraitView;
            }
        }
        UIImageView _ErrorIndicator;
        UIImageView ErrorIndicator {
            get {
                if (_ErrorIndicator == null) {
                    using (var image = UIImage.FromBundle ("Slide1-5")) {
                        _ErrorIndicator = new UIImageView (image);
                        _ErrorIndicator.AddGestureRecognizer (new UITapGestureRecognizer (ErrorTap));
                        _ErrorIndicator.UserInteractionEnabled = true;
                        AddSubview (_ErrorIndicator);
                    }
                }
                return _ErrorIndicator;
            }
        }
        List<UIView> AttachmentViews;

        public ChatMessageView (CGRect frame) : base (frame)
        {
            AttachmentViews = new List<UIView> ();
            MessageInsets = new UIEdgeInsets (6.0f, 9.0f, 6.0f, 9.0f);
            BubbleView = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width * 0.75f, Bounds.Height));
            BubbleView.Layer.MasksToBounds = true;
            BubbleView.Layer.BorderWidth = 1.0f;
            BubbleView.Layer.CornerRadius = 8.0f;
            MessageLabel = new UITextView (new CGRect (MessageInsets.Left, MessageInsets.Top, BubbleView.Bounds.Width - MessageInsets.Left - MessageInsets.Right, BubbleView.Bounds.Height - MessageInsets.Top - MessageInsets.Bottom));
            MessageLabel.Editable = false;
            MessageLabel.DataDetectorTypes = UIDataDetectorType.All;
            MessageLabel.UserInteractionEnabled = true;
            MessageLabel.ScrollEnabled = false;
            MessageLabel.DelaysContentTouches = false;
            MessageLabel.TextContainerInset = new UIEdgeInsets (0, 0, 0, 0);
            TimestampDividerLabel = new UILabel (new CGRect (0.0f, 0.0f, Bounds.Width, 40.0f));
            TimestampDividerLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            TimestampDividerLabel.TextAlignment = UITextAlignment.Center;
            TimestampDividerLabel.Lines = 1;
            TimestampRevealLabel = new UILabel (new CGRect (Bounds.Width, 0.0f, Bounds.Width, 20.0f));
            TimestampRevealLabel.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            TimestampRevealLabel.TextAlignment = UITextAlignment.Right;
            TimestampRevealLabel.Lines = 1;
            AddSubview (TimestampDividerLabel);
            AddSubview (TimestampRevealLabel);
            BubbleView.AddSubview (MessageLabel);
            AddSubview (BubbleView);

            TapRecognizer = new UITapGestureRecognizer (Tap);
            BubbleView.AddGestureRecognizer (TapRecognizer);
        }

        void Tap (UITapGestureRecognizer recognizer)
        {
            ChatView.Delegate.ChatViewDidSelectMessage (ChatView, Index);
        }

        public void AdoptTheme (Theme theme)
        {
            adoptedTheme = theme;
            MessageLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TimestampDividerLabel.Font = theme.BoldDefaultFont.WithSize (14.0f);
            TimestampDividerLabel.TextColor = theme.ChatTimestampTextColor;
            TimestampRevealLabel.Font = theme.DefaultFont.WithSize (14.0f);
            TimestampRevealLabel.TextColor = theme.ChatTimestampTextColor;
            UpdateBubbleForThemeAndParticipant ();
            ApplyThemeToNameLabel ();
            foreach (var view in AttachmentViews) {
                var attachmentView = view as ChatMessageAttachmentView;
                if (attachmentView != null) {
                    attachmentView.SetColors (MessageLabel.BackgroundColor, MessageLabel.TextColor);
                }
            }
            SetNeedsLayout ();
        }

        void ApplyThemeToNameLabel ()
        {
            if (adoptedTheme != null) {
                if (_NameLabel != null) {
                    _NameLabel.Font = adoptedTheme.DefaultFont.WithSize (14.0f);
                    _NameLabel.TextColor = adoptedTheme.ChatTimestampTextColor;
                }
            }
        }

        void UpdateBubbleForThemeAndParticipant ()
        {
            if (adoptedTheme != null) {
                if (Participant == null) {
                    BubbleView.BackgroundColor = adoptedTheme.ChatBubbleColorForMe;
                    BubbleView.Layer.BorderColor = adoptedTheme.ChatBubbleBorderColorForMe.CGColor;
                    MessageLabel.TextColor = adoptedTheme.ChatBubbleTextColorForMe;
                } else {
                    BubbleView.BackgroundColor = adoptedTheme.ChatBubbleColorForOther;
                    BubbleView.Layer.BorderColor = adoptedTheme.ChatBubbleBorderColorForOther.CGColor;
                    MessageLabel.TextColor = IsLoaded ? adoptedTheme.ChatBubbleTextColorForOther : adoptedTheme.ChatBubbleTextColorForOtherLoading;
                }
                MessageLabel.BackgroundColor = BubbleView.BackgroundColor;
            }
        }

        public void SetMessage (McEmailMessage message, McChatParticipant participant, List<McAttachment> attachments, bool forceIsLoading = false)
        {
            Message = message;
            Participant = participant;
            Attachments = attachments;
            ForceIsLoading = forceIsLoading;
            Update ();
        }

        public void SetShowsPortrait (bool showsPortrait)
        {
            if (ChatView != null && ChatView.ShowPortraits && Participant != null) {
                PortraitView.Hidden = !showsPortrait;
            }
        }

        public void SetShowsName (bool showsName)
        {
            if (ChatView != null && ChatView.ShowNameLabels && Participant != null) {
                NameLabel.Hidden = !showsName;
            }
        }

        public void SetShowsTimestamp (bool showsTimestamp)
        {
            TimestampDividerLabel.Hidden = !showsTimestamp;
        }

        public void Update (bool forceHasError = false)
        {
            bool hasError = false;
            IsLoaded = false;
            if (Message == null) {
                MessageLabel.Text = "";
            } else {
                if (ForceIsLoading) {
                    MessageLabel.Text = "Loading Message...";
                } else {
                    if (Message.BodyId == 0) {
                        MessageLabel.Text = "Loading Message...";
                        ChatView.Delegate.ChatMessageViewNeedsLoad (ChatView, Message);
                    } else {
                        var bundle = new NcEmailMessageBundle (Message);
                        if (bundle.NeedsUpdate) {
                            MessageLabel.Text = "Loading Message...";
                            ChatView.Delegate.ChatMessageViewNeedsLoad (ChatView, Message);
                        } else {
                            IsLoaded = true;
                            MessageLabel.Text = bundle.TopText;
                        }
                    }
                }
                TimestampDividerLabel.Text = Pretty.VariableDayTime (Message.DateReceived);
                TimestampRevealLabel.Text = Pretty.Time (Message.DateReceived);
                if (Participant == null) {
                    var pending = McPending.QueryByEmailMessageId (Message.AccountId, Message.Id);
                    hasError = forceHasError || (pending != null && pending.ResultKind == NcResult.KindEnum.Error);
                }
            }
            if (Participant == null) {
                if (ChatView != null && ChatView.ShowPortraits) {
                    PortraitView.SetPortrait (0, 0, "");
                    PortraitView.Hidden = true;
                }
                if (ChatView != null && ChatView.ShowNameLabels) {
                    NameLabel.Text = "";
                    NameLabel.Hidden = true;
                }
            } else {
                if (ChatView != null && ChatView.ShowPortraits) {
                    PortraitView.SetPortrait (Participant.CachedPortraitId, Participant.CachedColor, Participant.CachedInitials);
                    PortraitView.Hidden = false;
                }
                if (ChatView != null && ChatView.ShowNameLabels) {
                    NameLabel.Text = Participant.CachedName;
                    NameLabel.Hidden = false;
                }
            }
            if (hasError) {
                ErrorIndicator.Hidden = false;
            } else {
                if (_ErrorIndicator != null) {
                    _ErrorIndicator.Hidden = true;
                }
            }
            TimestampDividerLabel.Hidden = false;
            UpdateBubbleForThemeAndParticipant ();
            UpdateAttachments ();
            SetNeedsLayout ();
        }

        void UpdateAttachments ()
        {
            foreach (var view in AttachmentViews) {
                view.RemoveFromSuperview ();
            }
            AttachmentViews.Clear ();
            if (Attachments != null) {
                foreach (var attachment in Attachments) {
                    UIView view = null;
                    string contnentType = attachment.ContentType == null ? "" : attachment.ContentType.ToLower ();
                    if (contnentType.StartsWith ("image/") && attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        view = CreateImageViewForAttachment (attachment);
                    }
                    if (view == null) {
                        var frame = new CGRect (0.0f, 0.0f, Bounds.Width, ChatMessageAttachmentView.VIEW_HEIGHT);
                        var attachmentView = new ChatMessageAttachmentView (frame, attachment);
                        attachmentView.SetColors (MessageLabel.BackgroundColor, MessageLabel.TextColor);
                        attachmentView.OnAttachmentSelected = OnAttachmentSelected;
                        view = attachmentView;
                    }
                    if (view != null) {
                        BubbleView.AddSubview (view);
                        AttachmentViews.Add (view);
                    }
                }
            }
        }

        UIImageView CreateImageViewForAttachment (McAttachment attachment)
        {
            using (var image = UIImage.FromFile (attachment.GetFilePath ())) {
                if (image.Size.Width > 480 || image.Size.Height > 480) {
                    var imageView = new UIImageView (image);
                    imageView.UserInteractionEnabled = true;
                    imageView.AddGestureRecognizer (new UITapGestureRecognizer (() => {
                        OnAttachmentSelected (attachment);
                    }));
                    return imageView;
                }
            }
            return null;
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            if (_PortraitView != null) {
                var x = BubbleSideInset;
                if (ChatView != null) {
                    x += (ChatView.TimestampRevealWidth - _PortraitView.Frame.Width - x) * ChatView.TimestampRevealProgress;
                }
                nfloat portraitSize = MessageLabel.Font.LineHeight + MessageInsets.Top + MessageInsets.Bottom;
                PortraitView.Frame = new CGRect (x, Bounds.Height - portraitSize, portraitSize, portraitSize);
            }
            LayoutBubbleView ();
            if (_ErrorIndicator != null) {
                var frame = _ErrorIndicator.Frame;
                frame.X = BubbleView.Frame.X + BubbleView.Frame.Width + BubbleSideInset;
                frame.Y = BubbleView.Frame.Y + (BubbleView.Frame.Height - frame.Height) / 2.0f;
                _ErrorIndicator.Frame = frame;
            }
            if (ChatView != null) {
                var frame = TimestampDividerLabel.Frame;
                frame.X = (ChatView.TimestampRevealWidth * ChatView.TimestampRevealProgress);
                frame.Height = TimestampDividerLabel.Font.LineHeight * 2.0f;
                TimestampDividerLabel.Frame = frame;
                frame = TimestampRevealLabel.Frame;
                frame.Height = TimestampRevealLabel.Font.LineHeight;
                frame.Width = ChatView.TimestampRevealWidth - TimestampRevealRightSpacing;
                frame.Y = BubbleView.Frame.Y + (BubbleView.Frame.Height - frame.Height) / 2.0f;
                TimestampRevealLabel.Frame = frame;
            }
        }

        void LayoutBubbleView ()
        {
            var maxMessageWidth = (nfloat)Math.Floor (Bounds.Width * MaxBubbleWidthPercent - MessageInsets.Left - MessageInsets.Right);
            if (ChatView != null && ChatView.ShowPortraits && Participant != null) {
                // If we're showing portraits, they take up space, so we need to make the bubble smaller
                // than it would otherwise be.  Instead of removing the entire width added by the portrait,
                // just take a bit off.  BubbleSideInset is a good smallish size, but we could do anything here.
                maxMessageWidth -= BubbleSideInset;
            }
            var messageSize = MessageLabel.SizeThatFits (new CGSize (maxMessageWidth, 99999999.0f));
            if (messageSize.Height > MessageLabel.Font.LineHeight + 5.0f) {
                // If we've got more than one line, just use maxMessageWidth so consecutive mutli-line
                // messages line up.  Without this, each message would be a few pixels different because
                // of differences in how each message wraps before actually hitting maxMessageWidth
                messageSize.Width = maxMessageWidth;
            }
            if (AttachmentViews.Count > 0) {
                // If there's an attachment view, use the max width so there's enough room to show attachment info
                messageSize.Width = maxMessageWidth;
            }
            messageSize.Height = (nfloat)Math.Ceiling (messageSize.Height);
            MessageLabel.Frame = new CGRect (MessageInsets.Left, MessageInsets.Top, messageSize.Width, messageSize.Height);
            nfloat bubbleY = MessageLabel.Frame.Y + MessageLabel.Frame.Height;
            var bubbleSize = new CGSize (messageSize.Width + MessageInsets.Left + MessageInsets.Right, messageSize.Height + MessageInsets.Top + MessageInsets.Bottom);
            foreach (var attachmentView in AttachmentViews) {
                var imageView = attachmentView as UIImageView;
                if (imageView != null) {
                    imageView.Frame = new CGRect (MessageInsets.Left, bubbleY, messageSize.Width, messageSize.Width / imageView.Image.Size.Width * imageView.Image.Size.Height);
                } else {
                    attachmentView.Frame = new CGRect (MessageInsets.Left, bubbleY, messageSize.Width, attachmentView.Frame.Height);
                }
                bubbleSize.Height += attachmentView.Frame.Height;
                bubbleY += attachmentView.Frame.Height;
            }
            nfloat x = 0.0f;
            nfloat y = 0.0f;
            if (!TimestampDividerLabel.Hidden) {
                y = TimestampDividerLabel.Frame.Y + TimestampDividerLabel.Frame.Height;
            }
            if (Participant == null) {
                x = Bounds.Width - BubbleView.Frame.Width - BubbleSideInset;
                if (_ErrorIndicator != null && _ErrorIndicator.Hidden == false) {
                    x -= _ErrorIndicator.Frame.Width + BubbleSideInset;
                }
                BubbleView.Frame = new CGRect (x, y, bubbleSize.Width, bubbleSize.Height);
            } else {
                x = BubbleSideInset;
                if (ChatView != null && ChatView.ShowPortraits) {
                    x += PortraitView.Frame.Width + PortraitBubbleSpacing;
                }
                if (ChatView != null) {
                    x += (ChatView.TimestampRevealWidth + BubbleSideInset - x) * ChatView.TimestampRevealProgress;
                }
                if (_NameLabel != null && !_NameLabel.Hidden) {
                    NameLabel.Frame = new CGRect (x, y, maxMessageWidth + MessageInsets.Left + MessageInsets.Right, NameLabel.Font.LineHeight);
                    y += NameLabel.Frame.Height;
                }
                BubbleView.Frame = new CGRect (x, y, bubbleSize.Width, bubbleSize.Height);
            }
        }

        public override void SizeToFit ()
        {
            LayoutBubbleView ();
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, BubbleView.Frame.Y + BubbleView.Frame.Size.Height);
        }

        void ErrorTap ()
        {
            ChatView.Delegate.ChatMessageViewDidSelectError (ChatView, Index);
        }

    }

    public class ChatMessageAttachmentView : AttachmentView
    {

        public ChatMessageAttachmentView (CGRect frame, McAttachment attachment) : base (frame, attachment)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            HideSeparator ();
            if (attachment.IsInline && detailView.Text.StartsWith ("Inline ")) {
                detailView.Text = detailView.Text.Substring ("Inline ".Length);
            }
            //            imageView.BackgroundColor = UIColor.White;
            //            imageView.ContentMode = UIViewContentMode.Center;
            //            imageView.ClipsToBounds = true;
            //            imageView.Layer.CornerRadius = imageView.Frame.Size.Width / 2.0f;
        }

        public void SetColors (UIColor backgroundColor, UIColor foregroundColor)
        {
            BackgroundColor = backgroundColor;
            filenameView.BackgroundColor = backgroundColor;
            detailView.BackgroundColor = backgroundColor;
            filenameView.TextColor = foregroundColor;
            detailView.TextColor = foregroundColor;
        }
    }
}
