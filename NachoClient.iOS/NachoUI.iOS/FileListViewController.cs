// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Linq;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using CoreAnimation;

namespace NachoClient.iOS
{
    public partial class FileListViewController : NcUIViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoNotesControllerParent, IAttachmentTableViewSourceDelegate
    {
        public FileListViewController () : base ()
        {
            Account = NcApplication.Instance.Account;
        }

        INachoFileChooserParent Owner;
        FilesTableViewSource AttachmentsSource;
         
        string Token;

        protected McNote selectedNote;

        protected UITableView tableView;
        protected UISegmentedControl segmentedControl;
        protected UIView segmentedControlView;

        SwitchAccountButton switchAccountButton;

        UILabel EmptyListLabel;

        // animation constants
        public nfloat AnimationDuration = 3.0f;

        UIBarButtonItem searchButton;
        UIBarButtonItem multiSelectButton;
        UIBarButtonItem multiOpenInButton;
        UIBarButtonItem multiAttachButton;
        UIBarButtonItem multiDeleteButton;
        UIBarButtonItem multiCancelButton;
        public bool isMultiSelecting;

        private bool suppressLayout;

        UINavigationBar navbar = new UINavigationBar ();

        McAccount Account;

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void SetOwner (INachoFileChooserParent owner, McAccount account)
        {
            this.Owner = owner;
            Account = account;
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void DismissFileChooser (bool animated, Action action)
        {
            Owner = null;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Uncomment to hide <More
            // if (null != NavigationItem) {
            //     NavigationItem.SetHidesBackButton (true, false);
            // }

            CreateView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            if (null != switchAccountButton) {
                switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
            }
            RefreshTableSource ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: AttachmentsSetChanged");
                RefreshTableSource ();
            }
            if (NcResult.SubKindEnum.Info_SystemTimeZoneChanged == s.Status.SubKind) {
                // Refresh the view so that the displayed times will reflect the new time zone.
                RefreshTableSource ();
            }
        }

        private void CreateView ()
        {
            nfloat yOffset = 0;
            searchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.AccessibilityLabel = "Search";
            multiSelectButton = new NcUIBarButtonItem ();
            multiOpenInButton = new NcUIBarButtonItem ();
            multiAttachButton = new NcUIBarButtonItem ();
            multiDeleteButton = new NcUIBarButtonItem ();
            multiCancelButton = new NcUIBarButtonItem ();

            if (modal) {
                navbar.Frame = new CGRect (0, 0, View.Frame.Width, 64);
                View.AddSubview (navbar);
                navbar.BackgroundColor = A.Color_NachoGreen;
                navbar.Translucent = false;
                UINavigationItem title = new UINavigationItem ("Attach file");
                navbar.SetItems (new UINavigationItem[]{ title }, false);
                UIBarButtonItem cancelButton = new NcUIBarButtonItem ();
                cancelButton.AccessibilityLabel = "Cancel";
                Util.SetAutomaticImageForButton (cancelButton, "icn-close");

                navbar.TopItem.LeftBarButtonItem = cancelButton;
                navbar.TopItem.RightBarButtonItem = searchButton;
                cancelButton.Clicked += (object sender, EventArgs e) => {
                    DismissViewController (true, null);
                };
                yOffset += navbar.Frame.Height;
            } else {
                View.BackgroundColor = UIColor.White;
                switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
                NavigationItem.TitleView = switchAccountButton; 
                switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
            }

            segmentedControlView = new UIView (new CGRect (0, yOffset, View.Frame.Width, 40));
            segmentedControlView.BackgroundColor = UIColor.White;

            segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new CGRect (6, 5, View.Frame.Width - 12, 30);
            segmentedControl.InsertSegment ("By Name", 0, false);
            segmentedControl.InsertSegment ("By Date", 1, false);
            segmentedControl.InsertSegment ("By Contact", 2, false);
            segmentedControl.SelectedSegment = 0;
            segmentedControl.TintColor = A.Color_NachoGreen;

            var segmentedControlTextAttributes = new UITextAttributes ();
            segmentedControlTextAttributes.Font = A.Font_AvenirNextDemiBold14;
            segmentedControl.SetTitleTextAttributes (segmentedControlTextAttributes, UIControlState.Normal);

            segmentedControl.ValueChanged += (sender, e) => {
                AttachmentsSource.SetSegmentedIndex (segmentedControl.SelectedSegment);
                RefreshTableSource ();
            };

            yOffset += segmentedControlView.Frame.Height;

            Util.AddHorizontalLine (0, segmentedControlView.Frame.Height, View.Frame.Width, A.Color_NachoBorderGray, segmentedControlView);
            segmentedControlView.AddSubview (segmentedControl);
            View.AddSubview (segmentedControlView);

            tableView = new UITableView (new CGRect (0, 0, 0, 0), UITableViewStyle.Grouped);
            tableView.SeparatorColor = UIColor.Clear;
            tableView.AccessibilityLabel = "Attachments";

            InitializeSearchDisplayController ();
            AttachmentsSource = new FilesTableViewSource (this, Account);
            AttachmentsSource.SetOwner (this, SearchDisplayController);

            View.AddSubview (tableView);
            // set up the table view source
            tableView.Source = AttachmentsSource;
            SearchDisplayController.SearchResultsTableView.Source = AttachmentsSource;

            searchButton.TintColor = A.Color_NachoBlue;
            searchButton.AccessibilityLabel = "Search";
            NavigationItem.LeftItemsSupplementBackButton = true;
            NavigationItem.LeftBarButtonItem = searchButton;
            searchButton.Clicked += searchClicked;
                
            multiSelectButton.TintColor = A.Color_NachoBlue;
            multiSelectButton.Image = UIImage.FromBundle ("folder-edit");
            multiSelectButton.AccessibilityLabel = "Folder edit";
            NavigationItem.RightBarButtonItem = multiSelectButton;
            multiSelectButton.Clicked += multiClicked;

            multiOpenInButton.TintColor = A.Color_NachoBlue;
            multiOpenInButton.Image = UIImage.FromBundle ("files-open-in-app");
            multiOpenInButton.AccessibilityLabel = "Open in";
            multiOpenInButton.Clicked += openInClicked;

            multiAttachButton.TintColor = A.Color_NachoBlue;
            multiAttachButton.Image = UIImage.FromBundle ("files-email-attachment");
            multiAttachButton.AccessibilityLabel = "Send attachment";
            multiAttachButton.Clicked += attachClicked;

            multiDeleteButton.TintColor = A.Color_NachoBlue;
            multiDeleteButton.Image = UIImage.FromBundle ("gen-delete-all");
            multiDeleteButton.AccessibilityLabel = "Delete";
            multiDeleteButton.Clicked += deleteClicked;

            multiCancelButton.TintColor = A.Color_NachoBlue;
            multiCancelButton.Image = UIImage.FromBundle ("gen-close");
            multiCancelButton.AccessibilityLabel = "Close";
            multiCancelButton.Clicked += cancelClicked;

            EmptyListLabel = new UILabel (new CGRect (0, 80, UIScreen.MainScreen.Bounds.Width, 20));
            EmptyListLabel.TextAlignment = UITextAlignment.Center;
            EmptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            EmptyListLabel.TextColor = A.Color_NachoBorderGray;
            EmptyListLabel.Hidden = true;
            View.AddSubview (EmptyListLabel);

            View.BringSubviewToFront (segmentedControlView);
        }

        private void ToggleMultiSelect (bool isMultiSelect)
        {
            suppressLayout = true;
            if (isMultiSelect) {
                NavigationItem.LeftBarButtonItem = multiCancelButton;
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    multiDeleteButton,
                    multiAttachButton,
                    multiOpenInButton
                };
                NavigationItem.HidesBackButton = true;
                NavigationItem.Title = "";
                ToggleSearchBar (false);
                isMultiSelecting = true;
                AttachmentsSource.IsMultiSelecting = true;
                ConfigureMultiSelectNavBar (true, 0);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new CGPoint (segmentedControlView.Center.X, segmentedControlView.Center.Y - segmentedControlView.Frame.Height);
                        tableView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
                        ConfigureVisibleCells ();
                    },
                    () => {
                    }
                );

            } else {
                NavigationItem.LeftBarButtonItem = searchButton;
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    multiSelectButton
                };
                NavigationItem.HidesBackButton = false;
                NavigationItem.Title = "Files";
                ToggleSearchBar (true);
                isMultiSelecting = false;
                AttachmentsSource.IsMultiSelecting = false;
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new CGPoint (segmentedControlView.Center.X, segmentedControlView.Center.Y + segmentedControlView.Frame.Height);
                        tableView.Frame = new CGRect (0, segmentedControlView.Frame.Height, View.Frame.Width, View.Frame.Height - segmentedControlView.Frame.Height);
                        ConfigureVisibleCells ();
                    },
                    () => {

                    }
                );
            }
        }

        public void ConfigureVisibleCells ()
        {
            foreach (var path in tableView.IndexPathsForVisibleRows) {
                AttachmentsSource.ConfigureCell (tableView, tableView.CellAt (path), path);
            }
        }

        public void ConfigureMultiSelectNavBar (bool openIn, int count)
        {
            if (openIn) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    multiDeleteButton,
                    multiAttachButton,
                    multiOpenInButton
                };
                if (0 == count) {
                    multiDeleteButton.Enabled = false;
                    multiAttachButton.Enabled = false;
                    multiOpenInButton.Enabled = false;
                } else {
                    multiDeleteButton.Enabled = true;
                    multiAttachButton.Enabled = true;
                    multiOpenInButton.Enabled = true;
                }
            } else {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    multiDeleteButton,
                    multiAttachButton
                };
                if (0 == count) {
                    multiDeleteButton.Enabled = false;
                    multiAttachButton.Enabled = false;
                } else {
                    multiDeleteButton.Enabled = true;
                    multiAttachButton.Enabled = true;
                }
            }
        }

        private void ConfigureFilesView ()
        {
            if (0 == AttachmentsSource.Items.Count) {
                this.tableView.ScrollEnabled = false;
                tableView.Hidden = true;
                EmptyListLabel.Hidden = false;
                EmptyListLabel.Text = "No files";
            } else {
                this.tableView.ScrollEnabled = true;
                EmptyListLabel.Hidden = true;
                tableView.Hidden = false;
            }
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            Account = account;
            switchAccountButton.SetAccountImage (account);
            RefreshTableSource ();
        }

        protected void InitializeSearchDisplayController ()
        {
            var sb = new UISearchBar ();

            // creating the controller set up its pointers
            new UISearchDisplayController (sb, this);

            tableView.TableHeaderView = sb;
        }

        public string GetNoteText ()
        {
            if (null != selectedNote) {
                return selectedNote.noteContent;
            } else {
                return "";
            }
        }

        public void SaveNote (string noteText)
        {
            selectedNote.noteContent = noteText;
            selectedNote.Update ();
        }

        public void RefreshTableSource ()
        {
            // show most recent attachments first
            AttachmentsSource.Items = new List<NcFileIndex> ();
            AttachmentsSource.Items = McAbstrFileDesc.GetAllFiles (Account.Id);

            switch (segmentedControl.SelectedSegment) {
            case 0:
                break;
            case 1:
                AttachmentsSource.Items.Sort ((f1, f2) => DateTime.Compare (f1.CreatedAt, f2.CreatedAt));
                AttachmentsSource.Items.Reverse ();
                break;
            case 2:
                AttachmentsSource.Items = AttachmentsSource.Items.OrderBy (x => x.Contact, new EmptyContactsAreLast ()).ToList ();
                AttachmentsSource.SetItems (AttachmentsSource.Items); 
                break;
            }

            tableView.ReloadData ();
            ConfigureFilesView ();
        }

        public class EmptyContactsAreLast : IComparer<string>
        {
            public int Compare (string x, string y)
            {
                if (String.IsNullOrEmpty (y) && !String.IsNullOrEmpty (x)) {
                    return -1;
                } else if (!String.IsNullOrEmpty (y) && String.IsNullOrEmpty (x)) {
                    return 1;
                } else {
                    return String.Compare (x, y);
                }
            }
        }

        public void DownloadAndDoAction (int attachmentId, UITableViewCell cell, Action<McAttachment> attachmentAction)
        {
            // Handle completed downloads directly
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (McAbstrFileDesc.FilePresenceEnum.Complete == a.FilePresence) {
                attachmentAction (a);
                return;
            }

            // FIXME: Better status reporting
            var nr = PlatformHelpers.DownloadAttachment (a);
            var token = nr.GetValue<String> ();
            if (null == token) {
                NcAlertView.ShowMessage (this, "Download Error", "There was a problem downloading this attachment.");
                return;
            }

            Token = token; // make this the attachment that will get opened next

            // Start, or re-state, the downloading animation
            if (McAbstrFileDesc.FilePresenceEnum.Partial == a.FilePresence) {
                FilesTableViewSource.StopAnimationsOnCell (cell);
                AttachmentsSource.StartArrowAnimation (cell);
            } else {
                AttachmentsSource.StartDownloadingAnimation (cell);
            }
                
            EventHandler fileAction = null;

            // Handle download-related status inds
            fileAction = (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (null == s.Tokens) {
                    return;
                }
                if (!s.Tokens.Contains (token)) {
                    return;
                }
                // Bail out on errors
                if (NcResult.SubKindEnum.Error_AttDownloadFailed == s.Status.SubKind) {
                    NcApplication.Instance.StatusIndEvent -= fileAction;
                    FilesTableViewSource.DownloadCompleteAnimation (cell, displayAttachment: () => {
                    });
                    return;
                }
                // Open the attachment if we're done & it was last chosen
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    a = McAttachment.QueryById<McAttachment> (attachmentId); // refresh the now-downloaded attachment
                    if (McAbstrFileDesc.FilePresenceEnum.Complete == a.FilePresence) {
                        NcApplication.Instance.StatusIndEvent -= fileAction;
                        FilesTableViewSource.DownloadCompleteAnimation (cell, displayAttachment: () => {
                            if (Token == token) {
                                attachmentAction (a);
                            }
                        });
                    }
                }
            };

            NcApplication.Instance.StatusIndEvent += new EventHandler (fileAction);
        }

        public void DeleteAttachment (McAttachment attachment)
        {
            if (null != attachment) {
                if (attachment.IsInline) {
                    NcAlertView.ShowMessage (this, "File is Inline",
                        "Attachments that are contained within the body of an e-mail message cannot be deleted.");
                } else {
                    attachment.DeleteFile ();
                
                }
            }
            RefreshTableSource ();
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            var segHeight = segmentedControlView.Frame.Height;
            if (!suppressLayout) {
                if (!modal) {
                    tableView.Frame = new CGRect (0, segHeight, View.Frame.Width, View.Frame.Height - segHeight);
                } else {
                    tableView.Frame = new CGRect (0, segHeight + navbar.Frame.Height, View.Frame.Width, View.Frame.Height - (navbar.Frame.Height + segHeight));
                }
                // Initially let's hide the search controller
                tableView.SetContentOffset (new CGPoint (0.0f, 44.0f), false);
            }
            suppressLayout = false;
        }

        public void DeleteDocument (McDocument document)
        {
            if (null != document) {
                document.Delete ();
            }
            RefreshTableSource ();
        }

        public void DeleteNote (McNote note)
        {
            if (null != note) {
                note.Delete ();
            }
            RefreshTableSource ();
        }

        public void ForwardAttachment (McAttachment attachment, UITableViewCell cell)
        {
            DownloadAndDoAction (attachment.Id, cell, (a) => {
                var attachments = new List<McAttachment>();
                attachments.Add (a);
                ForwardAttachments (attachments);
            });
        }

        public void ForwardAttachments (List<McAttachment> attachments)
        {
            var account = NcApplication.Instance.DefaultEmailAccount;
            var composeViewController = new MessageComposeViewController (account);
            composeViewController.Composer.InitialAttachments = attachments;
            composeViewController.Present ();
        }

        public void OpenInOtherApp (McAttachment attachment, UITableViewCell cell)
        {
            DownloadAndDoAction (attachment.Id, cell, (a) => {
                DoOpenInOtherApp (a);
            });
        }

        // Xammit!  Looks like Preview gets GC'd
        // when its in the Preview function while
        // the UI is asking how to share the file.
        UIDocumentInteractionController Preview;

        public void DoOpenInOtherApp (McAttachment attachment)
        {
            var path = attachment.GetFilePath ();
            if (!String.IsNullOrEmpty (path)) {
                var url = NSUrl.FromFilename (path);
                // Xammit!  Preview seems to have been GC'd
                Preview = UIDocumentInteractionController.FromUrl (url);
                Preview.Delegate = new NachoClient.PlatformHelpers.DocumentInteractionControllerDelegate (this);
                if (!Preview.PresentOpenInMenu (View.Frame, View, true)) {
                    NcAlertView.ShowMessage (this, "Nacho Mail", "No viewer is available for this attachment.");
                }
            }
        }

        public void FileChooserSheet (McAbstrObject file, UIView alertParentView, Action displayAction)
        {
            NcActionSheet.Show (alertParentView, this,
                new NcAlertAction ("Preview", () => {
                    displayAction ();
                }),
                new NcAlertAction ("Add as attachment", () => {
                    Owner.SelectFile (this, file);
                }),
                new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null)
            );
        }

        public void AttachmentAction (int attachmentId, UITableViewCell cell)
        {
            DownloadAndDoAction (attachmentId, cell, (a) => {
                if (null == Owner) {
                    PlatformHelpers.DisplayAttachment (this, a);
                } else {
                    FileChooserSheet (a, cell, () => PlatformHelpers.DisplayAttachment (this, a));
                }
            });
        }

        public void DocumentAction (McDocument document, UIView alertParentView)
        {
            if (null == Owner) {
                PlatformHelpers.DisplayFile (this, document);
            } else {
                FileChooserSheet (document, alertParentView, () => PlatformHelpers.DisplayFile (this, document));
            }
        }

        public void NoteAction (McNote note, UIView alertParentView)
        {
            if (null == Owner) {
                ShowNote (note);
                return;
            }

            FileChooserSheet (note, alertParentView, () => {
                ShowNoteViewer (note);
            });
        }

        void ShowNoteViewer (McNote note)
        {
            var dc = new NotesViewerViewController ();
            selectedNote = note;
            dc.SetOwner (this);
            PresentViewController (dc, true, null);
        }

        void ShowNote (McNote note)
        {
            var dc = new NotesViewController ();
            selectedNote = note;
            dc.SetOwner (this, null, insertDate: false);
            NavigationController.PushViewController (dc, true);
        }

        private void searchClicked (object sender, EventArgs e)
        {
            SearchDisplayController.SearchBar.BecomeFirstResponder ();
        }

        private void multiClicked (object sender, EventArgs e)
        {
            ToggleMultiSelect (true);
        }

        private void openInClicked (object sender, EventArgs e)
        {
            if (0 < (AttachmentsSource.MultiSelect).Count) {
                var file = (AttachmentsSource.MultiSelect).First ();
                AttachmentsSource.OpenFileIn (file.Value, tableView.CellAt (file.Key));
            }
            EndMultiSelect ();
        }

        private void attachClicked (object sender, EventArgs e)
        {
            var tempAttachmentsList = new List<McAttachment> ();
            foreach (var entry in AttachmentsSource.MultiSelect) {
                var item = entry.Value;
                var tempAttachment = new McAttachment ();
                switch (item.FileType) {
                case 0:
                    McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);
                    if (null != attachment) {
                        tempAttachment = attachment;
                    }
                    break;
                case 1:
                    McNote note = McNote.QueryById<McNote> (item.Id);
                    if (null != note) {
                        tempAttachment = AttachmentsSource.NoteToAttachment (note);
                    }
                    break;
                case 2:
                        //McDocument document = McDocument.QueryById<McDocument> (item.Id);
                    break;
                default:
                    NcAssert.CaseError ("Attaching unknown file type");
                    break;
                }
                if (McAbstrFileDesc.FilePresenceEnum.Complete != tempAttachment.FilePresence) {
                    NcAlertView.ShowMessage (this, "Hold on!",
                        "All attachments must be downloaded before they can be attached to an e-mail message.");
                    return;
                } else {
                    tempAttachmentsList.Add (tempAttachment);
                }
            }
            ForwardAttachments (tempAttachmentsList);
            EndMultiSelect ();
        }

        private void deleteClicked (object sender, EventArgs e)
        {
            foreach (var item in AttachmentsSource.MultiSelect) {
                AttachmentsSource.DeleteFile (item.Value);
            }
            EndMultiSelect ();
        }

        private void cancelClicked (object sender, EventArgs e)
        {
            EndMultiSelect ();
        }

        private void EndMultiSelect ()
        {
            AttachmentsSource.MultiSelect.Clear ();
            ToggleMultiSelect (false);
        }

        UIView searchbarOverlay;

        private void ToggleSearchBar (bool enabled)
        {
            if (enabled) {
                searchbarOverlay.RemoveFromSuperview ();
                SearchDisplayController.SearchBar.UserInteractionEnabled = true;
            } else {
                searchbarOverlay = new UIView (new CGRect (0, 0, View.Frame.Width, 44));
                searchbarOverlay.Hidden = false;
                searchbarOverlay.BackgroundColor = UIColor.Black;
                searchbarOverlay.Alpha = 0;
                SearchDisplayController.SearchBar.AddSubview (searchbarOverlay);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        searchbarOverlay.Alpha = .3f;
                    },
                    () => {

                    }
                );
                SearchDisplayController.SearchBar.UserInteractionEnabled = false;
            }

        }

        protected bool modal;

        public void SetModal (bool modal)
        {
            this.modal = modal;
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            NcAssert.CaseError ();
        }
    }
}
