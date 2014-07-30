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
        protected static float LINE_OFFSET = 30f;
        protected static float KEYBOARD_HEIGHT = 216f;
        protected static float NOTES_TEXT_VIEW_HEIGHT = UIScreen.MainScreen.Bounds.Height - KEYBOARD_HEIGHT - LINE_OFFSET - 10;
        protected static float TEXT_LINE_HEIGHT = 19.124f;
        protected float NOTES_OFFSET = 0f;
        protected float keyboardHeight;

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

            scrollView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, View.Frame.Height - KEYBOARD_HEIGHT);
            //notes
            notesTextView = new UITextView (new RectangleF (0, LINE_OFFSET + 10, SCREEN_WIDTH, NOTES_TEXT_VIEW_HEIGHT));
            notesTextView.Font = A.Font_AvenirNextRegular14;
            notesTextView.TextColor = solidTextColor;
            notesTextView.BackgroundColor = UIColor.White;
            //notesTextView.ContentInset = new UIEdgeInsets (0, 35, 0, 15);
            var beginningRange = new NSRange (0, 0);
            notesTextView.SelectedRange = beginningRange;

            notesTextView.Changed += (object sender, EventArgs e) => {
                NotesSelectionChanged (notesTextView);
            };
            line1 = AddLine (0, LINE_OFFSET + 10, SCREEN_WIDTH, separatorColor);

            //Content View
            contentView.BackgroundColor = UIColor.White;

            var DateView = new UIView (new RectangleF (0, 0, SCREEN_WIDTH, LINE_OFFSET + 10));
            DateView.BackgroundColor = A.Color_NachoNowBackground;
            MakeDateLabel (0, LINE_OFFSET - 10, SCREEN_WIDTH, 15, DATE_DETAIL_TAG, DateView);
            contentView.Add (DateView);
            contentView.Add (notesTextView);
            contentView.Add (line1);
            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, NOTES_TEXT_VIEW_HEIGHT + LINE_OFFSET + 10);

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.ContentSize = contentView.Frame.Size;

        }


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

        protected void NotesSelectionChanged (UITextView textView)
        {
            // We want to scroll the caret rect into view
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.end);
            caretRect.Size = new SizeF (caretRect.Size.Width, caretRect.Size.Height + textView.TextContainerInset.Bottom);
            // Make sure our textview is big enough to hold the text
            var frame = textView.Frame;
            frame.Size = new SizeF (textView.ContentSize.Width, textView.ContentSize.Height + 40);
            textView.Frame = frame;
            // And update our enclosing scrollview for the new content size
            scrollView.ContentSize = new SizeF (scrollView.ContentSize.Width, textView.Frame.Y + textView.Frame.Height);
            // Adjust the caretRect to be in our enclosing scrollview, and then scroll it
            caretRect.Y += textView.Frame.Y;
            scrollView.ScrollRectToVisible (caretRect, true);
        }

    }
}

