//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;
using MimeKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyImageView : UIImageView, IBodyRender
    {
        protected SizeF _contentSize;
        public SizeF ContentSize {
            get {
                return _contentSize;
            }
        }

        public BodyImageView (RectangleF frame) : base (frame)
        {
            ViewHelper.SetDebugBorder (this, UIColor.Orange);

            Tag = (int)BodyView.TagType.MESSAGE_PART_TAG;
        }

        public void Configure (MimePart part)
        {
            var image = PlatformHelpers.RenderImage (part);

            if (null == image) {
                Log.Error (Log.LOG_UI, "Unable to render {0}", part.ContentType);
                // TODO - maybe put a bad image icon here?
                return;
            }

            float width = Frame.Width;
            float height = image.Size.Height * (width / image.Size.Width);
            Image = image.Scale (new SizeF (width, height));

            _contentSize = new SizeF (width, height);
        }

        public void ScrollTo (PointF upperLeft)
        {
            // Image view is not scrollable
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyImageView: offset=({0},{1})  frame=({2},{3})  content=({4},{5})",
                Frame.X, Frame.Y, Frame.Width, Frame.Height, ContentSize.Width, ContentSize.Height);
        }
    }
}

