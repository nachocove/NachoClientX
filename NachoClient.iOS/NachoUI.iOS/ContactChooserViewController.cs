// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class ContactChooserViewController : NcUIViewController, IUITableViewDelegate, IUITextFieldDelegate, INachoContactChooser, INachoContactChooserDelegate
    {
        // Interface
        protected McAccount account;
        protected NcEmailAddress address;
        protected NachoContactType contactType;
        protected INachoContactChooserDelegate owner;
        protected UIButton cancelSearchButton;
        protected UITextField autoCompleteTextField;
        protected UITableView resultsTableView;
        // Internal state
        List<McContactEmailAddressAttribute> searchResults;
        // ContactTableViewSource is used solely to create & config a cell
        ContactsTableViewSource contactTableViewSource;
        string contactSearchToken;
        float keyboardHeight;

        protected const string ContactCellReuseIdentifier = "ContactCell";

        public void SetOwner (INachoContactChooserDelegate owner, NcEmailAddress address, NachoContactType contactType)
        {
            this.owner = owner;
            this.address = address;
            this.contactType = contactType;
        }

        public void Cleanup ()
        {
            this.owner = null;
        }

        public ContactChooserViewController (IntPtr handle) : base (handle)
        {
        }

        public ContactChooserViewController () : base ()
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NcAssert.True (null != owner);
            NcAssert.True (null != address);

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            contactTableViewSource = new ContactsTableViewSource ();

            CreateView ();

            PermissionManager.DealWithContactsPermission ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            NachoCore.Utils.NcAbate.HighPriority ("ContactChooser ViewWillAppear");
            resultsTableView.ReloadData ();
            NachoCore.Utils.NcAbate.RegularPriority ("ContactChooser ViewWillAppear");
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
            autoCompleteTextField.BecomeFirstResponder ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            CancelSearchIfActive ();
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        public void CreateView ()
        {
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            UIView inputView = new UIView (new RectangleF (0, 0, View.Frame.Width, 44));
            inputView.BackgroundColor = A.Color_NachoBackgroundGray;

            keyboardHeight = NcKeyboardSpy.Instance.keyboardHeight;
            resultsTableView = new UITableView (new RectangleF (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight));

            resultsTableView.SeparatorColor = A.Color_NachoBorderGray;
            resultsTableView.Source = new ContactChooserDataSource (this);

            cancelSearchButton = new UIButton (UIButtonType.RoundedRect);
            cancelSearchButton.Frame = new RectangleF (View.Frame.Width - 58, 6, 50, 32);
            cancelSearchButton.SetTitle ("Cancel", UIControlState.Normal);
            cancelSearchButton.Font = A.Font_AvenirNextMedium14;
            cancelSearchButton.SetTitleColor (A.Color_NachoIconGray, UIControlState.Normal);
            cancelSearchButton.TouchUpInside += (object sender, EventArgs e) => {
                CancelSelected ();
            };
            inputView.AddSubviews (cancelSearchButton);

            UIView textInputView = new UIView (new RectangleF (8, 6, 246, 32));
            textInputView.BackgroundColor = UIColor.White;
            textInputView.Layer.CornerRadius = 4;

            autoCompleteTextField = new UITextField (new RectangleF (6, 0, 234, 32));
            autoCompleteTextField.BackgroundColor = UIColor.White;
            autoCompleteTextField.Font = A.Font_AvenirNextMedium14;
            autoCompleteTextField.ClearButtonMode = UITextFieldViewMode.Always;
            autoCompleteTextField.TintColor = A.Color_NachoIconGray;
            autoCompleteTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            autoCompleteTextField.AutocorrectionType = UITextAutocorrectionType.No;
            autoCompleteTextField.SpellCheckingType = UITextSpellCheckingType.No;

            // Update the auto-complete on each keystroke
            autoCompleteTextField.EditingChanged += delegate {
                KickoffSearchApi (0, autoCompleteTextField.Text);
                UpdateAutocompleteResults (0, autoCompleteTextField.Text);
            };

            // Finish up when the Done key is selected
            autoCompleteTextField.ShouldReturn = ((textField) => {
                DoneSelected (textField);
                return false;
            });

            autoCompleteTextField.Text = address.address;
            UpdateAutocompleteResults (0, address.address);

            textInputView.AddSubview (autoCompleteTextField);
            inputView.AddSubview (textInputView);

            View.AddSubview (inputView);
            View.AddSubview (resultsTableView);
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            resultsTableView.Frame = new RectangleF (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight);
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_SearchCommandSucceeded == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: Info_SearchCommandSucceeded");
                UpdateAutocompleteResults (0, autoCompleteTextField.Text);
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactChooserToContactSearch")) {
                var dvc = (INachoContactChooser)segue.DestinationViewController;
                dvc.SetOwner (this, address, NachoContactType.EmailRequired);
                return;
            }

            if (segue.Identifier.Equals ("SegueToContactEdit")) {
                var dvc = (ContactEditViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                var contact = (McContact)holder.value;
                dvc.contact = contact;
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected void UpdateEmailAddress (McContact contact, string address)
        {
            this.address.contact = contact;
            this.address.address = address;
            owner.UpdateEmailAddress (this, this.address);
            owner.DismissINachoContactChooser (this);
        }

        /// <summary>
        /// DoneSelected mean return the typed-in contact.
        /// </summary>
        public void DoneSelected (UITextField textField)
        {
            if ((null == textField.Text) || (0 == textField.Text.Length)) {
                owner.DeleteEmailAddress (this, address);
                owner.DismissINachoContactChooser (this);
            } else {
                UpdateEmailAddress (null, textField.Text);
            }
        }

        public void CancelSelected ()
        {
            owner.DismissINachoContactChooser (this);
        }

        /// <summary>
        /// Updates the search results.
        /// <returns><c>true</c>, if search results are updated, <c>false</c> otherwise.</returns>
        /// <param name="forSearchOption">Index of the selected tab.</param>
        /// <param name="forSearchString">The prefix string to search for.</param>
        public void UpdateAutocompleteResults (int forSearchOption, string forSearchString)
        {
            if (null == forSearchString) {
                searchResults = null;
                NachoCore.Utils.NcAbate.HighPriority ("ContactChooser UpdateAutocompleteResults");
                resultsTableView.ReloadData ();
                NachoCore.Utils.NcAbate.RegularPriority ("ContactChooser UpdateAutocompleteResults");
            } else {
                searchResults = McContact.SearchAllContactsWithEmailAddresses (forSearchString);
                NachoCore.Utils.NcAbate.HighPriority ("ContactChooser UpdateAutocompleteResults with string");
                resultsTableView.ReloadData ();
                NachoCore.Utils.NcAbate.RegularPriority ("ContactChooser UpdateAutocompleteResults with string");
            }
        }

        protected void KickoffSearchApi (int forSearchOption, string forSearchString)
        {
            if (String.IsNullOrEmpty (contactSearchToken)) {
                contactSearchToken = BackEnd.Instance.StartSearchContactsReq (account.Id, forSearchString, null);
            } else {
                BackEnd.Instance.SearchContactsReq (account.Id, forSearchString, null, contactSearchToken);
            }
        }

        // INachoContactChooser delegate
        public void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            vc.Cleanup ();
            owner.UpdateEmailAddress (this, address);
            owner.DismissINachoContactChooser (this);
        }

        // INachoContactChooser delegate
        public void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            NcAssert.CaseError ();
        }

        // INachoContactChooser delegate
        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            vc.Cleanup ();
            owner.DismissINachoContactChooser (this);
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;
                //Start an animation, using values from the keyboard
                UIView.BeginAnimations ("AnimateForKeyboard");
                UIView.SetAnimationBeginsFromCurrentState (true);
                UIView.SetAnimationDuration (UIKeyboard.AnimationDurationFromNotification (notification));
                UIView.SetAnimationCurve ((UIViewAnimationCurve)UIKeyboard.AnimationCurveFromNotification (notification));
                //Pass the notification, calculating keyboard height, etc.
                bool landscape = InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft || InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
                if (visible) {
                    var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                } else {
                    var keyboardFrame = UIKeyboard.FrameBeginFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                }
                //Commit the animation
                UIView.CommitAnimations (); 
            }
        }

        /// <summary>
        /// Override this method to apply custom logic when the keyboard is shown/hidden
        /// </summary>
        /// <param name='visible'>
        /// If the keyboard is visible
        /// </param>
        /// <param name='height'>
        /// Calculated height of the keyboard (width not generally needed here)
        /// </param>
        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            resultsTableView.Frame = new RectangleF (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight);
        }

        public class ContactChooserDataSource : UITableViewSource
        {
            ContactChooserViewController Owner;

            public ContactChooserDataSource (ContactChooserViewController owner)
            {
                Owner = owner;
            }

            public override int NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override int RowsInSection (UITableView tableview, int section)
            {
                if (null != Owner.searchResults) {
                    return Owner.searchResults.Count;
                } else {
                    return 0;
                }
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return Owner.contactTableViewSource.GetHeightForRow (tableView, indexPath);
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
                if (null == cell) {
                    cell = ContactCell.CreateCell (tableView, VipButtonTouched);
                }
                var contact = Owner.searchResults [indexPath.Row].GetContact ();
                ContactCell.ConfigureCell (tableView, cell, contact, null, false);
                return cell;
            }

            protected void VipButtonTouched (object sender, EventArgs e)
            {
                UIButton vipButton = (UIButton)sender;
                UITableViewCell containingCell = Util.FindEnclosingTableViewCell (vipButton);
                UITableView containingTable = Util.FindEnclosingTableView (vipButton);
                NSIndexPath cellIndexPath = containingTable.IndexPathForCell (containingCell);
                var contact = Owner.searchResults [cellIndexPath.Row].GetContact ();
                contact.SetVIP (!contact.IsVip);
                using (var image = UIImage.FromBundle (contact.IsVip ? "contacts-vip-checked" : "contacts-vip")) {
                    vipButton.SetImage (image, UIControlState.Normal);
                }
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.CellAt (indexPath);
                if (null != cell) {
                    cell.Selected = false;
                }

                var contact = Owner.searchResults [indexPath.Row].GetContact ();

                Owner.CancelSearchIfActive ();

                if (null == contact) {
                    Owner.CancelSelected ();
                    Owner.owner = null;
                    return;
                }

                // TODO: require phone numbers in contact chooser
                NcAssert.True (0 == (Owner.contactType & NachoContactType.PhoneNumberRequired));

                if (NachoContactType.EmailRequired == (Owner.contactType & NachoContactType.EmailRequired)) {
                    if (String.IsNullOrEmpty (contact.GetEmailAddress ())) {
                        Owner.ComplainAboutMissingEmailAddress (contact);
                        return;
                    }
                }

                Owner.UpdateEmailAddress (contact, contact.GetEmailAddress ());
            }

        }

        string complaintTitle = "Email Address Missing";
        string complaintMessage = "You've selected a contact who does not have an email address.";
        string complaintEditMessage = "  Would you like to edit this contact?";

        void ComplainAboutMissingEmailAddress (McContact contact)
        {
            var alert = new UIAlertView ();
            if (contact.CanUserEdit ()) {
                alert.AddButton ("No");
                alert.AddButton ("Edit Contact");
                alert.Message = complaintMessage + complaintEditMessage;
            } else {
                alert.AddButton ("OK");
                alert.Message = complaintMessage;
            }
            alert.Clicked += (s, b) => {
                if (1 == b.ButtonIndex) {
                    PerformSegue ("SegueToContactEdit", new SegueHolder (contact));
                }
            };
            alert.Title = complaintTitle;
            alert.Show ();
        }

        protected void CancelSearchIfActive ()
        {
            if (!String.IsNullOrEmpty (contactSearchToken)) {
                BackEnd.Instance.Cancel (account.Id, contactSearchToken);
                contactSearchToken = null;
            }
        }
    }
}
