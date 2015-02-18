//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

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
        public enum ActionType {
            REPLY,
            REPLY_ALL,
            FORWARD,
            ARCHIVE,
            DELETE
        };

        public EventHandler OnClick;

        protected UIBarButtonItem replyButton;
        protected UIBarButtonItem replyAllButton;
        protected UIBarButtonItem forwardButton;
        protected UIBarButtonItem archiveButton;
        protected UIBarButtonItem deleteButton;
        protected UIBarButtonItem flexibleSpace;
        protected UIBarButtonItem fixedSpace;

        static UIColor BUTTON_COLOR = A.Color_NachoGreen;

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

        private void ArchiveButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs (ActionType.ARCHIVE));
        }

        private void DeleteButtonClicked (object sender, EventArgs e)
        {
            OnClick (sender, new MessageToolbarEventArgs(ActionType.DELETE));
        }

        public MessageToolbar (CGRect frame) : base (frame)
        {
            Translucent = false;
            BarTintColor = UIColor.White;

            replyButton = new UIBarButtonItem ();
            replyButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (replyButton, "toolbar-icn-reply");
            replyButton.Clicked += ReplyButtonClicked;

            replyAllButton = new UIBarButtonItem ();
            replyAllButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (replyAllButton, "toolbar-icn-reply-all");
            replyAllButton.Clicked += ReplyAllButtonClicked;

            forwardButton = new UIBarButtonItem ();
            forwardButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (forwardButton, "toolbar-icn-fwd");
            forwardButton.Clicked += ForwardButtonClicked;

            archiveButton = new UIBarButtonItem ();
            archiveButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (archiveButton, "email-archive-gray");
            archiveButton.Clicked += ArchiveButtonClicked;

            flexibleSpace = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);

            fixedSpace = new UIBarButtonItem (UIBarButtonSystemItem.FixedSpace);
            fixedSpace.Width = 10;

            deleteButton = new UIBarButtonItem ();
            deleteButton.TintColor = BUTTON_COLOR;
            Util.SetAutomaticImageForButton (deleteButton, "email-delete");
            deleteButton.Clicked += DeleteButtonClicked;

            SetItems (new UIBarButtonItem[] {
                replyButton,
                fixedSpace,
                replyAllButton,
                fixedSpace,
                forwardButton,
                flexibleSpace,
                archiveButton,
                fixedSpace,
                deleteButton
            }, false);
        }

        public void Cleanup()
        {
            replyButton.Clicked -= ReplyButtonClicked;
            replyAllButton.Clicked -= ReplyAllButtonClicked;
            forwardButton.Clicked -= ForwardButtonClicked;
            archiveButton.Clicked -= ArchiveButtonClicked;
            deleteButton.Clicked -= DeleteButtonClicked;

            replyButton = null;
            replyAllButton = null;
            forwardButton = null;
            archiveButton = null;
            deleteButton = null;
        }
    }
}

