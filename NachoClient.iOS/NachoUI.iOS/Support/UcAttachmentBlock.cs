//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public class UcAttachmentCell : UIView
    {
        protected const float LINE_HEIGHT = 40;
        protected const float LEFT_INDENT = 15;
        protected const float LEFT_ADDRESS_INDENT = 57;

        public McAttachment attachment;

        public UcAttachmentCell (McAttachment attachment, float parentWidth) : base (new RectangleF (0, 0, parentWidth, LINE_HEIGHT))
        {
            this.attachment = attachment;

            CreateView (parentWidth);
        }

        public void CreateView (float parentWidth)
        {
            var hl = new UIView (new RectangleF (0, 0, parentWidth, 1));
            hl.Layer.BorderColor = A.Color_NachoNowBackground.CGColor;
            hl.Layer.BorderWidth = 1; 

            var icon = new UIImageView (new RectangleF (LEFT_INDENT, 7, 15, 15));
            using (var image = UIImage.FromBundle ("icn-attach-files")) {
                icon.Image = image;
            }

            var label = new UILabel ();
            label.Font = A.Font_AvenirNextRegular14;
            label.TextColor = A.Color_0B3239;
            label.Text = attachment.DisplayName;
            var labelSize = label.StringSize (label.Text, label.Font);
            var yLabel = (LINE_HEIGHT / 2) - (labelSize.Height / 2);
            label.Frame = new RectangleF (new PointF (LEFT_ADDRESS_INDENT, yLabel), labelSize);

            this.AddSubviews (new UIView[] { hl, icon, label });
        }
    }

    public class UcAttachmentBlock : UIView
    {
        protected int accountId;
        protected IUcAttachmentBlockDelegate owner;
        protected float parentWidth;
        protected List<UcAttachmentCell> list = new List<UcAttachmentCell> ();

        protected const int LINE_HEIGHT = 40;
        protected const int LEFT_INDENT = 15;
        protected const int RIGHT_INDENT = 15;

        bool isCompact;
        UIView contentView;
        UILabel mainLabel;

        public UcAttachmentBlock (IUcAttachmentBlockDelegate owner, int accountId, float parentWidth)
        {
            this.owner = owner;
            this.accountId = accountId;
            this.parentWidth = parentWidth;
            this.BackgroundColor = UIColor.White;

            this.AutoresizingMask = UIViewAutoresizing.None;
            this.AutosizesSubviews = false;

            CreateView ();
        }

        public void SetCompact (bool isCompact)
        {
            this.isCompact = isCompact;
        }

        public List<McAttachment> AttachmentList {
            get {
                var l = new List<McAttachment> ();
                foreach (var a in list) {
                    l.Add (a.attachment);
                }
                return l;
            }
        }

        public void CreateView ()
        {
            contentView = new UIView ();
            this.AddSubview (contentView);

            mainLabel = new UILabel ();
            mainLabel.Text = "Attachments";
            mainLabel.Font = A.Font_AvenirNextRegular14;
            mainLabel.TextColor = A.Color_0B3239;

            contentView.AddSubviews (new UIView[] { mainLabel });

            // Enabled & disable 'compact view' with a tap
            var tap = new UITapGestureRecognizer ();
            tap.AddTarget (() => {
                isCompact = !isCompact;
                ConfigureView ();
            });
            contentView.AddGestureRecognizer (tap);


        }

        public void Append (McAttachment attachment)
        {
            var c = new UcAttachmentCell (attachment, parentWidth);
            contentView.AddSubview (c);
            list.Add (c);

            var tap = new UITapGestureRecognizer ();
            tap.AddTarget (() => {
                AttachmentActionSheet (c);
            });
            c.AddGestureRecognizer (tap);

            Layout ();
            ConfigureView ();
        }

        public void PromptForAttachment (string compositionType)
        {
            AttachFileActionSheet (compositionType);

        }

        public void Remove (UcAttachmentCell c)
        {
            list.Remove (c);
            c.RemoveFromSuperview ();

            Layout ();
            ConfigureView ();
        }

        /// Adjusts x & y on the top line of a view
        protected void AdjustXY (UIView view, float X, float Y)
        {
            view.Center = new PointF (X + (view.Frame.Width / 2), LINE_HEIGHT / 2);
        }

        public void ConfigureView ()
        {
            mainLabel.Text = String.Format ("Attachments ({0})", list.Count);

            foreach (var c in list) {
                c.Hidden = isCompact;
            }

            if (null != owner) {
                owner.AttachmentBlockNeedsLayout (this);
            }
        }

        public void Layout ()
        {
            float yOffset = 0;

            var mainLabelSize = mainLabel.StringSize (mainLabel.Text, mainLabel.Font);
            mainLabel.Frame = new RectangleF (mainLabel.Frame.Location, mainLabelSize);
            AdjustXY (mainLabel, LEFT_INDENT, yOffset);

            yOffset += LINE_HEIGHT;

            foreach (var c in list) {
                if (!c.Hidden) {
                    c.Frame = new RectangleF (0, yOffset, c.Frame.Width, c.Frame.Height);
                    yOffset += LINE_HEIGHT;
                }
            }

            contentView.Frame = new RectangleF (0, 0, parentWidth, yOffset);
            this.Frame = new RectangleF (this.Frame.Location, contentView.Frame.Size);
        }

        protected void AttachFileActionSheet (string compositionType)
        {
            var actionSheet = new UIActionSheet ();

            actionSheet.Add ("Add Photo");
            actionSheet.Add ("Add Shared File");
            actionSheet.Add ("Add Existing Attachment");
            actionSheet.Add ("Cancel");
            actionSheet.CancelButtonIndex = 3;

            if ("message" == compositionType) {
                actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                    switch (b.ButtonIndex) {
                    case 0:
                        SetupPhotoPicker ();
                        break; 
                    case 1:
                        if (null != owner) {
                            owner.PerformSegueForAttachmentBlock ("ComposeToFiles", new SegueHolder (null));
                        }
                        break;
                    case 2:
                        if (null != owner) {
                            owner.PerformSegueForAttachmentBlock ("ComposeToAttachments", new SegueHolder (null));
                        }
                        break;
                    case 3:

                        break;// Cancel
                    default:
                        NcAssert.CaseError ();
                        break;
                    }
                };
            }
            if ("event" == compositionType) {
                actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                    switch (b.ButtonIndex) {
                    case 0:
                        SetupPhotoPicker ();
                        break; 
                    case 1:
                        if (null != owner) {
                            owner.PerformSegueForAttachmentBlock ("AddAttachmentToFiles", new SegueHolder (null));
                        }
                        break;
                    case 2:
                        if (null != owner) {
                            owner.PerformSegueForAttachmentBlock ("AddAttachmentToAttachment", new SegueHolder (null));
                        }
                        break;
                    case 3:

                        break;// Cancel
                    default:
                        NcAssert.CaseError ();
                        break;
                    }
                };

            }


            actionSheet.ShowInView (this);
        }

        void SetupPhotoPicker ()
        {
            var imagePicker = new UIImagePickerController ();

            imagePicker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;

            imagePicker.FinishedPickingMedia += Handle_FinishedPickingMedia;
            imagePicker.Canceled += Handle_Canceled;

            imagePicker.ModalPresentationStyle = UIModalPresentationStyle.CurrentContext;
            owner.PresentViewControllerForAttachmentBlock (imagePicker, true, null);
        }

        void Handle_Canceled (object sender, EventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;
            imagePicker.DismissViewController (true, null);
        }

        protected void Handle_FinishedPickingMedia (object sender, UIImagePickerMediaPickedEventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;

            bool isImage = false;
            switch (e.Info [UIImagePickerController.MediaType].ToString ()) {
            case "public.image":
                isImage = true;
                break;
            case "public.video":
                // TODO: Implement videos
                Log.Info (Log.LOG_UI, "video ignored");
                break;
            default:
                // TODO: Implement videos
                Log.Error (Log.LOG_UI, "unknown media type selected");
                break;
            }

            if (isImage) {
                var image = e.Info [UIImagePickerController.EditedImage] as UIImage;
                if (null == image) {
                    image = e.Info [UIImagePickerController.OriginalImage] as UIImage;
                }
                NcAssert.True (null != image);
                var attachment = new McAttachment ();
                attachment.AccountId = accountId;
                attachment.Insert ();
                attachment.DisplayName = attachment.Id.ToString () + ".jpg";
                var guidString = Guid.NewGuid ().ToString ("N");
                using (var stream = McAttachment.TempFileStream (guidString)) {
                    using (var jpg = image.AsJPEG ().AsStream ()) {
                        jpg.CopyTo (stream);
                        jpg.Close ();
                    }
                }
                attachment.SaveFromTemp (guidString);
                attachment.Update ();
                Append (attachment);
            }

            e.Info.Dispose ();
            imagePicker.DismissViewController (true, null);
        }

        protected void AttachmentActionSheet (UcAttachmentCell c)
        {
            var actionSheet = new UIActionSheet ();

            actionSheet.Add ("Remove Attachment");
            actionSheet.Add ("Preview Attachment");
            actionSheet.Add ("Cancel");
            actionSheet.CancelButtonIndex = 2;

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    Remove (c);
                    break; 
                case 1:
                    if (null != owner) {
                        owner.DisplayAttachmentForAttachmentBlock (c.attachment);
                    }
                    break;
                case 2:
                    break;// Cancel

                }
            };

            actionSheet.ShowInView (this);
        }
    }
}

