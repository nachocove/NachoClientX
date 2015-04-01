//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;

namespace NachoClient.iOS
{
    /// <summary>
    /// Skip evernote.app.htmlData, which causes
    /// UITextView paste problems -- slow render
    /// and badly formated (like raw data).
    /// </summary>
    public class NcTextView : UITextView
    {
        public NcTextView (CGRect rect) : base (rect)
        {
        }

        public override void Paste (NSObject sender)
        {
            bool justText = false;
            var pasteboard = UIPasteboard.General;

            foreach (var p in pasteboard.Items) {
                if (p.ContainsKey (NSObject.FromObject ("com.evernote.app.htmlData"))) {
                    justText = true;
                }
            }
            if (justText) {
                var savedString = pasteboard.String;
                var items = pasteboard.Items;
                pasteboard.String = savedString;
                base.Paste (sender);
                pasteboard.Items = items;
            } else {
                base.Paste (sender);
            }
        }
    }
}

