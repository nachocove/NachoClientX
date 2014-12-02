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
using System.Linq;

namespace NachoClient.iOS
{
    public partial class ContactDefaultSelectionViewController : NcUIViewControllerNoLeaks, INachoLabelChooserParent
    {
        public ContactListViewController owner;

        protected RegexUtilities regexUtil = new RegexUtilities ();
        protected const float X_INDENT = 30;

        protected const int ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG = 10;
        protected const int ADD_NEW_EMAIL_BUTTON_TAG = 11;
        protected const int ADD_NEW_DEFAULT_PHONE_BUTTON_TAG = 12;
        protected const int ADD_NEW_PHONE_BUTTON_TAG = 13;

        protected const int SET_DEFAULT_EMAIL_BUTTON_TAG = 14;
        protected const int COMPOSE_EMAIL_BUTTON_TAG = 15;
        protected const int SET_DEFAULT_PHONE_BUTTON_TAG = 16;
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
        protected int selectedPhoneButtonTag = PHONE_SELECTION_STARTING_BUTTON_TAG;

        protected const int EMAIL_SELECTION_STARTING_BUTTON_TAG = 2000;
        protected int selectedEmailButtonTag = EMAIL_SELECTION_STARTING_BUTTON_TAG;

        //FIXME don't use those anymore
        protected List<LabelSelectionViewController.ExchangeLabel> possiblePhones = new List<LabelSelectionViewController.ExchangeLabel> ();
        public LabelSelectionViewController.ExchangeLabel phoneLabel = new LabelSelectionViewController.ExchangeLabel (Xml.Contacts.MobilePhoneNumber, "Mobile");

        protected List<string> contactEmailList = new List<string> ();
        protected string selectedEmailType;

        UIBarButtonItem dismissButton;
        UINavigationItem navItem;

        protected McContact contact;

        public enum DefaultSelectionType
        {
            EmailAdder,
            PhoneNumberAdder,
            DefaultEmailSelector,
            DefaultPhoneSelector,
        }

        public DefaultSelectionType viewType;

        public ContactDefaultSelectionViewController (IntPtr handle) : base (handle)
        {

        }

        public override void ViewWillDisappear (bool animated)
        {
            View.EndEditing (true);
            base.ViewWillDisappear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoGreen;

            var navBar = new UINavigationBar (new RectangleF (0, 20, View.Frame.Width, 44));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            navItem = new UINavigationItem ();
            using (var image = UIImage.FromBundle ("modal-close")) {
                dismissButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                dismissButton.Clicked += DismissViewTouchUpInside;
                navItem.LeftBarButtonItem = dismissButton;
            }
            navBar.Items = new UINavigationItem[] { navItem };

            View.AddSubview (navBar);
            float yOffset = 64;
            float topOffset = 64;

            Util.AddHorizontalLine (0, yOffset, View.Frame.Width, UIColor.LightGray, View);
            yOffset += 15;

//////////////ADD EMAIL VIEW
//////////////ADD EMAIL VIEW
//////////////ADD EMAIL VIEW
//////////////ADD EMAIL VIEW
            UIView addEmailView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            addEmailView.BackgroundColor = A.Color_NachoGreen;
            addEmailView.Tag = ADD_EMAIL_VIEW_TAG;

            UILabel noEmailLabel = new UILabel (new RectangleF (X_INDENT, 0, View.Frame.Width - (X_INDENT * 2), 60));
            noEmailLabel.Text = "The contact you selected does not have an email address. Please add an email address for this contact.";
            noEmailLabel.Font = A.Font_AvenirNextRegular12;
            noEmailLabel.TextColor = UIColor.White;
            noEmailLabel.Lines = 3;
            noEmailLabel.TextAlignment = UITextAlignment.Left;
            noEmailLabel.LineBreakMode = UILineBreakMode.WordWrap;
            addEmailView.AddSubview (noEmailLabel);

            UIView emailBox = new UIView (new RectangleF (X_INDENT, noEmailLabel.Frame.Bottom + 15, View.Frame.Width - (X_INDENT * 2), 35));
            emailBox.BackgroundColor = UIColor.White;
            emailBox.Layer.CornerRadius = 4.0f;
            emailBox.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            emailBox.Layer.BorderWidth = 1.0f;

            var emailField = new UITextField (new RectangleF (30, 0, emailBox.Frame.Width - 35, emailBox.Frame.Height));
            emailField.BackgroundColor = UIColor.White;
            emailField.Placeholder = "Email Address";
            emailField.Font = A.Font_AvenirNextRegular14;
            emailField.BorderStyle = UITextBorderStyle.None;
            emailField.TextAlignment = UITextAlignment.Left;
            emailField.KeyboardType = UIKeyboardType.EmailAddress;
            emailField.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailField.AutocorrectionType = UITextAutocorrectionType.No;
            emailField.Tag = EMAIL_TEXTFIELD_TAG;
            emailBox.AddSubview (emailField);

            UIImageView mailImage = new UIImageView ();
            using (var loginImageTwo = UIImage.FromBundle ("Loginscreen-2")) {
                mailImage.Image = loginImageTwo;
            }
            mailImage.Frame = new RectangleF (8, 10, 16, 11);
            emailBox.AddSubview (mailImage);
            addEmailView.AddSubview (emailBox);

            var addDefaultButton = AddButton (emailBox.Frame.Left, emailBox.Frame.Bottom + 30, emailBox.Frame.Width / 2 - 10, "contacts-default", "Set As Default", AddDefaultEmailAndClose, addEmailView, ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG);
            AddButton (addDefaultButton.Frame.Right + 20, emailBox.Frame.Bottom + 30, emailBox.Frame.Width / 2 - 10, "now-newemail", "Compose Email", SaveAndCompose, addEmailView, ADD_NEW_EMAIL_BUTTON_TAG);

            addEmailView.Hidden = true;
            View.AddSubview (addEmailView);

//////////////ADD PHONE VIEW
//////////////ADD PHONE VIEW
//////////////ADD PHONE VIEW
//////////////ADD PHONE VIEW
            yOffset = topOffset + 16 + 15;

            UIView addPhoneView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            addPhoneView.BackgroundColor = A.Color_NachoGreen;
            addPhoneView.Tag = PHONE_ADD_VIEW_TAG;

            UILabel noPhoneLabel = new UILabel (new RectangleF (X_INDENT, 0, View.Frame.Width - (X_INDENT * 2), 60));
            noPhoneLabel.Text = "The contact you selected does not have a phone number. Please add a phone number for this contact.";
            noPhoneLabel.Font = A.Font_AvenirNextRegular12;
            noPhoneLabel.TextColor = UIColor.White;
            noPhoneLabel.Lines = 3;
            noPhoneLabel.TextAlignment = UITextAlignment.Left;
            noPhoneLabel.LineBreakMode = UILineBreakMode.WordWrap;
            addPhoneView.AddSubview (noPhoneLabel);

            UIButton selectLabelButton = new UIButton (new RectangleF (X_INDENT, noPhoneLabel.Frame.Bottom + 15, 90, 35));
            selectLabelButton.Tag = SELECT_PHONELABEL_BUTTON_TAG;
            selectLabelButton.TouchUpInside += SelectLabelTouchUpInside;
            selectLabelButton.BackgroundColor = UIColor.White;
            selectLabelButton.Layer.CornerRadius = 4.0f;
            selectLabelButton.Layer.BorderColor = A.Card_Border_Color;
            selectLabelButton.Layer.BorderWidth = 1.0f;
            addPhoneView.AddSubview (selectLabelButton);

            UILabel selectLabelLabel = new UILabel (new RectangleF (5, 7, 60, 20));
            selectLabelLabel.Font = A.Font_AvenirNextRegular12;
            selectLabelLabel.TextColor = A.Color_NachoGreen;
            selectLabelLabel.TextAlignment = UITextAlignment.Center;
            selectLabelLabel.Text = phoneLabel.label;
            selectLabelLabel.Tag = PHONE_LABEL_TAG;
            selectLabelButton.AddSubview (selectLabelLabel);

            UIImageView selectLabelButtonImage = new UIImageView (UIImage.FromBundle ("email-att-download-arrow"));
            selectLabelButtonImage.Frame = new RectangleF (68, 7, 20, 16);
            selectLabelButton.AddSubview (selectLabelButtonImage);

            UIView phoneInputBox = new UIView (new RectangleF (125, noPhoneLabel.Frame.Bottom + 15, View.Frame.Width - (125 + X_INDENT), 35));
            phoneInputBox.BackgroundColor = UIColor.White;
            phoneInputBox.Layer.CornerRadius = 4.0f;
            phoneInputBox.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            phoneInputBox.Layer.BorderWidth = 1.0f;

            var phoneNumberField = new UITextField (new RectangleF (27, 0, phoneInputBox.Frame.Width - 30, phoneInputBox.Frame.Height));
            phoneNumberField.BackgroundColor = UIColor.White;
            phoneNumberField.Placeholder = "Phone Number";
            phoneNumberField.Font = A.Font_AvenirNextRegular14;
            phoneNumberField.BorderStyle = UITextBorderStyle.None;
            phoneNumberField.TextAlignment = UITextAlignment.Left;
            phoneNumberField.KeyboardType = UIKeyboardType.PhonePad;
            phoneNumberField.AutocapitalizationType = UITextAutocapitalizationType.None;
            phoneNumberField.AutocorrectionType = UITextAutocorrectionType.No;
            phoneNumberField.Tag = PHONE_TEXTFIELD_TAG;
            phoneInputBox.AddSubview (phoneNumberField);

            UIImageView phoneImage = new UIImageView ();
            using (var loginImageTwo = UIImage.FromBundle ("contacts-call-active")) {
                phoneImage.Image = loginImageTwo;
            }
            phoneImage.Frame = new RectangleF (8, 11, 11, 11);
            phoneInputBox.AddSubview (phoneImage);
            addPhoneView.AddSubview (phoneInputBox);
            addPhoneView.Hidden = true;
           
            UIButton addNewDefaultPhoneButton = AddButton (emailBox.Frame.Left, phoneInputBox.Frame.Bottom + 30, emailBox.Frame.Width / 2 - 10, "contacts-default", "Set As Default", AddDefaultPhoneAndClose, addPhoneView, ADD_NEW_DEFAULT_PHONE_BUTTON_TAG);
            AddButton (addNewDefaultPhoneButton.Frame.Right + 20, phoneInputBox.Frame.Bottom + 30, emailBox.Frame.Width / 2 - 10, "contacts-call-swipe", "Call Contact", SaveAndCall, addPhoneView, ADD_NEW_PHONE_BUTTON_TAG);

            View.AddSubview (addPhoneView);

////////////MULTI PHONE SELECTOR VIEW
////////////MULTI PHONE SELECTOR VIEW
////////////MULTI PHONE SELECTOR VIEW
////////////MULTI PHONE SELECTOR VIEW
            yOffset = topOffset + 17;

            UIView selectPhoneView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            selectPhoneView.BackgroundColor = A.Color_NachoGreen;
            selectPhoneView.Tag = SELECT_PHONE_VIEW_TAG;
            selectPhoneView.Hidden = true;
            View.AddSubview (selectPhoneView);

            int i = 0;
            float internalYOffset = 0;
            foreach (var p in contact.PhoneNumbers) {
                LabelSelectionViewController.ListSelectionButton selectionButton = new LabelSelectionViewController.ListSelectionButton (p.Label, PHONE_SELECTION_STARTING_BUTTON_TAG + i);
                UIButton button = selectionButton.GetButton (View, internalYOffset);
                button.TouchUpInside += SelectionButtonClicked;
                possiblePhones.Add (new LabelSelectionViewController.ExchangeLabel (p.Name, p.Label));
                selectPhoneView.AddSubview (button);
                if (0 == i) {
                    if (viewType == DefaultSelectionType.DefaultPhoneSelector) {
                        button.SendActionForControlEvents (UIControlEvent.TouchUpInside);
                    }
                }
                internalYOffset += 58;
                Util.AddHorizontalLine (80, internalYOffset, View.Frame.Width - 80, UIColor.LightGray, selectPhoneView);
                internalYOffset += 1;
                i++;
            }

            internalYOffset += 30;

            var setDefaultPhoneButton = AddButton (emailBox.Frame.Left, internalYOffset, emailBox.Frame.Width / 2 - 10, "contacts-default", "Set As Default", SetDefaultPhoneAndClose, selectPhoneView, SET_DEFAULT_PHONE_BUTTON_TAG);
            AddButton (setDefaultPhoneButton.Frame.Right + 20, setDefaultPhoneButton.Frame.Y, emailBox.Frame.Width / 2 - 10, "contacts-call-swipe", "Call Contact", CallSelectedPhone, selectPhoneView, CALL_CONTACT_BUTTON_TAG);


////////////MULTI EMAIL SELECTOR VIEW
////////////MULTI EMAIL SELECTOR VIEW
////////////MULTI EMAIL SELECTOR VIEW
////////////MULTI EMAIL SELECTOR VIEW
            yOffset = topOffset + 17;
            UIView selectEmailView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset));
            selectEmailView.BackgroundColor = A.Color_NachoGreen;
            selectEmailView.Tag = SELECT_EMAIL_VIEW_TAG;
            selectEmailView.Hidden = true;
            View.AddSubview (selectEmailView);

            i = 0;
            internalYOffset = 0;
            foreach (var e in contact.EmailAddresses) {
                string canonicalEmailAddress = McEmailAddress.QueryById<McEmailAddress> (e.EmailAddress).CanonicalEmailAddress;
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
                internalYOffset += 58;
                Util.AddHorizontalLine (80, internalYOffset, View.Frame.Width - 80, UIColor.LightGray, selectEmailView);
                internalYOffset += 1;
                i++;
            }

            internalYOffset += 30;

            var setDefaultEmailButton = AddButton (emailBox.Frame.Left, internalYOffset, emailBox.Frame.Width / 2 - 10, "contacts-default", "Set As Default", SetDefaultEmailAndClose, selectEmailView, SET_DEFAULT_EMAIL_BUTTON_TAG);
            AddButton (setDefaultEmailButton.Frame.Right + 20, setDefaultEmailButton.Frame.Y, emailBox.Frame.Width / 2 - 10, "now-newemail", "Compose Email", EmailSelectedAddress, selectEmailView, COMPOSE_EMAIL_BUTTON_TAG);
        }

        protected UIButton AddButton (float frameX, float frameY, float width, string image, string label, EventHandler buttonClicked, UIView parent, int tag)
        {
            UIButton newButton = new UIButton (new RectangleF (frameX, frameY, width, 60));
            newButton.Tag = tag;
            newButton.TouchUpInside += buttonClicked;
            newButton.Layer.BorderColor = A.Color_NachoBlue.CGColor;
            newButton.Layer.BorderWidth = A.Card_Border_Width;
            newButton.Layer.CornerRadius = 4f;
            var newButtonImageView = new UIImageView (UIImage.FromBundle (image));
            newButtonImageView.SizeToFit ();
            ViewFramer.Create (newButtonImageView).X (newButton.Frame.Width / 2 - newButtonImageView.Frame.Width / 2).Y (8);
            newButton.AddSubview (newButtonImageView);

            UILabel newButtonLabel = new UILabel (new RectangleF (10, newButtonImageView.Frame.Bottom + 5, newButton.Frame.Width - 20, 15));
            newButtonLabel.Font = A.Font_AvenirNextMedium12;
            newButtonLabel.LineBreakMode = UILineBreakMode.WordWrap;
            newButtonLabel.Text = label;
            newButtonLabel.TextColor = UIColor.White;
            newButtonLabel.TextAlignment = UITextAlignment.Center;
            newButton.AddSubview (newButtonLabel);
            parent.AddSubview (newButton);

            return newButton;
        }

        protected void SelectionButtonClicked (object sender, EventArgs e)
        {
            int selectedButtonTag = 0;
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
                phoneLabel = possiblePhones [selectedPhoneButtonTag - PHONE_SELECTION_STARTING_BUTTON_TAG];
                break;
            case DefaultSelectionType.DefaultEmailSelector:
                selectedEmailButtonTag = selectedButton.Tag;
                selectedEmailType = contactEmailList [selectedEmailButtonTag - EMAIL_SELECTION_STARTING_BUTTON_TAG];
                break;
            }
        }

        protected override void Cleanup ()
        {
            dismissButton.Clicked -= DismissViewTouchUpInside;
            dismissButton = null;

            UIButton selectLabelButton = (UIButton)View.ViewWithTag (SELECT_PHONELABEL_BUTTON_TAG);
            selectLabelButton.TouchUpInside -= SelectLabelTouchUpInside;
            selectLabelButton = null;

            UIButton addDefaultEmailButton = (UIButton)View.ViewWithTag (ADD_NEW_DEFAULT_EMAIL_BUTTON_TAG);
            UIButton addEmailButton = (UIButton)View.ViewWithTag (ADD_NEW_EMAIL_BUTTON_TAG);
            UIButton addDefaultPhoneButton = (UIButton)View.ViewWithTag (ADD_NEW_DEFAULT_PHONE_BUTTON_TAG);
            UIButton addPhoneButton = (UIButton)View.ViewWithTag (ADD_NEW_PHONE_BUTTON_TAG);
            UIButton setDefaultPhoneButton = (UIButton)View.ViewWithTag (SET_DEFAULT_PHONE_BUTTON_TAG);
            UIButton callPhoneButton = (UIButton)View.ViewWithTag (CALL_CONTACT_BUTTON_TAG);
            UIButton setDefaultEmailButton = (UIButton)View.ViewWithTag (SET_DEFAULT_EMAIL_BUTTON_TAG);
            UIButton composeEmailButton = (UIButton)View.ViewWithTag (COMPOSE_EMAIL_BUTTON_TAG);

            addDefaultEmailButton.TouchUpInside -= AddDefaultEmailAndClose;
            addEmailButton.TouchUpInside -= SaveAndCompose;
            addDefaultPhoneButton.TouchUpInside -= AddDefaultPhoneAndClose;
            addPhoneButton.TouchUpInside -= SaveAndCall;
            setDefaultPhoneButton.TouchUpInside -= SetDefaultPhoneAndClose;
            callPhoneButton.TouchUpInside -= CallSelectedPhone;
            setDefaultEmailButton.TouchUpInside -= SetDefaultEmailAndClose;
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
                navItem.Title = "Add New Email";
                UIView emailAddView = (UIView)View.ViewWithTag (ADD_EMAIL_VIEW_TAG);
                emailAddView.Hidden = false;
                break;
            case DefaultSelectionType.PhoneNumberAdder:
                navItem.Title = "Add New Number";
                UIView phoneAddView = (UIView)View.ViewWithTag (PHONE_ADD_VIEW_TAG);
                phoneAddView.Hidden = false;
                break;
            case DefaultSelectionType.DefaultPhoneSelector:
                navItem.Title = "Select a Phone";
                UIView phoneSelectView = (UIView)View.ViewWithTag (SELECT_PHONE_VIEW_TAG);
                phoneSelectView.Hidden = false;
                break;
            case DefaultSelectionType.DefaultEmailSelector:
                navItem.Title = "Select an Email";
                UIView emailSelectView = (UIView)View.ViewWithTag (SELECT_EMAIL_VIEW_TAG);
                emailSelectView.Hidden = false;
                break;
            }
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
            PerformSegue ("SegueToLabelSelection", this);
        }

        private void SaveAndCall (object sender, EventArgs e)
        {
            UITextField phoneTextField = (UITextField)View.ViewWithTag (PHONE_TEXTFIELD_TAG);
            contact.AddPhoneNumberAttribute (LoginHelpers.GetCurrentAccountId (), phoneLabel.type, phoneLabel.label, phoneTextField.Text);
            contact.Update ();
            Util.PerformAction ("tel", phoneTextField.Text);
            DismissViewController (true, null);
        }

        private void SaveAndCompose (object sender, EventArgs e)
        {
            UITextField emailTextField = (UITextField)View.ViewWithTag (EMAIL_TEXTFIELD_TAG);
            if (regexUtil.IsValidEmail (emailTextField.Text)) {
                contact.AddEmailAddressAttribute (LoginHelpers.GetCurrentAccountId (), Xml.Contacts.Email1Address, null, emailTextField.Text);
                contact.Update ();
                DismissViewController (true, null);
                owner.PerformSegue ("ContactsToMessageCompose", new SegueHolder (emailTextField.Text));
            } else {
                emailTextField.TextColor = A.Color_NachoRed;
            }
        }

        private void AddDefaultEmailAndClose (object sender, EventArgs e)
        {
            UITextField emailTextField = (UITextField)View.ViewWithTag (EMAIL_TEXTFIELD_TAG);
            if (regexUtil.IsValidEmail (emailTextField.Text)) {
                contact.AddDefaultEmailAddressAttribute (LoginHelpers.GetCurrentAccountId (), Xml.Contacts.Email1Address, null, emailTextField.Text);
                contact.Update ();
                DismissViewController (true, null);
            } else {
                emailTextField.TextColor = A.Color_NachoRed;
            }
        }

        private void AddDefaultPhoneAndClose (object sender, EventArgs e)
        {
            UITextField phoneTextField = (UITextField)View.ViewWithTag (PHONE_TEXTFIELD_TAG);
            contact.AddDefaultPhoneNumberAttribute (LoginHelpers.GetCurrentAccountId (), phoneLabel.type, phoneLabel.label, phoneTextField.Text);
            contact.Update ();
            DismissViewController (true, null);
        }

        private void SetDefaultEmailAndClose (object sender, EventArgs e)
        {
            foreach (var em in contact.EmailAddresses) {
                if (em.Name == selectedEmailType) {
                    em.IsDefault = true;
                } else {
                    em.IsDefault = false;
                }
            }
            contact.Update ();
            DismissViewController (true, null);
        }

        private void SetDefaultPhoneAndClose (object sender, EventArgs e)
        {
            foreach (var p in contact.PhoneNumbers) {
                if (p.Name == phoneLabel.type) {
                    p.IsDefault = true;
                } else {
                    p.IsDefault = false;
                }
            }
            contact.Update ();
            DismissViewController (true, null);
        }

        private void CallSelectedPhone (object sender, EventArgs e)
        {
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
            foreach (var em in contact.EmailAddresses) {
                if (em.Name == selectedEmailType) {
                    owner.PerformSegue ("ContactsToMessageCompose", new SegueHolder (em.Value));
                    DismissViewController (true, null);
                    return;
                }
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToLabelSelection")) {
                LabelSelectionViewController destinationController = (LabelSelectionViewController)segue.DestinationViewController;
                ContactsHelper c = new ContactsHelper ();
                destinationController.SetLabelList(c.GetAvailablePhoneNames(contact));
                destinationController.SetSelectedName(c.GetAvailablePhoneNames (contact).First ());
                destinationController.SetOwner (this);
                return;
            }
        }

        public void PrepareForDismissal (string selectedName)
        {
            SetPhoneLabel ();
        }

        //TODO: Add interface for label selector, this method will override interface method
        public void SetPhoneLabel ()
        {
            UILabel phoneLabelLabel = (UILabel)View.ViewWithTag (PHONE_LABEL_TAG);
            phoneLabelLabel.Text = phoneLabel.label;
        }
    }
}
