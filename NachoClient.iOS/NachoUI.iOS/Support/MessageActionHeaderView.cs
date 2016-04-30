//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Model;

namespace NachoClient.iOS
{
    
    public class MessageActionHeaderView : UIView
    {

        private bool _Selected;
        public bool Selected {
            get {
                return _Selected;
            }
            set {
                SetSelected (value, false);
            }
        }

        private McAction _Action;
        public McAction Action {
            get {
                return _Action;
            }
            set {
                _Action = value;
                Update ();
            }
        }

        ActionCheckboxView CheckboxView;
        UILabel TitleLabel;
        UILabel DateLabel;
        UIView BottomBorder;
        UIEdgeInsets SeparatorInset;
        nfloat BorderWidth = 0.5f;
        nfloat RightSpacing = 14.0f;
        nfloat TextSpacing = 5.0f;
        nfloat CheckboxAreaWidth = 24.0f;

        public MessageActionHeaderView (CGRect frame) : base (frame)
        {
            
            BackgroundColor = UIColor.White;
            SeparatorInset = new UIEdgeInsets (0.0f, 14.0f, 0.0f, 0.0f);

            CheckboxView = new ActionCheckboxView ((float)Bounds.Height, 20.0f);

            TitleLabel = new UILabel ();
            TitleLabel.Font = A.Font_AvenirNextRegular17;
            TitleLabel.TextColor = A.Color_NachoDarkText;
            TitleLabel.Lines = 1;
            TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;

            DateLabel = new UILabel ();
            DateLabel.Font = A.Font_AvenirNextRegular14;
            DateLabel.TextColor = A.Color_NachoTextGray;

            BottomBorder = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, BorderWidth));
            BottomBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);

            AddSubview (CheckboxView);
            AddSubview (TitleLabel);
            AddSubview (BottomBorder);
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

        void Update ()
        {
            if (_Action != null) {
                CheckboxView.IsChecked = _Action.IsCompleted;
                if (_Action.IsCompleted) {
                    CheckboxView.TintColor = A.Color_NachoTextGray;
                    TitleLabel.TextColor = A.Color_NachoTextGray;
                }else{
                    TitleLabel.TextColor = A.Color_NachoDarkText;
                    if (_Action.IsHot) {
                        CheckboxView.TintColor = UIColor.FromRGB (0xEE, 0x70, 0x5B);
                    } else {
                        CheckboxView.TintColor = A.Color_NachoGreen;
                    }
                }
                TitleLabel.Text = _Action.Title;
                if (_Action.IsDeferred) {
                    DateLabel.Text = "Deferred";
                } else if (!_Action.IsCompleted) {
                    // DateLabel.Text = Pretty.Something ();
                    DateLabel.Text = "";
                } else {
                    DateLabel.Text = "";
                }
            }else{
                CheckboxView.IsChecked = false;
                TitleLabel.Text = "";
                DateLabel.Text = "";
            }
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var textHeight = TitleLabel.Font.RoundedLineHeight (1.0f);
            var textTop = (Bounds.Height - textHeight) / 2.0f;
            var dateSize = DateLabel.SizeThatFits (new CGSize (0.0f, 0.0f));

            CGRect frame;

            frame = DateLabel.Frame;
            frame.Width = dateSize.Width;
            frame.X = Bounds.Width - RightSpacing - frame.Width;
            frame.Height = dateSize.Height;
            frame.Y = textTop + (TitleLabel.Font.Ascender - DateLabel.Font.Ascender);
            DateLabel.Frame = frame;

            frame = TitleLabel.Frame;
            frame.X = SeparatorInset.Left + CheckboxAreaWidth + TextSpacing;
            frame.Width = DateLabel.Frame.X - frame.X - 5.0f;
            frame.Y = textTop;
            frame.Height = textHeight;
            TitleLabel.Frame = frame;

            CheckboxView.Center = new CGPoint (SeparatorInset.Left + CheckboxAreaWidth / 2.0f, Bounds.Height / 2.0f);

            BottomBorder.Frame = new CGRect (SeparatorInset.Left, Bounds.Height - BorderWidth, Bounds.Width - SeparatorInset.Left - SeparatorInset.Right, BorderWidth);
        }

        public void Cleanup ()
        {
            
        }
    }
}

