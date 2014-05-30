//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MCSwipeTableViewCellBinding;

namespace NachoClient.iOS
{
    public enum NachoMessageIcon
    {
        None,
        Read,
        Clock,
        Checked,
    };

    class NachoSwipeTableViewCell : MCSwipeTableViewCell
    {
        private bool _disposed;
        MessageSummaryView view;

        public NachoSwipeTableViewCell (UITableViewCellStyle style, string reuseIndentifier) : base (style, reuseIndentifier)
        {
            _disposed = false;
            Console.WriteLine ("New NachoSwipeTableCell");
            view = new MessageSummaryView ();
            ContentView.Add (view);
        }

        public void Update (string sender, string body, string subject, DateTime date, NachoMessageIcon icon, int messageCount)
        {
            view.Update (sender, body, subject, date, icon, messageCount);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            view.Frame = ContentView.Bounds;
            view.SetNeedsDisplay ();
        }

        public override void PrepareForReuse ()
        {
            if (null != base.View1) {
                base.View1.Dispose ();
                // base.View1 = null;
            }
            if (null != base.View2) {
                base.View2.Dispose ();
                // base.View2 = null;
            }
            if (null != base.View3) {
                base.View3.Dispose ();
                // base.View3 = null;
            }
            if (null != base.View4) {
                base.View4.Dispose ();
                // base.View4 = null;
            }
            if (null != base.Color1) {
                base.Color1.Dispose ();
                // base.Color1 = null;
            }
            if (null != base.Color2) {
                base.Color2.Dispose ();
                // base.Color2 = null;
            }
            if (null != base.Color3) {
                base.Color3.Dispose ();
                // base.Color3 = null;
            }
            if (null != base.Color4) {
                base.Color4.Dispose ();
                // base.Color4 = null;
            }

            base.PrepareForReuse ();
        }

        public new void Dispose ()
        {
            Dispose (true);     
        }

        protected new void Dispose (bool disposing)
        {
            Console.WriteLine ("Dispose NachoSwipeTableCell {0}", _disposed);

            if (!_disposed) {
                if (disposing) {
                    if (null != view) {
                        view.Dispose ();
                        view = null;
                    }
                    PrepareForReuse ();
                }
                // Indicate that the instance has been disposed.
                _disposed = true;   
            }
            base.Dispose (disposing);
        }

        public static NachoSwipeTableViewCell GetCell (UITableView tableView, McEmailMessageThread messageThread)
        {
            const string CellIdentifier = "EmailMessageThreadCell";

            NachoSwipeTableViewCell cell = (NachoSwipeTableViewCell)tableView.DequeueReusableCell (CellIdentifier);

            if (null == cell) {
                cell = new NachoSwipeTableViewCell (UITableViewCellStyle.Subtitle, CellIdentifier);
                if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    cell.SeparatorInset = UIEdgeInsets.Zero;
                }
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.ContentView.BackgroundColor = UIColor.White;
                cell.DefaultColor = UIColor.White;
            }

            var message = messageThread.SingleMessageSpecialCase ();
            var sender = message.From;
            var subject = message.Subject;
            if (null == message.Summary) {
                message.Summarize ();
            }
            NachoAssert.True (null != message.Summary);
            var summary = message.Summary;
            var date = message.DateReceived;
            var icon = (message.IsRead ? NachoMessageIcon.None : NachoMessageIcon.Read);
            if (DateTime.UtcNow < message.FlagUtcDeferUntil) {
                icon = NachoMessageIcon.Clock;
            }
            var count = (messageThread.Count > 1 ? messageThread.Count : 0);

            cell.Update (sender, summary, subject, date, icon, count);


            return cell;
        }
    }

    public class MessageSummaryView : UIView
    {
        public string Sender { get; private set; }

        public string Summary { get; private set; }

        public string Subject { get; private set; }

        public DateTime Date { get; private set; }

        public NachoMessageIcon Icon  { get; private set; }

        public int MessageCount  { get; private set; }

        static CGGradient gradient;

        static MessageSummaryView ()
        {
            using (var colorspace = CGColorSpace.CreateDeviceRGB ()) {
                gradient = new CGGradient (colorspace, new float [] {
                    /*                                 first */
                    0.52f, 0.69f, 0.96f, 1,
                    /*                                 second */
                    0.12f, 0.31f, 0.67f, 1
                }, null); //new float [] { 0, 1 });
            }
        }

        public MessageSummaryView ()
        {
            BackgroundColor = UIColor.White;
        }

        public void Update (string sender, string summary, string subject, DateTime date, NachoMessageIcon icon, int messageCount)
        {
            Sender = sender;
            Subject = subject;
            Summary = summary;
            Date = date;
            Icon = icon;
            MessageCount = messageCount;
            SetNeedsDisplay ();
        }
    }
}
