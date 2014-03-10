using System;
using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.Dialog;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public class MultilineEntryElement : UIViewElement, IElementSizing
    {
        public MultilineEntryElement (string placeholder, string value, float height, bool transparentBackground) 
            : base (null, new MultilineView (placeholder, value, height, transparentBackground), false)
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

        public override UITableViewCell GetCell (UITableView tv)
        {
            var c = base.GetCell (tv);
            foreach (var v in c.ContentView.Subviews) {
                if (v.GetType () == typeof(MultilineElement)) {
                    v.RemoveFromSuperview ();
                }
            }
            c.ContentView.AddSubview (View);
            return c;
        }

        public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
        {
            tableView.DeselectRow (path, false);
            UITextView tv = View as UITextView;
            if (tv != null) {
                tv.BecomeFirstResponder ();
            }
        }

        public override string Summary ()
        {
            UITextView tv = View as UITextView;
            if (tv == null) {
                return null;
            }
            if (tv.TextColor == UIColor.LightGray) {
                return null;
            }
            return tv.Text;
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
        }

        private class MultilineView : UITextView
        {
            public MultilineView (string placeholder, string value, float height, bool transparentBackground)
                : base (new RectangleF (0, 0, 10, height - (transparentBackground ? 0 : 12)))
            {
                int currentPos = base.SelectedRange.Location;
                NSRange newPos;

                base.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin;
              
                base.Text = placeholder;
                base.Font = UIFont.SystemFontOfSize(19.0f);
                base.TextAlignment = UITextAlignment.Left;
                base.TextColor = UIColor.LightGray;
                base.Delegate = new TextDelgate (placeholder);
                base.ContentInset = new UIEdgeInsets (0f, 0f, 10f, 0f);
                base.TextContainerInset = new UIEdgeInsets (0f, 0f, 10f, 0f);

                base.ScrollEnabled = true;


              

                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin;

                base.Frame = new RectangleF (transparentBackground ? 0 : 3, transparentBackground ? 0 : 2, 10, height);

                if (transparentBackground) {
                    base.BackgroundColor = UIColor.Clear;
                    base.Layer.BackgroundColor = UIColor.White.CGColor;
                }

                if (null != value) {
                    base.Delegate.EditingStarted (this);
                    base.Text = value;
                    base.Delegate.EditingEnded (this);
                    // and reset the cursor location to start of window now
                    newPos = new NSRange(currentPos, 0);
                    base.SelectedRange = newPos;
                }


            }

            public override void LayoutSubviews ()
            {
                var superWidth = Superview.Frame.Width;
                Frame = new RectangleF (Frame.X, Frame.Y, superWidth - Frame.X, Frame.Height);
                base.LayoutSubviews ();
            }
        }
    }
}
