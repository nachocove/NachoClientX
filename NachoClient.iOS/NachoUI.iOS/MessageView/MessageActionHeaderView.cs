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


        public McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                _Message = value;
                Update ();
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

        public readonly ActionCheckboxView CheckboxView;
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
            CheckboxView.Changed = CheckboxChanged;

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
            AddSubview (DateLabel);
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
            if (_Action != null && _Message != null) {
                CheckboxView.IsChecked = _Action.IsCompleted;
                if (_Action.IsCompleted) {
                    CheckboxView.TintColor = A.Color_NachoTextGray;
                    TitleLabel.TextColor = A.Color_NachoTextGray;
                } else {
                    TitleLabel.TextColor = A.Color_NachoDarkText;
                    if (_Action.IsHot) {
                        CheckboxView.TintColor = UIColor.FromRGB (0xEE, 0x70, 0x5B);
                    } else {
                        CheckboxView.TintColor = A.Color_NachoGreen;
                    }
                }
                if (Message.Subject.Equals (_Action.Title)) {
                    if (Message.Intent == McEmailMessage.IntentType.ResponseRequired) {
                        TitleLabel.Text = NSBundle.MainBundle.LocalizedString ("Response Required", "");
                    } else if (Message.Intent == McEmailMessage.IntentType.PleaseRead) {
                        TitleLabel.Text = NSBundle.MainBundle.LocalizedString ("Please Read", "");
                    } else if (Message.Intent == McEmailMessage.IntentType.Urgent) {
                        TitleLabel.Text = NSBundle.MainBundle.LocalizedString ("Urgent - Attention Required", "");
                    } else {
                        TitleLabel.Text = _Action.Title;
                    }
                } else {
                    TitleLabel.Text = _Action.Title;
                }
                if (!_Action.IsCompleted && _Action.DueDate != default (DateTime)) {
                    if (_Action.DueDate > DateTime.UtcNow) {
                        DateLabel.Text = string.Format (NSBundle.MainBundle.LocalizedString ("due {0} (message)", ""), Pretty.FutureDate (_Action.DueDate, _Action.DueDateIncludesTime));
                    } else {
                        DateLabel.Text = string.Format (NSBundle.MainBundle.LocalizedString ("by {0} (message)", ""), Pretty.FutureDate (_Action.DueDate, _Action.DueDateIncludesTime));
                    }
                } else if (_Action.IsDeferred) {
                    DateLabel.Text = NSBundle.MainBundle.LocalizedString ("Deferred (action state)", "");
                } else {
                    DateLabel.Text = "";
                }
            } else {
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
            dateSize.Height = DateLabel.Font.RoundedLineHeight (1.0f);

            CGRect frame;

            frame = DateLabel.Frame;
            frame.Width = dateSize.Width;
            frame.X = Bounds.Width - RightSpacing - frame.Width;
            frame.Height = dateSize.Height;
            frame.Y = textTop + (TitleLabel.Font.Ascender + (textHeight - TitleLabel.Font.LineHeight) / 2.0f - DateLabel.Font.Ascender - (dateSize.Height - DateLabel.Font.LineHeight) / 2.0f);
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
            CheckboxView.Changed = null;
        }

        void CheckboxChanged (bool isChecked)
        {
            NcTask.Run (() => {
                NcModel.Instance.RunInTransaction (() => {
                    if (isChecked) {
                        Action.Complete ();
                    } else {
                        Action.Uncomplete (McAction.ActionState.Open);
                    }
                });
                BeginInvokeOnMainThread (() => {
                    Update ();
                });
            }, "MessageActionHeaderView_CheckboxChanged", NcTask.ActionSerialScheduler);
        }
    }
}

