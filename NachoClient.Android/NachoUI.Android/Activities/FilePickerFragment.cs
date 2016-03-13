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
        private const string SAVED_ACCOUNT_ID_KEY = "FilePickerFragment.accountId";

        public FilePickerFragmentDelegate Delegate;

        int accountId;
        ListView FileListView;
        TextView SortSegmentByName;
        TextView SortSegmentByDate;
        TextView SortSegmentByContact;

        public static FilePickerFragment newInstance(int accountId)
        {
            var fragment = new FilePickerFragment ();
            fragment.accountId = accountId;
            return fragment;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            if (null != savedInstanceState) {
                accountId = savedInstanceState.GetInt (SAVED_ACCOUNT_ID_KEY);
            }
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.FilePickerFragment, null);
            FileListView = view.FindViewById<ListView> (Resource.Id.file_picker_list);
            FileListView.Adapter = new FilePickerAdapter (accountId, this);
            FileListView.ItemClick += FileClicked;
            SortSegmentByName = view.FindViewById<TextView> (Resource.Id.file_picker_by_name);
            SortSegmentByDate = view.FindViewById<TextView> (Resource.Id.file_picker_by_date);
            SortSegmentByContact = view.FindViewById<TextView> (Resource.Id.file_picker_by_sender);
            SortSegmentByName.Click += ClickNameSegment;
            SortSegmentByDate.Click += ClickDateSegment;
            SortSegmentByContact.Click += ClickContactSegment;
            HighlightTab (SortSegmentByName);
            builder.SetView (view);
            var dialog = builder.Create ();
            dialog.Window.RequestFeature (WindowFeatures.NoTitle);
            return dialog;
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (SAVED_ACCOUNT_ID_KEY, accountId);
        }

        void ClickContactSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByContact);
            var adapter = FileListView.Adapter as FilePickerAdapter;
            adapter.SortByContat ();
        }

        void ClickDateSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByDate);
            var adapter = FileListView.Adapter as FilePickerAdapter;
            adapter.SortByDate ();
        }

        void ClickNameSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByName);
            var adapter = FileListView.Adapter as FilePickerAdapter;
            adapter.SortByName ();
        }

        private void SelectTab (TextView view)
        {
            HighlightTab (view);
            if (view != SortSegmentByName) {
                UnhighlightTab (SortSegmentByName);
            }
            if (view != SortSegmentByDate) {
                UnhighlightTab (SortSegmentByDate);
            }
            if (view != SortSegmentByContact) {
                UnhighlightTab (SortSegmentByContact);
            }
        }

        private void HighlightTab (TextView view)
        {
            view.SetTextColor (Android.Graphics.Color.White);
            view.SetBackgroundResource (Resource.Color.NachoGreen);
        }

        private void UnhighlightTab (TextView view)
        {
            view.SetTextColor (Resources.GetColor (Resource.Color.NachoGreen));
            view.SetBackgroundResource (Resource.Drawable.BlackBorder);
        }

        void FileClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
            var adapter = FileListView.Adapter as FilePickerAdapter;
            var file = adapter [e.Position];
            if (file.FileType == 0) {
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
            } else if (file.FileType == 1) {
                var note = McNote.QueryById<McNote> (file.Id);
                if ((null != note) && (null != Delegate)) {
                    var attachment = EmailHelper.NoteToAttachment (note);
                    Delegate.FilePickerDidPickFile (this, attachment);
                }

            } else {
                NcAlertView.ShowMessage (Activity, "Cannot Attach", "Sorry, we cannot attach this item.");
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
            if (null != this.Activity) {
                NcAlertView.ShowMessage (this.Activity, "Download Error", "Sorry, we couldn't download the attachment. Please try again.");
            }
        }
    }

    public class FilePickerAdapter : BaseAdapter<NcFileIndex>
    {

        List<NcFileIndex> Files;
        Fragment Parent;
        bool ShowContactHeaders;

        public FilePickerAdapter (int accountId, Fragment parent) : base ()
        {
            Parent = parent;
            Files = McAbstrFileDesc.GetAllFiles (accountId);
            SortByName ();
        }

        public void SortByName ()
        {
            ShowContactHeaders = false;
            Files.Sort ((x, y) => {
                if (x.DisplayName != null && y.DisplayName != null) {
                    return x.DisplayName.CompareTo (y.DisplayName);
                } else if (x.DisplayName != null) {
                    return -1;
                } else if (y.DisplayName != null) {
                    return 1;
                }
                return x.Id - y.Id;
            });
            NotifyDataSetChanged ();
        }

        public void SortByDate ()
        {
            ShowContactHeaders = false;
            Files.Sort ((x, y) => {
                return y.CreatedAt.CompareTo (x.CreatedAt);
            });
            NotifyDataSetChanged ();
        }

        public void SortByContat ()
        {
            ShowContactHeaders = true;
            Files.Sort ((x, y) => {
                if (x.Contact != null && y.Contact != null) {
                    var result = x.Contact.CompareTo (y.Contact);
                    if (result == 0) {
                        return y.CreatedAt.CompareTo (x.CreatedAt);
                    } else {
                        return result;
                    }
                } else if (x.Contact != null) {
                    return -1;
                } else if (y.Contact != null) {
                    return 1;
                }
                return x.Id - y.Id;
            });
            NotifyDataSetChanged ();
        }

        public override int Count {
            get {
                return Files.Count;
            }
        }

        public override NcFileIndex this [int index] {
            get {
                return Files [index];
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int ViewTypeCount {
            get {
                return 3;
            }
        }

        public override int GetItemViewType (int position)
        {
            var file = Files [position];
            return file.FileType;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (view == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.FileItemView, null);
            }
            var file = Files [position];
            var iconView = view.FindViewById<ImageView> (Resource.Id.file_icon);
            var downloadView = view.FindViewById<ImageView> (Resource.Id.file_download);
            var nameLabel = view.FindViewById<TextView> (Resource.Id.file_name);
            var infoLabel = view.FindViewById<TextView> (Resource.Id.file_info);
            var dateLabel = view.FindViewById<TextView> (Resource.Id.file_date);
            var downloadIndicator = view.FindViewById<ProgressBar> (Resource.Id.file_download_indicator);
            bool populated = false;
            if (file.FileType == 0) {
                var attachment = McAttachment.QueryById<McAttachment> (file.Id);
                if (attachment != null) {
                    populated = true;
                    nameLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);
                    infoLabel.Text = DetailTextForAttachment (attachment);
                    iconView.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
                    dateLabel.Text = DateToString (attachment.CreatedAt);
                    downloadIndicator.Visibility = ViewStates.Gone;
                    if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        downloadView.Visibility = ViewStates.Gone;
                    } else {
                        downloadView.Visibility = ViewStates.Visible;
                    }
                }
            } else if (file.FileType == 1) {
                var note = McNote.QueryById<McNote> (file.Id);
                if (note != null) {
                    populated = true;
                    nameLabel.Text = Path.GetFileNameWithoutExtension (note.DisplayName);
                    infoLabel.Text = "Note";
                    iconView.SetImageResource (Resource.Drawable.email_att_files);
                    dateLabel.Text = DateToString (note.CreatedAt);
                    downloadIndicator.Visibility = ViewStates.Gone;
                    downloadView.Visibility = ViewStates.Gone;
                }
            }

            if (!populated) {
                nameLabel.Text = "";
                infoLabel.Text = "This file is unavailable";
                dateLabel.Text = "";
                iconView.Visibility = ViewStates.Invisible;
                downloadView.Visibility = ViewStates.Gone;
                downloadIndicator.Visibility = ViewStates.Gone;
            } else {
                iconView.Visibility = ViewStates.Visible;
            }

            var header = view.FindViewById<LinearLayout> (Resource.Id.file_contact_header);
            if (ShowContactHeaders && !String.IsNullOrEmpty (file.Contact)) {
                if (position == 0 || !Files [position - 1].Contact.Equals (file.Contact)) {
                    header.Visibility = ViewStates.Visible;
                    var contactNameLabel = view.FindViewById<TextView> (Resource.Id.file_contact_name);
                    contactNameLabel.Text = Pretty.SenderString (file.Contact);
                    var userPhotoView = view.FindViewById<ContactPhotoView> (Resource.Id.user_initials);
                    int colorIndex;
                    string initials;
                    EmailColorAndInitials (file.Contact, file.AccountId, out colorIndex, out initials); 
                    userPhotoView.SetEmailAddress (file.AccountId, file.Contact, initials, colorIndex);
                } else {
                    header.Visibility = ViewStates.Gone;
                }
            } else {
                header.Visibility = ViewStates.Gone;
            }

            return view;
        }

        void EmailColorAndInitials (string from, int accountId, out int ColorResource, out string Initials)
        {
            // Parse the from address
            var mailboxAddress = NcEmailAddress.ParseMailboxAddressString (from);
            if (null == mailboxAddress) {
                ColorResource = Bind.ColorForUser (1);
                Initials = "";
                return;
            }
            // And get a McEmailAddress
            McEmailAddress emailAddress;
            if (!McEmailAddress.Get (accountId, mailboxAddress, out emailAddress)) {
                ColorResource = Bind.ColorForUser (1);
                Initials = "";
                return;
            }
            // Cache the color
            ColorResource = Bind.ColorForUser (emailAddress.ColorIndex);
            Initials = EmailHelper.Initials (from);
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

