//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;

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

        public MessageToolbar (RectangleF frame) : base (frame)
        {
            Translucent = false;
            BarTintColor = UIColor.White;

            replyButton = new UIBarButtonItem ();
            replyButton.TintColor = BUTTON_COLOR;
            Util.SetOriginalImageForButton (replyButton, "toolbar-icn-reply");
            replyButton.Clicked += (object sender, EventArgs e) => {
                OnClick (sender, new MessageToolbarEventArgs (ActionType.REPLY));
            };

            replyAllButton = new UIBarButtonItem ();
            replyAllButton.TintColor = BUTTON_COLOR;
            Util.SetOriginalImageForButton (replyAllButton, "toolbar-icn-reply-all");
            replyAllButton.Clicked += (object sender, EventArgs e) => {
                OnClick (sender, new MessageToolbarEventArgs (ActionType.REPLY_ALL));
            };

            forwardButton = new UIBarButtonItem ();
            forwardButton.TintColor = BUTTON_COLOR;
            Util.SetOriginalImageForButton (forwardButton, "toolbar-icn-fwd");
            forwardButton.Clicked += (object sender, EventArgs e) => {
                OnClick (sender, new MessageToolbarEventArgs (ActionType.FORWARD));
            };

            archiveButton = new UIBarButtonItem ();
            archiveButton.TintColor = BUTTON_COLOR;
            Util.SetOriginalImageForButton (archiveButton, "email-archive-gray");
            archiveButton.Clicked += (object sender, EventArgs e) => {
                OnClick (sender, new MessageToolbarEventArgs (ActionType.ARCHIVE));
            };

            flexibleSpace = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);

            fixedSpace = new UIBarButtonItem (UIBarButtonSystemItem.FixedSpace);
            fixedSpace.Width = 10;

            deleteButton = new UIBarButtonItem ();
            deleteButton.TintColor = BUTTON_COLOR;
            Util.SetOriginalImageForButton (deleteButton, "email-delete-gray");
            deleteButton.Clicked += (object sender, EventArgs e) => {
                OnClick (sender, new MessageToolbarEventArgs(ActionType.DELETE));
            };

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
    }
}

