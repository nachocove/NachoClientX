// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class LabelSelectionViewController : NcUIViewControllerNoLeaks, INachoLabelChooser
    {
        protected const float X_INDENT = 30;

        public string selectedName;

        protected const int SELECTED_BUTTON_IMAGE_TAG = 88;
        protected const int NOT_SELECTED_BUTTON_IMAGE_TAG = 99;

        protected const int SELECTION_BUTTON_STARTING_TAG = 1000;

        protected nint selectedButtonTag = SELECTION_BUTTON_STARTING_TAG;

        protected INachoLabelChooserParent owner;
        protected List<string> labelList = new List<string> ();

        UIBarButtonItem DismissButton;

        UIScrollView scrollView = new UIScrollView ();
        UIView contentView = new UIView ();
        nfloat yOffset = 0;

        public LabelSelectionViewController () : base()
        {
            ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
        }

        public LabelSelectionViewController (IntPtr handle) : base (handle)
        {
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        // INachoLabelChooser
        public void SetOwner (INachoLabelChooserParent owner, int accountId)
        {
            this.owner = owner;
            // Ignores accountId
        }

        // INachoLabelChooser
        public void SetLabelList (List<string> labelList)
        {
            this.labelList = labelList;
        }

        // INachoLabelChooser
        public void SetSelectedName (string selectedName)
        {
            this.selectedName = selectedName;
        }

        public override void ViewDidLoad ()
        {
            if (!labelList.Contains (selectedName)) {
                labelList.Insert (0, selectedName);
            }
            base.ViewDidLoad ();
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoGreen;

            var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, 44));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            var navItem = new UINavigationItem ("Choose a Label");
            using (var image = UIImage.FromBundle ("modal-close")) {
                DismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                DismissButton.AccessibilityLabel = "Dismiss";
                DismissButton.Clicked += DismissViewTouchUpInside;
                navItem.LeftBarButtonItem = DismissButton;
            }
            navBar.Items = new UINavigationItem[] { navItem };

            View.AddSubview (navBar);
            yOffset = 64;

            Util.AddHorizontalLine (0, yOffset, View.Frame.Width, UIColor.LightGray, View);
            yOffset += 2;

            scrollView.Frame = new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height);
            contentView.Frame = new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height);

            scrollView.AddSubview (contentView);
            View.AddSubview (scrollView);


            int i = 0;
            yOffset = 2;
            foreach (string name in labelList) {
                ListSelectionButton selectionButton = new ListSelectionButton (ContactsHelper.ExchangeNameToLabel (name), SELECTION_BUTTON_STARTING_TAG + i);
                UIButton button = selectionButton.GetButton (View, yOffset);
                button.TouchUpInside += SelectionButtonClicked;
                contentView.AddSubview (button);
                if (name == selectedName) {
                    UIImageView selectedButtonSelectedImageView = (UIImageView)button.ViewWithTag (SELECTED_BUTTON_IMAGE_TAG);
                    UIImageView selectedButtonNotSelectedImageView = (UIImageView)button.ViewWithTag (NOT_SELECTED_BUTTON_IMAGE_TAG);
                    selectedButtonSelectedImageView.Hidden = false;
                    selectedButtonNotSelectedImageView.Hidden = true;
                }
                yOffset += 44;
                Util.AddHorizontalLine (62, yOffset, View.Frame.Width - 62, UIColor.LightGray, contentView);
                yOffset += 1;
                i++;
            }

            LayoutView ();
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new CGRect (0, 66, View.Frame.Width, View.Frame.Height - 64);
            contentView.Frame = new CGRect (0, 0, View.Frame.Width, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;
        }

        protected void SelectionButtonClicked (object sender, EventArgs e)
        {
            UIButton selectedButton = (UIButton)sender;
            selectedButtonTag = selectedButton.Tag;
            selectedName = labelList [selectedButtonTag.ToArrayIndex () - 1000];

            owner.PrepareForDismissal (selectedName);
            DismissViewController (true, null);
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

            public UIButton GetButton (UIView parentView, nfloat yOffset)
            {
                //FIXME make this button either be selected or not, don't hide image views
                UIButton selectionButton = new UIButton (new CGRect (0, yOffset, parentView.Frame.Width, 44));
                selectionButton.AccessibilityLabel = "Selection";
                selectionButton.Tag = tag;
                selectionButton.BackgroundColor = A.Color_NachoGreen;

                UIImageView buttonSelectedImageView = new UIImageView (UIImage.FromBundle ("modal-checkbox-checked"));
                buttonSelectedImageView.Frame = new CGRect (20, 14, buttonSelectedImageView.Frame.Width, buttonSelectedImageView.Frame.Height);
                buttonSelectedImageView.Tag = SELECTED_BUTTON_IMAGE_TAG;
                buttonSelectedImageView.Hidden = true;
                selectionButton.AddSubview (buttonSelectedImageView);

                UIImageView buttonNotSelectedImageView = new UIImageView (UIImage.FromBundle ("modal-checkbox"));
                buttonNotSelectedImageView.Frame = new CGRect (20, 14, buttonNotSelectedImageView.Frame.Width, buttonNotSelectedImageView.Frame.Height);
                buttonNotSelectedImageView.Tag = NOT_SELECTED_BUTTON_IMAGE_TAG;
                buttonNotSelectedImageView.Hidden = false;
                selectionButton.AddSubview (buttonNotSelectedImageView);

                UILabel buttonLabel = new UILabel (new CGRect (buttonSelectedImageView.Frame.Right + 26, 0, 210, 44));
                buttonLabel.TextColor = UIColor.White;
                buttonLabel.Font = A.Font_AvenirNextMedium14;
                buttonLabel.Text = label;
                selectionButton.AddSubview (buttonLabel);

                return selectionButton;
            }
        }

        public class ExchangeLabel
        {
            public string type;
            public string label;

            public ExchangeLabel (string type, string label)
            {
                this.type = type;
                this.label = label;
            }
        }

        protected override void Cleanup ()
        {
            DismissButton.Clicked -= DismissViewTouchUpInside;
            DismissButton = null;

            for (int i = 0; i < View.Subviews.Length; i++) {
                if (View.Subviews [i].GetType () == typeof(UIButton)) {
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

        private void DismissViewTouchUpInside (object sender, EventArgs e)
        {
            DismissViewController (true, null);
        }

    }
}