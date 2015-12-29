﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Widget;
using Android.Content.PM;
using System.Collections.Generic;
using Android.Content;
using Android.Views;
using Android.Graphics.Drawables;
using Android.Provider;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using Android.Support.V4.Content;
using NachoClient.Build;

namespace NachoClient.AndroidClient
{
    public class AttachmentHelper
    {
        private static string[] dataColumnProjection = {
            Android.Provider.MediaStore.MediaColumns.DisplayName
        };

        private static McAttachment MakeAttachment (string filePath, string displayName, string type)
        {
            var attachment = McAttachment.InsertSaveStart (NcApplication.Instance.Account.Id);
            attachment.ContentType = type;
            attachment.DisplayName = displayName;
            attachment.UpdateFileCopy (filePath);
            attachment.UpdateSaveFinish ();
            return attachment;
        }

        private static McAttachment MakeAttachment (System.IO.Stream iStream, string displayName, string type)
        {
            var attachment = McAttachment.InsertSaveStart (NcApplication.Instance.Account.Id);
            attachment.ContentType = type;
            attachment.DisplayName = displayName;
            attachment.UpdateData ((oStream) => {
                iStream.CopyTo (oStream);
            });
            attachment.UpdateSaveFinish ();
            return attachment;
        }

        public static McAttachment UriToAttachment (Context context, Android.Net.Uri uri, string mimeType)
        {
            try {
                if ("file" == uri.Scheme) {
                    return MakeAttachment (uri.Path, new System.IO.FileInfo (uri.Path).Name, mimeType);
                } else if ("content" == uri.Scheme) {
                    var contentResolver = context.ContentResolver;
                    mimeType = contentResolver.GetType (uri);
                    using (var stream = contentResolver.OpenTypedAssetFileDescriptor (uri, mimeType, null).CreateInputStream ()) {
                        var cursor = contentResolver.Query (uri, dataColumnProjection, null, null, null);
                        if (cursor.MoveToNext ()) {
                            return MakeAttachment (stream, cursor.GetString (0), mimeType);
                        }
                    }
                }
            } catch (Exception e) {
                Log.Error (Log.LOG_LIFECYCLE, "Exception while creating an attachment during processing of a Send intent: {0}", e.ToString ());
            }
            return null;
        }

        public static int FileIconFromExtension (McAttachment attachment)
        {
            var extension = Pretty.GetExtension (attachment.DisplayName);

            switch (extension) {
            case ".DOC":
            case ".DOCX":
                return Resource.Drawable.icn_files_wrd;
            case ".PPT":
            case ".PPTX":
                return Resource.Drawable.icn_files_ppt;
            case ".XLS":
            case ".XLSX":
                return Resource.Drawable.icn_files_xls;
            case ".PDF":
                return Resource.Drawable.icn_files_pdf;
            case ".TXT":
            case ".TEXT":
                return Resource.Drawable.icn_files_txt;
            case ".ZIP":
                return Resource.Drawable.icn_files_zip;
            case ".PNG":
                return Resource.Drawable.icn_files_png;
            default:
                if (attachment.IsImageFile ()) {
                    return Resource.Drawable.icn_files_img;
                } else {
                    return Resource.Drawable.email_att_files;
                }
            }
        }

        public static void OpenAttachment (Context context, McAttachment attachment, bool useInternalViewer = true)
        {
            if (useInternalViewer && attachment.IsImageFile ()) {
                var viewerIntent = ImageViewActivity.ImageViewIntent (context, attachment);
                context.StartActivity (viewerIntent);
                return;
            }

            try {
                Android.Net.Uri fileUri;
                var file = new Java.IO.File (attachment.GetFilePath ());
                try {
                    fileUri = FileProvider.GetUriForFile (context, BuildInfo.FileProvider, file);
                } catch (Java.Lang.IllegalArgumentException e) {
                    Log.Error (Log.LOG_UTILS, "FileProvider error\n{0}", e.StackTrace);
                    NcAlertView.ShowMessage (context, "Attachment", String.Format ("The selected file cannot be shared: {0}", attachment.DisplayName));
                    return;
                }
                var intent = new Intent (Intent.ActionView);
                intent.AddFlags (ActivityFlags.GrantReadUriPermission);
                string fileType;
                if(!String.IsNullOrEmpty(attachment.ContentType)) {
                    fileType = attachment.ContentType;
                } else {
                    fileType = context.ContentResolver.GetType (fileUri);
                }
                intent.SetDataAndType (fileUri, fileType.ToLower());
                // Look for potential handlers
                var packageManager = context.PackageManager;
                var activities = packageManager.QueryIntentActivities (intent, PackageInfoFlags.MatchDefaultOnly);
                var isIntentSafe = 0 < activities.Count;
                if (isIntentSafe) {
                    context.StartActivity (intent);
                } else {
                    NcAlertView.ShowMessage (context, "Attachment", String.Format ("No application can open this attachment: {0}", attachment.DisplayName));
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_UTILS, "Sharing error\n{0}", ex.StackTrace);
                NcAlertView.ShowMessage (context, "Attachment", String.Format ("The selected file cannot be shared: {0}", attachment.DisplayName));
            }
        }

    }


    public class ChooserArrayAdapter : ArrayAdapter<String>
    {
        PackageManager mPm;
        int mTextViewResourceId;
        List<String> mPackages;

        public static string ADD_FILE = "Add File";
        public static string TAKE_PHOTO = "Take Photo";

        public ChooserArrayAdapter (Context context, int resource, int textViewResourceId, List<String> packages) :
            base (context, resource, textViewResourceId, packages)
        {
            mPm = context.PackageManager;
            mTextViewResourceId = textViewResourceId;
            mPackages = packages;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var pkg = mPackages [position];
            var view = base.GetView (position, convertView, parent);

            try {
                string appName;
                Drawable appIcon;
                if (ADD_FILE == pkg) {
                    appName = pkg;
                    appIcon = parent.Resources.GetDrawable (Resource.Drawable.calendar_add_files);
                } else if (TAKE_PHOTO == pkg) {
                    appName = pkg;
                    appIcon = parent.Resources.GetDrawable (Resource.Drawable.calendar_take_photo);
                } else {
                    var ai = mPm.GetApplicationInfo (pkg, 0);
                    appName = mPm.GetApplicationLabel (ai);
                    appIcon = mPm.GetApplicationIcon (pkg);
                }
                TextView textView = (TextView)view.FindViewById (mTextViewResourceId);
                textView.Text = appName;
                textView.SetCompoundDrawablesWithIntrinsicBounds (appIcon, null, null, null);
                textView.CompoundDrawablePadding = (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, 14, Context.Resources.DisplayMetrics);
            } catch (Android.Content.PM.PackageManager.NameNotFoundException e) {
                NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_EMAIL, "ChooserArrayAdapter: {0}", e);
            }
            return view;
        }
    }
}

