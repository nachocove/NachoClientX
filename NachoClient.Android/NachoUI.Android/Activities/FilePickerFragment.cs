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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    public interface FilePickerFragmentDelegate
    {
        void FilePickerDidPickFile (FilePickerFragment picker, McAbstrFileDesc file);
    }

    public class FilePickerFragment : DialogFragment, AttachmentDownloaderDelegate
    {

        public FilePickerFragmentDelegate Delegate;
        ListView FileListView;

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.FilePickerFragment, null);
            FileListView = view.FindViewById<ListView> (Resource.Id.file_picker_list);
            FileListView.Adapter = new FilePickerAdapter (this);
            FileListView.ItemClick += FileClicked;
            builder.SetTitle ("Pick a File");
            builder.SetView (view);
            return builder.Create ();
        }

        void FileClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
            var adapter = FileListView.Adapter as FilePickerAdapter;
            var file = adapter [e.Position];
            var attachment = McAttachment.QueryById<McAttachment> (file.Id);
            if (attachment != null) {
                if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                    if (Delegate != null) {
                        Delegate.FilePickerDidPickFile (this, attachment);
                    }
                } else {
                    var downloader = new AttachmentDownloader ();
                    downloader.Delegate = this;
                    downloader.DownloadContext = e.View;
                    var downloadView = e.View.FindViewById (Resource.Id.file_download);
                    var downloadIndicator = e.View.FindViewById (Resource.Id.file_download_indicator);
                    downloadView.Visibility = ViewStates.Gone;
                    downloadIndicator.Visibility = ViewStates.Visible;
                    downloader.Download (attachment);
                }
            }
        }

        public void AttachmentDownloadDidFinish (AttachmentDownloader downloader)
        {
            var view = downloader.DownloadContext as View;
            var downloadView = view.FindViewById (Resource.Id.file_download);
            var downloadIndicator = view.FindViewById (Resource.Id.file_download_indicator);
            downloadView.Visibility = ViewStates.Gone;
            downloadIndicator.Visibility = ViewStates.Gone;
        }

        public void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result)
        {
            var view = downloader.DownloadContext as View;
            var downloadView = view.FindViewById (Resource.Id.file_download);
            var downloadIndicator = view.FindViewById (Resource.Id.file_download_indicator);
            downloadView.Visibility = ViewStates.Gone;
            downloadIndicator.ClearAnimation ();
            downloadIndicator.Visibility = ViewStates.Gone;
            NcAlertView.ShowMessage (Activity, "Download Error", "Sorry, we couldn't download the attachment.  Please try again.");
        }
    }

    public class FilePickerAdapter : BaseAdapter<NcFileIndex>
    {

        List<NcFileIndex> Files;
        Fragment Parent;

        public FilePickerAdapter (Fragment parent) : base ()
        {
            Parent = parent;
            Files = McAbstrFileDesc.GetAllFiles (NcApplication.Instance.Account.Id);
        }

        public override int Count {
            get {
                return Files.Count;
            }
        }

        public override NcFileIndex this[int index] {
            get {
                return Files [index];
            }
        }

        public override long GetItemId (int position)
        {
            return Files [position].Id;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (view == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.FileItemView, null);
            }
            var file = Files [position];
            if (file.FileType == 0) {
                var attachment = McAttachment.QueryById<McAttachment> (file.Id);
                var iconView = view.FindViewById<ImageView> (Resource.Id.file_icon);
                var downloadView = view.FindViewById<ImageView> (Resource.Id.file_download);
                var nameLabel = view.FindViewById<TextView> (Resource.Id.file_name);
                var infoLabel = view.FindViewById<TextView> (Resource.Id.file_info);
                var dateLabel = view.FindViewById<TextView> (Resource.Id.file_date);
                var downloadIndicator = view.FindViewById<ProgressBar> (Resource.Id.file_download_indicator);

                if (attachment != null) {
                    var extension = Pretty.GetExtension (attachment.DisplayName);
                    nameLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);
                    infoLabel.Text = DetailTextForAttachment (attachment);
                    iconView.SetImageResource (FileIconFromExtension (extension));
                    dateLabel.Text = DateToString (attachment.CreatedAt);
                    downloadIndicator.Visibility = ViewStates.Gone;
                    if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        downloadView.Visibility = ViewStates.Gone;
                    } else {
                        downloadView.Visibility = ViewStates.Visible;
                    }
                }
            }

            return view;
        }

        static string DetailTextForAttachment (McAttachment attachment)
        {
            var detailText = "";
            if (attachment.IsInline) {
                detailText += "Inline ";
            }
            string extension = Pretty.GetExtension (attachment.DisplayName);
            detailText += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
            detailText += "file";
            if (0 != attachment.FileSize) {
                detailText += " - " + Pretty.PrettyFileSize (attachment.FileSize);
            }
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                detailText += " - Downloaded";
            }
            return detailText;
        }

        static int FileIconFromExtension (string extension)
        {
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
                if (Pretty.TreatLikeAPhoto (extension)) {
                    return Resource.Drawable.icn_files_img;
                } else {
                    return Resource.Drawable.email_att_files;
                }
            }
        }

        static string DateToString (DateTime date)
        {
            string dateText = "Date unknown";
            if (date != DateTime.MinValue) {
                dateText = Pretty.MediumFullDateTime (date);
            }
            return dateText;
        }
    }
}

