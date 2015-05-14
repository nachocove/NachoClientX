// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using EventKit;
using System.IO;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class AddAttachmentViewController : NcUIViewControllerNoLeaks
    {

        const float BUTTON_SIZE = 64;
        const float BUTTON_LABEL_HEIGHT = 40;
        const float BUTTON_PADDING_HEIGHT = 15;
        const float BUTTON_PADDING_WIDTH = 35;

        protected McAccount account;
        protected McAbstrCalendarRoot c;
        protected INachoFileChooserParent owner;
        List<McAttachment> AttachmentsList = new List<McAttachment> ();
        List<ButtonInfo> buttonInfoList;

        UIBarButtonItem DismissButton;

        public AddAttachmentViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoFileChooserParent owner)
        {
            this.owner = owner;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            CreateViewHierarchy ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("AddAttachmentToAttachments")) {
                var dc = (FileListViewController)segue.DestinationViewController;
                dc.SetOwner (owner);
                dc.SetModal (true);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected const int DISMISS_TAG = 1000;
        protected const int BUTTON_TAG = 2000;

        protected override void CreateViewHierarchy ()
        {
            UIView priorityView = new UIView (View.Frame);
            priorityView.ClipsToBounds = true;
            priorityView.BackgroundColor = A.Color_NachoGreen;

            var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, 44));
            navBar.BarStyle = UIBarStyle.Default;
            navBar.Opaque = true;
            navBar.Translucent = false;

            var navItem = new UINavigationItem ("Add Attachment");
            using (var image = UIImage.FromBundle ("modal-close")) {
                DismissButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                DismissButton.AccessibilityLabel = "Close";
                DismissButton.Clicked += dismissClicked;
                navItem.LeftBarButtonItem = DismissButton;
            }
            navBar.Items = new UINavigationItem[] { navItem };

            priorityView.AddSubview (navBar);
            nfloat yOffset = 64;

            Util.AddHorizontalLine (0, yOffset, View.Frame.Width, UIColor.LightGray, priorityView);
            yOffset += 2;

            yOffset += 60;

            buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                new ButtonInfo ("Add Photo", "calendar-add-photo", () => SetupPhotoPicker (false)),
                new ButtonInfo ("Take Photo", "calendar-take-photo", () => SetupPhotoPicker (true)),
                new ButtonInfo ("Add File", "calendar-add-files", () => SetupAttachmentChooser ()),
                null,
            });

            var center = priorityView.Center;
            center.X = (priorityView.Frame.Width / 2);
            center.Y = center.Y;

            var xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;

            int i = 0;
            foreach (var buttonInfo in buttonInfoList) {
                if (null == buttonInfo) {
                    xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
                    continue;
                }
                if (null == buttonInfo.buttonLabel) {
                    xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;
                    yOffset += BUTTON_SIZE + BUTTON_LABEL_HEIGHT + BUTTON_PADDING_HEIGHT;
                    continue;
                }

                var buttonRect = UIButton.FromType (UIButtonType.RoundedRect);
                buttonRect.Layer.CornerRadius = BUTTON_SIZE / 2;
                buttonRect.Layer.MasksToBounds = true;
                buttonRect.Layer.BorderColor = UIColor.LightGray.CGColor;
                buttonRect.Layer.BorderWidth = .5f;  
                buttonRect.Tag = BUTTON_TAG + i;
                buttonRect.Frame = new CGRect (0, 0, BUTTON_SIZE, BUTTON_SIZE);
                buttonRect.Center = new CGPoint (xOffset, yOffset);
                buttonRect.AccessibilityLabel = "Add attachment";
                using (var image = UIImage.FromBundle (buttonInfo.buttonIcon).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    buttonRect.SetImage (image, UIControlState.Normal);
                }
                buttonRect.TouchUpInside += (object sender, EventArgs e) => {
                    buttonInfo.buttonAction ();
                };
                priorityView.Add (buttonRect);

                var label = new UILabel ();
                label.TextColor = UIColor.White;
                label.Text = buttonInfo.buttonLabel;
                label.Font = A.Font_AvenirNextMedium14;
                label.TextAlignment = UITextAlignment.Center;
                label.SizeToFit ();
                label.Center = new CGPoint (xOffset, 5 + yOffset + ((BUTTON_SIZE + BUTTON_LABEL_HEIGHT) / 2));
                priorityView.Add (label);

                xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
                i++;
            }

            View.AddSubview (priorityView);
        }

        public void dismissClicked (object sender, EventArgs e)
        {
            DismissViewController (true, null);
        }

        protected override void Cleanup ()
        {
            DismissButton.Clicked -= dismissClicked;
            DismissButton = null;

            //TODO
//            int i = 0;
//            foreach (var buttonInfo in buttonInfoList) {
//                UIButton button = (UIButton)View.ViewWithTag (BUTTON_TAG + i);
//                button = null;
//                i++;
//            }
        }

        public void SetupAttachmentChooser ()
        {
            PerformSegue ("AddAttachmentToAttachments", this);
        }

        protected override void ConfigureAndLayout ()
        {
        }

        //source is true if using camera
        //source is false if using photo library
        public void SetupPhotoPicker (bool source)
        {
            var imagePicker = new UIImagePickerController ();
            imagePicker.NavigationBar.Translucent = false;
            imagePicker.NavigationBar.BarTintColor = A.Color_NachoGreen;
            imagePicker.NavigationBar.TintColor = A.Color_NachoBlue;

            if (source) {
                if (UIImagePickerController.IsSourceTypeAvailable (UIImagePickerControllerSourceType.Camera)) {
                    imagePicker.SourceType = UIImagePickerControllerSourceType.Camera;
                } else {
                    Util.ComplainAbout ("Error", "Your device does not have a camera");
                }
            } else {
                imagePicker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;
            }

            imagePicker.FinishedPickingMedia += HandleFinishedPickingMedia;
            imagePicker.Canceled += HandleCanceled;

            imagePicker.ModalPresentationStyle = UIModalPresentationStyle.CurrentContext;
            this.PresentViewController (imagePicker, true, null);
            MaintainLightStyleStatusBar ();
        }

        protected void HandleCanceled (object sender, EventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;
            imagePicker.DismissViewController (true, null);
            MaintainLightStyleStatusBar ();
        }

        protected void HandleFinishedPickingMedia (object sender, UIImagePickerMediaPickedEventArgs e)
        {

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
                var attachment = McAttachment.InsertFile (account.Id, ((FileStream stream) => {
                    using (var jpg = image.AsJPEG ().AsStream ()) {
                        jpg.CopyTo (stream);
                    }
                }));
                attachment.SetDisplayName (attachment.Id.ToString () + ".jpg");
                attachment.UpdateSaveFinish ();
                owner.Append (attachment);
            }

            e.Info.Dispose ();
            owner.DismissPhotoPicker ();
            MaintainLightStyleStatusBar ();
        }

        protected class ButtonInfo
        {
            public string buttonLabel { get; set; }

            public string buttonIcon { get; set; }

            public Action buttonAction { get; set; }

            public ButtonInfo (string bl, string bi, Action ba)
            {
                buttonLabel = bl;
                buttonIcon = bi;
                buttonAction = ba;
            }
        }

        //Not perfect but a keeps it from sticking throughtout the app
        protected void MaintainLightStyleStatusBar ()
        {
            UIApplication.SharedApplication.SetStatusBarStyle (UIStatusBarStyle.LightContent, false);
        }

        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            List<McAttachment> tempList = new List<McAttachment> ();
            foreach (var a in AttachmentsList) {
                if (a.Id != attachment.Id) {
                    tempList.Add (a);
                }
            }
            AttachmentsList = tempList;
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            vc.DismissFileChooser (true, null);
        }
    }
}