// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using System.IO;
using System.Drawing;
using System.Collections.Generic;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class NotesViewController : NcUIViewController
    {
        public NotesViewController (IntPtr handle) : base (handle)
        {
        }

        UIColor separatorColor = A.Color_NachoSeparator;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected float LINE_OFFSET = 30f;
        protected float CELL_HEIGHT = 44f;
        protected float TEXT_LINE_HEIGHT = 19.124f;
        protected float NOTES_OFFSET = 0f;
        protected float keyboardHeight;
        protected float KEYBOARD_HEIGHT = 216f;
        UIColor solidTextColor = A.Color_NachoBlack;

        protected int DATE_DETAIL_TAG = 100;

        protected UIView notesView;
        protected UITextView notesTextView;

        protected UIView line1;
        protected UIView line2;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateNotesView ();
            notesTextView.BecomeFirstResponder ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            ConfigureNotesView ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }


        protected void CreateNotesView ()
        {
            //notes
            notesTextView = new UITextView (new RectangleF (0, LINE_OFFSET + 10, SCREEN_WIDTH, View.Frame.Height));
            notesTextView.Font = A.Font_AvenirNextRegular14;
            notesTextView.TextColor = solidTextColor;
            notesTextView.BackgroundColor = A.Color_NachoYellow;
            //notesTextView.ContentInset = new UIEdgeInsets (0, 35, 0, 15);
            var beginningRange = new NSRange (0, 0);
            notesTextView.SelectedRange = beginningRange;


            notesTextView.Changed += (object sender, EventArgs e) => {
                //NotesSelectionChanged (notesTextView);
            };
            line1 = AddLine (0, LINE_OFFSET + 10, SCREEN_WIDTH, separatorColor);

            //Content View

            //contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT);

            contentView.BackgroundColor = A.Color_NachoBlue;
            contentView.BackgroundColor = A.Color_NachoNowBackground;
//            contentView.AddSubviews (new UIView[] {
//                notesTextView,
//                line1,
//                line2
//            });
            MakeDateLabel (0, LINE_OFFSET - 10, SCREEN_WIDTH, 15, DATE_DETAIL_TAG, contentView);
            contentView.Add (notesTextView);
            contentView.Add (line1);
            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, 184);

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoRed;
            scrollView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, 194);
            //scrollView.ContentSize = new SizeF (SCREEN_WIDTH, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT);
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;

            Console.WriteLine ("______________________________________________________________________________________");
            Console.WriteLine ("contentView: " + contentView.Frame.ToString ());
            Console.WriteLine ("contentView.size: " + contentView.Frame.Size.ToString ());
            Console.WriteLine ("scrollView: " + scrollView.Frame.ToString ());
            Console.WriteLine ("scrollView.ContentSize: " + scrollView.ContentSize.ToString ());
            Console.WriteLine ("______________________________________________________________________________________");
        }

//        protected void NotesSelectionChanged (UITextView textView)
//        {
//            // We want to scroll the caret rect into view
//            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.end);
//            caretRect.Size = new SizeF (caretRect.Size.Width, caretRect.Size.Height + textView.TextContainerInset.Bottom);
//            // Make sure our textview is big enough to hold the text
//            var frame = textView.Frame;
//            var frameBefore = frame;
//            frame.Size = new SizeF (textView.ContentSize.Width, textView.ContentSize.Height);
//            var frameAfter = frame;
//            if (frameBefore.Height < frameAfter.Height) {
//                NOTES_OFFSET += TEXT_LINE_HEIGHT;
//                NotesLayoutView ();
//            }
//            if (frameBefore.Height > frameAfter.Height) {
//                NOTES_OFFSET -= TEXT_LINE_HEIGHT;
//                NotesLayoutView ();
//            }
//
//            textView.Frame = frame;
//            // And update our enclosing scrollview for the new content size
//            scrollView.ContentSize = contentView.Frame.Size;
//            // Adjust the caretRect to be in our enclosing scrollview, and then scroll it
//            caretRect.Y += textView.Frame.Y;
//            scrollView.ScrollRectToVisible (caretRect, true);
//        }

//        protected void NotesLayoutView ()
//        {
//            UIView.Animate (0.2, () => {
//                notesView.Frame = new RectangleF (0, LINE_OFFSET, SCREEN_WIDTH, (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT + NOTES_OFFSET);
//                line2.Frame = new RectangleF (0,  LINE_OFFSET + (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT + NOTES_OFFSET, SCREEN_WIDTH, .5f);
//
//            });
//            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT + NOTES_OFFSET);
//            scrollView.ContentSize = new SizeF (SCREEN_WIDTH, (LINE_OFFSET * 2) + (CELL_HEIGHT * 3) + TEXT_LINE_HEIGHT + NOTES_OFFSET);
//        }

        public UIView AddLine (float offset, float yVal, float width, UIColor color)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            return (lineUIView);
        }

        public void ConfigureNotesView () {
            //NotesLayoutView();

            //date
            var dateDetailLabel = contentView.ViewWithTag (DATE_DETAIL_TAG) as UILabel;
            dateDetailLabel.Text = Pretty.ExtendedDateString(DateTime.UtcNow);

        }

        public void MakeDateLabel (float xOffset, float yOffset, float width, float height, int tag, UIView parentView)
        {
            UILabel DateLabel = new UILabel (new RectangleF (xOffset, yOffset, width, height));
            DateLabel.Font = A.Font_AvenirNextRegular12;
            DateLabel.TextColor = UIColor.LightGray;
            DateLabel.Tag = tag;
            DateLabel.TextAlignment = UITextAlignment.Center;
            parentView.Add (DateLabel);
        }


    }
}
