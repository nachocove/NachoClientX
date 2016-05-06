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

    public class MessageCell : SwipeTableViewCell
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
                    _ThreadIndicator.TintColor = UIColor.FromRGB (0x01, 0xB2, 0xCD);
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
                    _HotAttachmentString = NSAttributedString.CreateFrom (new HotAttachment(A.Font_AvenirNextRegular14));
                }
                return _HotAttachmentString;
            }
        }

        static NSAttributedString _AttachAttachmentString;
        static NSAttributedString AttachAttachmentString {
            get {
                if (_AttachAttachmentString == null) {
                    _AttachAttachmentString = NSAttributedString.CreateFrom (new AttachAttachment(A.Font_AvenirNextRegular14));
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

        UIFont BaseDateLabelFont = A.Font_AvenirNextRegular14;

        public MessageCell (IntPtr handle) : base (handle)
        {
            DetailTextSpacing = 0.0f;

            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            TextLabel.TextColor = A.Color_NachoGreen;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            DetailTextLabel.TextColor = A.Color_NachoTextGray;
            DetailTextLabel.Lines = 3;

            DateLabel = new UILabel ();
            DateLabel.Font = BaseDateLabelFont;
            DateLabel.TextColor = A.Color_NachoTextGray;
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

        public void SetMessage (McEmailMessage message, int threadCount = 0)
        {
            NSMutableAttributedString attributedDateText;
            if (message.IntentDate != default(DateTime)) {
                if (message.IntentDate < DateTime.UtcNow) {
                    attributedDateText = new NSMutableAttributedString ("due " + Pretty.FutureDate (message.IntentDate, NachoCore.Brain.NcMessageIntent.IntentIsToday(message.IntentDateType)));
                } else {
                    attributedDateText = new NSMutableAttributedString ("by " + Pretty.FutureDate (message.IntentDate, NachoCore.Brain.NcMessageIntent.IntentIsToday(message.IntentDateType)));
                }
            } else {
                attributedDateText = new NSMutableAttributedString (Pretty.TimeWithDecreasingPrecision (message.DateReceived));
            }
            if (message.Intent != McEmailMessage.IntentType.None) {
                var intentString = NachoCore.Brain.NcMessageIntent.IntentEnumToString (message.Intent, uppercase: false);
                attributedDateText.Insert (new NSAttributedString (intentString + " "), 0);
                attributedDateText.AddAttribute (UIStringAttributeKey.Font, DateLabel.Font.WithSize (11.0f), new NSRange(0, intentString.Length));
                attributedDateText.AddAttribute (UIStringAttributeKey.ForegroundColor, UIColor.FromRGB(0xD2, 0x47, 0x47), new NSRange(0, intentString.Length));
            }
            DateLabel.AttributedText = attributedDateText;
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
            using (var attributedPreview = new NSMutableAttributedString (previewText)) {
                if (subjectLength > 0) {
                    attributedPreview.AddAttribute (UIStringAttributeKey.Font, A.Font_AvenirNextMedium14.WithSize(DetailTextLabel.Font.PointSize), new NSRange(0, subjectLength));
                    attributedPreview.AddAttribute (UIStringAttributeKey.ForegroundColor, A.Color_NachoGreen, new NSRange(0, subjectLength));
                }
                if (message.isHot ()) {
                    attributedPreview.Replace (new NSRange (0, 0), " ");
                    attributedPreview.Insert (HotAttachmentString, 0);
                    subjectLength += 2;
                }
                if (message.cachedHasAttachments) {
                    attributedPreview.Replace (new NSRange (subjectLength, 0), " ");
                    attributedPreview.Insert (AttachAttachmentString, subjectLength + 1);
                    // TODO: add space after if subjectLength was originally 0
                }
                DetailTextLabel.AttributedText = attributedPreview;
            }
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
            frame.Y = textTop + (TextLabel.Font.Ascender + (textHeight - TextLabel.Font.LineHeight) / 2.0f - BaseDateLabelFont.Ascender - (dateSize.Height - BaseDateLabelFont.LineHeight) / 2.0f);
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
                _ThreadIndicator.Frame = new CGRect (ContentView.Bounds.Width - rightPadding - threadWidth, DateLabel.Frame.Y + DateLabel.Frame.Height, threadWidth, Bounds.Height - DateLabel.Frame.Y - DateLabel.Frame.Height);
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

        private class ThreadIndicatorView : UIView
        {

            UILabel CountLabel;
            UIImageView ArrowView;

            public ThreadIndicatorView(CGRect frame) : base (frame)
            {
                CountLabel = new UILabel ();
                CountLabel.Font = A.Font_AvenirNextRegular12;
                CountLabel.TextColor = TintColor;

                ArrowView = new UIImageView(UIImage.FromBundle("thread-arrows").ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate));
                ArrowView.TintColor = TintColor;

                AddSubview (CountLabel);
                AddSubview (ArrowView);
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

