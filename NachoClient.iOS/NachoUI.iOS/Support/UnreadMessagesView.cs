//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using CoreGraphics;

using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class UnreadMessagesView : UIView
    {
        UcIconLabelPair unreadMessages;
        UcIconLabelPair deadlineMessages;
        UcIconLabelPair deferredMessages;

        public UnreadMessagesView (CGRect rect, Action<NSObject> onUnreadSelected, Action<NSObject> onDeadlineSelected, Action<NSObject> onDeferredSelected) : base (rect)
        {
            var cellHeight = rect.Height;
            var lineWidth = rect.Width + (2 * A.Card_Horizontal_Indent);

            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 0, lineWidth, A.Color_NachoBorderGray, this);
            unreadMessages = new UcIconLabelPair (new CGRect (0, 0, rect.Width, cellHeight), "gen-inbox", 0, 15, onUnreadSelected);
            unreadMessages.SetValue ("Go to Inbox");

            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, cellHeight, lineWidth, A.Color_NachoBorderGray, this);
            deadlineMessages = new UcIconLabelPair (new CGRect (0, cellHeight, rect.Width, cellHeight), "gen-deadline", 0, 15, onDeadlineSelected);
            deadlineMessages.SetValue ("Go to Deadlines");

            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 2 * cellHeight, lineWidth, A.Color_NachoBorderGray, this);
            deferredMessages = new UcIconLabelPair (new CGRect (0, 2 * cellHeight, rect.Width, cellHeight), "gen-deferred-msgs", 0, 15, onDeferredSelected);
            deferredMessages.SetValue ("Go to Deferred Messages");

            this.AddSubviews (new UIView[] { unreadMessages, deferredMessages, deadlineMessages, });
            ViewFramer.Create (this).Height (3 * cellHeight);
        }

        public void SetFont (UIFont font)
        {
            unreadMessages.valueFont = font;
            deadlineMessages.valueFont = font;
            deferredMessages.valueFont = font;
        }

        public void Update (McAccount account)
        {
            NcTask.Run (() => {
                System.Threading.Thread.Sleep (4000);
                int unreadMessageCount;
                int deferredMessageCount;
                int deadlineMessageCount;
                EmailHelper.GetMessageCounts (account, out unreadMessageCount, out deferredMessageCount, out deadlineMessageCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    unreadMessages.SetValue ("Go to Inbox (" + unreadMessageCount + " unread)");
                    deadlineMessages.SetValue ("Go to Deadlines (" + deadlineMessageCount + ")");
                    deferredMessages.SetValue ("Go to Deferred Messages (" + deferredMessageCount + ")");
                });
            }, "UpdateUnreadMessageView");
        }
    }
}
