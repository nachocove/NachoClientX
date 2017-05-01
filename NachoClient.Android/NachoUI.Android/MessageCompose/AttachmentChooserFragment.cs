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

namespace NachoClient.AndroidClient
{
    public class AttachmentChooserFragment : NcDialogFragment
    {

        public AttachmentSource SelectedSource { get; private set; }
        private AttachmentSourceAdapter Adapter;

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
            var resolvedActivities = Context.PackageManager.QueryIntentActivities (shareIntent, 0);

            var sources = new List<AttachmentSource> ();

            if (Util.CanTakePhoto (Context)) {
                sources.Add (new AttachmentSource () {
                    Identifier=AttachmentSource.IDENTIFIER_TAKE_PHOTO,
                    DisplayName=GetString (Resource.String.attachment_chooser_take_photo),
                    Icon=Context.GetDrawable (Resource.Drawable.attachment_take_photo)
                });
            }

            sources.Add (new AttachmentSource ()
            {
                Identifier = AttachmentSource.IDENTIFIER_NACHO_FILE,
                DisplayName = GetString (Resource.String.attachment_chooser_nacho_files),
                Icon=Context.GetDrawable (Resource.Drawable.attachment_add_files)
            });

            foreach (var resolvedActivity in resolvedActivities) {
                var packageName = resolvedActivity.ActivityInfo.PackageName;
                var applicationInfo = Context.PackageManager.GetApplicationInfo (packageName, 0);
                sources.Add (new AttachmentSource () {
                    Identifier = packageName,
                    DisplayName = Context.PackageManager.GetApplicationLabel (applicationInfo),
                    Icon = Context.PackageManager.GetApplicationIcon (applicationInfo)
                });
            }

            return sources;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var sources = GetSources ();
            Adapter = new AttachmentSourceAdapter (sources);

            var builder = new AlertDialog.Builder (this.Activity);
            builder.SetAdapter (Adapter, ItemClick);
            return builder.Create ();
        }

        private void ItemClick (object sender, Android.Content.DialogClickEventArgs e)
        {
            SelectedSource = Adapter [e.Which];
            Adapter.NotifyDataSetChanged ();
            Dismiss ();
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

