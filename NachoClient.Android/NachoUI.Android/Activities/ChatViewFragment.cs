
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

//using Android.Util;
using Android.Views;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Graphics.Drawables;
using NachoCore.Brain;
using Android.Views.InputMethods;
using NachoPlatform;
using Android.Widget;
using System.IO;

namespace NachoClient.AndroidClient
{
    public interface IChatViewFragmentOwner
    {
        void DoneWithMessage ();

        McChat ChatToView { get; }
    }

    public class ChatViewFragment : Fragment, FilePickerFragmentDelegate
    {
        private const string SAVED_SEARCHING_KEY = "ChatViewFragment.searching";

        bool searching;
        Android.Widget.EditText searchEditText;
        SwipeMenuListView listView;
        ChatAdapter chatAdapter;
        EmailAddressField ToField;
        Android.Widget.TextView titleView;
        ListView chatAttachmentListView;
        ChatAttachmentAdapter chatAttachmentAdapter;

        McChat chat;

        ButtonBar buttonBar;

        SwipeRefreshLayout mSwipeRefreshLayout;

        public event EventHandler<McChat> onChatClick;

        private const string FILE_PICKER_TAG = "FilePickerFragment";
        private const string ACCOUNT_CHOOSER_TAG = "AccountChooser";
        private const string CAMERA_OUTPUT_URI_KEY = "cameraOutputUri";

        private const int PICK_REQUEST_CODE = 1;
        private const int TAKE_PHOTO_REQUEST_CODE = 2;

        Android.Net.Uri CameraOutputUri;

        McAccount account;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ChatViewFragment, container, false);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            buttonBar = new ButtonBar (view);
            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.chat_add_contact, AddButton_Click);

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            var sendButton = view.FindViewById<Button> (Resource.Id.chat_send);
            sendButton.Click += SendButton_Click;

            var attachButton = view.FindViewById<ImageButton> (Resource.Id.chat_attach);
            attachButton.Click += AttachButton_Click;

            ToField = view.FindViewById<EmailAddressField> (Resource.Id.compose_to);
            ToField.AllowDuplicates (false);
            ToField.Adapter = new ContactAddressAdapter (this.Activity);
            ToField.TokensChanged += ToField_TokensChanged;

            titleView = view.FindViewById<Android.Widget.TextView> (Resource.Id.chat_title);
            titleView.Click += TitleView_Click;

            account = NcApplication.Instance.Account;
            if (McAccount.GetUnifiedAccount ().Id == account.Id) {
                account = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
            }

            return view;
        }

        void ToField_TokensChanged (object sender, EventArgs e)
        {
            UpdateChatFromToField ();
        }

        void TitleView_Click (object sender, EventArgs e)
        {
            if (null != chat) {
                var participants = McChatParticipant.GetChatParticipants (chat.Id);
                var intent = ChatParticipantListActivity.ParticipantsIntent (this.Activity, typeof(ChatParticipantListActivity), Intent.ActionView, chat.AccountId, participants);
                StartActivity (intent);
            }
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            chat = ((IChatViewFragmentOwner)Activity).ChatToView;
            chatAdapter = new ChatAdapter (chat, this);

            ShowAddressEditor (null == chat);

            listView = View.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = chatAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
            }
            );

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                return false;
            });

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });

            chatAttachmentAdapter = new ChatAttachmentAdapter ();
            chatAttachmentAdapter.OnViewAttachment = ViewAttachment;
            chatAttachmentAdapter.OnDeleteAttachment = DeleteAttachment;

            chatAttachmentListView = View.FindViewById<ListView> (Resource.Id.attachment_listView);
            chatAttachmentListView.Adapter = chatAttachmentAdapter;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            RefreshVisibleChatCells ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
            chatAdapter.Cleanup ();
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutBoolean (SAVED_SEARCHING_KEY, searching);
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onChatClick) {
                InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
                imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
                var message = chatAdapter [e.Position];
                // TODO: Now what?
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            if (IsAddressEditorVisible ()) {
                chat = null;
                UpdateChatFromToField ();
            }
            ShowAddressEditor ((null == chat) || !IsAddressEditorVisible ());
        }

        void SendButton_Click (object sender, EventArgs e)
        {
            var editText = View.FindViewById<EditText> (Resource.Id.chat_message);
            var text = editText.Text;

            if (String.IsNullOrEmpty (text)) {
                return;
            }
                
            if (null == chat) {
                UpdateChatFromToField ();
            }
            ShowAddressEditor (null == chat);

            if (null != chat) {
                foreach (var attachment in chatAttachmentAdapter.attachments) {
                    attachment.Link (chat.Id, chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
                }
                    
                ChatMessageComposer.SendChatMessage (chat, text, chatAdapter.GetNewestChats (3), (McEmailMessage message, NcResult result) => {
                    chat.AddMessage (message);
                    editText.Text = "";
                    ClearAttachments ();
                });
            }
        }

        void AttachButton_Click (object sender, EventArgs e)
        {
            PickAttachment ();
        }

        void UpdateChatFromToField ()
        {
            var addresses = NcEmailAddress.ParseToAddressListString (ToField.AddressString);
            if ((null == addresses) || (0 == addresses.Count)) {
                chat = null;
            } else {
                chat = McChat.ChatForAddresses (account.Id, addresses);
            }

            listView = View.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            chatAdapter = new ChatAdapter (chat, this);
            listView.Adapter = chatAdapter;
        }

        bool IsAddressEditorVisible ()
        {
            var toView = View.FindViewById (Resource.Id.to_view);
            return ViewStates.Visible == toView.Visibility;
        }

        void ShowAddressEditor (bool visible)
        {
            var toView = View.FindViewById (Resource.Id.to_view);

            if (visible) {
                titleView.Visibility = ViewStates.Gone;
                toView.Visibility = ViewStates.Visible;
                ToField.AddressList = (null == chat) ? null : McChatParticipant.ConvertToAddressList (McChatParticipant.GetChatParticipants (chat.Id));
            } else {
                toView.Visibility = ViewStates.Gone;
                titleView.Visibility = ViewStates.Visible;
                titleView.Text = (null == chat) ? "" : chat.CachedParticipantsLabel;
            }
        }

        void SearchButton_Click (object sender, EventArgs e)
        {
            StartSearching ();
        }


        void CancelButton_Click (object sender, EventArgs e)
        {
            if (searching) {
                CancelSearch ();
            }
        }

        void StartSearching ()
        {

        }

        void CancelSearch ()
        {

        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {

        }

        public void OnBackPressed ()
        {
            if (searching) {
                CancelSearch ();
            }
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (mSwipeRefreshLayout.Refreshing) {
                    mSwipeRefreshLayout.Refreshing = false;
                }
            });
        }

        NcTimer refreshTimer;

        void rearmRefreshTimer (int seconds)
        {
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
            refreshTimer = new NcTimer ("ChatFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (mSwipeRefreshLayout.Refreshing) {
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ChatSetChanged:
                RefreshVisibleChatCells ();
                break;
            }
        }

        void RefreshVisibleChatCells ()
        {
            if (MaybeDisplayNoChatsView (View)) {
                return;
            }
            for (var i = listView.FirstVisiblePosition; i <= listView.LastVisiblePosition; i++) {
                var cell = listView.GetChildAt (i - listView.FirstVisiblePosition);
                if (null != cell) {
                    chatAdapter.GetView (i, cell, listView);
                }
            }
        }

        public bool MaybeDisplayNoChatsView (View view)
        {
            if (null != view) {
                if (null != chatAdapter) {
                    var showEmpty = !searching && (0 == chatAdapter.Count);
                    view.FindViewById<Android.Widget.TextView> (Resource.Id.no_chats).Visibility = (showEmpty ? ViewStates.Visible : ViewStates.Gone);
                    return true;
                }
            }
            return false;
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        void PickAttachment ()
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);

            Intent shareIntent = new Intent ();
            shareIntent.SetAction (Intent.ActionPick);
            shareIntent.SetType ("image/*");
            shareIntent.PutExtra (Intent.ExtraAllowMultiple, true);
            var resInfos = Activity.PackageManager.QueryIntentActivities (shareIntent, 0);
            var packages = new List<string> ();
            if (Util.CanTakePhoto (Activity)) {
                packages.Add (ChooserArrayAdapter.TAKE_PHOTO);
            }
            packages.Add (ChooserArrayAdapter.ADD_FILE);
            foreach (var resInfo in resInfos) {
                packages.Add (resInfo.ActivityInfo.PackageName);
            }
            if (packages.Count > 1) {
                ArrayAdapter<String> adapter = new ChooserArrayAdapter (Activity, Android.Resource.Layout.SelectDialogItem, Android.Resource.Id.Text1, packages);
                var builder = new Android.App.AlertDialog.Builder (Activity);
                builder.SetTitle ("Get File");
                builder.SetAdapter (adapter, (s, ev) => {
                    InvokeApplication (packages [ev.Which]);
                });
                builder.Show ();
            } else if (1 == packages.Count) {
                InvokeApplication (packages [0]);
            }

        }

        void InvokeApplication (string packageName)
        {
            if (ChooserArrayAdapter.TAKE_PHOTO == packageName) {
                CameraOutputUri = Util.TakePhoto (this, TAKE_PHOTO_REQUEST_CODE);
                return;
            }
            if (ChooserArrayAdapter.ADD_FILE == packageName) {
                var filePicker = FilePickerFragment.newInstance (account.Id);
                filePicker.Delegate = this;
                filePicker.Show (FragmentManager, FILE_PICKER_TAG); 
                return;
            }
            var intent = new Intent ();
            intent.SetAction (Intent.ActionGetContent);
            intent.AddCategory (Intent.CategoryOpenable);
            intent.SetType ("*/*");
            intent.SetFlags (ActivityFlags.SingleTop);
            intent.PutExtra (Intent.ExtraAllowMultiple, true);
            intent.SetPackage (packageName);

            StartActivityForResult (intent, PICK_REQUEST_CODE);
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            if (Result.Ok != resultCode) {
                return;
            }
            if (TAKE_PHOTO_REQUEST_CODE == requestCode) {
                var mediaScanIntent = new Intent (Intent.ActionMediaScannerScanFile);
                mediaScanIntent.SetData (CameraOutputUri);
                Activity.SendBroadcast (mediaScanIntent);
                var attachment = McAttachment.InsertSaveStart (account.Id);
                var filename = Path.GetFileName (CameraOutputUri.Path);
                attachment.SetDisplayName (filename);
                attachment.ContentType = MimeKit.MimeTypes.GetMimeType (filename);
                attachment.UpdateFileCopy (CameraOutputUri.Path);
                attachment.UpdateSaveFinish ();
                File.Delete (CameraOutputUri.Path);
                AddAttachment (attachment);
            } else if (PICK_REQUEST_CODE == requestCode) {
                try {
                    var clipData = data.ClipData;
                    if (null == clipData) {
                        var attachment = AttachmentHelper.UriToAttachment (account.Id, Activity, data.Data, data.Type);
                        if (null != attachment) {
                            AddAttachment (attachment);
                        }
                    } else {
                        for (int i = 0; i < clipData.ItemCount; i++) {
                            var uri = clipData.GetItemAt (i).Uri;
                            var attachment = AttachmentHelper.UriToAttachment (account.Id, Activity, uri, data.Type);
                            if (null != attachment) {
                                AddAttachment (attachment);
                            }
                        }
                    }
                } catch (Exception e) {
                    NachoCore.Utils.Log.Error (NachoCore.Utils.Log.LOG_LIFECYCLE, "Exception while processing the STREAM extra of a Send intent: {0}", e.ToString ());
                }
            }
        }

        public void FilePickerDidPickFile (FilePickerFragment picker, McAbstrFileDesc file)
        {
            picker.Dismiss ();
            var attachment = file as McAttachment;
            if (attachment != null) {
                AddAttachment (attachment);
            }
        }

        void AddAttachment (McAttachment attachment)
        {
            chatAttachmentAdapter.attachments.Add (attachment);

            ResizeChatAttachmentListView ();

            chatAttachmentAdapter.NotifyDataSetChanged ();
        }

        void ViewAttachment (int position)
        {
            var attachment = chatAttachmentAdapter.attachments [position];
            AttachmentHelper.OpenAttachment (this.Activity, attachment);
        }

        // Callback
        void DeleteAttachment (int position)
        {
            chatAttachmentAdapter.attachments.RemoveAt (position);
            ResizeChatAttachmentListView ();
            chatAttachmentAdapter.NotifyDataSetChanged ();
        }

        void ClearAttachments ()
        {
            chatAttachmentAdapter.attachments.Clear ();
            ResizeChatAttachmentListView ();
            chatAttachmentAdapter.NotifyDataSetChanged ();
        }

        void ResizeChatAttachmentListView ()
        {
            // Resize the listview, up to three visible cells
            var count = Math.Min (chatAttachmentAdapter.attachments.Count, 3);
            var lp = chatAttachmentListView.LayoutParameters;
            lp.Height = dp2px (50) * count;
            chatAttachmentListView.LayoutParameters = lp;
        }
    }

    public class ChatAdapter : Android.Widget.BaseAdapter<McEmailMessage>
    {
        McChat chat;
        List<McEmailMessage> messages;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;
        Fragment parent;

        public ChatAdapter (McChat chat, Fragment parent)
        {
            this.chat = chat;
            this.parent = parent;

            if (null != chat) {
                ParticipantsByEmailId = McChatParticipant.GetChatParticipantsByEmailId (chat.Id);
            }

            RefreshChatIfVisible ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void Cleanup ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public List<McEmailMessage> GetNewestChats (int count)
        {
            var previousMessages = new List<McEmailMessage> ();
            for (int i = messages.Count - 1; i >= messages.Count - count && i >= 0; --i) {
                previousMessages.Add (messages [i]);
            }
            return previousMessages;
        }

        protected void RefreshChatIfVisible ()
        {
            if (null == chat) {
                messages = new List<McEmailMessage> ();
            } else {
                messages = chat.GetAllMessages ();
            }
            NotifyDataSetChanged ();
        }

        public override long GetItemId (int position)
        {
            return messages [position].Id;
        }

        public override int Count {
            get {
                return messages.Count;
            }
        }

        public override McEmailMessage this [int position] {  
            get {
                return messages [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ChatViewCell, parent, false);

            }
            var message = messages [position];
            var previousMessage = (0 == position) ? null : messages [position - 1];
            var nextMessage = ((messages.Count - 1) <= position) ? null : messages [position + 1];

            if (!message.IsRead) {
                EmailHelper.MarkAsRead (message, true);
            }

            McChatParticipant particpant = null;
            ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant);

            BindChatViewCell (message, previousMessage, nextMessage, particpant, view);
            BindChatAttachments (message, view, LayoutInflater.From (parent.Context), AttachmentSelectedCallback, AttachmentErrorCallback);
            BindChatAttachmentColors (view, null == particpant);
            return view;
        }

        void BindChatViewCell (McEmailMessage message, McEmailMessage previous, McEmailMessage next, McChatParticipant participant, View view)
        {
            var oneHour = TimeSpan.FromHours (1);
            var atTimeBlockStart = previous == null || (message.DateReceived - previous.DateReceived > oneHour);
            var atTimeBlockEnd = next == null || (next.DateReceived - message.DateReceived > oneHour);
            var atParticipantBlockStart = previous == null || previous.FromEmailAddressId != message.FromEmailAddressId;
            var atParticipantBlockEnd = next == null || next.FromEmailAddressId != message.FromEmailAddressId;
            var showName = atTimeBlockStart || atParticipantBlockStart;
            var showPortrait = atTimeBlockEnd || atParticipantBlockEnd;
            var showTimestamp = atTimeBlockStart;

            var dateView = view.FindViewById<TextView> (Resource.Id.date);
            if (showTimestamp) {
                dateView.Text = Pretty.VariableDayTime (message.DateReceived);
                dateView.Visibility = ViewStates.Visible;
            } else {
                dateView.Visibility = ViewStates.Gone;
            }

            showName = showName && (participant != null);

            var titleView = view.FindViewById<TextView> (Resource.Id.title);
            if (showName) {
                titleView.Text = Pretty.SenderString (message.From);
                titleView.Visibility = ViewStates.Visible;
            } else {
                titleView.Visibility = ViewStates.Gone;
            }

            var initials = view.FindViewById<ContactPhotoView> (Resource.Id.user_initials);
            if (showPortrait && (null != participant)) {
                initials.Visibility = ViewStates.Visible;
                initials.SetPortraitId (participant.CachedPortraitId, participant.CachedInitials, ColorForUser (participant.CachedColor));
            } else {
                initials.Visibility = ViewStates.Invisible;
            }
                
            var previewView = view.FindViewById<TextView> (Resource.Id.preview);
            var bundle = new NcEmailMessageBundle (message);
            if (bundle.NeedsUpdate) {
                previewView.Text = message.BodyPreview;  
            } else {
                previewView.Text = bundle.TopText;
            }

            var previewCardView = view.FindViewById<CardView> (Resource.Id.preview_card);
            var cardLayout = previewCardView.LayoutParameters as LinearLayout.LayoutParams;

            int textColorId;
            int backgroundColorId;

            if (null == participant) {
                textColorId = Android.Resource.Color.White;
                backgroundColorId = Resource.Color.NachoGreen;
                cardLayout.Gravity = GravityFlags.Right;
                cardLayout.LeftMargin = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 50.0f, view.Context.Resources.DisplayMetrics);
                cardLayout.RightMargin = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 0.0f, view.Context.Resources.DisplayMetrics);
            } else {
                backgroundColorId = Android.Resource.Color.White;
                textColorId = Resource.Color.NachoGreen;
                cardLayout.Gravity = GravityFlags.Left;
                cardLayout.LeftMargin = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 0.0f, view.Context.Resources.DisplayMetrics);
                cardLayout.RightMargin = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 50.0f, view.Context.Resources.DisplayMetrics);
            }
            previewView.SetTextColor (view.Resources.GetColor (textColorId));
            previewView.SetBackgroundResource (backgroundColorId);
            previewCardView.SetCardBackgroundColor (view.Resources.GetColor (backgroundColorId));
            previewView.RequestLayout ();

        }

        void BindChatAttachments (McEmailMessage message, View view, LayoutInflater inflater, NcAttachmentView.AttachmentSelectedCallback onAttachmentSelected, NcAttachmentView.AttachmentErrorCallback onAttachmentError)
        {
            var attachmentListView = view.FindViewById<LinearLayout> (Resource.Id.attachment_list_views);
            attachmentListView.RemoveAllViews ();

            var attachments = McAttachment.QueryByItemId (message.AccountId, message.Id, McAbstrFolderEntry.ClassCodeEnum.Email);
            if (null != attachments) {
                foreach (var attachment in attachments) {
                    string contentType = attachment.ContentType == null ? "" : attachment.ContentType.ToLower ();
                    if (contentType.StartsWith ("image/") && attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        var imageView = new ImageView (view.Context);
                        BindChatImageAttachment (attachment, imageView, onAttachmentSelected);
                        attachmentListView.AddView (imageView);
                    } else {
                        var cell = inflater.Inflate (Resource.Layout.AttachmentListViewCell, null);
                        new NcAttachmentView (attachment, cell, onAttachmentSelected, onAttachmentError);
                        attachmentListView.AddView (cell);
                    }
                }
            }
        }

        void BindChatImageAttachment(McAttachment attachment, ImageView imageView, NcAttachmentView.AttachmentSelectedCallback onAttachmentSelected){
            using (var image = BitmapFromPathConstrainedToSize(attachment.GetFilePath (), 1024, 1024)) {
                imageView.SetAdjustViewBounds (true);
                imageView.SetImageBitmap (image);
            }
            imageView.Click += (object sender, EventArgs e) => {
                onAttachmentSelected (attachment);
            };
        }

        private Bitmap BitmapFromPathConstrainedToSize (string path, int maxWidth, int maxHeight)
        {
            var options = new BitmapFactory.Options ();
            options.InJustDecodeBounds = true;
            BitmapFactory.DecodeFile (path, options);
            int width = options.OutWidth;
            int height = options.OutHeight;
            int sampleSize = 1;
            while (width > maxWidth || height > maxHeight) {
                sampleSize += 1;
                width = width / 2;
                height = height / 2;
            }
            if (sampleSize > 1) {
                sampleSize -= 1;
            }
            options.InJustDecodeBounds = false;
            options.InSampleSize = sampleSize;
            return BitmapFactory.DecodeFile (path, options);
        }

        void BindChatAttachmentColors (View view, bool useDarkBackground)
        {
            int textColorId;
            int backgroundColorId;

            if (useDarkBackground) {
                textColorId = Android.Resource.Color.White;
                backgroundColorId = Resource.Color.NachoGreen;
            } else {
                backgroundColorId = Android.Resource.Color.White;
                textColorId = Resource.Color.NachoGreen;
            }

            var attachmentListView = view.FindViewById<LinearLayout> (Resource.Id.attachment_list_views);
            for (int i = 0; i < attachmentListView.ChildCount; i++) {
                var attachmentView = attachmentListView.GetChildAt (i);
                attachmentView.SetBackgroundResource (backgroundColorId);
                var separator = attachmentView.FindViewById (Resource.Id.separator);
                if (separator != null) {
                    separator.Visibility = ViewStates.Gone;
                }
                var nameView = attachmentView.FindViewById<TextView> (Resource.Id.attachment_name);
                if (nameView != null) {
                    nameView.SetTextColor (view.Resources.GetColor (textColorId));
                }
                var descriptionView = attachmentView.FindViewById<TextView> (Resource.Id.attachment_description);
                if (descriptionView != null) {
                    descriptionView.SetTextColor (view.Resources.GetColor (textColorId));
                }
            }
        }


        public void AttachmentSelectedCallback (McAttachment attachment)
        {
            AttachmentHelper.OpenAttachment (parent.Activity, attachment);
        }

        public  void AttachmentErrorCallback (McAttachment attachment, NcResult nr)
        {
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ChatMessageAdded:
                RefreshChatIfVisible ();
                break;
            }
        }

    }

    // Used when composing a new chat message
    public class ChatAttachmentAdapter : Android.Widget.BaseAdapter<McAttachment>
    {
        public List<McAttachment> attachments;

        public delegate void ViewAttachment (int Position);

        public delegate void DeleteAttachment (int Position);

        public ViewAttachment OnViewAttachment;
        public DeleteAttachment OnDeleteAttachment;


        public ChatAttachmentAdapter ()
        {
            attachments = new List<McAttachment> ();
            NotifyDataSetChanged ();
        }

        public override long GetItemId (int position)
        {
            return attachments [position].Id;
        }

        public override int Count {
            get {
                return attachments.Count;
            }
        }

        public override McAttachment this [int position] {  
            get {
                return attachments [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available

            ImageButton deleteButton;

            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttachmentListViewCell, parent, false);
                view.Click += View_Click;
                deleteButton = view.FindViewById<ImageButton> (Resource.Id.attachment_remove);
                deleteButton.Visibility = ViewStates.Visible;
                deleteButton.Click += DeleteButton_Click;
            } else {
                deleteButton = view.FindViewById<ImageButton> (Resource.Id.attachment_remove);
            }
            deleteButton.Tag = position;
            view.Tag = position;

            var attachment = attachments [position];
            Bind.BindAttachmentView (attachment, view);

            return view;
        }

        void View_Click (object sender, EventArgs e)
        {
            var deleteButton = (View)sender;
            var position = (int)deleteButton.Tag;
            if (null != OnViewAttachment) {
                OnViewAttachment (position);
            }
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            var deleteButton = (View)sender;
            var position = (int)deleteButton.Tag;
            if (null != OnDeleteAttachment) {
                OnDeleteAttachment (position);
            }
        }
            
    }
}

