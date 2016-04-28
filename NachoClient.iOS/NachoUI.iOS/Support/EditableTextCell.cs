//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class EditableTextCell : SwipeTableViewCell, IUITextViewDelegate
    {

        public readonly UITextView TextView;
        public nfloat Height { get; private set; }
        nfloat MinimumHeight = 44.0f;
        nfloat RightPadding = 10.0f;
        public bool AllowsNewlines = true;
        public UIResponder FollowingResponder;
        UILabel PlaceholderLabel;
        public string Placeholder {
            get {
                return PlaceholderLabel.Text;
            }
            set {
                PlaceholderLabel.Text = value;
            }
        }

        public EditableTextCell () : base ("__EditableTextCell")
        {
            Height = MinimumHeight;
            TextView = new UITextView (ContentView.Bounds);
            TextView.Font = UIFont.SystemFontOfSize (UIFont.SystemFontSize);
            TextView.Delegate = this;
            TextView.ContentInset = new UIEdgeInsets (0.0f, 0.0f, 0.0f, 0.0f);
            TextView.ScrollEnabled = false;
            TextView.AllowsEditingTextAttributes = false;
            TextView.TextContainer.LineFragmentPadding = 0.0f;
            PlaceholderLabel = new UILabel ();
            ContentView.AddSubview(TextView);
            ContentView.AddSubview (PlaceholderLabel);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var width = ContentView.Bounds.Width - SeparatorInset.Left - SeparatorInset.Right;
            TextView.Frame = new CGRect (SeparatorInset.Left, 0.0f, width, ContentView.Bounds.Height);
            var inset = (nfloat)Math.Round ((MinimumHeight - TextView.Font.LineHeight) / 2.0f);
            TextView.TextContainerInset = new UIEdgeInsets (inset, 0.0f, inset, RightPadding);
            if (!PlaceholderLabel.Hidden) {
                PlaceholderLabel.Font = TextView.Font;
                PlaceholderLabel.TextColor = ContentView.BackgroundColor.ColorDarkenedByAmount (0.15f);
                PlaceholderLabel.SizeToFit ();
                PlaceholderLabel.Frame = new CGRect (TextView.Frame.X + TextView.TextContainerInset.Left, TextView.Frame.Y + TextView.TextContainerInset.Top, PlaceholderLabel.Frame.Width, PlaceholderLabel.Frame.Height);
            }
        }

        public override void Cleanup ()
        {
            TextView.Delegate = null;
            base.Cleanup ();
        }

        public void UpdatePlaceholderVisible ()
        {
            PlaceholderLabel.Hidden = !String.IsNullOrWhiteSpace (TextView.Text);
        }

        [Export ("textViewDidChange:")]
        public void Changed (UITextView textView)
        {
            UpdatePlaceholderVisible ();
            if (ResizeTextView ()) {
                // Evidently this reloads the sizes of the cells, but not the contents, which is exactly what we want in this case
                TableView.BeginUpdates ();
                TableView.EndUpdates ();
            }
        }

        [Export ("textView:shouldChangeTextInRange:replacementText:")]
        public bool ShouldChangeText (UITextView textView, NSRange range, string text)
        {
            if (!AllowsNewlines) {
                // user inputting text
                if (text == "\n" || text == "\r" || text == "\r\n") {
                    if (FollowingResponder != null) {
                        FollowingResponder.BecomeFirstResponder ();
                    } else {
                        ResignFirstResponder ();
                    }
                    return false;
                }
                // pasting text with newlines
                var sanitized = System.Text.RegularExpressions.Regex.Replace(text, "[\\r\\n]+", " ");
                if (text != sanitized) {
                    var zero = textView.BeginningOfDocument;
                    var start = textView.GetPosition (zero, range.Location);
                    var end = textView.GetPosition (start, range.Length);
                    var textRange = textView.GetTextRange (start, end);
                    textView.ReplaceText (textRange, sanitized);
                    return false;
                }
            }
            return true;
        }

        public void PrepareForWidth (nfloat width)
        {
            var frame = Frame;
            if (frame.Width != width){
                frame.Width = width;
                Frame = frame;
                LayoutIfNeeded ();
                ResizeTextView ();
            }
        }

        bool ResizeTextView ()
        {
            var frame = TextView.Frame;
            TextView.ScrollEnabled = true;
            TextView.Frame = new CGRect (frame.X, frame.Y, frame.Width, 1.0f);
            var roundedHeight = (nfloat)Math.Ceiling (TextView.ContentSize.Height);
            TextView.ScrollEnabled = false;
            if (roundedHeight != frame.Height) {
                Height = (nfloat)Math.Max (MinimumHeight, roundedHeight);
                TextView.Frame = frame;
                return Height != TextView.Frame.Height;
            } else {
                TextView.Frame = frame;
                return false;
            }
        }

    }

}

