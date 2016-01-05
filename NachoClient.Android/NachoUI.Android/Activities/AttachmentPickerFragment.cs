//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Provider;

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public interface AttachmentPickerFragmentDelegate
    {
        void AttachmentPickerDidPickAttachment (AttachmentPickerFragment picker, McAttachment attachment);
    }

    public class AttachmentPickerFragment : DialogFragment, FilePickerFragmentDelegate
    {
        private const string FILE_PICKER_TAG = "FilePickerFragment";

        GridView OptionsGridView;
        List<AttachmentOption> Options;
        static int SELECT_PHOTO = 1;
        static int TAKE_PHOTO = 2;
        public McAccount Account;
        Android.Net.Uri CameraOutputUri;

        public AttachmentPickerFragmentDelegate Delegate;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                var filePicker = FragmentManager.FindFragmentByTag<FilePickerFragment> (FILE_PICKER_TAG);
                if (null != filePicker) {
                    filePicker.Delegate = this;
                }
            }
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.AttachmentPickerFragment, null);
            OptionsGridView = view.FindViewById<GridView> (Resource.Id.attachment_options);
            Options = new List<AttachmentOption> (3);
            Options.Add (new AttachmentOption ("Add Photo", Resource.Drawable.calendar_add_photo, AddPhoto));
            if (Util.CanTakePhoto (Activity)) {
                Options.Add (new AttachmentOption ("Take Photo", Resource.Drawable.calendar_take_photo, TakePhoto));
            }
            Options.Add (new AttachmentOption ("Add File", Resource.Drawable.calendar_add_files, AddFile));
            OptionsGridView.Adapter = new AttachmentOptionsAdapter (this, Options);
            OptionsGridView.ItemClick += OptionClicked;
            builder.SetView (view);
            var dialog = builder.Create ();
            dialog.Window.RequestFeature (WindowFeatures.NoTitle);
            return dialog;
        }

        public override void OnAttach (Activity activity)
        {
            base.OnAttach (activity);
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == SELECT_PHOTO) {
                if (resultCode == Result.Ok && data != null) {
                    if (Delegate != null) {
                        var uri = data.Data;
                        string docId = null;
                        var cursor = Activity.ContentResolver.Query (uri, null, null, null, null);
                        cursor.MoveToFirst ();
                        docId = cursor.GetString (0);
                        docId = docId.Split (':') [1];
                        cursor.Close ();

                        var selection = MediaStore.Images.Media.InterfaceConsts.Id + "=?";
                        cursor = Activity.ContentResolver.Query (MediaStore.Images.Media.ExternalContentUri, null, selection, new string[] { docId }, null);
                        var index = cursor.GetColumnIndexOrThrow (MediaStore.Images.Media.InterfaceConsts.Data);
                        cursor.MoveToFirst ();
                        var path = cursor.GetString (index);
                        if (path != null) {
                            var attachment = McAttachment.InsertSaveStart (Account.Id);
                            var filename = Path.GetFileName (path);
                            attachment.SetDisplayName (filename);
                            attachment.ContentType = MimeKit.MimeTypes.GetMimeType (filename);
                            attachment.UpdateFileCopy (path);
                            Delegate.AttachmentPickerDidPickAttachment (this, attachment);
                            Dismiss ();
                        } else {
                            NcAlertView.ShowMessage (Activity, "Can't Attach Image", "Sorry, we can't attach that image.");
                        }
                        cursor.Close ();
                    }
                }
            } else if (requestCode == TAKE_PHOTO) {
                if (resultCode == Result.Ok) {
                    var mediaScanIntent = new Intent (Intent.ActionMediaScannerScanFile);
                    mediaScanIntent.SetData (CameraOutputUri);
                    Activity.SendBroadcast (mediaScanIntent);
                    var attachment = McAttachment.InsertSaveStart (Account.Id);
                    var filename = Path.GetFileName (CameraOutputUri.Path);
                    attachment.SetDisplayName (filename);
                    attachment.ContentType = MimeKit.MimeTypes.GetMimeType (filename);
                    attachment.UpdateFileCopy (CameraOutputUri.Path);
                    Delegate.AttachmentPickerDidPickAttachment (this, attachment);
                    Dismiss ();
                }
            } else {
                base.OnActivityResult (requestCode, resultCode, data);
            }
        }

        void AddPhoto ()
        {
            var intent = new Intent ();
            intent.SetType ("image/*");
            intent.SetAction (Intent.ActionGetContent);
            StartActivityForResult (Intent.CreateChooser (intent, "Select Photo"), SELECT_PHOTO);
        }

        void TakePhoto ()
        {
            CameraOutputUri = Util.TakePhoto (this, TAKE_PHOTO);
        }

        void AddFile ()
        {
            var filePicker = new FilePickerFragment ();
            filePicker.Delegate = this;
            filePicker.Show (FragmentManager, FILE_PICKER_TAG);
        }

        void OptionClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
            var option = Options [e.Position];
            option.Action ();
        }

        public void FilePickerDidPickFile (FilePickerFragment picker, McAbstrFileDesc file)
        {
            picker.Dismiss ();
            if (Delegate != null) {
                var attachment = file as McAttachment;
                if (attachment != null) {
                    Delegate.AttachmentPickerDidPickAttachment (this, attachment);
                }
            }
            Dismiss ();
        }
    }

    public class AttachmentOption
    {
        public string Label;
        public int Drawable;
        public Action Action;

        public AttachmentOption (string label, int drawable, Action action)
        {
            Label = label;
            Drawable = drawable;
            Action = action;
        }
    }

    public class AttachmentOptionsAdapter : BaseAdapter<AttachmentOption>
    {

        Fragment Parent;
        List<AttachmentOption> Options;

        public AttachmentOptionsAdapter (Fragment parent, List<AttachmentOption> options)
        {
            Parent = parent;
            Options = options;
        }

        public override int Count {
            get {
                return Options.Count;
            }
        }

        public override AttachmentOption this [int index] {
            get {
                return Options [index];
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (view == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.AttachmentOptionItemView, null);
            }
            var option = Options [position];
            var icon = view.FindViewById<ImageView> (Resource.Id.option_icon);
            var label = view.FindViewById<TextView> (Resource.Id.option_label);
            icon.SetImageResource (option.Drawable);
            label.Text = option.Label;
            return view;
        }

    }
}

