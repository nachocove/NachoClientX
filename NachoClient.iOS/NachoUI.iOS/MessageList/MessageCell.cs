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

    public class MessageCell : SwipeTableViewCell, ThemeAdopter
    {

        UIImageView UnreadIndicator;
        UIView _ColorIndicatorView;
        UIView ColorIndicatorView {
            get {
                if (_ColorIndicatorView == null) {
                    _ColorIndicatorView = new UIView ();
                    ContentView.AddSubview (ColorIndicatorView);
                }
                return _ColorIndicatorView;
            }
        }
        ThreadIndicatorView _ThreadIndicator;
        ThreadIndicatorView ThreadIndicator {
            get {
                if (_ThreadIndicator == null) {
                    _ThreadIndicator = new ThreadIndicatorView (new CGRect(0.0f, 0.0f, 20.0f, Bounds.Height));
                    if (adoptedTheme != null) {
                        _ThreadIndicator.AdoptTheme (adoptedTheme);
                    }
                    ContentView.AddSubview (_ThreadIndicator);
                }
                return _ThreadIndicator;
            }
        }
        public UIColor IndicatorColor {
            get {
                if (_ColorIndicatorView != null) {
                    return _ColorIndicatorView.BackgroundColor;
                }
                return null;
            }
            set {
                if (value == null) {
                    if (_ColorIndicatorView != null) {
                        _ColorIndicatorView.RemoveFromSuperview ();
                        _ColorIndicatorView = null;
                    }
                } else {
                    if (_ColorIndicatorView == null) {
                        SetNeedsLayout ();
                    }
                    ColorIndicatorView.BackgroundColor = value;
                }
            }
        }
        PortraitView PortraitView;
        UILabel DateLabel;
        nfloat PortraitSize = 40.0f;
        nfloat RightPadding = 10.0f;
        nfloat ColorIndicatorSize = 3.0f;
        UIEdgeInsets _ColorIndicatorInsets = new UIEdgeInsets (1.0f, 0.0f, 1.0f, 7.0f);
        public UIEdgeInsets ColorIndicatorInsets {
            get {
                return _ColorIndicatorInsets;
            }
            set {
                _ColorIndicatorInsets = value;
                SetNeedsLayout ();
            }
        }
        public bool UseRecipientName;

        static NSAttributedString _HotAttachmentString;
        static NSAttributedString HotAttachmentString {
            get {
                if (_HotAttachmentString == null) {
                    _HotAttachmentString = NSAttributedString.CreateFrom (new HotAttachment(Theme.Active.DefaultFont.WithSize(14.0f)));
                }
                return _HotAttachmentString;
            }
        }

        static NSAttributedString _AttachAttachmentString;
        static NSAttributedString AttachAttachmentString {
            get {
                if (_AttachAttachmentString == null) {
                    _AttachAttachmentString = NSAttributedString.CreateFrom (new AttachAttachment(Theme.Active.DefaultFont.WithSize (14.0f)));
                }
                return _AttachAttachmentString;
            }
        }

        public nint NumberOfPreviewLines {
            get {
                return DetailTextLabel.Lines;
            }
            set {
                if (value != DetailTextLabel.Lines) {
                    DetailTextLabel.Lines = value;
                    SetNeedsLayout ();
                }
            }
        }

        public MessageCell (IntPtr handle) : base (handle)
        {
            DetailTextSpacing = 0.0f;

            DetailTextLabel.Lines = 3;

            DateLabel = new UILabel ();
            ContentView.AddSubview (DateLabel);

            PortraitView = new PortraitView (new CGRect (0.0f, 0.0f, PortraitSize, PortraitSize));
            ContentView.AddSubview (PortraitView);

            using (var image = UIImage.FromBundle ("chat-stat-online")) {
                UnreadIndicator = new UIImageView (image);
            }
            UnreadIndicator.Hidden = true;
            ContentView.AddSubview (UnreadIndicator);

            SeparatorInset = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 0.0f);
        }

        Theme adoptedTheme = null;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TextLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
                DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
                DateLabel.Font = theme.DefaultFont.WithSize (14.0f);
                DateLabel.TextColor = theme.TableViewCellDateLabelTextColor;
                PortraitView.AdoptTheme (theme);
                if (_ThreadIndicator != null) {
                    _ThreadIndicator.AdoptTheme (theme);
                }
                UpdateAttributedText ();
            }
        }

        NSMutableAttributedString AttributedDateText;
        NSMutableAttributedString AttributedPreview;
        NSRange IntentRange = new NSRange(0, 0);
        NSRange SubjectRange = new NSRange(0, 0);

        void UpdateAttributedText ()
        {
            var theme = adoptedTheme == null ? Theme.Active : adoptedTheme;
            if (IntentRange.Length > 0) {
                AttributedDateText.AddAttribute (UIStringAttributeKey.Font, theme.DefaultFont.WithSize (11.0f), IntentRange);
                AttributedDateText.AddAttribute (UIStringAttributeKey.ForegroundColor, theme.MessageIntentTextColor, IntentRange);
            }
            DateLabel.AttributedText = AttributedDateText;
            if (SubjectRange.Length > 0) {
                AttributedPreview.AddAttribute (UIStringAttributeKey.Font, theme.MediumDefaultFont.WithSize (14.0f), SubjectRange);
                AttributedPreview.AddAttribute (UIStringAttributeKey.ForegroundColor, theme.TableViewCellMainLabelTextColor, SubjectRange);
            }
            DetailTextLabel.AttributedText = AttributedPreview;
        }

        public void SetMessage (McEmailMessage message, int threadCount = 0)
        {
            if (message.IntentDate != default(DateTime)) {
                if (message.IntentDate < DateTime.UtcNow) {
                    AttributedDateText = new NSMutableAttributedString ("due " + Pretty.FutureDate (message.IntentDate, NachoCore.Brain.NcMessageIntent.IntentIsToday(message.IntentDateType)));
                } else {
                    AttributedDateText = new NSMutableAttributedString ("by " + Pretty.FutureDate (message.IntentDate, NachoCore.Brain.NcMessageIntent.IntentIsToday(message.IntentDateType)));
                }
            } else {
                AttributedDateText = new NSMutableAttributedString (Pretty.TimeWithDecreasingPrecision (message.DateReceived));
            }
            IntentRange.Length = 0;
            if (message.Intent != McEmailMessage.IntentType.None) {
                var intentString = NachoCore.Brain.NcMessageIntent.IntentEnumToString (message.Intent, uppercase: false);
                IntentRange.Length = intentString.Length;
                AttributedDateText.Insert (new NSAttributedString (intentString + " "), 0);
            }
            if (UseRecipientName) {
                TextLabel.Text = Pretty.RecipientString (message.To);
                PortraitView.Hidden = true;
                SeparatorInset = new UIEdgeInsets (0.0f, 14.0f, 0.0f, 0.0f);
            } else {
                TextLabel.Text = Pretty.SenderString (message.From);
                PortraitView.SetPortrait (message.cachedPortraitId, message.cachedFromColor, message.cachedFromLetters);
                PortraitView.Hidden = false;
                SeparatorInset = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 0.0f);
            }
            int subjectLength;
            var previewText = Pretty.MessagePreview (message, out subjectLength);
            AttributedPreview = new NSMutableAttributedString (previewText);
            SubjectRange.Location = 0;
            SubjectRange.Length = subjectLength;
            if (message.isHot ()) {
                AttributedPreview.Replace (new NSRange (0, 0), " ");
                AttributedPreview.Insert (HotAttachmentString, 0);
                SubjectRange.Location += 2;
            }
            if (message.cachedHasAttachments) {
                AttributedPreview.Replace (new NSRange (SubjectRange.Location + SubjectRange.Length, 0), " ");
                AttributedPreview.Insert (AttachAttachmentString, SubjectRange.Location + SubjectRange.Length + 1);
                // TODO: add space after if subjectLength was originally 0
            }
            UpdateAttributedText ();
            if (threadCount > 1) {
                ThreadIndicator.SetCount (threadCount);
            } else {
                if (_ThreadIndicator != null) {
                    _ThreadIndicator.RemoveFromSuperview ();
                    _ThreadIndicator = null;
                }
            }
            UnreadIndicator.Hidden = message.IsRead;
        }

        public override void LayoutSubviews ()
        {
            nfloat threadWidth = 0.0f;
            if (_ThreadIndicator != null) {
                threadWidth = _ThreadIndicator.SizeThatFits (new CGSize (0.0f, Bounds.Height)).Width;
            }
            var rightPadding = RightPadding + (_ColorIndicatorView != null ? ColorIndicatorInsets.Right : 0.0f);
            base.LayoutSubviews ();
            var dateSize = DateLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
            dateSize.Height = DateLabel.Font.RoundedLineHeight (1.0f);
            var textHeight = TextLabel.Font.RoundedLineHeight (1.0f);
            var detailTextHeight = (nfloat)Math.Ceiling (DetailTextLabel.Font.LineHeight * DetailTextLabel.Lines);
            var totalTextHeight = textHeight + DetailTextSpacing + detailTextHeight;
            var textTop = (Bounds.Height - totalTextHeight) / 2.0f;
            var detailWidth = ContentView.Bounds.Width - rightPadding - SeparatorInset.Left - threadWidth;
            var detailHeight = DetailTextLabel.SizeThatFits (new CGSize (detailWidth, 0.0f)).Height;

            CGRect frame;

            frame = DateLabel.Frame;
            frame.X = ContentView.Bounds.Width - dateSize.Width - rightPadding;
            var baseDateLabelFont = Theme.Active.DefaultFont.WithSize(14.0f);
            frame.Y = textTop + (TextLabel.Font.Ascender + (textHeight - TextLabel.Font.LineHeight) / 2.0f - baseDateLabelFont.Ascender - (dateSize.Height - baseDateLabelFont.LineHeight) / 2.0f);
            frame.Width = dateSize.Width;
            frame.Height = dateSize.Height;
            DateLabel.Frame = frame;

            frame = TextLabel.Frame;
            frame.X = SeparatorInset.Left;
            frame.Y = textTop;
            frame.Width = DateLabel.Frame.X - frame.X - 3.0f;
            frame.Height = textHeight;
            TextLabel.Frame = frame;

            frame = DetailTextLabel.Frame;
            frame.X = TextLabel.Frame.X;
            frame.Y = TextLabel.Frame.Y + TextLabel.Frame.Height + DetailTextSpacing;
            frame.Width = detailWidth;
            frame.Height = detailHeight;
            DetailTextLabel.Frame = frame;

            PortraitView.Center = new CGPoint (SeparatorInset.Left / 2.0f, textTop * 2.0f + PortraitView.Frame.Height / 2.0f);
            UnreadIndicator.Center = new CGPoint (PortraitView.Frame.X + PortraitView.Frame.Width - UnreadIndicator.Frame.Width / 2.0f, PortraitView.Frame.Y + UnreadIndicator.Frame.Height / 2.0f);

            if (_ColorIndicatorView != null) {
                _ColorIndicatorView.Frame = new CGRect (ContentView.Bounds.Width - ColorIndicatorInsets.Right - ColorIndicatorSize, ColorIndicatorInsets.Top, ColorIndicatorSize, ContentView.Bounds.Height - ColorIndicatorInsets.Top - ColorIndicatorInsets.Bottom);
            }

            if (_ThreadIndicator != null) {
                _ThreadIndicator.Frame = new CGRect (ContentView.Bounds.Width - rightPadding - threadWidth, DetailTextLabel.Frame.Y, threadWidth, detailTextHeight);
            }
        }

        public static nfloat PreferredHeight (int numberOfPreviewLines, UIFont mainFont, UIFont previewFont)
        {
            var detailSpacing = 0.0f;
            var topPadding = 7.0f;
            var textHeight = mainFont.RoundedLineHeight (1.0f);
            var detailHeight = (nfloat)Math.Ceiling (previewFont.LineHeight * numberOfPreviewLines);
            return textHeight + detailHeight + detailSpacing + topPadding * 2.0f;
        }

        private class ThreadIndicatorView : UIView, ThemeAdopter
        {

            UILabel CountLabel;
            UIImageView ArrowView;

            public ThreadIndicatorView(CGRect frame) : base (frame)
            {
                CountLabel = new UILabel ();

                ArrowView = new UIImageView(UIImage.FromBundle("thread-arrows").ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate));

                AddSubview (CountLabel);
                AddSubview (ArrowView);
            }

            public void AdoptTheme (Theme theme)
            {
                CountLabel.Font = theme.DefaultFont.WithSize (12.0f);
                CountLabel.TextColor = theme.ThreadIndicatorColor;
                ArrowView.TintColor = theme.ThreadIndicatorColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                CountLabel.SizeToFit ();
                CountLabel.Center = new CGPoint (CountLabel.Frame.Width / 2.0f, Bounds.Height / 2.0f);
                ArrowView.Center = new CGPoint (CountLabel.Frame.Width + ArrowView.Frame.Width / 2.0f, Bounds.Height / 2.0f);
            }

            public override void TintColorDidChange ()
            {
                base.TintColorDidChange ();
                CountLabel.TextColor = TintColor;
                ArrowView.TintColor = TintColor;
            }

            public void SetCount (int count)
            {
                CountLabel.Text = count.ToString ();
            }

            public override CGSize SizeThatFits (CGSize size)
            {
                var countSize = CountLabel.SizeThatFits (size);
                return new CGSize (countSize.Width + ArrowView.Frame.Width, size.Height);
            }

        }

    }
}

