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

            AddSubview (unreadMessages);
            ViewFramer.Create (this).Height (cellHeight);
        }

        public void SetFont (UIFont font)
        {
            unreadMessages.valueFont = font;
        }

        public void Update (McAccount account)
        {
            NcTask.Run (() => {
                int unreadCount;
                int likelyCount;
                EmailHelper.GetMessageCounts (account, out unreadCount, out likelyCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    unreadMessages.SetValue (String.Format ("Go to Inbox ({0:N0} unread)", unreadCount));
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }
    }
}
