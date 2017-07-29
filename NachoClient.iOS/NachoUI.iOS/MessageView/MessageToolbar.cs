//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using Foundation;
using UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class MessageToolbarEventArgs : EventArgs
    {
        public MessageToolbar.ActionType Action;

        public MessageToolbarEventArgs (MessageToolbar.ActionType action) : base ()
        {
            Action = action;
        }
    }

    public class MessageToolbar : UIToolbar
    {
        public enum ActionType
        {
            QUICK_REPLY,
            REPLY,
            REPLY_ALL,
            FORWARD,
            MOVE,
            ARCHIVE,
            DELETE
        };

        public EventHandler OnClick;

        protected UIBarButtonItem quickReplyButton;
        protected UIBarButtonItem replyButton;
        protected UIBarButtonItem replyAllButton;
        protected UIBarButtonItem forwardButton;
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem archiveButton;
        protected UIBarButtonItem deleteButton;
        protected UIBarButtonItem flexibleSpace;
        protected UIBarButtonItem fixedSpace;

        static UIColor BUTTON_COLOR = A.Color_NachoGreen;

        private void QuickReplyButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.QUICK_REPLY));
        }

        private void ReplyButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.REPLY));
        }

        private void ReplyAllButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.REPLY_ALL));
        }

        private void ForwardButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.FORWARD));
        }

        private void MoveButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.MOVE));
        }

        private void ArchiveButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.ARCHIVE));
        }

        private void DeleteButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.DELETE));
        }

        public MessageToolbar (CGRect frame) : base (frame)
        {
            Translucent = false;
            BarTintColor = UIColor.White;

            quickReplyButton = new NcUIBarButtonItem ();
            quickReplyButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (quickReplyButton, "toolbar-quick-reply");
            quickReplyButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Quick Reply", "");
            quickReplyButton.Clicked += QuickReplyButtonClicked;

            replyButton = new NcUIBarButtonItem ();
            replyButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (replyButton, "toolbar-icn-reply");
            replyButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Reply (verb)", "");
            replyButton.Clicked += ReplyButtonClicked;

            replyAllButton = new NcUIBarButtonItem ();
            replyAllButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (replyAllButton, "toolbar-icn-reply-all");
            replyAllButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Reply all", "");
            replyAllButton.Clicked += ReplyAllButtonClicked;

            forwardButton = new NcUIBarButtonItem ();
            forwardButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (forwardButton, "toolbar-icn-fwd");
            forwardButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Forward (verb)", "");
            forwardButton.Clicked += ForwardButtonClicked;

            moveButton = new NcUIBarButtonItem ();
            moveButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (moveButton, "email-move-swipe");
            moveButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Move", "");
            moveButton.Clicked += MoveButtonClicked;

            archiveButton = new NcUIBarButtonItem ();
            archiveButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (archiveButton, "email-archive-gray");
            archiveButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Archive (verb)", "");
            archiveButton.Clicked += ArchiveButtonClicked;

            flexibleSpace = new NcUIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);

            fixedSpace = new NcUIBarButtonItem (UIBarButtonSystemItem.FixedSpace);
            fixedSpace.Width = 10;

            deleteButton = new NcUIBarButtonItem ();
            deleteButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (deleteButton, "email-delete");
            deleteButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Delete", "");
            deleteButton.Clicked += DeleteButtonClicked;

            SetItems (new UIBarButtonItem [] {
                quickReplyButton,
                fixedSpace,
                replyButton,
                fixedSpace,
                replyAllButton,
                fixedSpace,
                forwardButton,
                flexibleSpace,
                moveButton,
                fixedSpace,
                archiveButton,
                fixedSpace,
                deleteButton
            }, false);
        }

        public void Cleanup ()
        {
            quickReplyButton.Clicked -= QuickReplyButtonClicked;
            replyButton.Clicked -= ReplyButtonClicked;
            replyAllButton.Clicked -= ReplyAllButtonClicked;
            forwardButton.Clicked -= ForwardButtonClicked;
            moveButton.Clicked -= MoveButtonClicked;
            archiveButton.Clicked -= ArchiveButtonClicked;
            deleteButton.Clicked -= DeleteButtonClicked;

            quickReplyButton = null;
            replyButton = null;
            replyAllButton = null;
            forwardButton = null;
            moveButton = null;
            archiveButton = null;
            deleteButton = null;
        }
    }
}

