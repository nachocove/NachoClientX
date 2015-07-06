// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using CoreGraphics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foundation;
using UIKit;
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
        string contactSearchToken;

        protected const string ContactCellReuseIdentifier = "ContactCell";

        public void SetOwner (INachoContactChooserDelegate owner, McAccount account, NcEmailAddress address, NachoContactType contactType)
        {
            this.owner = owner;
            this.account = account;
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
            autoCompleteTextField.BecomeFirstResponder ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            CancelSearchIfActive ();
        }

        public override bool ShouldEndEditing {
            get {
                return false;
            }
        }

        public void CreateView ()
        {
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            UIView inputView = new UIView (new CGRect (0, 0, View.Frame.Width, 44));
            inputView.BackgroundColor = A.Color_NachoBackgroundGray;

            keyboardHeight = NcKeyboardSpy.Instance.keyboardHeight;
            resultsTableView = new UITableView (new CGRect (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight));

            resultsTableView.SeparatorColor = A.Color_NachoBorderGray;
            resultsTableView.Source = new ContactChooserDataSource (this);
            resultsTableView.AccessibilityLabel = "Contact chooser results";

            cancelSearchButton = new UIButton (UIButtonType.RoundedRect);
            cancelSearchButton.Frame = new CGRect (View.Frame.Width - 58, 6, 50, 32);
            cancelSearchButton.SetTitle ("Cancel", UIControlState.Normal);
            cancelSearchButton.AccessibilityLabel = "Cancel";
            cancelSearchButton.Font = A.Font_AvenirNextMedium14;
            cancelSearchButton.SetTitleColor (A.Color_NachoIconGray, UIControlState.Normal);
            cancelSearchButton.TouchUpInside += (object sender, EventArgs e) => {
                CancelSelected ();
            };
            inputView.AddSubviews (cancelSearchButton);

            UIView textInputView = new UIView (new CGRect (8, 6, 246, 32));
            textInputView.BackgroundColor = UIColor.White;
            textInputView.Layer.CornerRadius = 4;

            autoCompleteTextField = new UITextField (new CGRect (6, 0, 234, 32));
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
            resultsTableView.Frame = new CGRect (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight);
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: Info_SearchCommandSucceeded");
                UpdateAutocompleteResults (0, autoCompleteTextField.Text);
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ContactChooserToContactSearch")) {
                var dvc = (INachoContactChooser)segue.DestinationViewController;
                dvc.SetOwner (this, account, address, NachoContactType.EmailRequired);
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
            if (owner != null) {
                owner.UpdateEmailAddress (this, this.address);
                owner.DismissINachoContactChooser (this);
            } else {
                Log.Error (Log.LOG_UI, "ContactChooserViewController: null in update email address");
            }
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
            if (null != owner) {
                owner.DismissINachoContactChooser (this);
            }
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
                NachoCore.Utils.NcAbate.HighPriority ("ContactChooser UpdateAutocompleteResults with string");
                searchResults = McContact.SearchIndexAllContactsWithEmailAddresses (forSearchString, true);
                resultsTableView.ReloadData ();
                NachoCore.Utils.NcAbate.RegularPriority ("ContactChooser UpdateAutocompleteResults with string");

                /// This is a simple test code that issues a prefix search and print the results
                /// in console. Once the real search window is done, please remove this chunk of code
                /// HACK ALERT - TEST / DEMO CODE FOR INDEXING!!!!
//                if (!String.IsNullOrEmpty (forSearchString)) {
//                    var indexPath = NcModel.Instance.GetFileDirPath (2, "index");
//                    NachoCore.Index.Index index = new NachoCore.Index.Index (indexPath);
//                    var matches = index.Search (forSearchString + "*");
//                    int n = 1;
//                    foreach (var match in matches) {
//                        Console.WriteLine (">>> {0}: {1} {2}", n, match.Type, match.Id);
//                        n += 1;
//                    }
//                }
            }
        }

        protected void KickoffSearchApi (int forSearchOption, string forSearchString)
        {
            // TODO: Think about whether we want to users about errors during GAL search
            if (String.IsNullOrEmpty (contactSearchToken)) {
                contactSearchToken = BackEnd.Instance.StartSearchContactsReq (account.Id, forSearchString, null).GetValue<string> ();
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

        protected override void OnKeyboardChanged ()
        {
            resultsTableView.Frame = new CGRect (0, 44, View.Frame.Width, View.Frame.Height - keyboardHeight);
        }

        public class ContactChooserDataSource : UITableViewSource
        {
            ContactChooserViewController Owner;

            public ContactChooserDataSource (ContactChooserViewController owner)
            {
                Owner = owner;
            }

            public override nint NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override nint RowsInSection (UITableView tableview, nint section)
            {
                if (null != Owner.searchResults) {
                    return Owner.searchResults.Count;
                } else {
                    return 0;
                }
            }

            public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return ContactCell.ROW_HEIGHT;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell (ContactCellReuseIdentifier);
                if (null == cell) {
                    cell = ContactCell.CreateCell (tableView, VipButtonTouched);
                }
                var contactAddress = Owner.searchResults [indexPath.Row];
                var contact = contactAddress.GetContact ();
                ContactCell.ConfigureCell (tableView, cell, contact, null, false, contactAddress.Value);
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
                vipButton.AccessibilityLabel = "VIP";
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

                Owner.UpdateEmailAddress (contact, Owner.searchResults [indexPath.Row].Value);
            }

        }

        void ComplainAboutMissingEmailAddress (McContact contact)
        {
            if (contact.CanUserEdit ()) {
                NcAlertView.Show (this, "E-mail Address Missing",
                    "You have selected a contact without an e-mail address. Would you like to edit this contact?",
                    new NcAlertAction ("No", NcAlertActionStyle.Cancel, null),
                    new NcAlertAction ("Edit Contact", () => {
                        PerformSegue ("SegueToContactEdit", new SegueHolder (contact));
                    }));
            } else {
                NcAlertView.ShowMessage (this, "E-mail Address Missing",
                    "You have selected a contact without an e-mail address.");
            }
        }

        protected void CancelSearchIfActive ()
        {
            if (!String.IsNullOrEmpty (contactSearchToken)) {
                McPending.Cancel (account.Id, contactSearchToken);
                contactSearchToken = null;
            }
        }
    }
}
