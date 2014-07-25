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

namespace NachoClient.iOS
{
    public partial class AttachmentViewController : UITableViewController, INachoFileChooser, IUISearchDisplayDelegate, IUISearchBarDelegate
    {
        // cell Id's 
        const string FileCell = "FileCell";

        List<McAttachment> AttachmentList;

        INachoFileChooserParent owner;

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

        public void RefreshAttachmentSection ()
        {
            // show most recent attachments first
            AttachmentList = NcModel.Instance.Db.Table<McAttachment> ().OrderByDescending (a => a.Id).ToList ();
            this.TableView.ReloadData ();
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            if (AttachmentList == null) {
                RefreshAttachmentSection ();
            }
            return AttachmentList.Count;
        }

        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (FileCell);
            NcAssert.True (null != cell);

            var attachment = AttachmentList [indexPath.Row];
            cell.TextLabel.Text = attachment.DisplayName;
            cell.DetailTextLabel.Text = attachment.ContentType;
            if (attachment.IsDownloaded || attachment.IsInline) {
                cell.ImageView.Image = UIImage.FromFile ("icn-file-complete.png");
            } else {
                cell.ImageView.Image = UIImage.FromFile ("icn-file-download.png");
            }

            // styling
            cell.TextLabel.TextColor = A.Color_NachoBlack;
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.DetailTextLabel.TextColor = UIColor.LightGray;
            cell.DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            return cell;
        }

        public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            var attachment = AttachmentList [indexPath.Row];
            attachmentAction (attachment.Id);
            tableView.DeselectRow (indexPath, true);
        }

        void attachmentAction (int attachmentId)
        {
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (false == a.IsDownloaded) {
                PlatformHelpers.DownloadAttachment (a);
                return;
            }

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

            actionSheet.ShowInView (this.View);
        }
    }
}
