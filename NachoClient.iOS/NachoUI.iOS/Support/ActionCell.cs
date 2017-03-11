//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{

    public class ActionCell : SwipeTableViewCell, ThemeAdopter
    {

        McAction Action;
        public readonly ActionCheckboxView CheckboxView;
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

        public McAction.ActionState UncompleteState = McAction.ActionState.Open;
        public bool StrikesCompletedActions;

        public ActionCell (IntPtr handle) : base (handle)
        {
            BackgroundView = new UIView ();
            BackgroundView.BackgroundColor = UIColor.White;

            DetailTextSpacing = 0.0f;
            HideDetailWhenEmpty = true;

            DetailTextLabel.Lines = 3;

            DateLabel = new UILabel ();
            ContentView.AddSubview (DateLabel);

            CheckboxView = new ActionCheckboxView (viewSize: 44.0f, checkboxSize: 20.0f);
            CheckboxView.Changed = CheckboxChanged;

            ContentView.AddSubview (CheckboxView);

            SeparatorInset = new UIEdgeInsets (0.0f, 64.0f, 0.0f, 0.0f);
        }

        public void AdoptTheme (Theme theme)
        {
            TextLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
            TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            DetailTextLabel.Font = theme.DefaultFont.WithSize (14.0f);
            DetailTextLabel.TextColor = theme.TableViewCellDetailLabelTextColor;
            DateLabel.Font = theme.DefaultFont.WithSize (14.0f);
            DateLabel.TextColor = theme.TableViewCellDateLabelTextColor;
        }

        public void SetAction (McAction action)
        {
            Action = action;
            Update ();
        }

        void Update ()
        {
            if (Action.Description != null) {
                DetailTextLabel.Text = System.Text.RegularExpressions.Regex.Replace (Action.Description, "\\s+", " ");
            } else {
                DetailTextLabel.Text = "";
            }
            if (Action.DueDate != default(DateTime)) {
                if (Action.DueDate > DateTime.UtcNow) {
                    DateLabel.Text = "by " + Pretty.FutureDate (Action.DueDate, Action.DueDateIncludesTime);
                } else {
                    DateLabel.Text = "due " + Pretty.FutureDate (Action.DueDate, Action.DueDateIncludesTime);
                }
            } else {
                DateLabel.Text = "";
            }
            if (Action.IsCompleted) {
                var strickenAttributes = new UIStringAttributes ();
                strickenAttributes.StrikethroughStyle = NSUnderlineStyle.Single;
                TextLabel.AttributedText = new NSAttributedString (Action.Title, strickenAttributes);;
                TextLabel.TextColor = A.Color_NachoTextGray;
                CheckboxView.TintColor = A.Color_NachoTextGray;
            } else {
                TextLabel.Text = Action.Title;
                TextLabel.TextColor = A.Color_NachoGreen;
                if (Action.IsHot) {
                    CheckboxView.TintColor = UIColor.FromRGB (0xEE, 0x70, 0x5B);
                } else {
                    CheckboxView.TintColor = A.Color_NachoGreen;
                }
            }
            CheckboxView.IsChecked = Action.IsCompleted;
            if (Action.IsNew) {
                ContentView.BackgroundColor = UIColor.FromRGB (0xFF, 0xFB, 0xEF);
            } else {
                ContentView.BackgroundColor = UIColor.White;
            }
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
            frame.Y = textTop + (TextLabel.Font.Ascender + (textHeight - TextLabel.Font.LineHeight) / 2.0f - DateLabel.Font.Ascender - (dateSize.Height - DateLabel.Font.LineHeight) / 2.0f);
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

        public override void WillTransitionToState (UITableViewCellState mask)
        {
            base.WillTransitionToState (mask);
            if ((mask & UITableViewCellState.ShowingEditControlMask) != 0) {
                CheckboxView.Enabled = false;
            }
        }

        public override void DidTransitionToState (UITableViewCellState mask)
        {
            base.DidTransitionToState (mask);
            if ((mask & UITableViewCellState.ShowingEditControlMask) == 0) {
                CheckboxView.Enabled = true;
            }
        }

        public override void Cleanup ()
        {
            CheckboxView.Changed = null;
            base.Cleanup ();
        }

        void CheckboxChanged (bool isChecked)
        {
            NcTask.Run(() => {
                NcModel.Instance.RunInTransaction (() => {
                    if (isChecked) {
                        Action.Complete ();
                    } else {
                        Action.Uncomplete (UncompleteState);
                    }
                });
                BeginInvokeOnMainThread(() => {
                    Update ();
                });
                // If we send a status ind, the item will disappear right away.
                // If we don't send a statud ind, the item will remain until the next reload
                // Still tying to decide which behavior is better
                // var account = McAccount.QueryById<McAccount> (Action.AccountId);
                // NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs() {
                //     Account = account,
                //     Status = NcResult.Info (NcResult.SubKindEnum.Info_ActionSetChanged)
                // });
            }, "ActionCell_CheckboxChanged", NcTask.ActionSerialScheduler);
        }

        public override void SwipeViewWillBeginShowingActions (SwipeActionsView view)
        {
            base.SwipeViewWillBeginShowingActions (view);
            CheckboxView.Enabled = false;
        }

        public override void SwipeViewDidEndShowingActions (SwipeActionsView view)
        {
            base.SwipeViewDidEndShowingActions (view);
            CheckboxView.Enabled = true;
        }

    }
}

