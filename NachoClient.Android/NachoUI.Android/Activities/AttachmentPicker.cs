//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;

using Android.App;
using Android.Content;
using Android.OS;

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class AttachmentPicker
    {

        private const string FRAGMENT_ATTACHMENT_CHOOSER = "NachoClient.AndroidClient.AttachmentPicker.FRAGMENT_ATTACHMENT_CHOOSER";

        private const string CAMERA_OUTPUT_PHOTO_KEY = "cameraOutputPhoto";

        private const int REQUEST_EXTERNAL_APP = 101;
        private const int REQUEST_TAKE_PHOTO = 102;
        private const int REQUEST_NACHO_FILE = 103;

        string CameraOutputPhoto;

        public event EventHandler<McAttachment> AttachmentPicked;

        public AttachmentPicker ()
        {
        }

        public void OnCreate (Bundle savedInstanceState)
        {
            CameraOutputPhoto = savedInstanceState.GetString (CAMERA_OUTPUT_PHOTO_KEY);
        }

        public void OnSaveInstanceState (Bundle outState)
        {
            if (CameraOutputPhoto != null) {
                outState.PutString (CAMERA_OUTPUT_PHOTO_KEY, CameraOutputPhoto);
            }
        }

        public void Show (Fragment fragment, int accountId)
        {
            var attachmentChooser = new AttachmentChooserFragment ();
            attachmentChooser.Show (fragment.FragmentManager, FRAGMENT_ATTACHMENT_CHOOSER, () => {
                if (attachmentChooser.SelectedSource == null) {
                    return;
                }
                switch (attachmentChooser.SelectedSource.Identifier) {
                case AttachmentChooserFragment.AttachmentSource.IDENTIFIER_TAKE_PHOTO:
                    CameraOutputPhoto = Util.TakePhoto (fragment, REQUEST_TAKE_PHOTO);
                    break;
                case AttachmentChooserFragment.AttachmentSource.IDENTIFIER_NACHO_FILE:
                    var intent = FilePickerActivity.BuildIntent (fragment.Activity);
                    fragment.StartActivityForResult (intent, REQUEST_NACHO_FILE);
                    break;
                default:
                    InvokeApplication (fragment, attachmentChooser.SelectedSource.Identifier);
                    break;
                }
            });
        }

        void InvokeApplication (Fragment fragment, string packageName)
        {
            var intent = new Intent ();
            intent.SetAction (Intent.ActionGetContent);
            intent.AddCategory (Intent.CategoryOpenable);
            intent.SetType ("*/*");
            intent.SetFlags (ActivityFlags.SingleTop);
            intent.PutExtra (Intent.ExtraAllowMultiple, true);
            intent.SetPackage (packageName);

            fragment.StartActivityForResult (intent, REQUEST_EXTERNAL_APP);
        }

        public bool OnActivityResult (Fragment fragment, int accountId, int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == REQUEST_TAKE_PHOTO) {
                if (resultCode == Result.Ok) {
                    var mediaScanIntent = new Intent (Intent.ActionMediaScannerScanFile);
                    var uri = Util.ExternalPhotoUrl (fragment.Activity, CameraOutputPhoto);
                    var path = Util.ExternalPhotoFile (CameraOutputPhoto).Path;
                    mediaScanIntent.SetData (uri);
                    fragment.Activity.SendBroadcast (mediaScanIntent);
                    var attachment = McAttachment.InsertSaveStart (accountId);
                    var filename = Path.GetFileName (path);
                    attachment.SetDisplayName (filename);
                    attachment.ContentType = MimeKit.MimeTypes.GetMimeType (filename);
                    attachment.UpdateFileCopy (path);
                    attachment.UpdateSaveFinish ();
                    if (AttachmentPicked != null) {
                        AttachmentPicked (this, attachment);
                    }
                }
                return true;
            }
            if (requestCode == REQUEST_EXTERNAL_APP) {
                if (resultCode == Result.Ok) {
                    try {
                        var clipData = data.ClipData;
                        if (null == clipData) {
                            var attachment = AttachmentHelper.UriToAttachment (accountId, fragment.Activity, data.Data, data.Type);
                            if (null != attachment) {
                                AttachmentPicked?.Invoke (this, attachment);
                            }
                        } else {
                            for (int i = 0; i < clipData.ItemCount; i++) {
                                var uri = clipData.GetItemAt (i).Uri;
                                var attachment = AttachmentHelper.UriToAttachment (accountId, fragment.Activity, uri, data.Type);
                                if (null != attachment) {
                                    AttachmentPicked?.Invoke (this, attachment);
                                }
                            }
                        }
                    } catch (Exception e) {
                        NachoCore.Utils.Log.Error (NachoCore.Utils.Log.LOG_LIFECYCLE, "Exception while processing the STREAM extra of a Send intent: {0}", e.ToString ());
                    }
                }
                return true;
            }
            if (requestCode == REQUEST_NACHO_FILE) {
                if (resultCode == Result.Ok) {
                    var attachmentId = data.GetIntExtra (FilePickerActivity.EXTRA_ATTACHMENT_ID, 0);
                    var attachment = McAttachment.QueryById<McAttachment> (attachmentId);
                    if (attachment != null) {
                        AttachmentPicked?.Invoke (this, attachment);
                    }
                }
                return true;
            }
            return false;
        }
    }
}
