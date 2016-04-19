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
        UIEdgeInsets ColorIndicatorInsets = new UIEdgeInsets (1.0f, 0.0f, 1.0f, 7.0f);

        static NSAttributedString _HotAttachmentString;
        static NSAttributedString HotAttachmentString {
            get {
                if (_HotAttachmentString == null) {
                    _HotAttachmentString = NSAttributedString.CreateFrom (new HotAttachment());
                }
                return _HotAttachmentString;
            }
        }

        static NSAttributedString _AttachAttachmentString;
        static NSAttributedString AttachAttachmentString {
            get {
                if (_AttachAttachmentString == null) {
                    _AttachAttachmentString = NSAttributedString.CreateFrom (new AttachAttachment());
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

            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            TextLabel.TextColor = A.Color_NachoGreen;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            DetailTextLabel.TextColor = A.Color_NachoTextGray;
            DetailTextLabel.Lines = 3;

            DateLabel = new UILabel ();
            DateLabel.Font = A.Font_AvenirNextRegular14;
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

        public void SetMessage (McEmailMessage message)
        {
            PortraitView.SetPortrait (message.cachedPortraitId, message.cachedFromColor, message.cachedFromLetters);
            DateLabel.Text = Pretty.TimeWithDecreasingPrecision (message.DateReceived);
            TextLabel.Text = Pretty.SenderString (message.From);
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
            UnreadIndicator.Hidden = message.IsRead;
        }

        public override void LayoutSubviews ()
        {
            var rightPadding = RightPadding + (_ColorIndicatorView != null ? ColorIndicatorInsets.Right : 0.0f);
            base.LayoutSubviews ();
            var dateSize = DateLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
            dateSize.Height = DateLabel.Font.RoundedLineHeight (1.0f);
            var textHeight = TextLabel.Font.RoundedLineHeight (1.0f);
            var detailTextHeight = (nfloat)Math.Ceiling (DetailTextLabel.Font.LineHeight * DetailTextLabel.Lines);
            var totalTextHeight = textHeight + DetailTextSpacing + detailTextHeight;
            var textTop = (Bounds.Height - totalTextHeight) / 2.0f;
            var detailWidth = ContentView.Bounds.Width - rightPadding - SeparatorInset.Left;
            var detailHeight = DetailTextLabel.SizeThatFits (new CGSize (detailWidth, 0.0f)).Height;

            CGRect frame;

            frame = DateLabel.Frame;
            frame.X = ContentView.Bounds.Width - dateSize.Width - rightPadding;
            frame.Y = textTop + (TextLabel.Font.Ascender - DateLabel.Font.Ascender);
            frame.Width = dateSize.Width;
            frame.Height = dateSize.Height;
            DateLabel.Frame = frame;

            frame = TextLabel.Frame;
            frame.X = SeparatorInset.Left;
            frame.Y = textTop;
            frame.Width = DateLabel.Frame.X - frame.X - RightPadding;
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
        }

        public static nfloat PreferredHeight (int numberOfPreviewLines, UIFont mainFont, UIFont previewFont)
        {
            var detailSpacing = 0.0f;
            var topPadding = 7.0f;
            var textHeight = mainFont.RoundedLineHeight (1.0f);
            var detailHeight = (nfloat)Math.Ceiling (previewFont.LineHeight * numberOfPreviewLines);
            return textHeight + detailHeight + detailSpacing + topPadding * 2.0f;
        }

        private class SubjectAttachment : NSTextAttachment
        {
            public SubjectAttachment () : base ()
            {
            }

            public override CGRect GetAttachmentBounds (NSTextContainer textContainer, CGRect proposedLineFragment, CGPoint glyphPosition, nuint characterIndex)
            {
                var font = A.Font_AvenirNextRegular14;
                nfloat offset = 2.0f;
                nfloat size = proposedLineFragment.Size.Height - 2.0f * offset;
                return new CGRect (0.0f, font.Descender + offset, size, size);
            }
        }

        private class HotAttachment : SubjectAttachment
        {
            public HotAttachment () : base ()
            {
                Image = UIImage.FromBundle("email-hot");
            }
        }

        private class AttachAttachment : SubjectAttachment
        {
            public AttachAttachment () : base ()
            {
                Image = UIImage.FromBundle("email-icn-attachment");
            }
        }

    }
}

