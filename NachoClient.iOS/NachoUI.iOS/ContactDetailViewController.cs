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

namespace NachoClient.iOS
{
    public partial class ContactDetailViewController : NcUIViewController
    {
        public McContact contact;

        protected float startingY;

        protected UIColor originalBarTintColor;

        public ContactDetailViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            var editButton = new UIBarButtonItem (UIBarButtonSystemItem.Edit);
            NavigationItem.RightBarButtonItem = editButton;

            editButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("ContactToContactEdit", new SegueHolder (contact));
            };

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
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                this.NavigationController.NavigationBar.BarTintColor = originalBarTintColor;

            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactToNotes")) {
                var dc = (NotesViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                dc.SetContact (holder.value as McContact);
                dc.ViewDisappearing += (object s, EventArgs e) => {
                    dc.SaveContactNote();
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

        const int FOO_SIZE = 64;

        protected void CreateView ()
        {
            var topImage = new UIView (new RectangleF (0, 0, View.Frame.Width, 216 - FOO_SIZE));
            topImage.Tag = TOP_IMAGE_TAG;
            contentView.AddSubview (topImage);

            var topEmailButton = UIButton.FromType (UIButtonType.RoundedRect);
            topEmailButton.Frame = new RectangleF (0, 0, 40, 40);
            topEmailButton.Center = new PointF (62, (216 / 2) - FOO_SIZE);
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
            topCallButton.Center = new PointF (View.Frame.Width - 62, (216 / 2) - FOO_SIZE);
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
            topUserLabel.Center = new PointF (View.Frame.Width / 2, (216 / 2) - FOO_SIZE);
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
            segmentedControl.Frame = new RectangleF (6, yOffset, View.Frame.Width - 12, 40);
            segmentedControl.InsertSegment ("Contact Info", 0, false);
            segmentedControl.InsertSegment ("Interactions", 1, false);
            segmentedControl.InsertSegment ("Notes", 2, false);
            segmentedControl.SelectedSegment = 0;

            var segmentedControlTextAttributes = new UITextAttributes ();
            segmentedControlTextAttributes.Font = A.Font_AvenirNextRegular14;
            segmentedControlTextAttributes.TextColor = A.Color_009E85;
            segmentedControl.SetTitleTextAttributes (segmentedControlTextAttributes, UIControlState.Normal);

            segmentedControl.ValueChanged += (sender, e) => {
                var selectedSegmentId = (sender as UISegmentedControl).SelectedSegment;
                switch (selectedSegmentId) {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    PerformSegue ("ContactToNotes", new SegueHolder (contact));
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
            };

            segmentedControl.Tag = SEGMENTED_CONTROL_TAG;
            contentView.AddSubview (segmentedControl);

            yOffset += 40;
            yOffset += 6;

            var segmentedControlHL = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 1));
            segmentedControlHL.BackgroundColor = A.Color_NachoSeparator;
            segmentedControlHL.Tag = SEGMENTED_CONTROL_HL_TAG;
            contentView.AddSubview (segmentedControlHL);

            yOffset += 6;

            startingY = yOffset;
        }

        protected void ConfigureView ()
        {
            UIColor userBackgroundColor;

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

            var emailHL = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 1));
            emailHL.BackgroundColor = A.Color_NachoSeparator;
            emailHL.Tag = TRANSIENT_TAG;
            ;
            contentView.AddSubview (emailHL);

            yOffset += 6;

            skippedEmail:

            if (0 == contact.PhoneNumbers.Count) {
                goto skippedPhones;
            }

//            yOffset += AddHeader ("Phone", yOffset);

            foreach (var phoneNumberAttribute in contact.PhoneNumbers) {
                yOffset += AddPhoneNumber (phoneNumberAttribute, yOffset);
            }

            skippedPhones:

            contentView.Frame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;
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
            labelLabel.Text = email.Name;
            labelLabel.SizeToFit ();
            labelLabel.Tag = TRANSIENT_TAG;
            view.AddSubview (labelLabel);

            var imageView = new UIImageView (new RectangleF (View.Frame.Width - 24 - 15, 10, 24, 24));
            imageView.Image = UIImage.FromBundle ("icn-quick-message");
            view.AddSubview (imageView);

            var tap = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                TouchedEmailButton(email.Value);
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

            var labelLabel = new UILabel (new RectangleF (15, 0, 45, 20));
            labelLabel.Font = A.Font_AvenirNextRegular14;
            labelLabel.TextColor = A.Color_0B3239;
            labelLabel.Text = phone.Name;
            labelLabel.SizeToFit ();
            labelLabel.Tag = TRANSIENT_TAG;
            view.AddSubview (labelLabel);

            var phoneButton = UIButton.FromType (UIButtonType.RoundedRect);
            phoneButton.Frame = new RectangleF (View.Frame.Width - 24 - 15, 10, 24, 24);
            phoneButton.SetImage (UIImage.FromBundle ("icn-mtng-phone"), UIControlState.Normal);
            phoneButton.TouchUpInside += (sender, e) => {
                TouchedEmailButton (phone.Value);
            };
            view.AddSubview (phoneButton);

            var smsButton = UIButton.FromType (UIButtonType.RoundedRect);
            smsButton.Frame = new RectangleF (View.Frame.Width - 24 - 15 - 24 - 15, 10, 24, 24);
            smsButton.SetImage (UIImage.FromBundle ("icn-sms"), UIControlState.Normal);
            smsButton.TouchUpInside += (sender, e) => {
                TouchedEmailButton (phone.Value);
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
            PerformSegue ("ContactToEmailCompose", new SegueHolder(address));
        }

        protected void TouchedCallButton (string number)
        {
            Log.Info (Log.LOG_UI, "TouchedCallButton");

            if (string.IsNullOrEmpty (number)) {
                ComplainAbout ("No phone number", "You've selected a contact who does not have an phone number");
                return;
            }
            PerformAction ("tel", number);
        }

        protected void TouchedSmsButton (string number)
        {
            Log.Info (Log.LOG_UI, "TouchedSmsButton");

            if (null == number) {
                ComplainAbout ("No phone number", "You've selected a contact who does not have an phone number");
                return;
            }
            PerformAction ("sms", number);
        }

        protected void PerformAction (string action, string number)
        {
            UIApplication.SharedApplication.OpenUrl (new Uri (String.Format ("{0}://{1}", action, number)));
        }

        protected void ComplainAbout (string complaintTitle, string complaintMessage)
        {
            UIAlertView alert = new UIAlertView (complaintTitle, complaintMessage, null, "OK", null);
            alert.Show ();
        }

    }
}
