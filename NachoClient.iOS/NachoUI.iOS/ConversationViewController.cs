//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using System.Linq;

namespace NachoClient.iOS
{
    public class ConversationViewController : NcUIViewControllerNoLeaks, ChatMessageComposeDelegate, ChatViewDelegate, ChatViewDataSource, MessageDownloadDelegate, ThemeAdopter, INachoFileChooserParent, MessageComposerDelegate
    {
        
        public NachoEmailMessages Messages;
        McAccount Account;
        ChatView ChatView;
        ChatMessageComposeView ComposeView;
        Dictionary<int, List<McAttachment>> AttachmentsByMessageId;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;
        Dictionary<int, MessageDownloader> Downloaders;
        List<McAttachment> Attachments;
        List<string> SendTokens;

        public ConversationViewController ()
        {
            HidesBottomBarWhenPushed = true;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            AttachmentsByMessageId = new Dictionary<int, List<McAttachment>> ();
            ParticipantsByEmailId = new Dictionary<int, McChatParticipant> ();
            Downloaders = new Dictionary<int, MessageDownloader> ();
            Attachments = new List<McAttachment>();
            SendTokens = new List<string> ();
        }

        #region Theme

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (adoptedTheme != theme) {
                adoptedTheme = theme;
                ChatView.AdoptTheme (theme);
            }
        }

        #endregion

        #region View Lifecycle

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
            SubviewChangedHeight ();
            startListeningForNotifications ();
            if (IsMovingToParentViewController) {
                ChatView.ReloadData ();
                ChatView.ScrollToBottom ();
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            stopListeningForNotifications ();
        }

        protected override void CreateViewHierarchy ()
        {
            CGRect headerFrame = new CGRect (0.0f, 0.0f, View.Bounds.Width, 0.0f);
            CGRect composeFrame = new CGRect (0.0f, View.Bounds.Height - ChatMessageComposeView.STANDARD_HEIGHT, View.Bounds.Width, ChatMessageComposeView.STANDARD_HEIGHT);
            CGRect tableFrame = new CGRect (0.0f, headerFrame.Height, View.Bounds.Width, View.Bounds.Height - headerFrame.Height - composeFrame.Height);
            //HeaderView = new ChatMessagesHeaderView (headerFrame);
            //HeaderView.ChatViewController = this;
            //HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            ChatView = new ChatView (tableFrame);
            ChatView.DataSource = this;
            ChatView.Delegate = this;
            ChatView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            ChatView.ShowPortraits = true;
            ChatView.ShowNameLabels = true;
            ComposeView = new ChatMessageComposeView (composeFrame);
            ComposeView.ComposeDelegate = this;
            ComposeView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            //View.AddSubview (HeaderView);
            View.AddSubview (ChatView);
            View.AddSubview (ComposeView);
            //if (Chat == null) {
            //    HeaderView.EditingEnabled = true;
            //} else {
            //    UpdateForChat ();
            //    ComposeView.SetMessage (Chat.DraftMessage);
            //    var attachments = McAttachment.QueryByItemId (Chat.AccountId, Chat.Id, McAbstrFolderEntry.ClassCodeEnum.Chat);
            //    foreach (var attachment in attachments) {
            //        ComposeView.AddAttachment (attachment);
            //    }
            //}
            //UpdateFromField ();
        }

        #endregion

        #region Layout

        protected override void OnKeyboardChanged ()
        {
            base.OnKeyboardChanged ();
            SubviewChangedHeight ();
            ChatView.LayoutIfNeeded ();
        }

        public void SubviewChangedHeight ()
        {
            var y = 0;
            ComposeView.Frame = new CGRect (
                ComposeView.Frame.X,
                View.Bounds.Height - keyboardHeight - ComposeView.Frame.Height,
                ComposeView.Frame.Width,
                ComposeView.Frame.Height
            );
            ChatView.Frame = new CGRect (
                ChatView.Frame.X,
                y,
                ChatView.Frame.Width,
                ComposeView.Frame.Top - y
            );
        }

        #endregion

        public void ShowAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            }
        }

        #region User Actions

        MessageComposer Composer;

        public void Send ()
        {
            ComposeView.SetEnabled (false);
            var text = ComposeView.GetMessage ();
            //var previousMessages = new List<McEmailMessage> ();
            var mostRecentMessage = Messages.GetCachedMessage (0);

            Composer = new MessageComposer (Account);
            Composer.Delegate = this;
            if (mostRecentMessage != null) {
                Composer.RelatedThread = new McEmailMessageThread ();
                Composer.RelatedThread.FirstMessageId = mostRecentMessage.Id;
                Composer.RelatedThread.MessageCount = 1;
                Composer.Kind = EmailHelper.Action.ReplyAll;
            } else {
                Composer.Kind = EmailHelper.Action.Send;
            }
            Composer.InitialAttachments = Attachments;
            Composer.InitialText = text;

            Composer.StartPreparingMessage ();

            //ChatMessageComposer.SendChatMessage (Chat, text, previousMessages, (McEmailMessage message, NcResult result) => {
            //    if (IsViewLoaded) {
            //        ComposeView.SetEnabled (true);
            //        if (result.isOK ()) {
            //            Chat.AddMessage (message);
            //            ComposeView.Clear ();
            //            ChatView.ScrollToBottom ();
            //            PendingSendMap.Add (result.Value as string, message.Id);
            //        } else {
            //            NcAlertView.ShowMessage (this, "Could not send messasge", "Sorry, there was a problem sending the message.  Please try again.");
            //        }
            //    }
            //});
        }

        void ReportSendError ()
        {
            NcAlertView.ShowMessage (this, "Could not send messasge", "Sorry, there was a problem sending the message.  Please try again.");
            CleanupComposer ();
            ComposeView.SetEnabled (true);
        }

        public void MessageComposerDidCompletePreparation (MessageComposer composer)
        {
            composer.Save (composer.Bundle.FullHtml, invalidateBundle: false);
            var result = composer.Send ();
            if (result.isOK ()) {
                SendTokens.Add (result.Value as string);
            } else {
                ReportSendError ();
            }
        }

        public void MessageComposerDidFailToLoadMessage (MessageComposer composer)
        {
            ReportSendError ();
        }

        void CleanupComposer ()
        {
            Composer.Delegate = null;
            Composer = null;
        }

        public void Attach ()
        {
            var helper = new AddAttachmentViewController.MenuHelper (this, Account, View);
            PresentViewController (helper.MenuViewController, true, null);
        }

        // User picking a file as an attachment
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            var attachment = obj as McAttachment;
            if (attachment == null) {
                var file = obj as McDocument;
                if (file != null) {
                    attachment = McAttachment.InsertSaveStart (Account.Id);
                    attachment.SetDisplayName (file.DisplayName);
                    attachment.UpdateFileCopy (file.GetFilePath ());
                } else {
                    var note = obj as McNote;
                    if (note != null) {
                        attachment = McAttachment.InsertSaveStart (Account.Id);
                        attachment.SetDisplayName (note.DisplayName + ".txt");
                        attachment.UpdateData (note.noteContent);
                    }
                }
            }

            if (attachment != null) {
                Attachments.Add (attachment);
                ComposeView.AddAttachment (attachment);
                this.DismissViewController (true, null);
            } else {
                NcAssert.CaseError ();
            }

        }

        // User adding an attachment from media browser
        public void Append (McAttachment attachment)
        {
            attachment.Update ();
            Attachments.Add (attachment);
            ComposeView.AddAttachment (attachment);
        }

        public void AttachmentUpdated (McAttachment attachment)
        {
            ComposeView.UpdateAttachment (attachment);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissPhotoPicker ()
        {
            DismissViewController (true, null);
        }

        public void PresentFileChooserViewController (UIViewController vc)
        {
            PresentViewController (vc, true, null);
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            Attachments.Remove (attachment);
        }

        #endregion

        #region Compose Delegate

        public void ChatComposeDidSend (ChatMessageComposeView composeView)
        {
            Send ();
        }

        public void ChatComposeWantsAttachment (ChatMessageComposeView composeView)
        {
            //Attach ();
        }

        public void ChatComposeShowAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
            ShowAttachment (attachment);
        }

        public void ChatComposeDidRemoveAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
        }

        public bool ChatComposeCanSend (ChatMessageComposeView composeView)
        {
            return true;
        }

        public void ChatComposeChangedHeight (ChatMessageComposeView composeView)
        {
            SubviewChangedHeight ();
        }

        #endregion

        #region Chat View Delegate & Data Source

        public int NumberOfMessagesInChatView (ChatView chatView)
        {
            return Messages.Count ();
        }

        public ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index)
        {
            //var indexInArray = index - MessageCount + Messages.Count;
            //if (indexInArray < 0) {
            //    var messages = Chat.GetMessages (Messages.Count, Math.Max (MessagesPerQuery, -indexInArray));
            //    messages.Reverse ();
            //    Messages.InsertRange (0, messages);
            //}
            //indexInArray = index - MessageCount + Messages.Count;
            //if (indexInArray < 0) {
            //    // Hmmm...a message must have been deleted and messed up our count
            //    // Should rarely happen, and only if you scroll all the way back to the top
            //    // while a message gets deleted.
            //} else {
            //    message = Messages [indexInArray];
            //    ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant);
            //}
            McEmailMessage message = Messages.GetCachedMessage (Messages.Count () - 1 - index);
            McChatParticipant particpant = GetParticipant (message);
            List<McAttachment> attachments = GetAttachments(message);
            var messageView = chatView.DequeueReusableChatMessageView ();
            if (!message.IsRead) {
                EmailHelper.MarkAsRead (message, true);
            }
            messageView.OnAttachmentSelected = ShowAttachment;
            messageView.SetMessage (message, particpant, attachments, forceIsLoading: Downloaders.ContainsKey (message.Id));
            UpdateMessageViewBlockProperties (chatView, index, messageView);
            return messageView;
        }

        public void UpdateMessageViewBlockProperties (ChatView chatView, int index, ChatMessageView messageView)
        {
            var messageIndex = Messages.Count () - 1 - index;
            var message = Messages.GetCachedMessage(messageIndex);
            var previous = index > 0 ? Messages.GetCachedMessage(messageIndex + 1) : null;
            var next = messageIndex > 0 ? Messages.GetCachedMessage(messageIndex - 1) : null;
            var oneHour = TimeSpan.FromHours (1);
            var atTimeBlockStart = previous == null || (message.DateReceived - previous.DateReceived > oneHour);
            var atTimeBlockEnd = next == null || (next.DateReceived - message.DateReceived > oneHour);
            var atParticipantBlockStart = previous == null || previous.FromEmailAddressId != message.FromEmailAddressId;
            var atParticipantBlockEnd = next == null || next.FromEmailAddressId != message.FromEmailAddressId;
            var showName = atTimeBlockStart || atParticipantBlockStart;
            var showPortrait = atTimeBlockEnd || atParticipantBlockEnd;
            var showTimestamp = atTimeBlockStart;
            messageView.SetShowsName (showName);
            messageView.SetShowsPortrait (showPortrait);
            messageView.SetShowsTimestamp (showTimestamp);
        }

        public void ChatMessageViewDidSelectError (ChatView chatView, int index)
        { 
        }

        public void ChatMessageViewNeedsLoad (ChatView chatView, McEmailMessage message)
        {
            if (!Downloaders.ContainsKey (message.Id)) {
                var downloader = new MessageDownloader ();
                downloader.Delegate = this;
                Downloaders.Add (message.Id, downloader);
                downloader.Download (message);
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            Downloaders.Remove (downloader.Message.Id);
            ChatView.ReloadData ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            Downloaders.Remove (downloader.Message.Id);
        }

        public void ChatViewDidSelectMessage (ChatView chatView, int index)
        {
            var message = Messages.GetCachedMessage (Messages.Count () - 1 - index);
            var vc = new MessageViewController ();
            vc.Message = message;
            NavigationController.PushViewController (vc, true);
        }

        #endregion

        McChatParticipant GetParticipant (McEmailMessage message)
        {
            if (Account == null) {
                Account = McAccount.QueryById<McAccount> (message.AccountId);
            }
            McChatParticipant particpant = null;
            if (!ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant)) {
                var address = McEmailAddress.QueryById<McEmailAddress> (message.FromEmailAddressId);
                if (String.Equals (address.CanonicalEmailAddress, Account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                    particpant = null;
                } else {
                    particpant = new McChatParticipant ();
                    particpant.AccountId = Account.Id;
                    particpant.EmailAddrId = address.Id;
                    particpant.EmailAddress = address.CanonicalEmailAddress;
                    particpant.UpdateCachedProperties ();
                }
                ParticipantsByEmailId [message.FromEmailAddressId] = particpant;
            }
            return particpant;
        }

        List<McAttachment> GetAttachments (McEmailMessage message)
        {
            List<McAttachment> attachments = null;
            if (message.cachedHasAttachments) {
                if (!AttachmentsByMessageId.ContainsKey (message.Id)) {
                    AttachmentsByMessageId.Add (message.Id, McAttachment.QueryByItemId (message.AccountId, message.Id, McAbstrFolderEntry.ClassCodeEnum.Email));
                }
                AttachmentsByMessageId.TryGetValue (message.Id, out attachments);
            }
            return attachments;
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
            foreach (var pair in Downloaders) {
                pair.Value.Delegate = null;
            }
            Downloaders.Clear ();
        }

        #region Notifications

        bool IsListeningForNotifications = false;

        void startListeningForNotifications ()
        {
            if (!IsListeningForNotifications) {
                IsListeningForNotifications = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void stopListeningForNotifications ()
        {
            if (IsListeningForNotifications) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForNotifications = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (s.Account == null || (Messages != null && Messages.IsCompatibleWithAccount (s.Account))) {
                Log.Info (Log.LOG_UI, "ConversationViewController status indicator callback: {0}", s.Status.SubKind.ToString ());
                switch (s.Status.SubKind) {
                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
                    SetNeedsReload ();
                    break;
                case NcResult.SubKindEnum.Error_EmailMessageSendFailed:
                    if (SendTokens.Contains (s.Tokens [0])) {
                        ReportSendError ();
                        SendTokens.Remove (s.Tokens [0]);
                    }
                    break;
                case NcResult.SubKindEnum.Error_EmailMessageReplyFailed:
                    if (SendTokens.Contains(s.Tokens[0])){
                        ReportSendError ();
                        SendTokens.Remove (s.Tokens [0]);
                    }
                    break;
                case NcResult.SubKindEnum.Info_EmailMessageSendSucceeded:
                    if (SendTokens.Contains (s.Tokens [0])) {
                        SendTokens.Remove (s.Tokens [0]);
                        ComposeView.Clear ();
                        ComposeView.SetEnabled (true);
                    }
                    break;
                }
            }
        }

        #endregion
        #region Reloading Messages

        bool NeedsReload;
        bool IsReloading;

        object MessagesLock = new object ();

        protected void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        protected void Reload ()
        {
            if (!IsReloading) {
                IsReloading = true;
                NeedsReload = false;
                if (Messages.HasBackgroundRefresh ()) {
                    Log.Info (Log.LOG_UI, "ConversationViewController.Reload: using NachoEmailMessages background refresh");
                    Messages.BackgroundRefresh (HandleReloadResults);
                } else {
                    Log.Info (Log.LOG_UI, "ConversationViewController.Reload: simulating a background refresh because this NachoEmailMessages doesn't have one");
                    NcTask.Run (() => {
                        List<int> adds;
                        List<int> deletes;
                        NachoEmailMessages messages;
                        lock (MessagesLock) {
                            messages = Messages;
                        }
                        bool changed = messages.BeginRefresh (out adds, out deletes);
                        BeginInvokeOnMainThread (() => {
                            bool handledResults = false;
                            lock (MessagesLock) {
                                if (messages == Messages) {
                                    Messages.CommitRefresh ();
                                    HandleReloadResults (changed, adds, deletes);
                                    handledResults = true;
                                }
                            }
                            if (!handledResults) {
                                IsReloading = false;
                                if (NeedsReload) {
                                    Reload ();
                                }
                            }
                        });
                    }, "ConversationViewReloadTask");
                }
            }
        }

        void HandleReloadResults (bool changed, List<int> adds, List<int> deletes)
        {
            Log.Info (Log.LOG_UI, "ConversationViewController.HandleReloadResults: changed = {0}, {1} adds, {2} deletes",
                changed, adds == null ? 0 : adds.Count, deletes == null ? 0 : deletes.Count);
            if (changed) {
                Messages.ClearCache ();
                if (deletes.Count == 0 && adds.Count == 1 && adds [0] == 0) {
                    Log.Info (Log.LOG_UI, "ConversationViewController adding message to end");
                    ChatView.InsertMessageViewAtEnd ();
                } else {
                    Log.Info (Log.LOG_UI, "ConversationViewController reloading (complex changes)");
                    ChatView.ReloadData ();
                }
            }
            IsReloading = false;
            if (NeedsReload) {
                Reload ();
            }
        }

        #endregion

    }
}
