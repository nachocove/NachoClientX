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
using MobileCoreServices;
using Photos;
using AVFoundation;

namespace NachoClient.iOS
{
    public partial class AddAttachmentViewController : NcUIViewControllerNoLeaks
    {

        public class MenuHelper : NSObject, IUIDocumentMenuDelegate, IUIDocumentPickerDelegate
        {
            public UIDocumentMenuViewController MenuViewController { get; private set; }
            INachoFileChooserParent owner;
            McAccount account;

            public MenuHelper (INachoFileChooserParent owner, McAccount account, UIBarButtonItem parentButton, UIView parentView)
            {
                this.owner = owner;
                this.account = account;

                MenuViewController = new UIDocumentMenuViewController(new string[] {
                    UTType.Data,
                    UTType.Package
                }, UIDocumentPickerMode.Import);
                MenuViewController.Delegate = this;
                MenuViewController.AddOption ("Browse Attachments", UIImage.FromBundle("calendar-add-files"), UIDocumentMenuOrder.First, ShowBrowseAttachments);
                MenuViewController.AddOption ("Take a Photo", UIImage.FromBundle("calendar-take-photo"), UIDocumentMenuOrder.First, ShowTakePhoto);
                MenuViewController.AddOption ("Browse Photos", UIImage.FromBundle("calendar-add-photo"), UIDocumentMenuOrder.First, ShowBrowsePhotos);
                var ppc = MenuViewController.PopoverPresentationController;
                if (null != ppc) {
                    if (null != parentButton) {
                        ppc.BarButtonItem = parentButton;
                    } else {
                        ppc.SourceView = parentView;
                        ppc.SourceRect = parentView.Bounds;
                    }
                }
            }

            public MenuHelper (INachoFileChooserParent owner, McAccount account, UIBarButtonItem parentButton)
                : this (owner, account, parentButton, null)
            {
            }

            public MenuHelper (INachoFileChooserParent owner, McAccount account, UIView parentView)
                : this (owner, account, null, parentView)
            {
            }

            void ShowBrowsePhotos ()
            {
                ShowPhotoPicker (false);
            }

            void ShowTakePhoto ()
            {
                ShowPhotoPicker (true);
            }

            void ShowBrowseAttachments ()
            {
                var fileListViewController = new FileListViewController ();
                fileListViewController.SetOwner (owner, account);
                fileListViewController.SetModal (true);
                owner.PresentFileChooserViewController (fileListViewController);
            }

            public void DidPickDocumentPicker (UIDocumentMenuViewController documentMenu, UIDocumentPickerViewController documentPicker)
            {
                documentPicker.Delegate = this;
                owner.PresentFileChooserViewController (documentPicker);
            }

            public void WasCancelled (UIDocumentMenuViewController documentMenu)
            {
            }

            public void DidPickDocument (UIDocumentPickerViewController controller, NSUrl url)
            {
                if (url.IsFileUrl) {
                    try {
                        var path = url.Path;
                        if (Directory.Exists (path)) {
                            url = url.AppendPathExtension ("zip");
                            System.IO.Compression.ZipFile.CreateFromDirectory (path, url.Path);
                            Directory.Delete (path, true);
                            path = url.Path;
                        }
                        var attachment = McAttachment.InsertSaveStart (account.Id);
                        attachment.SetDisplayName (url.LastPathComponent);
                        attachment.UpdateFileMove (path);
                        owner.Append (attachment);
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_UI, "DidPickDocument: Could not insert file into attachment: {0}", ex);
                        presentError (ex);
                    }
                } else {
                    Log.Error (Log.LOG_UI, "DidPickDocument: received non-file URL: {0}", url);
                    presentError ("The picked file is not local to this device.");
                }
            }

            void presentError (Exception ex)
            {
                string message;
                if (ex is FileNotFoundException) {
                    message = "The file could not be found.";
                } else {
                    message = string.Format ("An Unknown error occurred ({0}).", ex.Message);
                }
                message += " Please try again.";
                presentError (message);
            }

            void presentError (string message, string title = null)
            {
                if (string.IsNullOrEmpty (title)) {
                    title = "Could not attach file";
                }
                var alert = UIAlertController.Create (title, message, UIAlertControllerStyle.Alert);
                alert.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default, null));
                owner.PresentFileChooserViewController (alert);
            }

            public void ShowPhotoPicker (bool useCamera, UIViewController fromViewController = null)
            {
                var imagePicker = new UIImagePickerController ();
                imagePicker.NavigationBar.Translucent = false;
                imagePicker.NavigationBar.BarTintColor = A.Color_NachoGreen;
                imagePicker.NavigationBar.TintColor = A.Color_NachoBlue;

                if (useCamera) {
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
                imagePicker.MediaTypes = new string[]{ UTType.Image.ToString (), UTType.Movie.ToString () };

                imagePicker.ModalPresentationStyle = UIModalPresentationStyle.CurrentContext;

                if (fromViewController != null) {
                    fromViewController.PresentViewController (imagePicker, true, null);
                } else {
                    owner.PresentFileChooserViewController (imagePicker);
                }
            }

            protected void HandleCanceled (object sender, EventArgs e)
            {
                var imagePicker = sender as UIImagePickerController;
                imagePicker.DismissViewController (true, null);
            }

            protected void HandleFinishedPickingMedia (object sender, UIImagePickerMediaPickedEventArgs e)
            {
                var type = e.Info [UIImagePickerController.MediaType].ToString ();
                if (type.Equals (UTType.Image) || type.Equals (UTType.Movie)) {
                    var referenceUrl = e.Info [UIImagePickerController.ReferenceUrl] as NSUrl;
                    var metadata = e.Info [UIImagePickerController.MediaMetadata] as NSDictionary;
                    if (referenceUrl != null) {
                        // picked an image or movie
                        var options = new PHFetchOptions ();
                        var assets = PHAsset.FetchAssets (new NSUrl[] { referenceUrl }, options);
                        if (assets.Count > 0){
                            var asset = assets.firstObject as PHAsset;
                            if (asset != null) {
                                var attachment = McAttachment.InsertSaveStart (account.Id);
                                var filenameObj = asset.ValueForKey (new NSString ("filename")) as NSString;
                                var filename = filenameObj != null ? filenameObj.ToString () : null;
                                if (type.Equals (UTType.Image)) {
                                    if (filename == null) {
                                        filename = "attachment.jpg";
                                    }
                                    var imageOptions = new PHImageRequestOptions ();
                                    imageOptions.Version = PHImageRequestOptionsVersion.Current;
                                    imageOptions.DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat;
                                    imageOptions.NetworkAccessAllowed = true;
                                    PHImageManager.DefaultManager.RequestImageData (asset, imageOptions, (NSData data, NSString dataUti, UIImageOrientation orientation, NSDictionary info) => {
                                        var error = info.ObjectForKey (PHImageKeys.Error);
                                        if (error == null) {
                                            attachment = McAttachment.QueryById<McAttachment> (attachment.Id);
                                            attachment.UpdateData (data.ToArray ());
                                            attachment.UpdateSaveFinish ();
                                            owner.AttachmentUpdated (attachment);
                                        } else {
                                            Log.Error (Log.LOG_UI, "AddAttachmentViewController error obtaining image data: {0}", error);
                                            attachment = McAttachment.QueryById<McAttachment> (attachment.Id);
                                            attachment.FilePresence = McAbstrFileDesc.FilePresenceEnum.Error;
                                            attachment.Update ();
                                            owner.AttachmentUpdated (attachment);
                                        }
                                    });
                                } else if (type.Equals (UTType.Movie)) {
                                    var movieUrl = e.Info [UIImagePickerController.MediaURL] as NSUrl;
                                    if (filename == null) {
                                        filename = movieUrl.LastPathComponent;
                                    }
                                    attachment.UpdateFileCopy (movieUrl.Path);
                                    attachment.UpdateSaveFinish ();
                                }
                                attachment.ContentType = MimeKit.MimeTypes.GetMimeType (filename);
                                attachment.SetDisplayName (filename);
                                attachment.Update ();
                                owner.Append (attachment);
                            } else {
                                Log.Error (Log.LOG_UI, "AddAttachmentViewController first result is not a PHAsset");
                            }
                        }else{
                            Log.Error (Log.LOG_UI, "AddAttachmentViewController could not find asset: {0}", referenceUrl);
                        }
                    } else if (metadata != null) {
                        // Took a picture with the camera.
                        var image = e.Info [UIImagePickerController.EditedImage] as UIImage;
                        if (image == null) {
                            image = e.Info [UIImagePickerController.OriginalImage] as UIImage;
                        }
                        var attachment = McAttachment.InsertSaveStart (account.Id);
                        attachment.SetDisplayName ("attachment.jpg");
                        attachment.ContentType = "image/jpeg";
                        attachment.Update ();
                        using (var jpg = image.AsJPEG ()) {
                            attachment.UpdateData (jpg.ToArray ());
                        }
                        PHPhotoLibrary.SharedPhotoLibrary.PerformChanges (() => {
                            PHAssetChangeRequest.FromImage (image);
                        }, (bool success, NSError error) => {
                            if (!success){
                                Log.Error (Log.LOG_UI, "AddAttachmentViewController could not add to photos: {0}", error);
                            }
                        });
                        owner.Append (attachment);
                    } else {
                        Log.Error (Log.LOG_UI, "AddAttachmentViewController no reference or metadata: {0}", e.Info);
                    }
                }else{
                    Log.Error (Log.LOG_UI, "AddAttachmentViewController unknown media type selected: {0}", type);
                }
                e.Info.Dispose ();
                owner.DismissPhotoPicker ();
            }
        }

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

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        public void SetOwner (INachoFileChooserParent owner, McAccount account)
        {
            this.owner = owner;
            this.account = account;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateViewHierarchy ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
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

        void SetupPhotoPicker (bool useCamera)
        {
            var helper = new MenuHelper (owner, account, View);
            helper.ShowPhotoPicker (useCamera, this);
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
            ShowFiles ();
        }

        void ShowFiles ()
        {
            var dc = new FileListViewController ();
            dc.SetOwner (owner, account);
            dc.SetModal (true);
            PresentViewController (dc, true, null);
        }

        protected override void ConfigureAndLayout ()
        {
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