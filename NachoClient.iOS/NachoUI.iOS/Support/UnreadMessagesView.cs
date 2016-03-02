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

        // FIXME - add LTR.
        public UnreadMessagesView (CGRect rect, Action<NSObject> onUnreadSelected, Action<NSObject> onDeadlineSelected, Action<NSObject> onDeferredSelected) : base (rect)
        {
            var cellHeight = rect.Height;
            var lineWidth = rect.Width + (2 * A.Card_Horizontal_Indent);

            var line = Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 0, lineWidth, A.Color_NachoBorderGray, this);
            line.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            unreadMessages = new UcIconLabelPair (new CGRect (0, 0, rect.Width, cellHeight), "gen-inbox", 0, 15, onUnreadSelected);
            unreadMessages.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            unreadMessages.SetValue ("Go to Inbox");

            line = Util.AddHorizontalLine (-A.Card_Horizontal_Indent, cellHeight, lineWidth, A.Color_NachoBorderGray, this);
            line.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            deadlineMessages = new UcIconLabelPair (new CGRect (0, cellHeight, rect.Width, cellHeight), "gen-deadline", 0, 15, onDeadlineSelected);
            deadlineMessages.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            deadlineMessages.SetValue ("Go to Deadlines");

            line = Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 2 * cellHeight, lineWidth, A.Color_NachoBorderGray, this);
            line.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            deferredMessages = new UcIconLabelPair (new CGRect (0, 2 * cellHeight, rect.Width, cellHeight), "gen-deferred-msgs", 0, 15, onDeferredSelected);
            deferredMessages.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
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
                int unreadCount;
                int likelyCount;
                int deferredCount;
                int deadlineCount;
                EmailHelper.GetMessageCounts (account, out unreadCount, out deferredCount, out deadlineCount, out likelyCount, EmailHelper.GetNewSincePreference ());
                InvokeOnUIThread.Instance.Invoke (() => {
                    unreadMessages.SetValue (String.Format ("Go to Inbox ({0:N0} unread)", unreadCount));
                    deadlineMessages.SetValue (String.Format ("Go to Deadlines ({0:N0})", deadlineCount));
                    deferredMessages.SetValue (String.Format ("Go to Deferred Messages ({0:N0})", deferredCount));
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }
    }
}
