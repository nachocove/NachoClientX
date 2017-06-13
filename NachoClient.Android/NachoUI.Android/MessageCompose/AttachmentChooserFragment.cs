//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using Android.Content;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;
using Android.Widget;
using Android.Views;
using Android.OS;
using NachoPlatform;
using Android.Graphics.Drawables;
using Android.Support.V4.Content;
using Android.Content.PM;

namespace NachoClient.AndroidClient
{
    public class AttachmentChooserFragment : NcDialogFragment
    {

        private const int REQUEST_CAMERA_PERMISSION = 1;

        public AttachmentSource SelectedSource { get; private set; }
        private AttachmentSourceAdapter Adapter;
        private AttachmentSource CameraSource;

        public AttachmentChooserFragment () : base ()
        {
            RetainInstance = true;
        }

        List<AttachmentSource> GetSources ()
        {
            Intent shareIntent = new Intent ();
            shareIntent.SetAction (Intent.ActionGetContent);
            shareIntent.AddCategory (Intent.CategoryOpenable);
            shareIntent.SetType ("*/*");
            shareIntent.PutExtra (Intent.ExtraAllowMultiple, true);
            var resolvedActivities = Activity.PackageManager.QueryIntentActivities (shareIntent, 0);

            var sources = new List<AttachmentSource> ();

            if (Util.CanTakePhoto (Activity)) {
                CameraSource = new AttachmentSource () {
                    Identifier = AttachmentSource.IDENTIFIER_TAKE_PHOTO,
                    DisplayName = GetString (Resource.String.attachment_chooser_take_photo),
                    Icon = Activity.GetDrawable (Resource.Drawable.attachment_take_photo)
                };
                sources.Add (CameraSource);
            }

            sources.Add (new AttachmentSource ()
            {
                Identifier = AttachmentSource.IDENTIFIER_NACHO_FILE,
                DisplayName = GetString (Resource.String.attachment_chooser_nacho_files),
                Icon=Activity.GetDrawable (Resource.Drawable.attachment_add_files)
            });

            foreach (var resolvedActivity in resolvedActivities) {
                var packageName = resolvedActivity.ActivityInfo.PackageName;
                var applicationInfo = Activity.PackageManager.GetApplicationInfo (packageName, 0);
                sources.Add (new AttachmentSource () {
                    Identifier = packageName,
                    DisplayName = Activity.PackageManager.GetApplicationLabel (applicationInfo),
                    Icon = Activity.PackageManager.GetApplicationIcon (applicationInfo)
                });
            }

            return sources;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var sources = GetSources ();
            Adapter = new AttachmentSourceAdapter (sources);

            var builder = new AlertDialog.Builder (Activity);
            var view = new ListView (Activity);
            view.Divider = null;
            view.DividerHeight = 0;
            view.Adapter = Adapter;
            view.ItemClick += ItemClick;
            builder.SetView (view);
            return builder.Create ();
        }

        private void ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var source = Adapter [e.Position];
            if (source == CameraSource){
	            bool hasCameraPermission = ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.Camera) == Permission.Granted;
	            bool hasStorage = ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;
                if (hasCameraPermission && hasStorage) {
                    SelectedSource = source;
                }else{
                    RequestCameraPermissions ();
	            }
            }else{
                SelectedSource = source;
            }
			if (SelectedSource != null) {
                Dismiss ();
            }
        }

        void RequestCameraPermissions ()
        {
            if (ShouldShowRequestPermissionRationale (Android.Manifest.Permission.Camera) || ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteExternalStorage)) {
                var builder = new AlertDialog.Builder (Activity);
                builder.SetTitle (Resource.String.attachment_chooser_camera_permission_request_title);
                builder.SetMessage (Resource.String.attachment_chooser_camera_permission_request_message);
                builder.SetPositiveButton (Resource.String.attachment_chooser_camera_permission_request_ack, (sender, e) => {
                    RequestPermissions (new string [] {
                        Android.Manifest.Permission.Camera,
                        Android.Manifest.Permission.WriteExternalStorage
                    }, REQUEST_CAMERA_PERMISSION);
                });
                builder.Show ();
			} else {
				RequestPermissions (new string [] {
					Android.Manifest.Permission.Camera,
					Android.Manifest.Permission.WriteExternalStorage
				}, REQUEST_CAMERA_PERMISSION);
            }
        }

        public override void OnRequestPermissionsResult (int requestCode, string [] permissions, Permission [] grantResults)
        {
            if (requestCode == REQUEST_CAMERA_PERMISSION){
                if (grantResults.Length == 2 && grantResults[0] == Permission.Granted && grantResults[1] == Permission.Granted){
                    SelectedSource = CameraSource;
                    Dismiss ();
                }else{
                    if (ShouldShowRequestPermissionRationale (Android.Manifest.Permission.Camera) || ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteExternalStorage)){
                        RequestCameraPermissions ();
                    }else{
                        var builder = new AlertDialog.Builder (Activity);
						builder.SetTitle (Resource.String.attachment_chooser_camera_permission_denied_title);
						builder.SetMessage (Resource.String.attachment_chooser_camera_permission_denied_message);
						builder.SetPositiveButton (Resource.String.attachment_chooser_camera_permission_denied_settings, (sender, e) => {
							var uri = Android.Net.Uri.FromParts ("package", Activity.PackageName, null);
                            var intent = new Intent (Android.Provider.Settings.ActionApplicationDetailsSettings, uri);
                            StartActivity (intent);
                        });
                        builder.Show ();
                    }
                }
            }
            base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
        }

        public class AttachmentSource
        {
            public Drawable Icon;
            public string DisplayName;
            public string Identifier;

            public const string IDENTIFIER_TAKE_PHOTO = "NachoClient.AndroidClient.AttachmentChooserFragment.AttachmentSource.IDENTIFIER_TAKE_PHOTO";
            public const string IDENTIFIER_NACHO_FILE = "NachoClient.AndroidClient.AttachmentChooserFragment.AttachmentSource.IDENTIFIER_NACHO_FILE";
        }

        private class AttachmentSourceAdapter : BaseAdapter<AttachmentSource>
        {
            private List<AttachmentSource> AttachmentSources;

            public AttachmentSourceAdapter (List<AttachmentSource> attachmentSources)
            {
                AttachmentSources = attachmentSources;
            }

            public override int Count {
                get {
                    return AttachmentSources.Count;
                }
            }

            public override AttachmentSource this [int index] {
                get {
                    return AttachmentSources [index];
                }
            }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View view = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttachmentSourceListItem, parent, false);
                var source = AttachmentSources [position];

                var iconView = view.FindViewById<ImageView> (Resource.Id.icon);
                var nameLabel = view.FindViewById<TextView> (Resource.Id.attachment_source_name);

                iconView.SetImageDrawable (source.Icon);
                nameLabel.Text = source.DisplayName;

                return view;
            }
        }
    }
}

