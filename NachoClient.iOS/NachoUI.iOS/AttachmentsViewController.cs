// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Linq;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using MonoTouch.CoreAnimation;

namespace NachoClient.iOS
{
    public partial class AttachmentsViewController : UIViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoNotesControllerParent, IAttachmentTableViewSourceDelegate
    {
        public AttachmentsViewController (IntPtr handle) : base (handle)
        {
        }

        INachoFileChooserParent Owner;
        protected McAccount account;
        AttachmentsTableViewSource AttachmentsSource;
         
        string Token;

        protected McNote selectedNote;

        protected UITableView tableView;
        protected UISegmentedControl segmentedControl;
        protected UIView segmentedControlView;

        UILabel EmptyListLabel;

        // segue id's
        string FilesToComposeSegueId = "AttachmentsToCompose";
        string FilesToNotesSegueId = "AttachmentsToNotes";

        // animation constants
        public float AnimationDuration = 3.0f;

        UIBarButtonItem searchButton;
        UIBarButtonItem multiSelectButton;
        UIBarButtonItem multiOpenInButton;
        UIBarButtonItem multiAttachButton;
        UIBarButtonItem multiDeleteButton;
        UIBarButtonItem multiCancelButton;
        public bool isMultiSelecting;

        private bool suppressLayout;

        UINavigationBar navbar = new UINavigationBar ();

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void SetOwner (INachoFileChooserParent owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void DismissFileChooser (bool animated, NSAction action)
        {
            Owner = null;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NavigationItem.SetHidesBackButton (true, false);
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            CreateView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            RefreshTableSource ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            // In case we exit during scrolling
            NachoCore.Utils.NcAbate.RegularPriority ("AttachmentsViewController ViewWillDisappear");
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: AttachmentsSetChanged");
                RefreshTableSource ();
            }
        }

        private void CreateView ()
        {
            var yOffset = 0f;
            searchButton = new UIBarButtonItem (UIBarButtonSystemItem.Search);
            multiSelectButton = new UIBarButtonItem ();
            multiOpenInButton = new UIBarButtonItem ();
            multiAttachButton = new UIBarButtonItem ();
            multiDeleteButton = new UIBarButtonItem ();
            multiCancelButton = new UIBarButtonItem ();

            if (modal) {
                navbar.Frame = new RectangleF (0, 0, View.Frame.Width, 64);
                View.AddSubview (navbar);
                navbar.BackgroundColor = A.Color_NachoGreen;
                navbar.Translucent = false;
                UINavigationItem title = new UINavigationItem ("Attach file");
                navbar.SetItems (new UINavigationItem[]{ title }, false);
                UIBarButtonItem cancelButton = new UIBarButtonItem ();
                Util.SetAutomaticImageForButton (cancelButton, "icn-close");

                navbar.TopItem.LeftBarButtonItem = cancelButton;
                navbar.TopItem.RightBarButtonItem = searchButton;
                cancelButton.Clicked += (object sender, EventArgs e) => {
                    DismissViewController (true, null);
                };
                yOffset += navbar.Frame.Height;
            } else {
                NavigationItem.Title = "Files";
                Util.ConfigureNavBar (false, this.NavigationController);
            }

            segmentedControlView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 40));
            segmentedControlView.BackgroundColor = UIColor.White;

            segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new RectangleF (6, 5, View.Frame.Width - 12, 30);
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

            tableView = new UITableView (new RectangleF (0, 0, 0, 0), UITableViewStyle.Grouped);
            tableView.SeparatorColor = UIColor.Clear;

            InitializeSearchDisplayController ();
            AttachmentsSource = new AttachmentsTableViewSource (this, account);
            AttachmentsSource.SetOwner (this, SearchDisplayController);

            View.AddSubview (tableView);
            // set up the table view source
            tableView.Source = AttachmentsSource;
            SearchDisplayController.SearchResultsTableView.Source = AttachmentsSource;

            searchButton.TintColor = A.Color_NachoBlue;
            NavigationItem.LeftBarButtonItem = searchButton;
            searchButton.Clicked += searchClicked;
                
            multiSelectButton.TintColor = A.Color_NachoBlue;
            multiSelectButton.Image = UIImage.FromBundle ("folder-edit");
            NavigationItem.RightBarButtonItem = multiSelectButton;
            multiSelectButton.Clicked += multiClicked;

            multiOpenInButton.TintColor = A.Color_NachoBlue;
            multiOpenInButton.Image = UIImage.FromBundle ("files-open-in-app");
            multiOpenInButton.Clicked += openInClicked;

            multiAttachButton.TintColor = A.Color_NachoBlue;
            multiAttachButton.Image = UIImage.FromBundle ("files-email-attachment");
            multiAttachButton.Clicked += attachClicked;

            multiDeleteButton.TintColor = A.Color_NachoBlue;
            multiDeleteButton.Image = UIImage.FromBundle ("gen-delete-all");
            multiDeleteButton.Clicked += deleteClicked;

            multiCancelButton.TintColor = A.Color_NachoBlue;
            multiCancelButton.Image = UIImage.FromBundle ("gen-close");
            multiCancelButton.Clicked += cancelClicked;

            EmptyListLabel = new UILabel (new RectangleF (0, 80, UIScreen.MainScreen.Bounds.Width, 20));
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
                NavigationItem.Title = "";
                ToggleSearchBar (false);
                isMultiSelecting = true;
                AttachmentsSource.IsMultiSelecting = true;
                ConfigureMultiSelectNavBar (true, 0);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new PointF (segmentedControlView.Center.X, segmentedControlView.Center.Y - segmentedControlView.Frame.Height);
                        tableView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
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
                NavigationItem.Title = "Files";
                ToggleSearchBar (true);
                isMultiSelecting = false;
                AttachmentsSource.IsMultiSelecting = false;
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new PointF (segmentedControlView.Center.X, segmentedControlView.Center.Y + segmentedControlView.Frame.Height);
                        tableView.Frame = new RectangleF (0, segmentedControlView.Frame.Height, View.Frame.Width, View.Frame.Height - segmentedControlView.Frame.Height);
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

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals (FilesToComposeSegueId)) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                var attachments = (List<McAttachment>)holder.value;

                dc.SetEmailPresetFields (attachmentList: attachments);
                return;
            }

            if (segue.Identifier.Equals (FilesToNotesSegueId)) {
                var dc = (NotesViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                selectedNote = (McNote)holder.value;

                dc.SetOwner (this, true);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
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

        public void SaveNote (int accountId, string noteText)
        {
            selectedNote.noteContent = noteText;
            selectedNote.Update ();
        }

        public void RefreshTableSource ()
        {
            // show most recent attachments first
            AttachmentsSource.Items = new List<NcFileIndex> ();
            AttachmentsSource.Items = McAbstrFileDesc.GetAllFiles (account.Id);

            switch (segmentedControl.SelectedSegment) {
            case 0:
                break;
            case 1:
                AttachmentsSource.Items.Sort ((f1, f2) => DateTime.Compare (f1.CreatedAt, f2.CreatedAt));
                AttachmentsSource.Items.Reverse ();
                break;
            case 2:
                AttachmentsSource.Items.Sort ((x, y) => x.Contact.CompareTo (y.Contact));
                AttachmentsSource.SetItems (AttachmentsSource.Items);
                break;
            }

            tableView.ReloadData ();
            ConfigureFilesView ();
        }

        public void DownloadAndDoAction (int attachmentId, UITableViewCell cell, Action<McAttachment> attachmentAction)
        {
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (McAbstrFileDesc.FilePresenceEnum.Complete != a.FilePresence) {
                if (McAbstrFileDesc.FilePresenceEnum.Partial == a.FilePresence) {
                    // replace animations if one is already going
                    AttachmentsTableViewSource.StopAnimationsOnCell (cell);
                    AttachmentsSource.StartArrowAnimation (cell);
                } else {
                    AttachmentsSource.StartDownloadingAnimation (cell);
                }
                string token = PlatformHelpers.DownloadAttachment (a);
                Token = token; // make this the attachment that will get opened next
                NcAssert.NotNull (Token, "Found token should not be null");
                EventHandler fileAction = null;

                // prepare to do action on the most recently clicked item
                fileAction = (object sender, EventArgs e) => {
                    var s = (StatusIndEventArgs)e;
                    var eventTokens = s.Tokens;

                    // open attachment if the statusInd says this attachment has downloaded
                    if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind && eventTokens.Contains (token)) {
                        a = McAttachment.QueryById<McAttachment> (attachmentId); // refresh the now-downloaded attachment
                        if (McAbstrFileDesc.FilePresenceEnum.Complete == a.FilePresence) {
                            // wait until download-complete animation finishes to do the attachment action
                            AttachmentsTableViewSource.DownloadCompleteAnimation (cell, displayAttachment: () => {
                                // check if this is still the next attachment we want to open
                                if (Token == token) {
                                    attachmentAction (a);
                                }
                            });
                        } else {
                            NcAssert.True (false, "Item should have been downloaded at this point");
                        }
                    }
                    NcApplication.Instance.StatusIndEvent -= fileAction;
                    var tempA = McAttachment.QueryById<McAttachment> (attachmentId);
                    if (McAbstrFileDesc.FilePresenceEnum.Complete == tempA.FilePresence) {
                        AttachmentsTableViewSource.DownloadCompleteAnimation (cell, displayAttachment: () => {
                        });
                    }
                };

                NcApplication.Instance.StatusIndEvent += new EventHandler (fileAction);
                return;
            } else {
                attachmentAction (a);
            }
        }

        public void DeleteAttachment (McAttachment attachment)
        {
            if (null != attachment) {
                if (attachment.IsInline) {
                    UIAlertView alert = new UIAlertView (
                                            "File is Inline", 
                                            "Attachments that are contained within the body of an email cannot be deleted", 
                                            null, 
                                            "OK"
                                        );
                    alert.Show ();
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
                    tableView.Frame = new RectangleF (0, segHeight, View.Frame.Width, View.Frame.Height - segHeight);
                } else {
                    tableView.Frame = new RectangleF (0, segHeight + navbar.Frame.Height, View.Frame.Width, View.Frame.Height - (navbar.Frame.Height + segHeight));
                }
                // Initially let's hide the search controller
                tableView.SetContentOffset (new PointF (0.0f, 44.0f), false);
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
                PerformSegue (FilesToComposeSegueId, new SegueHolder (a));
            });
        }

        public void ForwardAttachments (List<McAttachment> attachments)
        {
            PerformSegue (FilesToComposeSegueId, new SegueHolder (attachments));
        }

        public void OpenInOtherApp (McAttachment attachment, UITableViewCell cell)
        {
            DownloadAndDoAction (attachment.Id, cell, (a) => {
                UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (a.GetFilePath ()));
                Preview.Delegate = new NachoClient.PlatformHelpers.DocumentInteractionControllerDelegate (this);
                Preview.PresentOpenInMenu (View.Frame, View, true);
            });
        }

        public void FileChooserSheet (McAbstrObject file, Action displayAction)
        {
            // We're in "chooser' mode & the attachment is downloaded
            var actionSheet = new UIActionSheet ();
            actionSheet.TintColor = A.Color_NachoBlue;
            actionSheet.Add ("Preview");
            actionSheet.Add ("Add as attachment");
            actionSheet.Add ("Cancel");
            actionSheet.CancelButtonIndex = 2;

            actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    displayAction ();
                    break; 
                case 1:
                    Owner.SelectFile (this, file);
                    break;
                case 2:
                    break; // Cancel
                default:
                    NcAssert.CaseError ();
                    break;
                }
            };

            actionSheet.ShowInView (View);
        }

        public void AttachmentAction (int attachmentId, UITableViewCell cell)
        {
            DownloadAndDoAction (attachmentId, cell, (a) => {
                if (null == Owner) {
                    PlatformHelpers.DisplayAttachment (this, a);
                    return;
                }
                FileChooserSheet (a, () => PlatformHelpers.DisplayAttachment (this, a));
            });
        }

        public void DocumentAction (McDocument document)
        {
            if (null == Owner) {
                PlatformHelpers.DisplayFile (this, document);
                return;
            }

            FileChooserSheet (document, () => PlatformHelpers.DisplayFile (this, document));
        }

        public void NoteAction (McNote note)
        {
            if (null == Owner) {
                PerformSegue (FilesToNotesSegueId, new SegueHolder (note));
                return;
            }

            FileChooserSheet (note, () => {
                PerformSegue (FilesToNotesSegueId, new SegueHolder (note));
            });
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
                    UIAlertView alert = new UIAlertView (
                                            "Hold on!", 
                                            "All attachments must be downloaded before they can be attached to an email.", 
                                            null, 
                                            "OK"
                                        );
                    alert.Show ();
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
                searchbarOverlay = new UIView (new RectangleF (0, 0, 320, 44));
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

        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            NcAssert.CaseError ();
        }
    }
}
