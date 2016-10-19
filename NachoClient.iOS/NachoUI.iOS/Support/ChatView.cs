//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using System.Linq;
namespace NachoClient.iOS
{
    public interface ChatViewDataSource
    {
        int NumberOfMessagesInChatView (ChatView chatView);
        ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index);
        void UpdateMessageViewBlockProperties (ChatView chatView, int index, ChatMessageView messageView);
    }

    public interface ChatViewDelegate
    {
        void ChatMessageViewDidSelectError (ChatView chatView, int index);
        void ChatMessageViewNeedsLoad (ChatView chatView, McEmailMessage message);
        void ChatViewDidSelectMessage (ChatView chatView, int index);
    }

    public class ChatView : UIView, IUIScrollViewDelegate, ThemeAdopter
    {
        public readonly UIScrollView ScrollView;
        public ChatViewDataSource DataSource;
        public ChatViewDelegate Delegate;
        Queue<ChatMessageView> ReusableMessageViews;
        List<ChatMessageView> VisibleMessageViews;
        int MessageCount;
        int TopCalculatedIndex;
        nfloat MessageSpacing = 4.0f;
        public bool ShowPortraits;
        public bool ShowNameLabels;
        public nfloat TimestampRevealProgress = 0.0f;
        public nfloat TimestampRevealWidth = 72.0f;
        public nfloat LastOffsetX = 0.0f;

        public bool IsScrolledToBottom {
            get {
                return ScrollView.ContentOffset.Y >= Math.Max (0, ScrollView.ContentSize.Height - ScrollView.Bounds.Height - MessageSpacing);
            }
        }

        public ChatView (CGRect frame) : base (frame)
        {
            ScrollView = new UIScrollView (Bounds);
            ScrollView.Delegate = this;
            ScrollView.AlwaysBounceVertical = true;
            ScrollView.ShowsHorizontalScrollIndicator = false;
            ScrollView.AlwaysBounceHorizontal = false;
            ScrollView.DirectionalLockEnabled = true;
            AddSubview (ScrollView);
            ReusableMessageViews = new Queue<ChatMessageView> ();
            VisibleMessageViews = new List<ChatMessageView> ();
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            adoptedTheme = theme;
            BackgroundColor = theme.ChatBackgroundColor;
            foreach (var view in VisibleMessageViews) {
                view.AdoptTheme (theme);
            }
        }

        public ChatMessageView DequeueReusableChatMessageView ()
        {
            ChatMessageView messageView;
            if (ReusableMessageViews.Count > 0) {
                messageView = ReusableMessageViews.Dequeue ();
            }
            messageView = new ChatMessageView (new CGRect (0.0f, 0.0f, Bounds.Width, 100.0f));
            messageView.ChatView = this;
            return messageView;
        }

        void EnqueueReusableChatMessageView (ChatMessageView messageView)
        {
            messageView.ChatView = null;
            ReusableMessageViews.Enqueue (messageView);
        }

        public void ReloadData ()
        {
            MessageCount = DataSource.NumberOfMessagesInChatView (this);
            var index = MessageCount - 1;
            nfloat height = MessageSpacing;
            ChatMessageView messageView;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i) {
                messageView = VisibleMessageViews [i];
                messageView.RemoveFromSuperview ();
                EnqueueReusableChatMessageView (messageView);
            }
            VisibleMessageViews.Clear ();
            TopCalculatedIndex = 0;
            while (index >= 0 && height < Bounds.Height * 3.0f) {
                messageView = GetMessageViewAtIndex (index);
                if (height < Bounds.Height) {
                    VisibleMessageViews.Add (messageView);
                } else {
                    EnqueueReusableChatMessageView (messageView);
                }
                height += messageView.Frame.Height + MessageSpacing;
                TopCalculatedIndex = index;
                --index;
            }
            VisibleMessageViews.Sort (CompareMessageViews);
            var y = height - MessageSpacing;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i) {
                messageView = VisibleMessageViews [i];
                messageView.Frame = new CGRect (messageView.Frame.X, y - messageView.Frame.Height, messageView.Frame.Width, messageView.Frame.Height);
                ScrollView.AddSubview (messageView);
                y -= messageView.Frame.Height + MessageSpacing;
            }
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width + TimestampRevealWidth, height);
        }

        ChatMessageView GetMessageViewAtIndex (int index)
        {
            var messageView = DataSource.ChatMessageViewAtIndex (this, index);
            messageView.Index = index;
            if (adoptedTheme != null) {
                messageView.AdoptTheme (adoptedTheme);
            }
            messageView.LayoutIfNeeded ();
            messageView.SizeToFit ();
            return messageView;
        }

        int CompareMessageViews (ChatMessageView a, ChatMessageView b)
        {
            return a.Index - b.Index;
        }

        public void InsertMessageViewAtEnd ()
        {
            MessageCount += 1;
            bool isAtBottom = IsScrolledToBottom;
            var index = MessageCount - 1;
            var messageView = GetMessageViewAtIndex (index);
            messageView.Frame = new CGRect (messageView.Frame.X, ScrollView.ContentSize.Height, messageView.Frame.Width, messageView.Frame.Height);
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width + TimestampRevealWidth, ScrollView.ContentSize.Height + messageView.Frame.Height + MessageSpacing);
            if (isAtBottom) {
                messageView.Index = index;
                VisibleMessageViews.Add (messageView);
                ScrollView.AddSubview (messageView);
                ScrollToBottom (true);
            } else {
                EnqueueReusableChatMessageView (messageView);
            }
            foreach (var visibleView in VisibleMessageViews) {
                DataSource.UpdateMessageViewBlockProperties (this, visibleView.Index, visibleView);
            }
        }

        public ChatMessageView MessageViewAtIndex (int index)
        {
            foreach (var view in VisibleMessageViews) {
                if (view.Index == index) {
                    return view;
                }
            }
            return null;
        }

        public void ScrollToBottom (bool animated = false)
        {
            if (VisibleMessageViews.Count > 0) {
                var finalMessageView = VisibleMessageViews.Last ();
                var bottom = Math.Max (0, ScrollView.ContentSize.Height - ScrollView.Bounds.Height);
                if (finalMessageView.IsMe) {
                    ScrollView.SetContentOffset (new CGPoint (0.0f, bottom), animated);
                } else {
                        ScrollView.SetContentOffset (new CGPoint (0.0f, Math.Min (bottom, finalMessageView.Frame.Y)), animated);
                }
            }
        }

        [Foundation.Export ("scrollViewDidScroll:")]
        public void Scrolled (UIScrollView scrollView)
        {
            if (MessageCount == 0) {
                return;
            }
            if (TopCalculatedIndex > 0 && scrollView.ContentOffset.Y < Bounds.Height) {
                var i = TopCalculatedIndex - 1;
                nfloat extraHeight = 0.0f;
                ChatMessageView messageView;
                while (i >= 0 && extraHeight < ScrollView.Bounds.Height * 3.0) {
                    messageView = GetMessageViewAtIndex (i);
                    EnqueueReusableChatMessageView (messageView);
                    extraHeight += messageView.Frame.Height + MessageSpacing;
                    TopCalculatedIndex = i;
                    --i;
                }
                ScrollView.ContentSize = new CGSize (ScrollView.ContentSize.Width, ScrollView.ContentSize.Height + extraHeight);
                foreach (var visibleView in VisibleMessageViews) {
                    var frame = visibleView.Frame;
                    frame.Y += extraHeight;
                    visibleView.Frame = frame;
                }
                ScrollView.ContentOffset = new CGPoint (ScrollView.ContentOffset.X, ScrollView.ContentOffset.Y + extraHeight);
            }
            if (scrollView.ContentOffset.X < 0.0f) {
                scrollView.ContentOffset = new CGPoint (0.0f, scrollView.ContentOffset.Y);
            }
            if (scrollView.ContentOffset.X > TimestampRevealWidth) {
                scrollView.ContentOffset = new CGPoint (TimestampRevealWidth, scrollView.ContentOffset.Y);
            }
            var topY = scrollView.ContentOffset.Y;
            var bottomY = topY + scrollView.Bounds.Height;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i) {
                var messageView = VisibleMessageViews [i];
                if ((messageView.Frame.Y >= scrollView.ContentOffset.Y + scrollView.Bounds.Height) || (messageView.Frame.Y + messageView.Frame.Height <= scrollView.ContentOffset.Y)) {
                    messageView.RemoveFromSuperview ();
                    VisibleMessageViews.RemoveAt (i);
                    EnqueueReusableChatMessageView (messageView);
                }
            }
            if (VisibleMessageViews.Count == 0) {
                // This can happen if we've scrolled so far that none of the previously visible messages are in the
                // window anymore.  It only happens (currently) if we are scrolled far up and a request is made to
                // scroll to the bottom.  Since we're at the bottom, we can just reload the data, which starts at the bottom.
                ReloadData ();
                return;
            }
            var firstVisibleView = VisibleMessageViews [0];
            var lastVisibleView = VisibleMessageViews [VisibleMessageViews.Count - 1];
            int index;
            var y = firstVisibleView.Frame.Y - MessageSpacing;
            while (topY < y && firstVisibleView.Index > 0) {
                index = firstVisibleView.Index - 1;
                firstVisibleView = GetMessageViewAtIndex (index);
                VisibleMessageViews.Insert (0, firstVisibleView);
                firstVisibleView.SizeToFit ();
                firstVisibleView.Frame = new CGRect (firstVisibleView.Frame.X, y - firstVisibleView.Frame.Height, firstVisibleView.Frame.Width, firstVisibleView.Frame.Height);
                ScrollView.AddSubview (firstVisibleView);
                y -= firstVisibleView.Frame.Height + MessageSpacing;
            }
            y = lastVisibleView.Frame.Y + lastVisibleView.Frame.Height + MessageSpacing;
            while (bottomY > y && lastVisibleView.Index < MessageCount - 1) {
                index = lastVisibleView.Index + 1;
                lastVisibleView = GetMessageViewAtIndex (index);
                VisibleMessageViews.Add (lastVisibleView);
                lastVisibleView.SizeToFit ();
                lastVisibleView.Frame = new CGRect (lastVisibleView.Frame.X, y, lastVisibleView.Frame.Width, lastVisibleView.Frame.Height);
                ScrollView.AddSubview (lastVisibleView);
                y += lastVisibleView.Frame.Height + MessageSpacing;
            }
            if (Math.Abs (LastOffsetX - scrollView.ContentOffset.X) >= 0.5f) {
                TimestampRevealProgress = (nfloat)Math.Max (0.0f, Math.Min (1.0f, scrollView.ContentOffset.X / TimestampRevealWidth));
                foreach (var visibleView in VisibleMessageViews) {
                    visibleView.SetNeedsLayout ();
                    visibleView.LayoutIfNeeded ();
                }
            }
            LastOffsetX = scrollView.ContentOffset.X;
        }

        [Export ("scrollViewWillEndDragging:withVelocity:targetContentOffset:")]
        public virtual void WillEndDragging (UIScrollView scrollView, CGPoint velocity, ref CGPoint targetContentOffset)
        {
            targetContentOffset.X = 0.0f;
        }

        public override void LayoutSubviews ()
        {
            bool scrolledToBottom = IsScrolledToBottom;
            base.LayoutSubviews ();
            ScrollView.Frame = Bounds;
            if (scrolledToBottom) {
                ScrollToBottom ();
            }
        }

    }
}
