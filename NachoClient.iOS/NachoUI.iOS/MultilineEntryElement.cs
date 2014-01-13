
// Highly modified from this starting point
// https://gist.github.com/akcoder/5723722

using System;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Dialog;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public class MultilineEntryElement : UIViewElement, IElementSizing
    {
        public MultilineEntryElement (string placeholder, float height, bool transparentBackground) 
            : base (null, new MultilineView (placeholder, height, transparentBackground), false)
        {
            Flags = CellFlags.DisableSelection;
            if (transparentBackground) {
                Flags |= CellFlags.Transparent;
            }
        }

        /// <summary>
        /// The key used for reusable UITableViewCells.
        /// </summary>
        private static readonly NSString EntryKey = new NSString ("MultilineEntryElement");

        protected override NSString CellKey {
            get { return EntryKey; }
        }

        public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
        {
            tableView.DeselectRow (path, false);
            UITextView tv = View.Subviews [0] as UITextView;
            if (tv != null) {
                tv.BecomeFirstResponder ();
            }
        }

        public override string Summary ()
        {
            UITextView tv = View.Subviews [0] as UITextView;
            if (tv != null) {
                return tv.Text;
            }
            return null;
        }

        private class TextDelgate : UITextViewDelegate
        {
            private string _placeholder;

            public TextDelgate (string placeholder)
            {
                _placeholder = placeholder;
            }

            public override void EditingStarted (UITextView textView)
            {
                if (textView.TextColor == UIColor.LightGray) {
                    textView.Text = "";
                    textView.TextColor = UIColor.Black;
                    textView.SelectedRange = new NSRange (0, 0);
                }
            }

            public override void EditingEnded (UITextView textView)
            {
                if (textView.Text.Length == 0) {
                    textView.Text = _placeholder;
                    textView.TextColor = UIColor.LightGray;
                    textView.SelectedRange = new NSRange (0, 0);
                }
            }

            public override void SelectionChanged (UITextView textView)
            {
                NSRange r = textView.SelectedRange;
                textView.ScrollRangeToVisible (r);
            }

            float GetHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 120f;
            }
        }

        private class MultilineView : UIView
        {
            public MultilineView (string placeholder, float height, bool transparentBackground)
            {
                //Temporary width until we can re-layout
                float containerWidth = 10;

                // create actual text view
                UITextView textView = new UITextView (new RectangleF (0, 0, containerWidth, height - (transparentBackground ? 0 : 12))) {
                    AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin,
                    Text = placeholder,
                    TextAlignment = UITextAlignment.Left,
                    TextColor = UIColor.LightGray,
                    Delegate = new TextDelgate (placeholder),
                    ContentInset = new UIEdgeInsets (0f, 0f, 10f, 0f),
                    TextContainerInset = new UIEdgeInsets (0f, 0f, 10f, 0f),

                };


                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin;

                base.Frame = new RectangleF (transparentBackground ? 0 : 3, transparentBackground ? 0 : 2, containerWidth, height);

                if (transparentBackground) {
                    base.BackgroundColor = UIColor.Clear;
                    textView.Layer.BackgroundColor = UIColor.White.CGColor;
                }
                base.AddSubview (textView);
            }

            public override void LayoutSubviews ()
            {
                var superWidth = Superview.Superview.Frame.Width;
                Frame = new RectangleF (Frame.X, Frame.Y, superWidth - Frame.X, Frame.Height);

                var subFrame = Subviews [0].Frame;
                Subviews [0].Frame = new RectangleF (subFrame.X, subFrame.Y, superWidth - (Frame.X * 3), subFrame.Height);
                Subviews [0].LayoutSubviews ();
            }
        }
    }
}
