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

        string FancyFromString (string From)
        {
            System.Net.Mail.MailAddress address = new System.Net.Mail.MailAddress (From);
            if (null != address.DisplayName) {
                return address.DisplayName;
            }
            if (null != address.User) {
                return address.User;
            }
            return From;
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

            var message = messageThread.SingleMessageSpecialCase();
            var sender = message.From;
            var subject = message.Subject;
            if (null == message.Summary) {
                MimeHelpers.UpdateDbWithSummary (message);
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

        public override void Draw (RectangleF rect)
        {
            using (var ctx = UIGraphics.GetCurrentContext ()) {
                const int padright = 21;
                float boxWidth;
                float dateSize ;

                if (MessageCount > 0) {
                    using (var CountFont = UIFont.BoldSystemFontOfSize (13)) {
                        var ms = MessageCount.ToString ();
                        var ssize = StringSize (ms, CountFont);
                        boxWidth = Math.Min (22 + ssize.Width, 18);
                        var crect = new RectangleF (Bounds.Width - 20 - boxWidth, 32, boxWidth, 16);
                        var rectPath = UIBezierPath.FromRoundedRect (crect, 3.0f);
                        using (var context = UIGraphics.GetCurrentContext ()) {
                            context.SaveState ();
                            UIColor.LightGray.SetFill ();
                            rectPath.Fill ();
                            context.RestoreState ();
                        }
                        UIColor.White.SetColor ();
                        crect.X += 5;
                        DrawString (ms, crect, CountFont);
                    }
                    boxWidth += padright;
                } else {
                    boxWidth = 0;
                }

                UIColor.FromRGB (36, 112, 216).SetColor ();
                using (var DateFont = UIFont.SystemFontOfSize (14)) {
                    var date = Pretty.CompactDateString (Date);
                    var ssize = StringSize (date, DateFont);
                    dateSize = ssize.Width + padright + 5;
                    DrawString (date, new RectangleF (Bounds.Width - dateSize, 6, dateSize, 14), DateFont, UILineBreakMode.Clip, UITextAlignment.Left);
                }

                const int offset = 33;
                float bw = Bounds.Width - offset;

                UIColor.Black.SetColor ();

                using (var SenderFont = UIFont.SystemFontOfSize (19)) {
                    var sender = Pretty.SenderString (Sender);
                    DrawString (sender, new PointF (offset, 2), bw - dateSize, SenderFont, UILineBreakMode.TailTruncation);
                }

                using (var SubjectFont = UIFont.SystemFontOfSize (14)) {
                    var subject = Pretty.SubjectString (Subject);
                    DrawString (subject, new PointF (offset, 23), bw - offset - boxWidth, SubjectFont, UILineBreakMode.TailTruncation);
                }

                UIColor.Gray.SetColor ();

                using (var TextFont = UIFont.SystemFontOfSize (13)) {
                    DrawString (Summary, new RectangleF (offset, 40, bw - boxWidth, 34), TextFont, UILineBreakMode.TailTruncation, UITextAlignment.Left);
                }

                if (NachoMessageIcon.Checked == Icon) {
                    drawRectChecked (new RectangleF (5, 27, 22, 22));
                } else if (NachoMessageIcon.Clock == Icon) {
                    drawRectClock (new RectangleF (5, 27, 22, 22));
                } else if (NachoMessageIcon.Read == Icon) {
                    ctx.SaveState ();
                    ctx.AddEllipseInRect (new RectangleF (10, 32, 12, 12));
                    ctx.Clip ();
                    ctx.DrawLinearGradient (gradient, new PointF (10, 32), new PointF (22, 44), CGGradientDrawingOptions.DrawsAfterEndLocation);
                    ctx.RestoreState ();
                }

#if WANT_SHADOWS
            ctx.SaveState ();
            UIColor.FromRGB (78, 122, 198).SetStroke ();
            ctx.SetShadow (new SizeF (1, 1), 3);
            ctx.StrokeEllipseInRect (new RectangleF (10, 32, 12, 12));
            ctx.RestoreState ();
#endif
            }
        }

        void drawRectChecked (RectangleF rect)
        {
            //// General Declarations

            //// Color Declarations
            var checkmarkColor2 = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);

            //// Shadow Declarations
            var shadow2 = UIColor.Black;
            var shadow2Offset = new SizeF (0.1f, -0.1f);
            var shadow2ColorRadius = 2.5f;

            var checkedOvalPath = UIBezierPath.FromOval (
                                      new RectangleF (
                                          rect.GetMinX (),
                                          rect.GetMinY (),
                                          (float)Math.Floor (rect.Width * 1.00000f + 0.5f),
                                          (float)Math.Floor (rect.Height * 1.00000f + 0.5f)));

            using (var context = UIGraphics.GetCurrentContext ()) {
                context.SaveState ();
                context.SetShadowWithColor (shadow2Offset, shadow2ColorRadius, shadow2.CGColor);
                checkmarkColor2.SetFill ();
                checkedOvalPath.Fill ();
                context.RestoreState ();
            }

            UIColor.White.SetStroke ();
            checkedOvalPath.LineWidth = 1;
            checkedOvalPath.Stroke ();

            // Bezier Drawing
            using (var bezierPath = UIBezierPath.Create ()) {
                bezierPath.MoveTo (new PointF (rect.GetMinX () + 0.27083f * rect.Width, rect.GetMinY () + 0.54167f * rect.Height));
                bezierPath.AddLineTo (new PointF (rect.GetMinX () + 0.41667f * rect.Width, rect.GetMinY () + 0.68750f * rect.Height));
                bezierPath.AddLineTo (new PointF (rect.GetMinX () + 0.75000f * rect.Width, rect.GetMinY () + 0.35417f * rect.Height));
                bezierPath.LineCapStyle = CGLineCap.Square;
                UIColor.White.SetStroke ();
                bezierPath.LineWidth = 1.3f;
                bezierPath.Stroke ();
            }
        }

        void drawRectClock (RectangleF rect)
        {
            //// General Declarations

            //// Color Declarations
            var clockColor2 = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);


            //// Shadow Declarations
            var shadow2 = UIColor.Black;
            var shadow2Offset = new SizeF (0.1f, -0.1f);
            var shadow2ColorRadius = 2.5f;

            var checkedOvalPath = UIBezierPath.FromOval (
                                      new RectangleF (
                                          rect.GetMinX (),
                                          rect.GetMinY (),
                                          (float)Math.Floor (rect.Width * 1.00000f + 0.5f),
                                          (float)Math.Floor (rect.Height * 1.00000f + 0.5f)));

            using (var context = UIGraphics.GetCurrentContext ()) {
                context.SaveState ();
                context.SetShadowWithColor (shadow2Offset, shadow2ColorRadius, shadow2.CGColor);
                clockColor2.SetFill ();
                checkedOvalPath.Fill ();
                context.RestoreState ();
            }

            UIColor.White.SetStroke ();
            checkedOvalPath.LineWidth = 1;
            checkedOvalPath.Stroke ();

            // Bezier Drawing
            using (var bezierPath = UIBezierPath.Create ()) {
                bezierPath.MoveTo (new PointF (rect.GetMinX () + 0.45f * rect.Width, rect.GetMinY () + 0.25f * rect.Height));
                bezierPath.AddLineTo (new PointF (rect.GetMinX () + 0.45f * rect.Width, rect.GetMinY () + 0.55f * rect.Height));
                bezierPath.AddLineTo (new PointF (rect.GetMinX () + 0.70f * rect.Width, rect.GetMinY () + 0.55f * rect.Height));
                bezierPath.LineCapStyle = CGLineCap.Square;
                UIColor.White.SetStroke ();
                bezierPath.LineWidth = 2.7f;
                bezierPath.Stroke ();
            }
        }
    }
}
