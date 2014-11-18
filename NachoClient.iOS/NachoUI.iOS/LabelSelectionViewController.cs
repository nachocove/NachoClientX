// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class LabelSelectionViewController : NcUIViewControllerNoLeaks
	{
        protected const float X_INDENT = 30;

        protected List<PhoneLabel> phoneLabels = new List<PhoneLabel> ();
        public PhoneLabel selectedPhoneLabel; 
        protected McContact contact;

        protected const int SELECTED_BUTTON_IMAGE_TAG = 88;
        protected const int NOT_SELECTED_BUTTON_IMAGE_TAG = 99;

        protected const int DISMISS_VIEW_BUTTON_TAG = 100;

        protected const int SELECTION_BUTTON_STARTING_TAG = 1000;

        protected int selectedButtonTag = SELECTION_BUTTON_STARTING_TAG;

        public ContactDefaultSelectionViewController owner; 

		public LabelSelectionViewController (IntPtr handle) : base (handle)
		{

		}

        public override void ViewDidLoad ()
        {
            CreateLabelList ();
            base.ViewDidLoad ();
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoGreen;

            UIButton dismissViewButton = new UIButton (new RectangleF (X_INDENT, X_INDENT, 25, 25));
            dismissViewButton.SetImage (UIImage.FromBundle ("modal-close"), UIControlState.Normal);
            dismissViewButton.TouchUpInside += DismissViewTouchUpInside;
            dismissViewButton.Tag = DISMISS_VIEW_BUTTON_TAG;
            View.AddSubview (dismissViewButton);    

            UILabel headerLabel = new UILabel (new RectangleF (View.Frame.Width / 2 - 75, X_INDENT, 150, 25));
            headerLabel.TextAlignment = UITextAlignment.Center;
            headerLabel.Font = A.Font_AvenirNextDemiBold17;
            headerLabel.TextColor = UIColor.White;
            headerLabel.Text = "Choose a Label";
            View.AddSubview (headerLabel);

            float yOffset = headerLabel.Frame.Bottom + 16;
            Util.AddHorizontalLine (0, yOffset, View.Frame.Width, UIColor.LightGray, View);
            yOffset += 1;

            int i = 0;
            foreach (var p in phoneLabels) {
                ListSelectionButton selectionButton = new ListSelectionButton (p.label, SELECTION_BUTTON_STARTING_TAG + i);
                UIButton button = selectionButton.GetButton (View, yOffset);
                button.TouchUpInside += SelectionButtonClicked;
                View.AddSubview (button);
                if (0 == i) {
                    button.SendActionForControlEvents (UIControlEvent.TouchUpInside);
                }
                yOffset += 58;
                Util.AddHorizontalLine (80, yOffset, View.Frame.Width - 80, UIColor.LightGray, View);
                yOffset += 1;
                i++;
            }
        }

        protected void SelectionButtonClicked (object sender, EventArgs e)
        {
            UIButton previouslySelectedButton = (UIButton)View.ViewWithTag (selectedButtonTag);
            UIImageView previouslySelectedButtonSelectedImageView = (UIImageView)previouslySelectedButton.ViewWithTag (SELECTED_BUTTON_IMAGE_TAG);
            UIImageView previouslySelectedButtonNotSelectedImageView = (UIImageView)previouslySelectedButton.ViewWithTag (NOT_SELECTED_BUTTON_IMAGE_TAG);
            previouslySelectedButtonSelectedImageView.Hidden = true;
            previouslySelectedButtonNotSelectedImageView.Hidden = false;

            UIButton selectedButton = (UIButton)sender;
            UIImageView selectedButtonSelectedImageView = (UIImageView)selectedButton.ViewWithTag (SELECTED_BUTTON_IMAGE_TAG);
            UIImageView selectedButtonNotSelectedImageView = (UIImageView)selectedButton.ViewWithTag (NOT_SELECTED_BUTTON_IMAGE_TAG);
            selectedButtonSelectedImageView.Hidden = false;
            selectedButtonNotSelectedImageView.Hidden = true;

            selectedButtonTag = selectedButton.Tag;
            selectedPhoneLabel = phoneLabels [selectedButtonTag - 1000];
        }

        public class PhoneLabel 
        {
            public string type;
            public string label;

            public PhoneLabel(string type, string label)
            {
                this.type = type;
                this.label = label;
            }
        }

        public class ListSelectionButton
        {
            protected string label;
            protected int tag;

            public ListSelectionButton (string label, int tag)
            {
                this.label = label;
                this.tag = tag;
            }

            public UIButton GetButton(UIView parentView, float yOffset)
            {
                UIButton selectionButton = new UIButton (new RectangleF (0, yOffset, parentView.Frame.Width, 58));
                selectionButton.Tag = tag;
                selectionButton.BackgroundColor = A.Color_NachoGreen;

                UIImageView buttonSelectedImageView = new UIImageView (UIImage.FromBundle ("modal-checkbox-checked"));
                buttonSelectedImageView.Frame = new RectangleF (30, 18, buttonSelectedImageView.Frame.Width, buttonSelectedImageView.Frame.Height);
                buttonSelectedImageView.Tag = SELECTED_BUTTON_IMAGE_TAG;
                buttonSelectedImageView.Hidden = true;
                selectionButton.AddSubview (buttonSelectedImageView);

                UIImageView buttonNotSelectedImageView = new UIImageView (UIImage.FromBundle ("modal-checkbox"));
                buttonNotSelectedImageView.Frame = new RectangleF (30, 18, buttonNotSelectedImageView.Frame.Width, buttonNotSelectedImageView.Frame.Height);
                buttonNotSelectedImageView.Tag = NOT_SELECTED_BUTTON_IMAGE_TAG;
                buttonNotSelectedImageView.Hidden = false;
                selectionButton.AddSubview (buttonNotSelectedImageView);

                UILabel buttonLabel = new UILabel (new RectangleF (buttonSelectedImageView.Frame.Right + 34, buttonSelectedImageView.Frame.Y, 210, 20));
                buttonLabel.TextColor = UIColor.White;
                buttonLabel.Font = A.Font_AvenirNextMedium14;
                buttonLabel.Text = label;
                selectionButton.AddSubview (buttonLabel);

                return selectionButton;
            }
        }

        protected void CreateLabelList ()
        {
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.HomePhoneNumber, "Home"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.MobilePhoneNumber, "Mobile"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.BusinessPhoneNumber, "Work"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.CarPhoneNumber, "Car"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.AssistantPhoneNumber, "Assistant"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.RadioPhoneNumber, "Radio"));
            phoneLabels.Add (new PhoneLabel (Xml.Contacts.PagerNumber, "Pager"));
        }

        protected override void Cleanup ()
        {
            //TODO
            UIButton dismissViewButton = (UIButton)View.ViewWithTag (DISMISS_VIEW_BUTTON_TAG);
            dismissViewButton.TouchUpInside -= DismissViewTouchUpInside;
            dismissViewButton = null;

            for (int i = 0; i < View.Subviews.Length; i++) {
                if (View.Subviews [i].GetType == UIButton) {
                    if (View.Subviews [i].Tag >= SELECTION_BUTTON_STARTING_TAG) {
                        UIButton selectionButton = (UIButton)View.Subviews [i];
                        selectionButton.TouchUpInside -= SelectionButtonClicked;
                        selectionButton = null;
                    }
                }
            }
        }

        protected override void ConfigureAndLayout ()
        {

        }

        public void SetContact (McContact contact)
        {
            this.contact = contact;
        }

        private void DismissViewTouchUpInside (object sender, EventArgs e)
        {
            owner.phoneLabel = selectedPhoneLabel;
            owner.SetPhoneLabel ();
            DismissViewController (true, null);
        }
	}
}
