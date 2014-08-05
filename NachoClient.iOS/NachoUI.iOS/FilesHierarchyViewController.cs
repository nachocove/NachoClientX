// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;
using SWRevealViewControllerBinding;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class FilesHierarchyViewController : NcUIViewController
    {
        INachoFileChooserParent owner;
        FilesViewController.ItemType itemType;

        UIColor separatorColor = A.Color_NachoSeparator;
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

            if (null == owner) {
                NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                    A.RevealButton (this),
                    A.NachoNowButton (this),
                };

            }
                
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

            // Attechment Cell
            UIView filesAttachmentsView = new UIView (new RectangleF (0, LINE_OFFSET, SCREEN_WIDTH, CELL_HEIGHT));
            filesAttachmentsView.BackgroundColor = UIColor.White;

            UIImageView attachmentAccessoryImage = new UIImageView (new RectangleF (SCREEN_WIDTH - 23, 14, 10, 16));
            attachmentAccessoryImage.Image = Util.MakeArrow (A.Color_NachoBlue);
            filesAttachmentsView.AddSubview (attachmentAccessoryImage);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Attachments", UIImage.FromBundle ("icn-mtng-attachment"), 14.5f, filesAttachmentsView);

            SegueHolder holder = new SegueHolder (null);
            if (owner != null) {
                holder = new SegueHolder (owner);
            }

            var attachmentTap = new UITapGestureRecognizer ();
            attachmentTap.AddTarget (() => {
                itemType = FilesViewController.ItemType.Attachment;
                PerformSegue (FilesHierarchyToFiles, holder); // holder can be filled with owner or null
            });
            filesAttachmentsView.AddGestureRecognizer (attachmentTap);

            // Notes Cell
            UIView filesSharedFilesView = new UIView (new RectangleF (0, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, CELL_HEIGHT));
            filesSharedFilesView.BackgroundColor = UIColor.White;

            UIImageView sharedFilesAccessoryImage = new UIImageView (new RectangleF (SCREEN_WIDTH - 23, 14, 10, 16));
            sharedFilesAccessoryImage.Image = Util.MakeArrow (A.Color_NachoBlue);
            filesSharedFilesView.AddSubview (sharedFilesAccessoryImage);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Shared Files", UIImage.FromBundle ("icn-mtng-attachment"), 14.5f, filesSharedFilesView);

            var sharedFilesTap = new UITapGestureRecognizer ();
            sharedFilesTap.AddTarget (() => {
                itemType = FilesViewController.ItemType.Document;
                PerformSegue (FilesHierarchyToFiles, holder); // holder can be filled with owner or null
            });
            filesSharedFilesView.AddGestureRecognizer (sharedFilesTap);

            // Shared Files Cell
            UIView filesNotesView = new UIView (new RectangleF (0, LINE_OFFSET + CELL_HEIGHT * 2, SCREEN_WIDTH, CELL_HEIGHT));
            filesNotesView.BackgroundColor = UIColor.White;

            UIImageView noteAccessoryImage = new UIImageView (new RectangleF (SCREEN_WIDTH - 23, 14, 10, 16));
            noteAccessoryImage.Image = Util.MakeArrow (A.Color_NachoBlue);
            filesNotesView.AddSubview (noteAccessoryImage);

            AddTextLabelWithImage (40, 12.438f, 100, TEXT_LINE_HEIGHT, "Notes", UIImage.FromBundle ("icn-mtng-attachment"), 14.5f, filesNotesView);

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

            AddLine (0, LINE_OFFSET, SCREEN_WIDTH, separatorColor, contentView);
            AddLine (15, LINE_OFFSET + CELL_HEIGHT, SCREEN_WIDTH, separatorColor, contentView);
            AddLine (15, LINE_OFFSET + CELL_HEIGHT * 2, SCREEN_WIDTH, separatorColor, contentView);
            AddLine (0, LINE_OFFSET + CELL_HEIGHT * 3, SCREEN_WIDTH, separatorColor, contentView);

            contentView.Frame = new RectangleF (0, 0, SCREEN_WIDTH, View.Frame.Height);
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            //Scroll View
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.ContentSize = new SizeF (SCREEN_WIDTH, contentView.Frame.Height);

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

        public void AddLine (float offset, float yVal, float width, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            parentView.Add (lineUIView);
        }
    }
}
