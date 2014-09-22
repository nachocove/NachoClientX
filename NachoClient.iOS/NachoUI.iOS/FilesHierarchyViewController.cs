// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class FilesHierarchyViewController : NcUIViewController
    {
        INachoFileChooserParent owner;
        FilesViewController.ItemType itemType;

        UIColor separatorColor = A.Color_NachoBorderGray;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected int LINE_OFFSET = 30;
        protected int CELL_HEIGHT = 44;
        protected float TEXT_LINE_HEIGHT = 19.124f;
        protected UIColor solidTextColor = A.Color_NachoBlack;

        // segue id's
        string FilesHierarchyToFiles = "FilesHierarchyToFiles";
        string FilesHierarchyToNachoNow = "SegueToNachoNow";

        public FilesHierarchyViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// INachoFileChooser delegate
        /// </summary>
        public void SetOwner (INachoFileChooserParent owner)
        {
            this.owner = owner;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            FilesHierarchyView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        const int FILES_ATTACHMENT_DETAIL_TAG = 100;
        const int FILES_SHARED_FILE_DETAIL_TAG = 101;
        const int FILES_NOTES_DETAIL_TAG = 102;

        public void FilesHierarchyView ()
        {
            scrollView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, View.Frame.Height);

            NavigationItem.Title = "Files";

            // Attechment Cell
            UIView filesAttachmentsView = new UIView (new RectangleF (0, LINE_OFFSET, SCREEN_WIDTH, CELL_HEIGHT));
            filesAttachmentsView.BackgroundColor = UIColor.White;

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, filesAttachmentsView);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Attachments", UIImage.FromBundle ("icn-attachedfile"), 14.5f, filesAttachmentsView);

            var holder = new SegueHolder (owner); // ok if owner is null

            var attachmentTap = new UITapGestureRecognizer ();
            attachmentTap.AddTarget (() => {
                itemType = FilesViewController.ItemType.Attachment;
                PerformSegue (FilesHierarchyToFiles, holder); // holder can be filled with owner or null
            });
            filesAttachmentsView.AddGestureRecognizer (attachmentTap);

            // Shared Files Cell
            UIView filesSharedFilesView = new UIView (new RectangleF (0, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, CELL_HEIGHT));
            filesSharedFilesView.BackgroundColor = UIColor.White;

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, filesSharedFilesView);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Shared Files", UIImage.FromBundle ("icn-sharedfiles"), 14.5f, filesSharedFilesView);

            var sharedFilesTap = new UITapGestureRecognizer ();
            sharedFilesTap.AddTarget (() => {
                itemType = FilesViewController.ItemType.Document;
                PerformSegue (FilesHierarchyToFiles, holder); // holder can be filled with owner or null
            });
            filesSharedFilesView.AddGestureRecognizer (sharedFilesTap);

            // Notes Cell
            UIView filesNotesView = new UIView (new RectangleF (0, LINE_OFFSET + CELL_HEIGHT * 2, SCREEN_WIDTH, CELL_HEIGHT));
            filesNotesView.BackgroundColor = UIColor.White;

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, filesNotesView);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Notes", UIImage.FromBundle ("icn-notes"), 14.5f, filesNotesView);

            var noteTap = new UITapGestureRecognizer ();
            noteTap.AddTarget (() => {
                itemType = FilesViewController.ItemType.Note;
                PerformSegue (FilesHierarchyToFiles, holder); // holder can be filled with owner or null
            });
            filesNotesView.AddGestureRecognizer (noteTap);

            //Content View
            contentView.AddSubviews (new UIView[] {
                filesAttachmentsView,
                filesNotesView,
                filesSharedFilesView
            }); 

            Util.AddHorizontalLine (0, LINE_OFFSET, SCREEN_WIDTH, separatorColor, contentView);
            Util.AddHorizontalLine (15, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, separatorColor, contentView);
            Util.AddHorizontalLine (15, LINE_OFFSET + CELL_HEIGHT * 2, SCREEN_WIDTH, separatorColor, contentView);
            Util.AddHorizontalLine (0, LINE_OFFSET + CELL_HEIGHT * 3, SCREEN_WIDTH, separatorColor, contentView);

            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, View.Frame.Height);
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.ContentSize = new SizeF (SCREEN_WIDTH, contentView.Frame.Height - 64);

        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals (FilesHierarchyToFiles)) {
                var dc = (FilesViewController)segue.DestinationViewController;

                var holder = sender as SegueHolder;
                INachoFileChooserParent viewOwner;
                if (holder.value != null) { // does owner exist?
                    viewOwner = (INachoFileChooserParent)holder.value;
                    dc.SetOwner (viewOwner);
                }

                dc.itemType = this.itemType; // tell the files VC what item type to display
                return;
            }
            if (segue.Identifier.Equals (FilesHierarchyToNachoNow)) {
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void AddTextLabelWithImage (float xOffset, float yOffset, float width, float height, string text, UIImage image, float imageOffset, UIView parentView)
        {
            var textLabel = new UILabel (new RectangleF (xOffset, yOffset, width, height));
            textLabel.Text = text;
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = solidTextColor;
            parentView.AddSubview (textLabel);

            UIImageView theImage = new UIImageView (new RectangleF ((xOffset - 25), imageOffset, 15, 15));
            theImage.Image = image;
            parentView.Add (theImage);
        }  
    }
}
