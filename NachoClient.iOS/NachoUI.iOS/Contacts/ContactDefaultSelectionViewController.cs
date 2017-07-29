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
using System.Linq;

namespace NachoClient.iOS
{
    public partial class ContactDefaultSelectionViewController : NcUIViewControllerNoLeaks, INachoLabelChooserParent
    {
        public INachoContactDefaultSelector owner;

        protected bool isDefaultSelected = false;

        protected static readonly nfloat X_INDENT = 20;
        protected static readonly nfloat CELL_HEIGHT = 44;
        protected const int CELL_TEXT_INSET = 62;

        //Default Buttons
        protected const int ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG = 10;
        protected const int ADD_NEW_DEFAULT_PHONE_BUTTON_TAG = 12;
        protected const int SET_DEFAULT_EMAIL_BUTTON_TAG = 14;
        protected const int SET_DEFAULT_PHONE_BUTTON_TAG = 16;

        //Action (call/email) buttons
        protected const int ADD_NEW_PHONE_BUTTON_TAG = 13;
        protected const int ADD_NEW_EMAIL_BUTTON_TAG = 11;
        protected const int COMPOSE_EMAIL_BUTTON_TAG = 15;
        protected const int CALL_CONTACT_BUTTON_TAG = 17;

        protected const int SELECTED_BUTTON_IMAGE_TAG = 88;
        protected const int NOT_SELECTED_BUTTON_IMAGE_TAG = 99;

        protected const int ADD_EMAIL_VIEW_TAG = 200;
        protected const int EMAIL_TEXTFIELD_TAG = 201;

        protected const int PHONE_ADD_VIEW_TAG = 300;
        protected const int PHONE_TEXTFIELD_TAG = 301;
        protected const int PHONE_LABEL_TAG = 302;
        protected const int SELECT_PHONELABEL_BUTTON_TAG = 303;

        protected const int SELECT_PHONE_VIEW_TAG = 400;
        protected const int SELECT_EMAIL_VIEW_TAG = 500;

        protected const int PHONE_SELECTION_STARTING_BUTTON_TAG = 1000;
        protected nint selectedPhoneButtonTag = PHONE_SELECTION_STARTING_BUTTON_TAG;

        protected const int EMAIL_SELECTION_STARTING_BUTTON_TAG = 2000;
        protected nint selectedEmailButtonTag = EMAIL_SELECTION_STARTING_BUTTON_TAG;

        protected List<LabelSelectionViewController.ExchangeLabel> possiblePhones = new List<LabelSelectionViewController.ExchangeLabel> ();
        public LabelSelectionViewController.ExchangeLabel phoneLabel = new LabelSelectionViewController.ExchangeLabel (Xml.Contacts.MobilePhoneNumber, "Mobile");

        protected List<string> contactEmailList = new List<string> ();
        protected string selectedEmailName;

        UIBarButtonItem dismissButton;
        UINavigationItem navItem;

        protected McContact contact;
        protected string newPhoneString;

        UIScrollView scrollView;

        public enum DefaultSelectionType
        {
            EmailAdder,
            PhoneNumberAdder,
            DefaultEmailSelector,
            DefaultPhoneSelector,
        }

        public DefaultSelectionType viewType;

        public ContactDefaultSelectionViewController () : base ()
        {
            ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
        }

        public ContactDefaultSelectionViewController (IntPtr handle) : base (handle)
        {
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoGreen;

            scrollView = new UIScrollView (View.Frame);
            scrollView.BackgroundColor = A.Color_NachoGreen;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
            View.AddSubview (scrollView);

            var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, CELL_HEIGHT));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            navItem = new UINavigationItem ();
            using (var image = UIImage.FromBundle ("modal-close")) {
                dismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                dismissButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Close", "");
                dismissButton.Clicked += DismissViewTouchUpInside;
                navItem.LeftBarButtonItem = dismissButton;
            }
            navBar.Items = new UINavigationItem [] { navItem };

            View.AddSubview (navBar);
            nfloat yOffset = 64;
            nfloat topOffset = 64;

            Util.AddHorizontalLine (0, yOffset, View.Frame.Width, A.Color_NachoBorderGray.ColorWithAlpha (.5f), View);

            //////////////ADD EMAIL VIEW
            //////////////ADD EMAIL VIEW
            //////////////ADD EMAIL VIEW
            //////////////ADD EMAIL VIEW

            yOffset = topOffset + 1;
            UIView addEmailView = new UIView (new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            addEmailView.BackgroundColor = A.Color_NachoGreen;
            addEmailView.Tag = ADD_EMAIL_VIEW_TAG;

            UILabel noEmailLabel = new UILabel (new CGRect (X_INDENT, 15, View.Frame.Width - (X_INDENT * 2), 60));
            noEmailLabel.Text = NSBundle.MainBundle.LocalizedString ("The contact you selected does not have an email address. Please add an email address for this contact.", "Messge for contact that does not have an email address");
            noEmailLabel.Font = A.Font_AvenirNextMedium14;
            noEmailLabel.TextColor = UIColor.White;
            noEmailLabel.Lines = 3;
            noEmailLabel.TextAlignment = UITextAlignment.Left;
            noEmailLabel.LineBreakMode = UILineBreakMode.WordWrap;
            addEmailView.AddSubview (noEmailLabel);

            UIView emailBox = new UIView (new CGRect (X_INDENT, noEmailLabel.Frame.Bottom + 15, View.Frame.Width - (X_INDENT * 2), CELL_HEIGHT));
            emailBox.BackgroundColor = UIColor.White;

            UIImageView mailImage = new UIImageView ();
            using (var loginImageTwo = UIImage.FromBundle ("Loginscreen-2")) {
                mailImage.Image = loginImageTwo;
            }
            mailImage.Frame = new CGRect (15, 16, 16, 11);
            emailBox.AddSubview (mailImage);

            var emailField = new UITextField (new CGRect (mailImage.Frame.Right + 15, 0, emailBox.Frame.Width - (mailImage.Frame.Right + 15), emailBox.Frame.Height));
            emailField.BackgroundColor = UIColor.White;
            emailField.TextColor = A.Color_NachoGreen;
            emailField.Placeholder = NSBundle.MainBundle.LocalizedString ("Email Address", "");
            emailField.Font = A.Font_AvenirNextMedium14;
            emailField.BorderStyle = UITextBorderStyle.None;
            emailField.TextAlignment = UITextAlignment.Left;
            emailField.KeyboardType = UIKeyboardType.EmailAddress;
            emailField.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailField.AutocorrectionType = UITextAutocorrectionType.No;
            emailField.ClearButtonMode = UITextFieldViewMode.WhileEditing;
            emailField.Tag = EMAIL_TEXTFIELD_TAG;
            emailField.ShouldReturn += TextFieldShouldReturn;
            emailBox.AddSubview (emailField);
            addEmailView.AddSubview (emailBox);

            AddDefaultToggleButton (emailBox.Frame.Left, emailBox.Frame.Bottom + 20, emailBox.Frame.Width, NSBundle.MainBundle.LocalizedString ("Set as default email address", "Button title for setting default email"), ToggleDefault, addEmailView, ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG);

            UIView actionView = CreateActionView (NSBundle.MainBundle.LocalizedString ("Compose Email (contact)", "Button title to compose email"), "now-newemail", SaveAndCompose, addEmailView, ADD_NEW_EMAIL_BUTTON_TAG);
            addEmailView.AddSubview (actionView);

            scrollView.AddSubview (addEmailView);

            //////////////ADD PHONE VIEW
            //////////////ADD PHONE VIEW
            //////////////ADD PHONE VIEW
            //////////////ADD PHONE VIEW
            yOffset = topOffset + 1;

            UIView addPhoneView = new UIView (new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            addPhoneView.BackgroundColor = A.Color_NachoGreen;
            addPhoneView.Tag = PHONE_ADD_VIEW_TAG;

            UILabel noPhoneLabel = new UILabel (new CGRect (X_INDENT, 15, View.Frame.Width - (X_INDENT * 2), 60));
            noPhoneLabel.Text = NSBundle.MainBundle.LocalizedString ("The contact you selected does not have a phone number. Please add a phone number for this contact.", "Message for label when contact does not have a phone number");
            noPhoneLabel.Font = A.Font_AvenirNextMedium14;
            noPhoneLabel.TextColor = UIColor.White;
            noPhoneLabel.Lines = 3;
            noPhoneLabel.TextAlignment = UITextAlignment.Left;
            noPhoneLabel.LineBreakMode = UILineBreakMode.WordWrap;
            addPhoneView.AddSubview (noPhoneLabel);

            UIView phoneBox = new UIView (new CGRect (X_INDENT, noPhoneLabel.Frame.Bottom + 15, View.Frame.Width - (X_INDENT * 2), CELL_HEIGHT));
            phoneBox.BackgroundColor = UIColor.White;

            UIButton selectLabelButton = new UIButton (new CGRect (0, 0, 90, CELL_HEIGHT));
            selectLabelButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Phone (noun)", "");
            selectLabelButton.Tag = SELECT_PHONELABEL_BUTTON_TAG;
            selectLabelButton.TouchUpInside += SelectLabelTouchUpInside;
            selectLabelButton.BackgroundColor = UIColor.White;
            phoneBox.AddSubview (selectLabelButton);

            UILabel selectLabelLabel = new UILabel (new CGRect (5, 0, 60, CELL_HEIGHT));
            selectLabelLabel.Font = A.Font_AvenirNextRegular12;
            selectLabelLabel.TextColor = UIColor.DarkGray;
            selectLabelLabel.TextAlignment = UITextAlignment.Center;
            selectLabelLabel.Text = phoneLabel.label;
            selectLabelLabel.Tag = PHONE_LABEL_TAG;
            selectLabelButton.AddSubview (selectLabelLabel);

            UIImageView selectLabelButtonImage = new UIImageView (UIImage.FromBundle ("gen-dropdown"));
            selectLabelButtonImage.Frame = new CGRect (selectLabelLabel.Frame.Right + 10, 10, selectLabelButtonImage.Frame.Width, selectLabelButtonImage.Frame.Height);
            selectLabelButton.AddSubview (selectLabelButtonImage);

            var phoneField = new UITextField (new CGRect (selectLabelButton.Frame.Right + 15, 0, phoneBox.Frame.Width - (selectLabelButton.Frame.Right + 20), phoneBox.Frame.Height));
            phoneField.BackgroundColor = UIColor.White;
            phoneField.Placeholder = NSBundle.MainBundle.LocalizedString ("Phone Number", "");
            phoneField.TextColor = A.Color_NachoGreen;
            phoneField.Font = A.Font_AvenirNextMedium14;
            phoneField.BorderStyle = UITextBorderStyle.None;
            phoneField.TextAlignment = UITextAlignment.Left;
            phoneField.KeyboardType = UIKeyboardType.PhonePad;
            phoneField.AutocapitalizationType = UITextAutocapitalizationType.None;
            phoneField.AutocorrectionType = UITextAutocorrectionType.No;
            phoneField.ClearButtonMode = UITextFieldViewMode.WhileEditing;
            phoneField.Tag = PHONE_TEXTFIELD_TAG;
            phoneField.ShouldReturn += TextFieldShouldReturn;
            phoneBox.AddSubview (phoneField);
            addPhoneView.AddSubview (phoneBox);

            AddDefaultToggleButton (phoneBox.Frame.Left, phoneBox.Frame.Bottom + 20, phoneBox.Frame.Width, NSBundle.MainBundle.LocalizedString ("Set as default phone number", "Button title for setting default phone number"), ToggleDefault, addPhoneView, ADD_NEW_DEFAULT_PHONE_BUTTON_TAG);

            UIView callActionView = CreateActionView (NSBundle.MainBundle.LocalizedString ("Call Contact", "Button title for calling a contact"), "contacts-call-swipe", SaveAndCall, addPhoneView, ADD_NEW_PHONE_BUTTON_TAG);
            addPhoneView.AddSubview (callActionView);

            addPhoneView.Hidden = true;
            scrollView.AddSubview (addPhoneView);


            ////////////MULTI PHONE SELECTOR VIEW
            ////////////MULTI PHONE SELECTOR VIEW
            ////////////MULTI PHONE SELECTOR VIEW
            ////////////MULTI PHONE SELECTOR VIEW
            yOffset = topOffset + 1;

            UIView selectPhoneView = new UIView (new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            selectPhoneView.BackgroundColor = A.Color_NachoGreen;
            selectPhoneView.Tag = SELECT_PHONE_VIEW_TAG;
            selectPhoneView.Hidden = true;
            scrollView.AddSubview (selectPhoneView);

            UIScrollView phoneListScrollView = new UIScrollView (new CGRect (0, 0, View.Frame.Width, CELL_HEIGHT * 4));
            selectPhoneView.AddSubview (phoneListScrollView);

            UIView listContentView = new UIView (new CGRect (0, 0, View.Frame.Width, CELL_HEIGHT * 4));
            phoneListScrollView.AddSubview (listContentView);

            int i = 0;
            nfloat internalYOffset = 0;
            foreach (var p in contact.PhoneNumbers) {
                LabelSelectionViewController.ListSelectionButton selectionButton = new LabelSelectionViewController.ListSelectionButton (ContactsHelper.ExchangeNameToLabel (p.Name), PHONE_SELECTION_STARTING_BUTTON_TAG + i);
                UIButton button = selectionButton.GetButton (View, internalYOffset);
                button.TouchUpInside += SelectionButtonClicked;
                possiblePhones.Add (new LabelSelectionViewController.ExchangeLabel (p.Name, p.Label));
                listContentView.AddSubview (button);
                if (0 == i) {
                    if (viewType == DefaultSelectionType.DefaultPhoneSelector) {
                        button.SendActionForControlEvents (UIControlEvent.TouchUpInside);
                    }
                }
                internalYOffset += CELL_HEIGHT;
                Util.AddHorizontalLine (CELL_TEXT_INSET, internalYOffset, View.Frame.Width - CELL_TEXT_INSET, UIColor.LightGray, listContentView);
                internalYOffset += 1;
                i++;
            }

            phoneListScrollView.Frame = new CGRect (0, 0, View.Frame.Width, CELL_HEIGHT * 4 + 10);
            listContentView.Frame = new CGRect (0, 0, View.Frame.Width, internalYOffset);
            phoneListScrollView.ContentSize = listContentView.Frame.Size;

            if (i > 4) {
                Util.AddHorizontalLine (0, phoneListScrollView.Frame.Bottom, View.Frame.Width, A.Color_NachoBorderGray.ColorWithAlpha (.5f), selectPhoneView);
                internalYOffset = phoneListScrollView.Frame.Bottom + 20;
            } else {
                internalYOffset += 20;
            }

            AddDefaultToggleButton (phoneBox.Frame.Left, internalYOffset, phoneBox.Frame.Width, NSBundle.MainBundle.LocalizedString ("Set as default phone number", ""), ToggleDefault, selectPhoneView, SET_DEFAULT_PHONE_BUTTON_TAG);

            UIView callAction = CreateActionView (NSBundle.MainBundle.LocalizedString ("Call Contact", "Button title for calling a contact"), "contacts-call-swipe", CallSelectedPhone, selectPhoneView, CALL_CONTACT_BUTTON_TAG);
            selectPhoneView.AddSubview (callAction);

            ////////////MULTI EMAIL SELECTOR VIEW
            ////////////MULTI EMAIL SELECTOR VIEW
            ////////////MULTI EMAIL SELECTOR VIEW
            ////////////MULTI EMAIL SELECTOR VIEW
            yOffset = topOffset + 1;
            UIView selectEmailView = new UIView (new CGRect (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            selectEmailView.BackgroundColor = A.Color_NachoGreen;
            selectEmailView.Tag = SELECT_EMAIL_VIEW_TAG;
            selectEmailView.Hidden = true;
            scrollView.AddSubview (selectEmailView);

            i = 0;
            internalYOffset = 0;
            foreach (var e in contact.EmailAddresses) {
                string canonicalEmailAddress = "Empty Email Address";
                if (null != McEmailAddress.QueryById<McEmailAddress> (e.EmailAddress)) {
                    canonicalEmailAddress = McEmailAddress.QueryById<McEmailAddress> (e.EmailAddress).CanonicalEmailAddress;
                }
                LabelSelectionViewController.ListSelectionButton selectionButton = new LabelSelectionViewController.ListSelectionButton (canonicalEmailAddress, EMAIL_SELECTION_STARTING_BUTTON_TAG + i);
                UIButton button = selectionButton.GetButton (View, internalYOffset);
                contactEmailList.Add (e.Name);
                button.TouchUpInside += SelectionButtonClicked;
                selectEmailView.AddSubview (button);
                if (0 == i) {
                    if (viewType == DefaultSelectionType.DefaultEmailSelector) {
                        button.SendActionForControlEvents (UIControlEvent.TouchUpInside);
                    }
                }
                internalYOffset += CELL_HEIGHT;
                Util.AddHorizontalLine (CELL_TEXT_INSET, internalYOffset, View.Frame.Width - CELL_TEXT_INSET, UIColor.LightGray, selectEmailView);
                internalYOffset += 1;
                i++;
            }

            internalYOffset += 20;

            UIButton toggleButton = AddDefaultToggleButton (phoneBox.Frame.Left, internalYOffset, phoneBox.Frame.Width, NSBundle.MainBundle.LocalizedString ("Set as default email address", "Button title for setting a default address"), ToggleDefault, selectEmailView, SET_DEFAULT_EMAIL_BUTTON_TAG);
            toggleButton.TitleEdgeInsets = new UIEdgeInsets (8, 0, 8, 34);

            UIView emailAction = CreateActionView (NSBundle.MainBundle.LocalizedString ("Compose Email", ""), "now-newemail", EmailSelectedAddress, selectEmailView, COMPOSE_EMAIL_BUTTON_TAG);
            selectEmailView.AddSubview (emailAction);
        }

        //View with label and circle-button for compose email or call contact
        protected UIView CreateActionView (string label, string image, EventHandler buttonClicked, UIView parent, int buttonTag)
        {
            UIView actionView = new UIView (new CGRect ((parent.Frame.Width / 2) - 60, parent.Frame.Bottom - 200, 120, 150));
            actionView.BackgroundColor = A.Color_NachoGreen;

            UIButton actionButton = UIButton.FromType (UIButtonType.RoundedRect);
            actionButton.AccessibilityLabel = label;
            actionButton.TintColor = A.Color_NachoBlue;
            actionButton.Layer.CornerRadius = 64 / 2;
            actionButton.Layer.MasksToBounds = true;
            actionButton.Layer.BorderColor = A.Color_NachoBackgroundGray.ColorWithAlpha (.5f).CGColor;
            actionButton.Layer.BorderWidth = .5f;
            actionButton.Frame = new CGRect (0, 0, 64, 64);
            actionButton.Tag = buttonTag;
            actionButton.Center = new CGPoint (actionView.Frame.Width / 2, actionView.Frame.Height / 2 - 50);
            using (var buttonImg = UIImage.FromBundle (image).ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                actionButton.SetImage (buttonImg, UIControlState.Normal);
            }
            actionButton.TouchUpInside += buttonClicked;
            actionView.AddSubview (actionButton);

            UILabel actionLabel = new UILabel (new CGRect (0, actionButton.Frame.Bottom + 15, actionView.Frame.Width, 15));
            actionLabel.Text = label;
            actionLabel.Font = A.Font_AvenirNextMedium14;
            actionLabel.TextAlignment = UITextAlignment.Center;
            actionLabel.TextColor = UIColor.White;
            actionView.AddSubview (actionLabel);

            return actionView;
        }

        //Set as default ... button
        protected UIButton AddDefaultToggleButton (nfloat frameX, nfloat frameY, nfloat width, string label, EventHandler buttonClicked, UIView parent, int tag)
        {
            UIButton toggleButton = new UIButton (new CGRect (frameX, frameY, width, CELL_HEIGHT));
            toggleButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Toggle Default", "Button title for toggling default");
            toggleButton.Tag = tag;
            toggleButton.TouchUpInside += buttonClicked;
            toggleButton.Layer.BorderColor = A.Color_NachoBlue.CGColor;
            toggleButton.Layer.BorderWidth = 1.0f;
            toggleButton.Layer.CornerRadius = 5f;

            //Configure Image
            toggleButton.ImageEdgeInsets = new UIEdgeInsets (8, -21, 8, 33);
            toggleButton.SetImage (UIImage.FromBundle ("modal-checkbox-checked"), UIControlState.Selected);
            toggleButton.SetImage (UIImage.FromBundle ("modal-checkbox"), UIControlState.Normal);

            //Configure Title
            toggleButton.SetTitle (label, UIControlState.Normal);
            toggleButton.TitleEdgeInsets = new UIEdgeInsets (8, 18, 8, 40);
            toggleButton.TitleLabel.Font = A.Font_AvenirNextMedium14;
            toggleButton.TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
            toggleButton.TitleLabel.Text = label;
            toggleButton.TitleLabel.TextColor = UIColor.White;
            toggleButton.TitleLabel.TextAlignment = UITextAlignment.Left;
            parent.AddSubview (toggleButton);

            return toggleButton;
        }

        public bool TextFieldShouldReturn (UITextField whatField)
        {
            whatField.EndEditing (true);
            return true;
        }

        protected void SelectionButtonClicked (object sender, EventArgs e)
        {
            nint selectedButtonTag = 0;
            switch (viewType) {
            case DefaultSelectionType.DefaultPhoneSelector:
                selectedButtonTag = selectedPhoneButtonTag;
                break;
            case DefaultSelectionType.DefaultEmailSelector:
                selectedButtonTag = selectedEmailButtonTag;
                break;
            }

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

            switch (viewType) {
            case DefaultSelectionType.DefaultPhoneSelector:
                selectedPhoneButtonTag = selectedButton.Tag;
                phoneLabel = possiblePhones [((int)selectedPhoneButtonTag) - PHONE_SELECTION_STARTING_BUTTON_TAG];
                break;
            case DefaultSelectionType.DefaultEmailSelector:
                selectedEmailButtonTag = selectedButton.Tag;
                selectedEmailName = contactEmailList [((int)selectedEmailButtonTag) - EMAIL_SELECTION_STARTING_BUTTON_TAG];
                break;
            }
        }

        protected override void Cleanup ()
        {
            //'X' button
            dismissButton.Clicked -= DismissViewTouchUpInside;
            dismissButton = null;

            //Label Selector for phone
            UIButton selectLabelButton = (UIButton)View.ViewWithTag (SELECT_PHONELABEL_BUTTON_TAG);
            selectLabelButton.TouchUpInside -= SelectLabelTouchUpInside;
            selectLabelButton = null;

            //Default Toggles
            UIButton addDefaultEmailButton = (UIButton)View.ViewWithTag (ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG);
            UIButton setDefaultEmailButton = (UIButton)View.ViewWithTag (SET_DEFAULT_EMAIL_BUTTON_TAG);
            UIButton addDefaultPhoneButton = (UIButton)View.ViewWithTag (ADD_NEW_DEFAULT_PHONE_BUTTON_TAG);
            UIButton setDefaultPhoneButton = (UIButton)View.ViewWithTag (SET_DEFAULT_PHONE_BUTTON_TAG);

            //Action Buttons
            UIButton addEmailButton = (UIButton)View.ViewWithTag (ADD_NEW_EMAIL_BUTTON_TAG);
            UIButton addPhoneButton = (UIButton)View.ViewWithTag (ADD_NEW_PHONE_BUTTON_TAG);
            UIButton callPhoneButton = (UIButton)View.ViewWithTag (CALL_CONTACT_BUTTON_TAG);
            UIButton composeEmailButton = (UIButton)View.ViewWithTag (COMPOSE_EMAIL_BUTTON_TAG);

            addDefaultEmailButton.TouchUpInside -= ToggleDefault;
            addEmailButton.TouchUpInside -= SaveAndCompose;
            addDefaultPhoneButton.TouchUpInside -= ToggleDefault;
            addPhoneButton.TouchUpInside -= SaveAndCall;
            setDefaultPhoneButton.TouchUpInside -= ToggleDefault;
            callPhoneButton.TouchUpInside -= CallSelectedPhone;
            setDefaultEmailButton.TouchUpInside -= ToggleDefault;
            composeEmailButton.TouchUpInside -= EmailSelectedAddress;

            addDefaultEmailButton = null;
            addEmailButton = null;
            addDefaultPhoneButton = null;
            addPhoneButton = null;
            setDefaultPhoneButton = null;
            callPhoneButton = null;
            setDefaultEmailButton = null;
            composeEmailButton = null;
        }

        protected override void ConfigureAndLayout ()
        {
            switch (viewType) {
            case DefaultSelectionType.EmailAdder:
                navItem.Title = NSBundle.MainBundle.LocalizedString ("Add a New Email", "View title for setting default email");
                UIView emailAddView = (UIView)View.ViewWithTag (ADD_EMAIL_VIEW_TAG);
                emailAddView.Hidden = false;
                break;
            case DefaultSelectionType.PhoneNumberAdder:
                navItem.Title = NSBundle.MainBundle.LocalizedString ("Add a New Number", "View title for setting default phone");
                UIView phoneAddView = (UIView)View.ViewWithTag (PHONE_ADD_VIEW_TAG);
                phoneAddView.Hidden = false;
                break;
            case DefaultSelectionType.DefaultPhoneSelector:
                navItem.Title = NSBundle.MainBundle.LocalizedString ("Select a Phone", "View title for choosing default phone");
                UIView phoneSelectView = (UIView)View.ViewWithTag (SELECT_PHONE_VIEW_TAG);
                phoneSelectView.Hidden = false;
                break;
            case DefaultSelectionType.DefaultEmailSelector:
                navItem.Title = NSBundle.MainBundle.LocalizedString ("Select an Email", "View title for choosing default email");
                UIView emailSelectView = (UIView)View.ViewWithTag (SELECT_EMAIL_VIEW_TAG);
                emailSelectView.Hidden = false;
                break;
            }

            LayoutView ();
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            CGRect contentFrame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            scrollView.ContentSize = contentFrame.Size;
        }

        public void SetContact (McContact contact)
        {
            this.contact = contact;
        }

        private void DismissViewTouchUpInside (object sender, EventArgs e)
        {
            DismissViewController (true, null);
        }

        private void SelectLabelTouchUpInside (object sender, EventArgs e)
        {
            UITextField phoneTextField = (UITextField)View.ViewWithTag (PHONE_TEXTFIELD_TAG);
            newPhoneString = phoneTextField.Text;

            SelectLabel ();
        }

        void SelectLabel ()
        {
            var destinationController = new LabelSelectionViewController ();
            destinationController.SetLabelList (ContactsHelper.GetAvailablePhoneNames (contact));
            destinationController.SetSelectedName (ContactsHelper.GetAvailablePhoneNames (contact).First ());
            destinationController.SetOwner (this, contact.AccountId);
            PresentViewController (destinationController, true, null);
        }

        private void SaveAndCall (object sender, EventArgs e)
        {
            UITextField phoneTextField = (UITextField)View.ViewWithTag (PHONE_TEXTFIELD_TAG);
            if (!string.IsNullOrEmpty (phoneTextField.Text)) {
                if (isDefaultSelected) {
                    contact.AddDefaultPhoneNumberAttribute (contact.AccountId, phoneLabel.type, phoneLabel.label, phoneTextField.Text);
                } else {
                    contact.AddPhoneNumberAttribute (contact.AccountId, phoneLabel.type, phoneLabel.label, phoneTextField.Text);
                }
                contact.Update ();
                NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
                contact = McContact.QueryById<McContact> (contact.Id); // Re-read to get fields set by BE
                Util.PerformAction ("tel", phoneTextField.Text);
            }
            DismissViewController (true, null);
        }

        private void SaveAndCompose (object sender, EventArgs e)
        {
            UITextField emailTextField = (UITextField)View.ViewWithTag (EMAIL_TEXTFIELD_TAG);
            if (EmailHelper.IsValidEmail (emailTextField.Text)) {
                if (isDefaultSelected) {
                    contact.AddDefaultEmailAddressAttribute (contact.AccountId, Xml.Contacts.Email1Address, ContactsHelper.ExchangeNameToLabel (Xml.Contacts.Email1Address), emailTextField.Text);
                } else {
                    contact.AddEmailAddressAttribute (contact.AccountId, Xml.Contacts.Email1Address, ContactsHelper.ExchangeNameToLabel (Xml.Contacts.Email1Address), emailTextField.Text);
                }
                contact.Update ();
                NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
                contact = McContact.QueryById<McContact> (contact.Id); // Re-read to get fields set by BE
                DismissViewController (true, null);
                owner.ContactDefaultSelectorComposeMessage (emailTextField.Text);
            } else {
                emailTextField.TextColor = A.Color_NachoRed;
            }
        }

        private void ToggleDefault (object sender, EventArgs e)
        {
            View.EndEditing (true);
            isDefaultSelected = !isDefaultSelected;

            UIButton defaultToggleButton = (UIButton)sender;
            defaultToggleButton.Selected = !defaultToggleButton.Selected;
        }

        private void SetDefaultEmail ()
        {
            foreach (var em in contact.EmailAddresses) {
                if (em.Name == selectedEmailName) {
                    em.IsDefault = true;
                } else {
                    em.IsDefault = false;
                }
            }
            contact.Update ();
        }

        private void SetDefaultPhone ()
        {
            foreach (var p in contact.PhoneNumbers) {
                if (p.Name == phoneLabel.type) {
                    p.IsDefault = true;
                } else {
                    p.IsDefault = false;
                }
            }
            contact.Update ();
        }

        private void CallSelectedPhone (object sender, EventArgs e)
        {
            if (isDefaultSelected) {
                SetDefaultPhone ();
            }

            foreach (var p in contact.PhoneNumbers) {
                if (p.Name == phoneLabel.type) {
                    Util.PerformAction ("tel", p.Value);
                    DismissViewController (true, null);
                    return;
                }
            }
        }

        private void EmailSelectedAddress (object sender, EventArgs e)
        {
            if (isDefaultSelected) {
                SetDefaultEmail ();
            }

            foreach (var em in contact.EmailAddresses) {
                if (em.Name == selectedEmailName) {
                    owner.ContactDefaultSelectorComposeMessage (em.Value);
                    DismissViewController (true, null);
                    return;
                }
            }
        }

        public void PrepareForDismissal (string selectedName)
        {
            UILabel phoneLabelLabel = (UILabel)View.ViewWithTag (PHONE_LABEL_TAG);
            phoneLabelLabel.Text = ContactsHelper.ExchangeNameToLabel (selectedName);

            UITextField phoneTextField = (UITextField)View.ViewWithTag (PHONE_TEXTFIELD_TAG);
            phoneTextField.Text = newPhoneString;
        }

        protected override void OnKeyboardChanged ()
        {
            LayoutView ();
        }

    }
}
