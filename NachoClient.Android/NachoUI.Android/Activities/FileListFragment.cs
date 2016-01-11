
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore;
using NachoCore.Utils;
using Android.Support.V4.Widget;
using NachoCore.Model;
using System.IO;
using Android.Content.PM;

namespace NachoClient.AndroidClient
{
    public class FileListFragment : Fragment, AttachmentDownloaderDelegate
    {
        int accountId;

        public static FileListFragment newInstance (int accountId)
        {
            var fragment = new FileListFragment ();
            fragment.accountId = accountId;
            return fragment;
        }

        class ActionAttachmentDownloader : AttachmentDownloader
        {
            public int tag;

            public ActionAttachmentDownloader (int tag) : base ()
            {
                this.tag = tag;
            }
        }

        private const int DELETE_TAG = 1;
        private const int FORWARD_TAG = 2;
        private const int VIEW_TAG = 3;

        public FilePickerFragmentDelegate Delegate;
        SwipeMenuListView FileListView;
        FilePickerAdapter FileListAdapter;

        TextView SortSegmentByName;
        TextView SortSegmentByDate;
        TextView SortSegmentByContact;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.FileListFragment, container, false);

            var swipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            swipeRefreshLayout.Enabled = false;

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            FileListView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            FileListView.ItemClick += FileClicked;
            SortSegmentByName = view.FindViewById<TextView> (Resource.Id.file_picker_by_name);
            SortSegmentByDate = view.FindViewById<TextView> (Resource.Id.file_picker_by_date);
            SortSegmentByContact = view.FindViewById<TextView> (Resource.Id.file_picker_by_sender);
            SortSegmentByName.Click += ClickNameSegment;
            SortSegmentByDate.Click += ClickDateSegment;
            SortSegmentByContact.Click += ClickContactSegment;
            HighlightTab (SortSegmentByName);

            SetupFileListAdapter (view);
 
            return view;
        }

        void SetupFileListAdapter (View view)
        {
            FileListAdapter = new FilePickerAdapter (accountId, this);
            FileListView.Adapter = FileListAdapter;

            FileListView.setMenuCreator ((menu) => {
                SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                forwardItem.setBackground (A.Drawable_NachoSwipeFileForward (Activity));
                forwardItem.setWidth (dp2px (90));
                forwardItem.setTitle ("Forward");
                forwardItem.setTitleSize (14);
                forwardItem.setTitleColor (A.Color_White);
                forwardItem.setIcon (A.Id_NachoSwipeEmailDefer);
                forwardItem.setId (FORWARD_TAG);
                menu.addMenuItem (forwardItem, SwipeMenu.SwipeSide.LEFT);
                if (0 == menu.getViewType ()) {
                    SwipeMenuItem deleteItem = new SwipeMenuItem (Activity.ApplicationContext);
                    deleteItem.setBackground (A.Drawable_NachoSwipeFileDelete (Activity));
                    deleteItem.setWidth (dp2px (90));
                    deleteItem.setTitle ("Delete");
                    deleteItem.setTitleSize (14);
                    deleteItem.setTitleColor (A.Color_White);
                    deleteItem.setIcon (A.Id_NachoSwipeFileDelete);
                    deleteItem.setId (DELETE_TAG);
                    menu.addMenuItem (deleteItem, SwipeMenu.SwipeSide.RIGHT);
                }
            }
            );

            FileListView.setOnMenuItemClickListener ((position, menu, index) => {
                var fileIndex = FileListAdapter [position];
                switch (index) {
                case FORWARD_TAG:
                    AttachFile (position, fileIndex);
                    break;
                case DELETE_TAG:
                    DeleteFile (fileIndex);
                    RefreshVisibleItems ();
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });
                
        }

        public override void OnResume ()
        {
            base.OnResume ();
            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_FileSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                SetupFileListAdapter (View);
                break;
            }
        }

        void ClickContactSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByContact);
            FileListAdapter.SortByContat ();
        }

        void ClickDateSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByDate);
            FileListAdapter.SortByDate ();
        }

        void ClickNameSegment (object sender, EventArgs e)
        {
            SelectTab (SortSegmentByName);
            FileListAdapter.SortByName ();
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
            var fileIndex = FileListAdapter [e.Position];
            if (0 == fileIndex.FileType) {
                DownloadAndDo (e.Position, VIEW_TAG, fileIndex);
            } else if (1 == fileIndex.FileType) {
                var note = McNote.QueryById<McNote> (fileIndex.Id);
                var intent = NoteActivity.EditNoteIntent (Activity, note.DisplayName, null, note.noteContent, false);
                StartActivity (intent);
            } else {
                NcAssert.CaseError ();
            }
        }

        public void DeleteFile (NcFileIndex item)
        {
            switch (item.FileType) {
            case 0:
                McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);
                if (null != attachment) {
                    attachment.DeleteFile ();
                }
                break;
            case 1:
                McNote note = McNote.QueryById<McNote> (item.Id);
                if (null != note) {
                    note.Delete ();
                }
                break;
            case 2:
                McDocument document = McDocument.QueryById<McDocument> (item.Id);
                if (null != document) {
                    document.Delete ();
                }
                break;
            default:
                NcAssert.CaseError ("Deleting unknown file type");
                break;
            }
        }

        public void AttachFile (int position, NcFileIndex item)
        {
            switch (item.FileType) {
            case 0:
                DownloadAndDo (position, FORWARD_TAG, item);
                break;
            case 1:
                var note = McNote.QueryById<McNote> (item.Id);
                if (null != note) {
                    var attachment = EmailHelper.NoteToAttachment (note);
                    var intent = MessageComposeActivity.ForwardAttachmentIntent (Activity, attachment.AccountId, attachment.Id);
                    StartActivity (intent);
                }
                break;
            case 2:
                var document = McDocument.QueryById<McDocument> (item.Id);
                if (null != document) {
                    ;
                }
                break;
            default:
                NcAssert.CaseError ("Attaching unknown file type");
                break;
            }

        }

        void DownloadAndDo (int position, int action, NcFileIndex fileIndex)
        {
            NcAssert.True (0 == fileIndex.FileType);

            var attachment = McAttachment.QueryById<McAttachment> (fileIndex.Id);
            if (null == attachment) {
                return;
            }
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                AttachmentHelper.OpenAttachment (Activity, attachment);
                return;
            }
            var cell = GetViewByPosition (position);
            var downloader = new ActionAttachmentDownloader (action);
            downloader.Delegate = this;
            if (null != cell) {
                var downloadView = cell.FindViewById (Resource.Id.file_download);
                var downloadIndicator = cell.FindViewById (Resource.Id.file_download_indicator);
                downloadView.Visibility = ViewStates.Gone;
                downloadIndicator.Visibility = ViewStates.Visible;
            }
            downloader.Download (attachment);
        }

        public void AttachmentDownloadDidFinish (AttachmentDownloader downloader)
        {
            RefreshVisibleItems ();

            var attachment = downloader.Attachment;
            if (null == attachment) {
                return;
            }

            var actionAttachmentDownloader = (ActionAttachmentDownloader)downloader;
            switch (actionAttachmentDownloader.tag) {
            case VIEW_TAG:
                AttachmentHelper.OpenAttachment (Activity, attachment);
                break;
            case FORWARD_TAG:
                var intent = MessageComposeActivity.ForwardAttachmentIntent (Activity, attachment.AccountId, attachment.Id);
                StartActivity (intent);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
        }

        public void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result)
        {
            RefreshVisibleItems ();
            NcAlertView.ShowMessage (Activity, "Download Error", "Sorry, we couldn't download the file.  Please try again.");
        }

        void RefreshVisibleItems ()
        {
            for (var i = FileListView.FirstVisiblePosition; i <= FileListView.LastVisiblePosition; i++) {
                var cell = FileListView.GetChildAt (i - FileListView.FirstVisiblePosition);
                if (null != cell) {
                    FileListAdapter.GetView (i, cell, FileListView);
                }
            }
        }

        View GetViewByPosition (int position)
        {
            var firstListItemPosition = FileListView.FirstVisiblePosition;
            var lastListItemPosition = FileListView.LastVisiblePosition;
            if ((position < firstListItemPosition) || (position > lastListItemPosition)) {
                return null;
            } else {
                return FileListView.GetChildAt (position - firstListItemPosition);
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

    }
}

