// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using UIKit;

namespace NachoClient.iOS
{
    public partial class SignatureEditViewController : NcUIViewControllerNoLeaks
    {
        public SignatureEditViewController (IntPtr handle)
            : base (handle)
        {
        }

        public delegate void OnSaveCallback (string text);

        string itemTitle;
        string explanatoryText;
        string existingText;
        public OnSaveCallback OnSave;

        UILabel labelView;
        NcTextView textView;

        private static nfloat HORIZONTAL_MARGIN = 15f;
        private static nfloat VERTICAL_MARGIN = 15f;

        public void Setup (string title, string explanatoryText, string existingText)
        {
            this.itemTitle = title;
            this.explanatoryText = explanatoryText;
            this.existingText = existingText;
        }

        protected override void CreateViewHierarchy ()
        {
            labelView = new UILabel (new CGRect (HORIZONTAL_MARGIN, 0, View.Frame.Width - (2 * HORIZONTAL_MARGIN), 0));
            labelView.Font = A.Font_AvenirNextRegular14;
            labelView.TextColor = A.Color_NachoBlack;
            labelView.BackgroundColor = A.Color_NachoLightGrayBackground;
            View.AddSubview (labelView);

            textView = new NcTextView (new CGRect (HORIZONTAL_MARGIN, 0, View.Frame.Width - (2 * HORIZONTAL_MARGIN), View.Frame.Height));
            textView.Font = A.Font_AvenirNextRegular14;
            textView.TextColor = A.Color_NachoBlack;
            textView.BackgroundColor = UIColor.White;
            textView.ContentInset = new UIEdgeInsets (VERTICAL_MARGIN, 0, VERTICAL_MARGIN, 0);
            textView.SelectionChanged += SelectionChangedHandler;
            View.AddSubview (textView);
        }

        protected override void ConfigureAndLayout ()
        {
            if (null == explanatoryText) {
                labelView.Hidden = true;
                ViewFramer.Create (labelView).Height (0);
            } else {
                labelView.Hidden = false;
                labelView.Lines = 0;
                labelView.Text = explanatoryText;
                labelView.LineBreakMode = UILineBreakMode.WordWrap;
                labelView.SizeToFit ();
            }

            textView.Text = existingText;
            textView.SelectedRange = new NSRange (0, 0);
            textView.BecomeFirstResponder ();

            Layout ();
        }

        protected override void Cleanup ()
        {
            textView.SelectionChanged -= SelectionChangedHandler;
            textView = null;
        }

        protected override void OnKeyboardChanged ()
        {
            Layout ();
            MakeCaretVisible (false);
        }

        private void Layout ()
        {
            var yOffset = labelView.Frame.Bottom;
            ViewFramer.Create (textView).Y(yOffset).Height (View.Frame.Height - yOffset - keyboardHeight);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NavigationItem.Title = itemTitle;
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != OnSave) {
                OnSave (textView.Text);
            }
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        private void MakeCaretVisible (bool animated)
        {
            var caretFrame = textView.GetCaretRectForPosition (textView.SelectedTextRange.End);
            textView.ScrollRectToVisible (caretFrame, animated);
        }

        private void SelectionChangedHandler (object sender, EventArgs args)
        {
            MakeCaretVisible (true);
        }
    }
}
