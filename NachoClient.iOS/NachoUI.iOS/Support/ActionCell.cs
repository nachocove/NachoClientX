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

    public class ActionCell : SwipeTableViewCell
    {

        public readonly ActionCheckboxView CheckboxView;
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
        UILabel DateLabel;
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

        static NSAttributedString _HotAttachmentString;
        static NSAttributedString HotAttachmentString {
            get {
                if (_HotAttachmentString == null) {
                    _HotAttachmentString = NSAttributedString.CreateFrom (new HotAttachment(A.Font_AvenirNextRegular14));
                }
                return _HotAttachmentString;
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

        public ActionCell (IntPtr handle) : base (handle)
        {
            DetailTextSpacing = 0.0f;
            HideDetailWhenEmpty = true;

            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            TextLabel.TextColor = A.Color_NachoGreen;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            DetailTextLabel.TextColor = A.Color_NachoTextGray;
            DetailTextLabel.Lines = 3;

            DateLabel = new UILabel ();
            DateLabel.Font = A.Font_AvenirNextRegular14;
            DateLabel.TextColor = A.Color_NachoTextGray;
            ContentView.AddSubview (DateLabel);

            using (var image = UIImage.FromBundle ("chat-stat-online")) {
                UnreadIndicator = new UIImageView (image);
            }
            UnreadIndicator.Hidden = true;

            CheckboxView = new ActionCheckboxView (viewSize: 44.0f, checkboxSize: 20.0f);

            ContentView.AddSubview (CheckboxView);
            ContentView.AddSubview (UnreadIndicator);

            SeparatorInset = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 0.0f);
        }

        public void SetAction (McAction action)
        {
            TextLabel.Text = action.Title;
            if (action.Description != null) {
                DetailTextLabel.Text = System.Text.RegularExpressions.Regex.Replace (action.Description, "\\s+", " ");
            } else {
                DetailTextLabel.Text = "";
            }
            if (action.DueDate != default(DateTime)) {
                // DateLabel.Text = Pretty.Something ();
            }
            CheckboxView.IsChecked = action.IsCompleted;
            // UnreadIndicator.Hidden = message.IsRead;
        }

        public override void LayoutSubviews ()
        {
            var rightPadding = RightPadding + (_ColorIndicatorView != null ? ColorIndicatorInsets.Right : 0.0f);
            base.LayoutSubviews ();
            var dateSize = DateLabel.SizeThatFits (new CGSize (0.0f, 0.0f));
            dateSize.Height = DateLabel.Font.RoundedLineHeight (1.0f);
            var showDetail = !String.IsNullOrWhiteSpace (DetailTextLabel.Text);
            var textHeight = TextLabel.Font.RoundedLineHeight (1.0f);
            var detailTextHeight = (nfloat)Math.Ceiling (DetailTextLabel.Font.LineHeight * DetailTextLabel.Lines);
            var totalTextHeight = textHeight;
            if (showDetail) {
                totalTextHeight += DetailTextSpacing + detailTextHeight;
            }
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

            if (showDetail) {
                frame = DetailTextLabel.Frame;
                frame.X = TextLabel.Frame.X;
                frame.Y = TextLabel.Frame.Y + TextLabel.Frame.Height + DetailTextSpacing;
                frame.Width = detailWidth;
                frame.Height = detailHeight;
                DetailTextLabel.Frame = frame;
            }

            CheckboxView.Center = new CGPoint (SeparatorInset.Left / 2.0f, ContentView.Bounds.Height / 2.0f);
            // UnreadIndicator.Center = new CGPoint (PortraitView.Frame.X + PortraitView.Frame.Width - UnreadIndicator.Frame.Width / 2.0f, PortraitView.Frame.Y + UnreadIndicator.Frame.Height / 2.0f);

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

    }
}

