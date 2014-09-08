// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;
using System.Text.RegularExpressions;

namespace NachoClient.iOS
{
    public partial class ContactDetailViewController : NcUIViewController, IMessageTableViewSourceDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, INachoNotesControllerParent
    {
        public McContact contact;

        protected float startingY;

        protected UIColor originalBarTintColor;

        protected UIBarButtonItem vipButton;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";
        MessageTableViewSource messageSource;
        protected HashSet<int> MultiSelect = null;
        protected UITableView InteractionsTable;
        protected UIScrollView notesScrollView;
        protected UIView notesView;
        protected UITextView notesTextView;
        protected UIButton editNotes;

        public ContactDetailViewController (IntPtr handle) : base (handle)
        {
            messageSource = new MessageTableViewSource ();
            MultiSelect = new HashSet<int> ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

//            Beta 1 -- No editing
//            var editButton = new UIBarButtonItem (UIBarButtonSystemItem.Edit);
//            NavigationItem.RightBarButtonItem = editButton;
//
//            editButton.Clicked += (object sender, EventArgs e) => {
//                PerformSegue ("ContactToContactEdit", new SegueHolder (contact));
//            };

            vipButton = new UIBarButtonItem ();
            NavigationItem.RightBarButtonItem = vipButton;

            vipButton.Clicked += (object sender, EventArgs e) => {
                ToggleVipStatus ();
            };

            messageSource.owner = this;
            InteractionsTable = new UITableView ();
            InteractionsTable.Source = messageSource;
            MultiSelectToggle (messageSource, false);
            SetEmailMessages (new UserInteractionEmailMessages (contact));

            CreateView ();

        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                originalBarTintColor = this.NavigationController.NavigationBar.BarTintColor;

            }
            ConfigureView ();
            UpdateVipButton ();
            Util.ConfigureNavBar (true, NavigationController);
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.NavigationBar.BarTintColor = originalBarTintColor;

            }
            Util.ConfigureNavBar (false, NavigationController);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactToNotes")) {
                var dc = (NotesViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                dc.SetOwner (this);
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    DisplayContactInfo ();
                };
                return;
            }

            if (segue.Identifier.Equals ("ContactToEmailCompose")) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = holder.value as string;
                dc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, address));
                return;
            }
            if (segue.Identifier.Equals ("ContactToContactEdit")) {
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, h);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var vc = (MessageViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;                
                return;
            }
            if (segue.Identifier == "NachoNowToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var c = holder.value as McCalendar;
                if (null == c) {
                    vc.SetCalendarItem (null, CalendarItemEditorAction.create);
                } else {
                    vc.SetCalendarItem (c, CalendarItemEditorAction.create);
                }
                vc.SetOwner (this);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        const int TOP_IMAGE_TAG = 101;
        const int TOP_EMAIL_BUTTON_TAG = 102;
        const int TOP_CALL_BUTTON_TAG = 103;
        const int TOP_USER_IMAGE_TAG = 104;
        const int TOP_USER_LABEL_TAG = 105;
        const int TOP_USER_NAME_TAG = 106;
        const int TOP_USER_TITLE_TAG = 107;
        const int SEGMENTED_CONTROL_TAG = 108;
        const int SEGMENTED_CONTROL_HL_TAG = 109;
        const int TRANSIENT_TAG = 300;
        const int NOTES_TEXT_VIEW_TAG = 400;
        const int EDIT_NOTES_BUTTON_TAG = 500;

        const int FOO_SIZE = 64;
        const float TEXT_LINE_HEIGHT = 19.124f;

        protected void CreateView ()
        {
            var topImage = new UIView (new RectangleF (0, -FOO_SIZE, View.Frame.Width, 216));
            topImage.Tag = TOP_IMAGE_TAG;
            contentView.AddSubview (topImage);

            var topEmailButton = UIButton.FromType (UIButtonType.RoundedRect);
            topEmailButton.Frame = new RectangleF (0, 0, 40, 40);
            topEmailButton.Center = new PointF (62, (216 / 2));
            topEmailButton.Layer.CornerRadius = 20;
            topEmailButton.Layer.MasksToBounds = true;
            topEmailButton.TintColor = UIColor.White;
            topEmailButton.SetImage (UIImage.FromBundle ("icn-contact-quickemail"), UIControlState.Normal);
            topEmailButton.TouchUpInside += (sender, e) => {
                TouchedEmailButton (contact.GetEmailAddress ());
            };
            topEmailButton.Tag = TOP_EMAIL_BUTTON_TAG;
            topImage.AddSubview (topEmailButton);

            var topCallButton = UIButton.FromType (UIButtonType.RoundedRect);
            topCallButton.Frame = new RectangleF (0, 0, 40, 40);
            topCallButton.Center = new PointF (View.Frame.Width - 62, (216 / 2));
            topCallButton.Layer.CornerRadius = 20;
            topCallButton.Layer.MasksToBounds = true;
            topCallButton.TintColor = UIColor.White;
            topCallButton.SetImage (UIImage.FromBundle ("icn-contact-quickcall"), UIControlState.Normal);
            topCallButton.TouchUpInside += (sender, e) => {
                TouchedCallButton (contact.GetPhoneNumber ());
            };
            topCallButton.Tag = TOP_CALL_BUTTON_TAG;
            topImage.AddSubview (topCallButton);

            var topUserLabel = new UILabel (new RectangleF (0, 0, 72, 72));
            topUserLabel.Center = new PointF (View.Frame.Width / 2, (216 / 2));
            topUserLabel.Font = A.Font_AvenirNextRegular24;
            topUserLabel.TextColor = UIColor.White;
            topUserLabel.TextAlignment = UITextAlignment.Center;
            topUserLabel.LineBreakMode = UILineBreakMode.Clip;
            topUserLabel.Layer.CornerRadius = 36;
            topUserLabel.Layer.MasksToBounds = true;
            topUserLabel.Layer.BorderColor = UIColor.White.CGColor;
            topUserLabel.Layer.BorderWidth = 2;
            topUserLabel.Tag = TOP_USER_LABEL_TAG;
            topImage.AddSubview (topUserLabel);

            var topUserNameLabel = new UILabel (new RectangleF (0, 0, View.Frame.Width, 24));
            topUserNameLabel.Center = new PointF (View.Frame.Width / 2, topUserLabel.Center.Y + 60);
            topUserNameLabel.ContentMode = UIViewContentMode.Center;
            topUserNameLabel.Font = A.Font_AvenirNextDemiBold17;
            topUserNameLabel.TextColor = UIColor.White;
            topUserNameLabel.TextAlignment = UITextAlignment.Center;
            topUserNameLabel.LineBreakMode = UILineBreakMode.Clip;
            topUserNameLabel.BackgroundColor = UIColor.Clear;
            topUserNameLabel.Tag = TOP_USER_NAME_TAG;
            topImage.AddSubview (topUserNameLabel);

            var topUserTitleLabel = new UILabel (new RectangleF (0, 0, View.Frame.Width, 24));
            topUserTitleLabel.Center = new PointF (View.Frame.Width / 2, topUserNameLabel.Center.Y + 24);
            topUserTitleLabel.ContentMode = UIViewContentMode.Center;
            topUserTitleLabel.Font = A.Font_AvenirNextDemiBold14;
            topUserTitleLabel.TextColor = UIColor.White;
            topUserTitleLabel.TextAlignment = UITextAlignment.Center;
            topUserTitleLabel.LineBreakMode = UILineBreakMode.Clip;
            topUserTitleLabel.BackgroundColor = UIColor.Clear;
            topUserTitleLabel.Tag = TOP_USER_TITLE_TAG;
            topImage.AddSubview (topUserTitleLabel);

            float yOffset = 216 - FOO_SIZE;
         
            yOffset += 6;

            var segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new RectangleF (6, yOffset, View.Frame.Width - 12, 30);
            segmentedControl.TintColor = A.Color_NachoIconGray;
            segmentedControl.InsertSegment ("Contact Info", 0, false);
            segmentedControl.InsertSegment ("Interactions", 1, false);
            segmentedControl.InsertSegment ("Notes", 2, false);
            segmentedControl.SelectedSegment = 0;

            var segmentedControlTextAttributes = new UITextAttributes ();
            segmentedControlTextAttributes.Font = A.Font_AvenirNextRegular14;
            segmentedControlTextAttributes.TextColor = A.Color_NachoIconGray;
            segmentedControl.SetTitleTextAttributes (segmentedControlTextAttributes, UIControlState.Normal);

            segmentedControl.ValueChanged += (sender, e) => {
                var selectedSegmentId = (sender as UISegmentedControl).SelectedSegment;
                switch (selectedSegmentId) {
                case 0:
                    InteractionsTable.Hidden = true;
                    notesScrollView.Hidden = true;
                    contentView.Hidden = false;
                    editNotes.Hidden = true;
                    break;
                case 1:
                    InteractionsTable.Hidden = false;
                    notesScrollView.Hidden = true;
                    editNotes.Hidden = true;
                    RefreshData ();
                    break;
                case 2:
                    InteractionsTable.Hidden = true;
                    notesScrollView.Hidden = false;
                    editNotes.Hidden = false;
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
            };

            segmentedControl.Tag = SEGMENTED_CONTROL_TAG;
            contentView.AddSubview (segmentedControl);

            yOffset += 30;
            yOffset += 6;

            // Segmented Control
            var segmentedControlHL = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 1));
            segmentedControlHL.BackgroundColor = A.Color_NachoIconGray;
            segmentedControlHL.Tag = SEGMENTED_CONTROL_HL_TAG;
            contentView.AddSubview (segmentedControlHL);

            // Interactions Table
            InteractionsTable.Frame = new RectangleF (0, yOffset + 6 + 60, View.Frame.Width, 300);
            InteractionsTable.Hidden = true;
            InteractionsTable.BackgroundColor = UIColor.White;
            View.AddSubview (InteractionsTable);

            // Notes
            notesScrollView = new UIScrollView (new RectangleF (0, yOffset + 66, View.Frame.Width, 300));
            notesView = new UIView (new RectangleF (0, 0, View.Frame.Width, 309 - 40));
            notesView.BackgroundColor = UIColor.White;
            notesTextView = new UITextView (new RectangleF (15, 0, View.Frame.Width - 30, 309 - 40));
            notesTextView.Font = A.Font_AvenirNextRegular14;
            notesTextView.Editable = false;
            notesTextView.TextColor = A.Color_NachoBlack;
            notesTextView.BackgroundColor = UIColor.White;
            notesTextView.Tag = NOTES_TEXT_VIEW_TAG;

            // Notes Scroll View
            notesView.Add (notesTextView);
            notesScrollView.Add (notesView);
            notesScrollView.Frame = new RectangleF (0, yOffset + 65, View.Frame.Width, 309 - 40);
            notesScrollView.Hidden = true;

            View.AddSubview (notesScrollView);

            editNotes = new UIButton (UIButtonType.RoundedRect);
            editNotes.Tag = EDIT_NOTES_BUTTON_TAG;
            editNotes.TintColor = A.Color_NachoBlue;
            editNotes.BackgroundColor = A.Color_NachoNowBackground;
            editNotes.Font = A.Font_AvenirNextMedium14;
            editNotes.SizeToFit ();
            editNotes.Hidden = true;
            editNotes.Frame = new RectangleF (0, View.Frame.Height - editNotes.Frame.Height, View.Frame.Width, 40);
            editNotes.TouchUpInside += (sender, e) => {
                PerformSegue ("ContactToNotes", new SegueHolder (contact));
            };
            View.Add (editNotes);

            yOffset += 6;

            startingY = yOffset;
        }

        protected void ConfigureView ()
        {
            UIColor userBackgroundColor;
            InteractionsTable.Hidden = true;
            notesScrollView.Hidden = true;
            contentView.Hidden = false;
            editNotes.Hidden = true;

            if (0 == contact.EmailAddresses.Count) {
                userBackgroundColor = Util.ColorForUser (Util.PickRandomColorForUser ());
            } else {
                var emailAddressAttribute = contact.EmailAddresses [0];
                var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
                userBackgroundColor = Util.ColorForUser (emailAddress.ColorIndex);
            }

            var topImage = contentView.ViewWithTag (TOP_IMAGE_TAG) as UIView;
            topImage.BackgroundColor = LighterColor (userBackgroundColor);

            var topEmailButton = contentView.ViewWithTag (TOP_EMAIL_BUTTON_TAG) as UIButton;
            topEmailButton.BackgroundColor = userBackgroundColor;

            var topCallButton = contentView.ViewWithTag (TOP_CALL_BUTTON_TAG) as UIButton;
            topCallButton.BackgroundColor = userBackgroundColor;

            var topUserLabel = contentView.ViewWithTag (TOP_USER_LABEL_TAG) as UILabel;
            topUserLabel.Text = GetInitials ();

            var topUserName = contentView.ViewWithTag (TOP_USER_NAME_TAG) as UILabel;
            topUserName.Text = contact.GetDisplayNameOrEmailAddress ();

            var topUserTitle = contentView.ViewWithTag (TOP_USER_TITLE_TAG) as UILabel;
            topUserTitle.Text = GetTitleFromContact ();

            // Clean out old transient views
            for (;;) {
                UIView view;
                if (null == (view = contentView.ViewWithTag (TRANSIENT_TAG))) {
                    break;
                }
                view.RemoveFromSuperview ();
            }

            var yOffset = startingY;

            if (0 == contact.EmailAddresses.Count) {
                goto skippedEmail;
            }

//            yOffset += AddHeader ("Email", yOffset);

            foreach (var emailAddressAttributes in contact.EmailAddresses) {
                yOffset += AddEmailAddress (emailAddressAttributes, yOffset);
            }

            yOffset += 3;

            var emailSectionSeparator = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 1));
            emailSectionSeparator.BackgroundColor = A.Color_NachoBorderGray;
            emailSectionSeparator.Tag = TRANSIENT_TAG;
            ;
            contentView.AddSubview (emailSectionSeparator);

            yOffset += 6;

            skippedEmail:

            if (0 == contact.PhoneNumbers.Count) {
                goto skippedPhones;
            }

//            yOffset += AddHeader ("Phone", yOffset);

            foreach (var phoneNumberAttribute in contact.PhoneNumbers) {
                yOffset += AddPhoneNumber (phoneNumberAttribute, yOffset);
            }

            yOffset += 3;

            var phoneSectionSeparator = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 1));
            phoneSectionSeparator.BackgroundColor = A.Color_NachoBorderGray;
            phoneSectionSeparator.Tag = TRANSIENT_TAG;
            ;
            contentView.AddSubview (phoneSectionSeparator);

            skippedPhones:

            contentView.Frame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;

            // Notes
            var notesTextView = notesScrollView.ViewWithTag (NOTES_TEXT_VIEW_TAG) as UITextView;

            McBody contactBody = McBody.QueryById<McBody> (contact.BodyId);
            if (null != contactBody) {
                notesTextView.Text = contactBody.GetContentsString ();
            }

            if (contact.Source != McAbstrItem.ItemSource.ActiveSync) {
                notesTextView.Text = "This contact has not been synced. Adding or editing notes is disabled.";
            }

            notesScrollView.ContentSize = notesTextView.ContentSize;

            var editNotesButton = View.ViewWithTag (EDIT_NOTES_BUTTON_TAG) as UIButton;
            if ("" == notesTextView.Text) {
                editNotesButton.SetTitle ("Add Note", UIControlState.Normal);
                editNotesButton.SizeToFit ();
            } else {
                editNotesButton.SetTitle ("Edit Note", UIControlState.Normal);
                editNotesButton.SizeToFit ();
            }

            if (contact.Source != McAbstrItem.ItemSource.ActiveSync) {
                editNotesButton.Enabled = false;
            } else {
                editNotesButton.Enabled = true;
            }
            editNotes.Frame = new RectangleF (0, UIScreen.MainScreen.Bounds.Height - 40, View.Frame.Width, 40);
        }

        protected float AddHeader (string header, float yOffset)
        {
            var label = new UILabel (new RectangleF (15, yOffset, View.Frame.Width - 15, 20));
            label.Font = A.Font_AvenirNextDemiBold17;
            label.TextColor = A.Color_0B3239;
            label.Text = header;
            label.Tag = TRANSIENT_TAG;
            contentView.AddSubview (label);

            return 25;
        }

        protected float AddEmailAddress (McContactEmailAddressAttribute email, float yOffset)
        {
            var view = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 40));
            view.Tag = TRANSIENT_TAG;
            contentView.AddSubview (view);

            var emailAddress = McEmailAddress.QueryById<McEmailAddress> (email.EmailAddress);
            var labelLabel = new UILabel (new RectangleF (15, 0, 45, 20));
            labelLabel.Font = A.Font_AvenirNextRegular14;
            labelLabel.TextColor = A.Color_0B3239;
            labelLabel.Text = "Email Address";
            labelLabel.SizeToFit ();
            labelLabel.Tag = TRANSIENT_TAG;
            view.AddSubview (labelLabel);

            var imageView = new UIImageView (new RectangleF (View.Frame.Width - 24 - 15, 10, 24, 24));
            imageView.Image = UIImage.FromBundle ("contact-quickemail-gray");
            view.AddSubview (imageView);

            var tap = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                TouchedEmailButton (email.Value);
            });
            view.AddGestureRecognizer (tap);
            view.UserInteractionEnabled = true;

            var valueLabel = new UILabel (new RectangleF (35, 20, View.Frame.Width - 75, 20));
            valueLabel.Font = A.Font_AvenirNextRegular14;
            valueLabel.TextColor = A.Color_808080;
            valueLabel.Text = emailAddress.CanonicalEmailAddress;
            valueLabel.SizeToFit ();
            view.AddSubview (valueLabel);

            return 40;
        }

        protected float AddPhoneNumber (McContactStringAttribute phone, float yOffset)
        {
            var view = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 40));
            view.Tag = TRANSIENT_TAG;
            contentView.AddSubview (view);

            string phoneLabel = "";

            if (NachoCore.ActiveSync.Xml.Contacts.MobilePhoneNumber == phone.Name) {
                phoneLabel = "Mobile Phone Number";
            } else if (NachoCore.ActiveSync.Xml.Contacts.BusinessPhoneNumber == phone.Name) {
                phoneLabel = "Business Phone Number";
            } else if (NachoCore.ActiveSync.Xml.Contacts.HomePhoneNumber == phone.Name) {
                phoneLabel = "Home Phone Number";
            } else if (NachoCore.ActiveSync.Xml.Contacts.AssistantPhoneNumber == phone.Name) {
                phoneLabel = "Assistant Phone Number";
            } else {
                phoneLabel = "Phone Number";
            }

            var labelLabel = new UILabel (new RectangleF (15, 0, 45, 20));
            labelLabel.Font = A.Font_AvenirNextRegular14;
            labelLabel.TextColor = A.Color_0B3239;
            labelLabel.Text = phoneLabel;
            labelLabel.SizeToFit ();
            labelLabel.Tag = TRANSIENT_TAG;
            view.AddSubview (labelLabel);

            var phoneButton = UIButton.FromType (UIButtonType.RoundedRect);
            phoneButton.Frame = new RectangleF (View.Frame.Width - 24 - 15, 10, 24, 24);
            phoneButton.SetImage (UIImage.FromBundle ("icn-mtng-phone"), UIControlState.Normal);
            phoneButton.TouchUpInside += (sender, e) => {
                TouchedCallButton (phone.Value);
            };
            view.AddSubview (phoneButton);

            var smsButton = UIButton.FromType (UIButtonType.RoundedRect);
            smsButton.Frame = new RectangleF (View.Frame.Width - 24 - 15 - 24 - 15, 10, 24, 24);
            smsButton.SetImage (UIImage.FromBundle ("icn-sms"), UIControlState.Normal);
            smsButton.TouchUpInside += (sender, e) => {
                TouchedSmsButton (phone.Value);
            };
            view.AddSubview (smsButton);

            yOffset += 20;

            var valueLabel = new UILabel (new RectangleF (35, 20, View.Frame.Width - 75, 20));
            valueLabel.Font = A.Font_AvenirNextRegular14;
            valueLabel.TextColor = A.Color_808080;
            valueLabel.Text = phone.Value;
            valueLabel.SizeToFit ();
            view.AddSubview (valueLabel);

            return 40;
        }

        protected void DisplayContactInfo ()
        {
            var segmentedController = contentView.ViewWithTag (SEGMENTED_CONTROL_TAG) as UISegmentedControl;
            segmentedController.SelectedSegment = 0;
        }

        protected UIColor LighterColor (UIColor color)
        {
            float hue, saturation, brightness, alpha;
            color.GetHSBA (out hue, out saturation, out brightness, out alpha);
            return UIColor.FromHSBA (hue, saturation, Math.Min (brightness * 1.3f, 1.0f), alpha);
        }

        protected string GetInitials ()
        {
            string initials = "";
            if (!String.IsNullOrEmpty (contact.FirstName)) {
                initials += Char.ToUpper (contact.FirstName [0]);
            }
            if (!String.IsNullOrEmpty (contact.LastName)) {
                initials += Char.ToUpper (contact.LastName [0]);
            }
            // Or, failing that, the first char
            if (String.IsNullOrEmpty (initials)) {
                if (0 != contact.EmailAddresses.Count) {
                    var emailAddressAttribute = contact.EmailAddresses [0];
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
                    foreach (char c in emailAddress.CanonicalEmailAddress) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            return initials;
        }

        /// Return email address iff display name is set
        protected string GetTitleFromContact ()
        {
            var name = contact.GetDisplayName ();

            if (String.IsNullOrEmpty (name)) {
                return "";
            }
            var emailAddressAttribute = contact.EmailAddresses [0];
            var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
            return emailAddress.CanonicalEmailAddress;
        }

        protected void TouchedEmailButton (string address)
        {
            Log.Info (Log.LOG_UI, "TouchedEmailButton");

            if (string.IsNullOrEmpty (address)) {
                ComplainAbout ("No email address", "You've selected a contact who does not have an email address");
                return;
            }
            PerformSegue ("ContactToEmailCompose", new SegueHolder (address));
        }

        protected void TouchedCallButton (string number)
        {
            Log.Info (Log.LOG_UI, "TouchedCallButton");

            if (string.IsNullOrEmpty (number)) {
                ComplainAbout ("No phone number", "You've selected a contact who does not have a phone number");
                return;
            }
            PerformAction ("tel", number);
        }

        protected void TouchedSmsButton (string number)
        {
            Log.Info (Log.LOG_UI, "TouchedSmsButton");

            if (null == number) {
                ComplainAbout ("No phone number", "You've selected a contact who does not have a phone number");
                return;
            }
            PerformAction ("sms", number);
        }

        protected void PerformAction (string action, string number)
        {
            UIApplication.SharedApplication.OpenUrl (new Uri (String.Format ("{0}:{1}", action, number)));
        }

        protected void ComplainAbout (string complaintTitle, string complaintMessage)
        {
            UIAlertView alert = new UIAlertView (complaintTitle, complaintMessage, null, "OK", null);
            alert.Show ();
        }

        protected void ToggleVipStatus ()
        {
            contact.SetVIP (!contact.IsVip);
            UpdateVipButton ();
        }

        protected void UpdateVipButton ()
        {
            var vipImageName = (contact.IsVip ? "icn-contact-vip" : "icn-vip");

            using (var rawImage = UIImage.FromBundle (vipImageName)) {
                var image = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
                vipButton.Image = image;
            }
        }

        ////////InteractionsTableViewStuff

        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback");
                RefreshData ();
            }

            if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: Contact Set Changed");
                RefreshData ();
            }
        }

        public void RefreshData ()
        {
            NachoClient.Util.HighPriority ();
            messageSource.RefreshEmailMessages ();
            InteractionsTable.ReloadData ();
            NachoClient.Util.RegularPriority ();
        }

        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                this.DismissViewController (true, null);
            }));
        }

        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var t = CalendarHelper.CreateTask (m);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("", new SegueHolder (t));
            }));
        }

        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var c = CalendarHelper.CreateMeeting (m);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("NachoNowToEditEvent", new SegueHolder (c));
            }));
        }

        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.SetOwner (null, null);
            vc.DismissFolderChooser (false, null);
        }

        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            if (null != messageSource) {
                messageSource.FolderSelected (vc, folder, cookie);
            }
            vc.DismissFolderChooser (true, null);
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
            return;
        }

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads);
        }

        public void SaveNote (string noteText)
        {
            if (null != contact) {
                McBody contactBody = McBody.QueryById<McBody> (contact.BodyId);
                if (null != contactBody) {
                    contactBody.UpdateBody (noteText);
                } else {
                    contact.BodyId = McBody.Save (noteText).Id;
                }

                contact.Update ();
                NachoCore.BackEnd.Instance.UpdateContactCmd (contact.AccountId, contact.Id);
            }
        }

        public string GetNoteText ()
        {
            NcAssert.True (null != contact);

            if (contact.Source != McAbstrItem.ItemSource.ActiveSync) {
                return "This contact has not been synced. Adding or editing notes is disabled.";
            } else {
                McBody contactBody = McBody.QueryById<McBody> (contact.BodyId);
                if (null != contactBody) {
                    return contactBody.Body;
                }
                return "";
            }
        }
    }
}
