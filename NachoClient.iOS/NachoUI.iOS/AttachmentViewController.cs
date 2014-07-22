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

namespace NachoClient.iOS
{
    public partial class AttachmentViewController : NcDialogViewController, INachoFileChooser
    {
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

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("AttachmentsToNachoNow", this);
            };

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
            var root = new RootElement ("Attachments");
            var section = new Section ();

            var attachmentList = NcModel.Instance.Db.Table<McAttachment> ().ToList ();
            foreach (var a in attachmentList) {
                StyledStringElement s;
                if (a.IsInline) {
                    s = new StyledStringElement (a.DisplayName, "Is inline", UITableViewCellStyle.Subtitle);
                } else if (a.IsDownloaded) {
                    s = new StyledStringElement (a.DisplayName, "Is downloaded", UITableViewCellStyle.Subtitle);
                    s.Tapped += delegate {
                        var id = a.Id;
                        attachmentAction (id);
                    };
                } else if (a.PercentDownloaded > 0) {
                    s = new StyledStringElement (a.DisplayName, "Downloading...", UITableViewCellStyle.Subtitle);
                } else {
                    s = new StyledStringElement (a.DisplayName, "Is not downloaded", UITableViewCellStyle.Subtitle);
                    s.Tapped += delegate {
                        var id = a.Id;
                        attachmentAction (id);
                    };
                }
                section.Add (s);
            }
            root.Add (section);
            Root = root;

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
