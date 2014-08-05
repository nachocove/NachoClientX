using System;
using System.IO;
using System.Linq;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using MonoTouch.CoreAnimation;

namespace NachoClient.iOS
{
    public partial class FilesViewController : NcUITableViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate
    {
        INachoFileChooserParent owner;
        FilesTableSource filesSource;
        SearchDelegate searchDelegate;
        Action<object, EventArgs> fileAction;
        public ItemType itemType;

        // set by caller
        public enum ItemType {Attachment = 1, Note, Document};

        // segue ids
        string FilesToComposeSegueId = "FilesToEmailCompose";

        public FilesViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void SetOwner (INachoFileChooserParent owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void DismissFileChooser (bool animated, NSAction action)
        {
            var controllers = this.NavigationController.ViewControllers;
            int currentVC = controllers.Count () - 1; // take 0 indexing into account
            NavigationController.PopToViewController (controllers [currentVC - 2], true); // pop 2 views: one for attachments page, one for hierarchy
            owner = null;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NcAssert.True (itemType != 0, "Item type should be set before transitioning to FilesViewController");

            // set up the table view source
            filesSource = new FilesTableSource (this);
            TableView.Source = filesSource;

            // set up the search bar
            searchDelegate = new SearchDelegate (filesSource);
            SearchDisplayController.Delegate = searchDelegate;
            SearchDisplayController.SearchResultsSource = filesSource;
            SearchDisplayController.SearchBar.SearchButtonClicked += (s, e) => { SearchDisplayController.SearchBar.ResignFirstResponder(); };

            // Initially let's hide the search controller
            TableView.SetContentOffset (new PointF (0.0f, 44.0f), false);

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    RefreshTableSource ();
                }
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            RefreshTableSource ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            // remove any remaining file actions before leaving
            if (fileAction != null) {
                NcApplication.Instance.StatusIndEvent -= new EventHandler (fileAction);
            }
            fileAction = null;
            base.ViewWillDisappear (animated);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals (FilesToComposeSegueId)) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                var attachment = (McAttachment)holder.value;

                dc.SetEmailPresetFields (attachment: attachment);
            }
        }

        public void RefreshTableSource ()
        {
            // show most recent attachments first
            filesSource.Items = new List<IFilesViewItem> ();

            switch (itemType) {
            case ItemType.Attachment:
                filesSource.Items.AddRange (NcModel.Instance.Db.Table<McAttachment> ().OrderByDescending (a => a.Id));
                break;
            case ItemType.Note:
                filesSource.Items.AddRange (NcModel.Instance.Db.Table<McNote> ().Where (a => a.noteType == McNote.NoteType.Event)
                    .OrderByDescending (a => a.Id));
                break;
            case ItemType.Document:
                filesSource.Items.AddRange (NcModel.Instance.Db.Table<McDocument> ().OrderByDescending (a => a.Id));
                break;
            }

            SearchDisplayController.SearchResultsTableView.ReloadData ();
            if (searchDelegate != null && searchDelegate.searchString != null) {
                searchDelegate.ShouldReloadForSearchString (SearchDisplayController, searchDelegate.searchString);
            }
            base.TableView.ReloadData ();
        }

        // TODO: make this animation look like the design spec in Dropbox
        public CABasicAnimation DownloadAnimation ()
        {
            CABasicAnimation rotation = CABasicAnimation.FromKeyPath ("transform.rotation");
            rotation.From = NSNumber.FromFloat (0.0F);
            rotation.To = NSNumber.FromDouble (2.0 * Math.PI);
            rotation.Duration = 1.1; // Speed
            rotation.RepeatCount = 10000; // Repeat forever. Can be a finite number.
            return rotation;
        }

        public void DownloadAndDoAction (int attachmentId, Action<McAttachment> attachmentAction)
        {
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (!a.IsDownloaded) {
                string token = PlatformHelpers.DownloadAttachment (a);
                NcAssert.NotNull (token, "Found token should not be null");
                // If another download action has been registered, don't do action on it
                if (fileAction != null) {
                    NcApplication.Instance.StatusIndEvent -= new EventHandler (fileAction);
                }
                // prepare to do action on the most recently clicked item
                fileAction = (object sender, EventArgs e) => {
                    var s = (StatusIndEventArgs)e;
                    var eventTokens = s.Tokens;
                    if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind && eventTokens.Contains (token)) {
                        a = McAttachment.QueryById<McAttachment> (attachmentId); // refresh the now-downloaded attachment
                        if (a.IsDownloaded) {
                            attachmentAction (a);
                        } else {
                            NcAssert.True (false, "Item should have been downloaded at this point");
                        }
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
            if (attachment.IsInline) {
                UIAlertView alert = new UIAlertView (
                    "File is Inline", 
                    "Attachments that are contained within the body of an email cannot be deleted", 
                    null, 
                    "OK"
                );
                alert.Show();
            } else {
                attachment.RemoveFromStorage ();
                RefreshTableSource ();
            }
        }

        public void DeleteDocument (McDocument document)
        {
            document.Delete ();
            RefreshTableSource ();
        }

        public void ForwardAttachment (McAttachment attachment)
        {
            DownloadAndDoAction (attachment.Id, (a) => {
                PerformSegue (FilesToComposeSegueId, new SegueHolder (a));
            });
        }

        public void OpenInOtherApp (McAttachment attachment)
        {
            DownloadAndDoAction (attachment.Id, (a) => {
                UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (a.FilePath ()));
                Preview.Delegate = new NachoClient.PlatformHelpers.DocumentInteractionControllerDelegate (this);
                Preview.PresentOpenInMenu (View.Frame, View, true);
            });
        }

        public void AttachmentAction (int attachmentId)
        {
            DownloadAndDoAction (attachmentId, (a) => {
                if (null == owner) {
                    PlatformHelpers.DisplayAttachment (this, a);
                    return;
                }

                // We're in "chooser' mode & the attachment is downloaded
                var actionSheet = new UIActionSheet ();
                actionSheet.TintColor = A.Color_NachoBlue;
                actionSheet.Add ("Preview");
                actionSheet.Add ("Select Attachment");
                actionSheet.Add ("Cancel");
                actionSheet.CancelButtonIndex = 2;

                actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                    switch (b.ButtonIndex) {
                    case 0:
                        PlatformHelpers.DisplayAttachment (this, a);
                        break; 
                    case 1:
                        owner.SelectFile (this, a);
                        break;
                    case 2:
                        break; // Cancel
                    default:
                        NcAssert.CaseError ();
                        break;
                    }
                };

                actionSheet.ShowInView (View);
            });
        }
            
        protected class FilesTableSource : UITableViewSource
        {
            // cell Id's
            const string FileCell = "FileCell";

            protected List<IFilesViewItem> items;
            protected List<IFilesViewItem> searchResults;

            FilesViewController vc;

            public List<IFilesViewItem> Items
            {
                get { return items; }
                set { items = value; }
            }

            public List<IFilesViewItem> SearchResults
            {
                get { return searchResults; }
                set { searchResults = value; }
            }

            public FilesTableSource (FilesViewController vc)
            {
                this.vc = vc;
                Items = new List<IFilesViewItem> ();
                SearchResults = new List<IFilesViewItem> ();
            }

            public override int RowsInSection (UITableView tableview, int section)
            {
                if (tableview == vc.SearchDisplayController.SearchResultsTableView) {
                    return SearchResults.Count;
                } else {
                    return Items.Count;
                }
            }

            public override int NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return 50.0f;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                UITableViewCell cell = null;
                cell = tableView.DequeueReusableCell (FileCell);
                if (cell == null) {
                    cell = new MCSwipeTableViewCell (UITableViewCellStyle.Subtitle, FileCell);
                }
                NcAssert.True (null != cell);

                IFilesViewItem item;

                // determine if table is for search results or all attachments
                if (tableView == vc.SearchDisplayController.SearchResultsTableView) {
                    item = SearchResults [indexPath.Row];
                } else {
                    item = Items [indexPath.Row];
                }

                cell.TextLabel.Text = item.DisplayName;

                switch (vc.itemType) {
                case ItemType.Attachment:
                    cell = FormatAttachmentCell (cell, item as McAttachment);
                    ConfigureSwipes (cell as MCSwipeTableViewCell, item);
                    break;
                case ItemType.Note:
                    cell = FormatNoteCell (cell, item as McNote);
                    break;
                case ItemType.Document:
                    ConfigureSwipes (cell as MCSwipeTableViewCell, item);
                    cell = FormatDocumentCell (cell, item as McDocument);
                    break;
                }

                // styling
                cell.TextLabel.TextColor = A.Color_NachoBlack;
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                cell.DetailTextLabel.TextColor = UIColor.LightGray;
                cell.DetailTextLabel.Font = A.Font_AvenirNextRegular14;

                return cell;
            }

            private UITableViewCell FormatAttachmentCell (UITableViewCell cell, McAttachment attachment)
            {
                cell.TextLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);

                cell.DetailTextLabel.Text = "";
                if (attachment.IsInline) {
                    cell.DetailTextLabel.Text += "Inline ";
                }
                string extension = Path.GetExtension (attachment.DisplayName).ToUpper ();
                cell.DetailTextLabel.Text += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
                cell.DetailTextLabel.Text += "file";

                if (attachment.IsDownloaded || attachment.IsInline) {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-complete.png");
                    cell.ImageView.Layer.RemoveAllAnimations ();
                } else if (attachment.PercentDownloaded > 0 && attachment.PercentDownloaded < 100) {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-download.png");
                    SetAnimationOnCell (cell, attachment.IsDownloaded);
                } else {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-download.png");
                }
                return cell;
            }

            private UITableViewCell FormatNoteCell (UITableViewCell cell, McNote note)
            {
                cell.DetailTextLabel.Text = note.noteContent;
                cell.ImageView.Image = UIImage.FromFile ("icn-file-complete.png");
                return cell;
            }

            private UITableViewCell FormatDocumentCell (UITableViewCell cell, McDocument document)
            {
                cell.DetailTextLabel.Text = document.SourceApplication;
                cell.ImageView.Image = UIImage.FromFile ("icn-file-complete.png");
                return cell;
            }
                
            public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
            {
                IFilesViewItem item;
                if (tableView == vc.SearchDisplayController.SearchResultsTableView) {
                    item = SearchResults [indexPath.Row];
                } else {
                    item = Items [indexPath.Row];
                }

                switch (vc.itemType) {
                case ItemType.Attachment:
                    McAttachment att = (McAttachment)item;
                    vc.AttachmentAction (att.Id);
                    if (!att.IsDownloaded) {
                        var rotation = vc.DownloadAnimation ();
                        tableView.CellAt (indexPath).ImageView.Layer.AddAnimation (rotation, "downloadAnimation");
                    }
                    break;
                case ItemType.Note:
                    McNote note = (McNote)item;
                    // TODO: Add segue to edit notes view
                    break;
                case ItemType.Document:
                    McDocument document = (McDocument)item;
                    PlatformHelpers.DisplayFile (vc, document);
                    break;
                }

                tableView.DeselectRow (indexPath, true);
            }
                
            /// <summary>
            /// Configures the swipes.
            /// </summary>
            void ConfigureSwipes (MCSwipeTableViewCell cell, IFilesViewItem item)
            {
                cell.FirstTrigger = 0.20f;
                cell.SecondTrigger = 0.50f;

                UIView forwardView = null;
                UIColor greenColor = null;
                UIView crossView = null;
                UIColor redColor = null;
                UIView previewView = null;
                UIColor yellowColor = null;
                UIView openView = null;
                UIColor brownColor = null;

                try { 
                    forwardView = ViewWithImageName ("check");
                    greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (forwardView, greenColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.ForwardAttachment (attachment);
                            SetAnimationOnCell (cell, attachment.IsDownloaded);
                        }
                        return;
                    });
                    crossView = ViewWithImageName ("cross");
                    redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        switch (vc.itemType) {
                        case ItemType.Attachment:
                            McAttachment attachment = (McAttachment)item;
                            vc.DeleteAttachment (attachment);
                            break;
                        case ItemType.Document:
                            McDocument document = (McDocument)item;
                            vc.DeleteDocument (document);
                            break;
                        }
                        return;
                    });
                    previewView = ViewWithImageName ("clock");
                    yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (previewView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.AttachmentAction (attachment.Id);
                            SetAnimationOnCell (cell, attachment.IsDownloaded);
                        }
                        return;
                    });
                    openView = ViewWithImageName ("list");
                    brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (openView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.OpenInOtherApp (attachment);
                            SetAnimationOnCell (cell, attachment.IsDownloaded);
                        }
                        return;
                    });
                } finally {
                    if (null != forwardView) {
                        forwardView.Dispose ();
                    }
                    if (null != greenColor) {
                        greenColor.Dispose ();
                    }
                    if (null != crossView) {
                        crossView.Dispose ();
                    }
                    if (null != redColor) {
                        redColor.Dispose ();
                    }
                    if (null != previewView) {
                        previewView.Dispose ();
                    }
                    if (null != yellowColor) {
                        yellowColor.Dispose ();
                    }
                    if (null != openView) {
                        openView.Dispose ();
                    }
                    if (null != brownColor) {
                        brownColor.Dispose ();
                    }
                }
            }


            private void SetAnimationOnCell (UITableViewCell cell, bool isDownloaded)
            {
                if (!isDownloaded) {
                    var rotation = vc.DownloadAnimation ();
                    cell.ImageView.Layer.AddAnimation (rotation, "downloadAnimation");
                }
            }

            UIView ViewWithImageName (string imageName)
            {
                var image = UIImage.FromBundle (imageName);
                var imageView = new UIImageView (image);
                imageView.ContentMode = UIViewContentMode.Center;
                return imageView;
            }
        }

        protected class SearchDelegate : UISearchDisplayDelegate
        {
            FilesTableSource filesSource;
            public string searchString;

            public SearchDelegate (FilesTableSource filesSource)
            {
                this.filesSource = filesSource;
                this.searchString = null;
            }

            public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
            {
                filesSource.SearchResults = filesSource.Items.Where (w => w.DisplayName.Contains (forSearchString)).ToList ();
                searchString = forSearchString;
                controller.SearchResultsTableView.ReloadData ();
                return true;
            }
        }
    }
}
