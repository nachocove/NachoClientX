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
    public partial class FilesViewController : NcUITableViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoNotesControllerParent
    {
        INachoFileChooserParent Owner;
        FilesTableSource FilesSource;
        SearchDelegate searchDelegate;
        string Token;
        public ItemType itemType;

        protected McNote selectedNote;

        UILabel EmptyListLabel;

        // set by caller
        public enum ItemType {Attachment = 1, Note, Document};

        // segue id's
        string FilesToComposeSegueId = "FilesToEmailCompose";
        string FilesToNotesSegueId = "FilesToNotes";

        // animation constants
        public float AnimationDuration = 3.0f;
   
        public FilesViewController (IntPtr handle) : base (handle)
        {
        }

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
            var controllers = this.NavigationController.ViewControllers;
            int currentVC = controllers.Count () - 1; // take 0 indexing into account
            NavigationController.PopToViewController (controllers [currentVC - 2], true); // pop 2 views: one for attachments page, one for hierarchy
            Owner = null;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NcAssert.True (itemType != 0, "Item type should be set before transitioning to FilesViewController");

            // set up the table view source
            FilesSource = new FilesTableSource (this);
            TableView.Source = FilesSource;

            // set up the search bar
            searchDelegate = new SearchDelegate (FilesSource);
            SearchDisplayController.Delegate = searchDelegate;
            SearchDisplayController.SearchResultsSource = FilesSource;
            SearchDisplayController.SearchBar.SearchButtonClicked += (s, e) => { SearchDisplayController.SearchBar.ResignFirstResponder(); };

            // Initially let's hide the search controller
            TableView.SetContentOffset (new PointF (0.0f, 44.0f), false);

            EmptyListLabel = new UILabel (new RectangleF (0, 80, UIScreen.MainScreen.Bounds.Width, 20));
            EmptyListLabel.TextAlignment = UITextAlignment.Center;
            EmptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            EmptyListLabel.TextColor = A.Color_NachoBorderGray;
            EmptyListLabel.Hidden = true;
            View.AddSubview (EmptyListLabel);

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    RefreshTableSource ();
                }
            };
        }

        private void ConfigureFilesView ()
        {
            this.TableView.TableFooterView = new UIView (new System.Drawing.RectangleF (0, 0, 0, 0));
            if (FilesSource.Items.Count == 0) {
                this.TableView.ScrollEnabled = false;
                EmptyListLabel.Hidden = false;
                switch (itemType) {
                case ItemType.Attachment:
                    EmptyListLabel.Text = "No attachments";
                    break;
                case ItemType.Document:
                    EmptyListLabel.Text = "No documents";
                    break;
                case ItemType.Note:
                    EmptyListLabel.Text = "No notes";
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
            } else {
                this.TableView.ScrollEnabled = true;
                EmptyListLabel.Hidden = true;
            }
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
            base.ViewWillDisappear (animated);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals (FilesToComposeSegueId)) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                var attachment = (McAttachment)holder.value;

                dc.SetEmailPresetFields (attachment: attachment);
                return;
            }

            if (segue.Identifier.Equals (FilesToNotesSegueId)) {
                var dc = (NotesViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                selectedNote = (McNote)holder.value;

                dc.SetOwner (this);
                return;
            }
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
            FilesSource.Items = new List<IFilesViewItem> ();

            switch (itemType) {
            case ItemType.Attachment:
                NavigationItem.Title = "Attachments";
                FilesSource.Items.AddRange (NcModel.Instance.Db.Table<McAttachment> ().OrderByDescending (a => a.Id));
                break;
            case ItemType.Note:
                NavigationItem.Title = "Notes";
                FilesSource.Items.AddRange (NcModel.Instance.Db.Table<McNote> ().Where (a => a.noteType == McNote.NoteType.Event)
                    .OrderByDescending (a => a.Id));
                break;
            case ItemType.Document:
                NavigationItem.Title = "Shared Files";
                FilesSource.Items.AddRange (NcModel.Instance.Db.Table<McDocument> ().OrderByDescending (a => a.Id));
                break;
            }

            SearchDisplayController.SearchResultsTableView.ReloadData ();
            if (searchDelegate != null && searchDelegate.searchString != null) {
                searchDelegate.ShouldReloadForSearchString (SearchDisplayController, searchDelegate.searchString);
            }
            base.TableView.ReloadData ();
            ConfigureFilesView ();
        }

        public void DownloadAndDoAction (int attachmentId, UITableViewCell cell, Action<McAttachment> attachmentAction)
        {
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (McAbstrFileDesc.FilePresenceEnum.Complete != a.FilePresence) {
                if (McAbstrFileDesc.FilePresenceEnum.Partial == a.FilePresence) {
                    // replace animations if one is already going
                    FilesTableSource.StopAnimationsOnCell (cell);
                    FilesSource.StartArrowAnimation (cell);
                } else {
                    FilesSource.StartDownloadingAnimation (cell);
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
                            FilesTableSource.DownloadCompleteAnimation (cell, displayAttachment: () => {
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
                attachment.DeleteFile ();
                RefreshTableSource ();
            }
        }

        public void DeleteDocument (McDocument document)
        {
            document.Delete ();
            RefreshTableSource ();
        }

        public void ForwardAttachment (McAttachment attachment, UITableViewCell cell)
        {
            DownloadAndDoAction (attachment.Id, cell, (a) => {
                PerformSegue (FilesToComposeSegueId, new SegueHolder (a));
            });
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

        protected class FilesTableSource : UITableViewSource
        {
            // cell Id's
            const string FileCell = "FileCell";

            protected List<IFilesViewItem> items;
            protected List<IFilesViewItem> searchResults;

            FilesViewController vc;

            // icon id's
            string DownloadIcon = "downloadicon.png";
            public static string DownloadCompleteIcon = "icn-file-complete.png";
            string DownloadArrow = "downloadarrow.png";
            string DownloadLine = "downloadline.png";
            string DownloadCircle = "downloadcircle.png";

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
                    if (vc.Owner == null) {
                        ConfigureSwipes (cell as MCSwipeTableViewCell, item);
                    }
                    break;
                case ItemType.Note:
                    cell = FormatNoteCell (cell, item as McNote);
                    break;
                case ItemType.Document:
                    cell = FormatDocumentCell (cell, item as McDocument);
                    if (vc.Owner == null) {
                        ConfigureSwipes (cell as MCSwipeTableViewCell, item);
                    }
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
                // sanitize file name so that /'s in display name don't cause formatting issues in the cells
                string displayName = attachment.DisplayName.SantizeFileName ();
                cell.TextLabel.Text = Path.GetFileNameWithoutExtension (displayName);

                cell.DetailTextLabel.Text = "";
                if (attachment.IsInline) {
                    cell.DetailTextLabel.Text += "Inline ";
                }
                string extension = Path.GetExtension (attachment.DisplayName).ToUpper ();
                cell.DetailTextLabel.Text += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
                cell.DetailTextLabel.Text += "file";

                if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence || attachment.IsInline) {
                    cell.ImageView.Image = UIImage.FromFile (DownloadCompleteIcon);
                } else if (McAbstrFileDesc.FilePresenceEnum.Partial == attachment.FilePresence) {
                    vc.AttachmentAction (attachment.Id, cell);
                } else {
                    cell.ImageView.Image = UIImage.FromFile (DownloadIcon);
                }
                return cell;
            }

            private UITableViewCell FormatNoteCell (UITableViewCell cell, McNote note)
            {
                cell.DetailTextLabel.Text = note.noteContent;
                cell.ImageView.Image = UIImage.FromFile (DownloadCompleteIcon);
                return cell;
            }

            private UITableViewCell FormatDocumentCell (UITableViewCell cell, McDocument document)
            {
                cell.DetailTextLabel.Text = document.SourceApplication;
                cell.ImageView.Image = UIImage.FromFile (DownloadCompleteIcon);
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
                    UITableViewCell cell = tableView.CellAt (indexPath);
                    vc.AttachmentAction (att.Id, cell);
                    break;
                case ItemType.Note:
                    McNote note = (McNote)item;
                    vc.NoteAction (note);
                    break;
                case ItemType.Document:
                    McDocument document = (McDocument)item;
                    vc.DocumentAction (document);
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
                    forwardView = ViewWithImageName ("forwardicon");
                    greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (forwardView, greenColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.ForwardAttachment (attachment, cell);
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
                    previewView = ViewWithImageName ("previewicon");
                    yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (previewView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.AttachmentAction (attachment.Id, cell);
                        }
                        return;
                    });
                    openView = ViewWithImageName ("openicon");
                    brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (openView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        if (vc.itemType == ItemType.Attachment) {
                            McAttachment attachment = (McAttachment)item;
                            vc.OpenInOtherApp (attachment, cell);
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


            public static void StopAnimationsOnCell (UITableViewCell cell)
            {
                foreach (UIView subview in cell.ImageView.Subviews) {
                    subview.Layer.RemoveAllAnimations ();
                    subview.RemoveFromSuperview ();
                }
            }

            UIView ViewWithImageName (string imageName)
            {
                var image = UIImage.FromBundle (imageName);
                var imageView = new UIImageView (image);
                imageView.ContentMode = UIViewContentMode.Center;
                return imageView;
            }

            // Do arrow with line animation followed by repeating arrow-only animations
            public void StartDownloadingAnimation (UITableViewCell cell)
            {
                cell.ImageView.Image = UIImage.FromFile (DownloadCircle);
                UIImageView line =  new UIImageView (UIImage.FromBundle (DownloadLine));
                UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
                cell.ImageView.AddSubview (line);
                cell.ImageView.AddSubview (arrow);

                PointF center = line.Center;
                UIView.Animate (
                    duration: 0.4, 
                    delay: 0, 
                    options: UIViewAnimationOptions.CurveEaseIn,
                    animation: () => {
                        line.Center = new PointF (center.X, cell.ImageView.Image.Size.Height * 3 / 4);
                        arrow.Center = new PointF (center.X, cell.ImageView.Image.Size.Height * 3 / 4);
                        line.Alpha = 0.0f;
                        arrow.Alpha = 0.4f;
                    },
                    completion: () => {
                        arrow.Center = new PointF (center.X, 2);
                        arrow.Alpha = 1.0f;
                        ArrowAnimation (cell, arrow, center);
                    }
               );
            }

            // Start only the arrow animation
            public void StartArrowAnimation (UITableViewCell cell)
            {
                cell.ImageView.Image = UIImage.FromFile (DownloadCircle);
                UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
                cell.ImageView.AddSubview (arrow);

                ArrowAnimation (cell, arrow, arrow.Center);
            }

            private static void ArrowAnimation (UITableViewCell cell, UIImageView arrow, PointF center)
            {
                UIView.Animate (
                    duration: 0.4,
                    delay: 0,
                    options: UIViewAnimationOptions.CurveEaseIn,
                    animation: () => {
                        arrow.Center = new PointF (center.X, cell.ImageView.Image.Size.Height * 3 / 4);
                        arrow.Alpha = 0.4f;
                    },
                    completion: () => {
                        arrow.Center = new PointF (center.X, 2);
                        arrow.Alpha = 1.0f;
                        ArrowAnimation (cell, arrow, center);
                    }
                );
            }

            public static void DownloadCompleteAnimation (UITableViewCell cell, Action displayAttachment)
            {
                // Place the download icon in a separate view on the screen and animate it
                FilesTableSource.StopAnimationsOnCell (cell);
                var imageView = new UIImageView (new RectangleF (cell.ImageView.Frame.Width / 2, cell.ImageView.Frame.Height / 2, cell.ImageView.Frame.Width, cell.ImageView.Frame.Height));
                imageView.Center = cell.ImageView.Center;
                imageView.Image = UIImage.FromFile (FilesTableSource.DownloadCompleteIcon);
                cell.ImageView.Alpha = 0.0f;
                cell.ContentView.AddSubview (imageView);

                Action<double, Action, Action> transformAnimation = (duration, transformAction, transformComplete) => UIView.Animate (
                    duration: duration,
                    delay: 0,
                    options: UIViewAnimationOptions.CurveEaseIn,
                    animation: () => {
                        transformAction ();
                    },
                    completion: () => {
                        transformComplete ();
                    }
                );

                transformAnimation (0.0, () => {
                    imageView.Layer.Transform = MonoTouch.CoreAnimation.CATransform3D.MakeScale (0.7f, 0.7f, 1.0f);
                }, () => {
                    transformAnimation (0.15, () => {
                        imageView.Layer.Transform = MonoTouch.CoreAnimation.CATransform3D.MakeScale (1.3f, 1.3f, 1.0f);
                    }, () => {
                        transformAnimation (0.15, () => {
                            imageView.Layer.Transform = MonoTouch.CoreAnimation.CATransform3D.MakeScale (0.8f, 0.8f, 1.0f);
                        }, () => {
                            transformAnimation (0.15, () => {
                                imageView.Layer.Transform = MonoTouch.CoreAnimation.CATransform3D.MakeScale (1.0f, 1.0f, 1.0f);
                            }, () => {
                                // return the cell to it's normal state
                                cell.ImageView.Alpha = 1.0f;
                                imageView.RemoveFromSuperview ();

                                // allow caller to decide how to open the attachment
                                displayAttachment ();
                            });
                        });
                    });
                });


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
