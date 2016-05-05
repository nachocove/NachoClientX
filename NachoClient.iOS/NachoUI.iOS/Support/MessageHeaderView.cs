//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using UIKit;
using Foundation;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class MessageHeaderView : UIView
    {

        McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                Update ();
            }
        }

        private bool _Selected;
        public bool Selected {
            get {
                return _Selected;
            }
            set {
                SetSelected (value, false);
            }
        }

        UILabel FromLabel;
        UILabel SubjectLabel;
        UILabel DateLabel;
        PortraitView PortraitView;
        public readonly UIView BottomBorder;

        UIEdgeInsets _TextInsets;
        public UIEdgeInsets TextInsets {
            get {
                return _TextInsets;
            }
            set {
                _TextInsets = value;
                SetNeedsLayout ();
            }
        }

        nfloat _PortraitSize = 40.0f;
        public nfloat PortraitSize {
            get {
                return _PortraitSize;
            }
            set {
                _PortraitSize = value;
                SetNeedsLayout ();
            }
        }

        nfloat BorderWidth = 0.5f;

        static NSAttributedString _HotAttachmentString;
        static NSAttributedString HotAttachmentString {
            get {
                if (_HotAttachmentString == null) {
                    _HotAttachmentString = NSAttributedString.CreateFrom (new HotAttachment(A.Font_AvenirNextDemiBold17));
                }
                return _HotAttachmentString;
            }
        }

        public MessageHeaderView (CGRect rect) : base (rect)
        {

            BackgroundColor = UIColor.White;

            TextInsets = new UIEdgeInsets (7.0f, 14.0f, 7.0f, 14.0f);

            PortraitView = new PortraitView (new CGRect (0.0f, 0.0f, PortraitSize, PortraitSize));

            FromLabel = new UILabel ();
            FromLabel.Font = A.Font_AvenirNextDemiBold17;
            FromLabel.TextColor = A.Color_NachoGreen;
            FromLabel.Lines = 1;
            FromLabel.LineBreakMode = UILineBreakMode.TailTruncation;

            SubjectLabel = new UILabel ();
            SubjectLabel.Font = A.Font_AvenirNextRegular17;
            SubjectLabel.TextColor = A.Color_NachoGreen;
            SubjectLabel.Lines = 0;
            SubjectLabel.LineBreakMode = UILineBreakMode.WordWrap;

            DateLabel = new UILabel ();
            DateLabel.Font = A.Font_AvenirNextRegular14;
            DateLabel.TextColor = A.Color_NachoTextGray;
            DateLabel.Lines = 1;
            DateLabel.LineBreakMode = UILineBreakMode.TailTruncation;

            BottomBorder = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, BorderWidth));
            BottomBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);

            AddSubview (BottomBorder);
            AddSubview (PortraitView);
            AddSubview (FromLabel);
            AddSubview (SubjectLabel);
            AddSubview (DateLabel);
        }

        public void Update ()
        {
            PortraitView.SetPortrait (Message.cachedPortraitId, Message.cachedFromColor, Message.cachedFromLetters);
            var attributedDateText = new NSMutableAttributedString (Pretty.FriendlyFullDateTime (Message.DateReceived));
            if (!Message.IsAction) {
                if (Message.Intent != McEmailMessage.IntentType.None) {
                    var intentString = NachoCore.Brain.NcMessageIntent.IntentEnumToString (Message.Intent, uppercase: false);
                    var location = attributedDateText.Length + 1;
                    attributedDateText.Append (new NSAttributedString (" " + intentString));
                    attributedDateText.AddAttribute (UIStringAttributeKey.ForegroundColor, UIColor.FromRGB (0xD2, 0x47, 0x47), new NSRange (location, intentString.Length));
                    if (Message.IntentDateType != MessageDeferralType.None) {
                        var dueDateString = Pretty.FutureDate (Message.IntentDate, NachoCore.Brain.NcMessageIntent.IntentIsToday (Message.IntentDateType));
                        location = attributedDateText.Length;
                        attributedDateText.Append (new NSAttributedString (" by " + dueDateString));
                        attributedDateText.AddAttribute (UIStringAttributeKey.ForegroundColor, A.Color_NachoTextGray, new NSRange (location, dueDateString.Length + 3));
                    }
                }
            }
            DateLabel.AttributedText = attributedDateText;
            FromLabel.Text = Pretty.SenderString (Message.From);
            string prettySubject = "";
            if (String.IsNullOrWhiteSpace (Message.Subject)) {
                prettySubject = "(no subject)";
                SubjectLabel.TextColor = A.Color_NachoTextGray;
            } else {
                SubjectLabel.TextColor = A.Color_NachoGreen;
                prettySubject = Pretty.SubjectString (Message.Subject);
            }
            using (var attributedSubject = new NSMutableAttributedString (prettySubject)) {
                if (Message.isHot ()) {
                    attributedSubject.Replace (new NSRange (0, 0), " ");
                    attributedSubject.Insert (HotAttachmentString, 0);
                }
                SubjectLabel.AttributedText = attributedSubject;
            }
            SetNeedsLayout ();
        }

        public void SetSelected (bool selected, bool animated = false)
        {
            _Selected = selected;
            if (animated) {
                UIView.BeginAnimations (null, IntPtr.Zero);
                UIView.SetAnimationDuration (0.25f);
            }
            if (selected) {
                BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);
            } else {
                BackgroundColor = UIColor.White;
            }
            if (animated) {
                UIView.CommitAnimations ();
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            PortraitView.Frame = new CGRect (Bounds.Width - TextInsets.Right - PortraitSize, TextInsets.Top, PortraitSize, PortraitSize);
            nfloat textWidth = PortraitView.Frame.X - TextInsets.Left;
            nfloat subjectHeight = (nfloat)Math.Ceiling (SubjectLabel.SizeThatFits (new CGSize (textWidth, 0.0f)).Height);
            FromLabel.Frame = new CGRect (TextInsets.Left, TextInsets.Top, textWidth, FromLabel.Font.RoundedLineHeight (1.0f));
            SubjectLabel.Frame = new CGRect (FromLabel.Frame.X, FromLabel.Frame.Y + FromLabel.Frame.Height, textWidth, subjectHeight);
            DateLabel.Frame = new CGRect (FromLabel.Frame.X, SubjectLabel.Frame.Y + SubjectLabel.Frame.Height, Bounds.Width - TextInsets.Right - FromLabel.Frame.X, DateLabel.Font.RoundedLineHeight (1.0f));
            BottomBorder.Frame = new CGRect (TextInsets.Left, Bounds.Height - BorderWidth, Bounds.Width - TextInsets.Left, BorderWidth);
        }

        public override CGSize SizeThatFits (CGSize size)
        {
            nfloat height = TextInsets.Top + TextInsets.Bottom;
            nfloat minHeight = height + PortraitSize;
            nfloat subjectWidth = size.Width - TextInsets.Left - TextInsets.Right - PortraitSize;
            height += FromLabel.Font.RoundedLineHeight (1.0f);
            height += DateLabel.Font.RoundedLineHeight (1.0f);
            height += (nfloat)Math.Ceiling (SubjectLabel.SizeThatFits (new CGSize (subjectWidth, 0.0f)).Height);
            return new CGSize (size.Width, (nfloat)Math.Max (minHeight, height));
        }

        public override void SizeToFit ()
        {
            var width = Frame.Width;
            Frame = new CGRect (Frame.X, Frame.Y, width, SizeThatFits (new CGSize (width, 0.0f)).Height);
        }

    }
}

