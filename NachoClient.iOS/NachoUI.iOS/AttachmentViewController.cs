// This file has been autogenerated from a class added in the UI designer.

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
    public partial class AttachmentViewController : UITableViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate
    {
        INachoFileChooserParent owner;
        FilesTableSource filesSource;
        SearchDelegate searchDelegate;
        Action<object, EventArgs> fileAction;

        // segue ids
        string FilesToComposeSegueId = "FilesToEmailCompose";

        public AttachmentViewController (IntPtr handle) : base (handle)
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
            owner = null;
            NavigationController.PopViewControllerAnimated (true);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

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

            // don't show hamburger/nachonow buttons if selecting attachment for event or email
            if (owner == null) {
                NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
                using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                    nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
                }
                nachoButton.Clicked += (object sender, EventArgs e) => {
                    PerformSegue ("AttachmentsToNachoNow", this);
                };
            }

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    RefreshAttachmentSection ();
                }
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            RefreshAttachmentSection ();
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

        public void RefreshAttachmentSection ()
        {
            // show most recent attachments first
            filesSource.Attachments = NcModel.Instance.Db.Table<McAttachment> ().OrderByDescending (a => a.Id).ToList ();
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

        public void downloadAndDoAction (int attachmentId, Action<McAttachment> attachmentAction)
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

        public void ForwardAttachment (McAttachment attachment)
        {
            downloadAndDoAction (attachment.Id, (a) => {
                PerformSegue (FilesToComposeSegueId, new SegueHolder (a));
            });
        }

        public void openInOtherApp (McAttachment attachment)
        {
            downloadAndDoAction (attachment.Id, (a) => {
                UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (a.FilePath ()));
                Preview.Delegate = new NachoClient.PlatformHelpers.DocumentInteractionControllerDelegate (this);
                Preview.PresentOpenInMenu (View.Frame, View, true);
            });
        }

        public void attachmentAction (int attachmentId)
        {
            downloadAndDoAction (attachmentId, (a) => {
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

            protected List<McAttachment> attachments = new List<McAttachment> ();
            protected List<McAttachment> searchResults = new List<McAttachment> ();

            AttachmentViewController vc;

            public List<McAttachment> Attachments
            {
                get { return attachments; }
                set { attachments = value; }
            }

            public List<McAttachment> SearchResults
            {
                get { return searchResults; }
                set { searchResults = value; }
            }

            public FilesTableSource (AttachmentViewController vc)
            {
                this.vc = vc;
            }

            public override int RowsInSection (UITableView tableview, int section)
            {
                if (tableview == vc.SearchDisplayController.SearchResultsTableView) {
                    return SearchResults.Count;
                } else {
                    return Attachments.Count;
                }
            }

            public override int NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                UITableViewCell cell = null;
                cell = tableView.DequeueReusableCell (FileCell);
                if (cell == null) {
                    cell = new MCSwipeTableViewCell (UITableViewCellStyle.Value1, FileCell);
                }
                NcAssert.True (null != cell);

                McAttachment attachment;

                // determine if table is for search results or all attachments
                if (tableView == vc.SearchDisplayController.SearchResultsTableView) {
                    attachment = SearchResults [indexPath.Row];
                } else {
                    attachment = Attachments [indexPath.Row];
                }

                cell.TextLabel.Text = attachment.DisplayName;
                cell.DetailTextLabel.Text = attachment.ContentType;
                if (attachment.IsDownloaded || attachment.IsInline) {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-complete.png");
                    cell.ImageView.Layer.RemoveAllAnimations ();
                } else if (attachment.PercentDownloaded > 0 && attachment.PercentDownloaded < 100) {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-download.png");
                    SetAnimationOnCell (cell);
                } else {
                    cell.ImageView.Image = UIImage.FromFile ("icn-file-download.png");
                }

                // styling
                cell.TextLabel.TextColor = A.Color_NachoBlack;
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                cell.DetailTextLabel.TextColor = UIColor.LightGray;
                cell.DetailTextLabel.Font = A.Font_AvenirNextRegular14;

                // swipes
                ConfigureSwipes (cell as MCSwipeTableViewCell, attachment);

                return cell;
            }
                
            public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
            {
                McAttachment attachment;
                if (tableView == vc.SearchDisplayController.SearchResultsTableView) {
                    attachment = SearchResults [indexPath.Row];
                } else {
                    attachment = Attachments [indexPath.Row];
                }
                vc.attachmentAction (attachment.Id);
                if (!attachment.IsDownloaded) {
                    var rotation = vc.DownloadAnimation ();
                    tableView.CellAt(indexPath).ImageView.Layer.AddAnimation (rotation, "downloadAnimation");
                }
                tableView.DeselectRow (indexPath, true);
            }
                
            /// <summary>
            /// Configures the swipes.
            /// </summary>
            void ConfigureSwipes (MCSwipeTableViewCell cell, McAttachment attachment)
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
                        vc.ForwardAttachment (attachment);
                        SetAnimationOnCell (cell);
                        return;
                    });
                    crossView = ViewWithImageName ("cross");
                    redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (crossView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        vc.attachmentAction (attachment.Id);
                        return;
                    });
                    previewView = ViewWithImageName ("clock");
                    yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (previewView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        vc.attachmentAction (attachment.Id);
                        SetAnimationOnCell (cell);
                        return;
                    });
                    openView = ViewWithImageName ("list");
                    brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
                    cell.SetSwipeGestureWithView (openView, brownColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                        vc.openInOtherApp (attachment);
                        SetAnimationOnCell (cell);
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


            private void SetAnimationOnCell (UITableViewCell cell)
            {
                var rotation = vc.DownloadAnimation ();
                cell.ImageView.Layer.AddAnimation (rotation, "downloadAnimation");
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
                filesSource.SearchResults = filesSource.Attachments.Where (w => w.DisplayName.Contains (forSearchString)).ToList ();
                searchString = forSearchString;
                controller.SearchResultsTableView.ReloadData ();
                return true;
            }
        }
    }
}
