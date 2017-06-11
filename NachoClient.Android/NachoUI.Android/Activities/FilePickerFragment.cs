//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.Support.V4.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    public class FilePickerFragment : Fragment, FilePickerAdapter.Listener
    {

        FilePickerAdapter Adapter;

        #region Subviews

        RecyclerView ListView;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecyle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.FilePickerFragment, container, false);
            FindSubviews (view);
            Adapter = new FilePickerAdapter (this);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            Adapter.Cleanup ();
            base.OnDestroyView ();
        }

        #endregion

        #region Listener

        public void OnAttachmentSelected (McAttachment attachment)
        {
            FinishWithAttachment (attachment);
        }

        #endregion

        #region Private Helpers

        void FinishWithAttachment (McAttachment attachment)
        {
            var intent = new Intent ();
            intent.PutExtra (FilePickerActivity.EXTRA_ATTACHMENT_ID, attachment.Id);
            Activity.SetResult (Android.App.Result.Ok, intent);
            Activity.Finish ();
        }

        #endregion

        public void SortByName ()
        {
            Adapter.SetSort (FilePickerAdapter.SortMode.Name);
        }

        public void SortByDate ()
        {
            Adapter.SetSort (FilePickerAdapter.SortMode.Date);
        }

        public void SortByContact ()
        {
            Adapter.SetSort (FilePickerAdapter.SortMode.Contact);
        }
    }

    public class FilePickerAdapter : GroupedListRecyclerViewAdapter, AttachmentDownloaderDelegate
    {

        public interface Listener
        {
            void OnAttachmentSelected (McAttachment attachment);
        }

        public enum SortMode
        {
            Name,
            Date,
            Contact
        }

        class FileGroup 
        {
            public string Name;
            public List<NcFileIndex> Files;
        }

        SortMode Sort = SortMode.Name;
        bool IsLoaded;

        WeakReference<Listener> WeakListener;
        List<NcFileIndex> Files;
        List<FileGroup> Groups;
        Dictionary<int, AttachmentDownloader> DownloadersByAttachmentId;

        public FilePickerAdapter (Listener listener) : base()
        {
            WeakListener = new WeakReference<Listener> (listener);
            DownloadersByAttachmentId = new Dictionary<int, AttachmentDownloader> ();
            Groups = new List<FileGroup> ();
            Reload ();
        }

        public void Cleanup()
        {
            foreach (var pair in DownloadersByAttachmentId) {
                pair.Value.Delegate = null;
            }
            DownloadersByAttachmentId.Clear ();
        }

        void Reload ()
        {
            NcTask.Run (() => {
                var files = McAbstrFileDesc.GetAllFiles (McAccount.GetUnifiedAccount ().Id);
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                    Files = files;
                    Resort ();
                    IsLoaded = true;
                });
            }, "FilePickerFragment.Reload");
        }

        public void SetSort (SortMode sort)
        {
            Sort = sort;
            if (IsLoaded) {
                Resort ();
            }
        }

        void Resort ()
        {
            Groups.Clear ();
            if (Sort == SortMode.Name){
                Files.Sort ((x, y) => {
                    if (x.DisplayName == null && y.DisplayName == null){
                        return x.Id - y.Id;
                    }
                    if (x.DisplayName == null){
                        return 1;
					}
					if (y.DisplayName == null) {
						return -1;
					}
                    return string.Compare (x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                });
                Groups.Add (new FileGroup (){
                    Name = null,
                    Files = Files
                });
            }else if (Sort == SortMode.Date){
                Files.Sort ((x, y) => {
                    return y.CreatedAt.CompareTo (x.CreatedAt);
				});
                Groups.Add (new FileGroup () {
					Name = null,
					Files = Files
				});
			} else if (Sort == SortMode.Contact) {
				Files.Sort ((x, y) => {
                    if (x.Contact == y.Contact) {
                        return y.CreatedAt.CompareTo (x.CreatedAt);
                    }
                    if (x.Contact == null){
                        return 1;
                    }
                    if (y.Contact == null){
                        return -1;
                    }
                    return string.Compare (x.Contact, y.Contact, StringComparison.OrdinalIgnoreCase);
                });
                string contact = null;
                FileGroup fileGroup = null;
                foreach (var file in Files){
                    if (file.Contact != null) {
                        if (file.Contact != contact) {
                            var mailbox = NcEmailAddress.ParseMailboxAddressString (file.Contact);
                            string name = file.Contact;
                            if (mailbox != null){
                                if (!String.IsNullOrEmpty (mailbox.Name)){
                                    name = mailbox.Name;
                                }else{
                                    name = mailbox.Address;
                                }
                            }
                            fileGroup = new FileGroup () { Name = name };
                            fileGroup.Files = new List<NcFileIndex> ();
                            Groups.Add (fileGroup);
                            contact = file.Contact;
                        }
                        fileGroup.Files.Add (file);
                    }
                }
            }
            NotifyDataSetChanged ();
        }

        public override int GroupCount {
            get {
                return Groups.Count;
            }
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            return Groups [groupPosition].Name;
        }

        public override int GroupItemCount (int groupPosition)
        {
            return Groups [groupPosition].Files.Count;
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            return FileViewHolder.Create (parent);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var file = Groups [groupPosition].Files [position];
            (holder as FileViewHolder).SetFile (file);
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                var file = Groups [groupPosition].Files [position];
                McAttachment attachment = null;
                if (file.FileType == 0) {
                    attachment = McAttachment.QueryById<McAttachment> (file.Id);
					if (attachment.FilePresence != McAbstrFileDesc.FilePresenceEnum.Complete) {
						DownloadAttachment (attachment);
						NotifyItemChanged (groupPosition, position);
						attachment = null;
                    }
                } else if (file.FileType == 1) {
                    var note = McNote.QueryById<McNote> (file.Id);
                    attachment = EmailHelper.NoteToAttachment (note);
                }
                if (attachment != null){
                    listener.OnAttachmentSelected (attachment);
                }
            }
        }

        void NotifyAttachmentChanged (McAttachment attachment)
        {
            for (int groupPosition = 0; groupPosition < Groups.Count; ++groupPosition){
                for (int itemPosition = 0; itemPosition < Groups [groupPosition].Files.Count; ++itemPosition){
                    var file = Groups [groupPosition].Files [itemPosition];
                    if (file.FileType == 0 && file.Id == attachment.Id){
                        NotifyItemChanged (groupPosition, itemPosition);
                        break;
                    }
                }
            }
        }

        void DownloadAttachment (McAttachment attachment)
		{
			if (!DownloadersByAttachmentId.ContainsKey (attachment.Id)) {
				var downloader = new AttachmentDownloader ();
				DownloadersByAttachmentId.Add (attachment.Id, downloader);
				downloader.Delegate = this;
				downloader.Download (attachment);
			}
		}

		public void AttachmentDownloadDidFinish (AttachmentDownloader downloader)
		{
			DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
            NotifyAttachmentChanged (downloader.Attachment);
        }

		public void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result)
		{
			DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
			NotifyAttachmentChanged (downloader.Attachment);
		}

        class FileViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
		{
			ImageView IconView;
			TextView NameLabel;
			TextView DetailLabel;
			View DownloadFrame;
			ImageView DownloadIndicator;
			ImageView ErrorIndicator;
			ProgressBar DownloadProgress;
			EventHandler ClickHandler;

            public static FileViewHolder Create (ViewGroup parent)
			{
                var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FilePickerListItem, parent, false);
                return new FileViewHolder (view);
			}

            public FileViewHolder (View view) : base (view)
			{
				IconView = view.FindViewById (Resource.Id.icon) as ImageView;
				NameLabel = view.FindViewById (Resource.Id.attachment_name) as TextView;
				DetailLabel = view.FindViewById (Resource.Id.attachment_detail) as TextView;
				DownloadFrame = view.FindViewById (Resource.Id.attachment_download_frame);
				ErrorIndicator = DownloadFrame.FindViewById (Resource.Id.error_indicator) as ImageView;
				DownloadIndicator = DownloadFrame.FindViewById (Resource.Id.download_indicator) as ImageView;
				DownloadProgress = DownloadFrame.FindViewById (Resource.Id.download_progress) as ProgressBar;
			}

            public void SetFile (NcFileIndex file)
			{
                McAttachment attachment = null;
				if (file.FileType == 0) {
					attachment = McAttachment.QueryById<McAttachment> (file.Id);
				}
                var name = Path.GetFileNameWithoutExtension (file.DisplayName);
				if (String.IsNullOrEmpty (name)) {
                    name = ItemView.Context.GetString (Resource.String.file_picker_noname);
				}
				NameLabel.Text = name;
                if (attachment != null) {
                    IconView.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
                    DetailLabel.Text = Pretty.GetAttachmentDetail (attachment);
                    if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Error) {
                        DownloadFrame.Visibility = ViewStates.Visible;
                        ErrorIndicator.Visibility = ViewStates.Visible;
                        DownloadIndicator.Visibility = ViewStates.Gone;
                        DownloadProgress.Visibility = ViewStates.Gone;
                    } else if (attachment.FilePresence != McAbstrFileDesc.FilePresenceEnum.Complete) {
                        DownloadFrame.Visibility = ViewStates.Visible;
                        ErrorIndicator.Visibility = ViewStates.Gone;
                        if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Partial) {
                            var pending = McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id);
                            if (pending != null && pending.State != McPending.StateEnum.Failed) {
                                DownloadIndicator.Visibility = ViewStates.Gone;
                                DownloadProgress.Visibility = ViewStates.Visible;
                            } else {
                                DownloadIndicator.Visibility = ViewStates.Visible;
                                DownloadProgress.Visibility = ViewStates.Gone;
                            }
                        } else {
                            DownloadIndicator.Visibility = ViewStates.Visible;
                            DownloadProgress.Visibility = ViewStates.Gone;
                        }
                    } else {
                        DownloadFrame.Visibility = ViewStates.Gone;
                    }
                }else{
                    IconView.SetImageResource (Resource.Drawable.icn_files_txt);
                    DetailLabel.SetText (Resource.String.file_picker_note);
                    DownloadFrame.Visibility = ViewStates.Gone;
                    ErrorIndicator.Visibility = ViewStates.Gone;
                }
			}
		}
    }

}

