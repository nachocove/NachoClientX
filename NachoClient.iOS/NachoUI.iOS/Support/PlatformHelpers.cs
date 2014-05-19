//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;

using MonoTouch.UIKit;
using MimeKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient
{
    /// <summary>
    ///  Functions to help us with Mime stuffs on iOS
    /// </summary>
    public class PlatformHelpers
    {
        static public MimeMessage motd;

        public PlatformHelpers ()
        {
        }

        static public UIImage RenderContentId (string value)
        {
            MimeEntity e = MimeHelpers.SearchMessage (value, motd);
            if (null == e) {
                Log.Error ("RenderContentId: MimeEntity is null: {0}", value);
                return RenderStringToImage (value);
            }

            var part = e as MimePart;
            if (null == part) {
                Log.Error ("RenderContentId: MimePart is null: {0}", value);
                return RenderStringToImage (value);
            }

            var image = RenderImage (part);
            if (null == image) {
                Log.Error ("RenderContentId: image is null: {0}", value);
                return RenderStringToImage (value);
            }
            return image;
        }

        /// <summary>
        /// Renders the image from a MIME part.
        /// Supported Image Formats & Filename extensions
        /// Tagged Image File Format (TIFF) .tiff, .tif
        /// Joint Photographic Experts Group (JPEG) .jpg, .jpeg
        /// Graphic Interchange Format (GIF) .gif
        /// Portable Network Graphic (PNG) .png
        /// Windows Bitmap Format (DIB) .bmp, .BMPf
        /// Windows Icon Format .ico
        /// Windows Cursor .cur
        /// X Window System bitmap .xbm
        /// </summary>
        /// <returns>The image as a UIImage or null if the image type isn't supported.</returns>
        /// <param name="part">The MIME part.</param>
        static public UIImage RenderImage (MimePart part)
        {
            if (!part.ContentType.Matches ("image", "*")) {
                return null;
            }
            if (!RendersToUIImage (part)) {
                return null;
            }

            using (var content = new MemoryStream ()) {
                // If the content is base64 encoded (which it probably is), decode it.
                part.ContentObject.DecodeTo (content);
                content.Seek (0, SeekOrigin.Begin);
                var data = NSData.FromStream (content);
                var image = UIImage.LoadFromData (data);
                return image;
            }
        }

        static public bool RendersToUIImage (MimePart part)
        {
            string[] subtype = {
                "tiff",
                "jpeg",
                "gif",
                "png",
                "x-icon",
                " vnd.microsoft.ico",
                "x-win-bitmap",
                "x-xbitmap",
            };

            foreach (var s in subtype) {
                if (part.ContentType.Matches ("image", s)) {
                    return true;
                }
            }
            return false;
        }

        static public UIImage RenderStringToImage (string value)
        {
            NSString text = new NSString (string.IsNullOrEmpty (value) ? " " : value);
            UIFont font = UIFont.SystemFontOfSize (20);
            SizeF size = text.StringSize (font);
            UIGraphics.BeginImageContextWithOptions (size, false, 0.0f);
            UIColor.Red.SetColor ();
            text.DrawString (new PointF (0, 0), font);
            UIImage image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();

            return image;
        }


        public static void DisplayAttachment (UIViewController vc, McAttachment attachment)
        {
            var path = attachment.FilePath ();
            DisplayFile (vc, path);
        }

        public static void DisplayFile (UIViewController vc, McFile file)
        {
            var path = file.FilePath ();
            DisplayFile (vc, path);
        }

        protected static void DisplayFile (UIViewController vc, string path)
        {
            UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (path));
            Preview.Delegate = new DocumentInteractionControllerDelegate (vc);
            Preview.PresentPreview (true);
        }

        public static void DownloadAttachment (McAttachment attachment)
        {
            if (!attachment.IsDownloaded && (attachment.PercentDownloaded == 0)) {
                BackEnd.Instance.DnldAttCmd (attachment.AccountId, attachment.Id);
            }
        }

        public class DocumentInteractionControllerDelegate : UIDocumentInteractionControllerDelegate
        {
            UIViewController viewC;

            public DocumentInteractionControllerDelegate (UIViewController controller)
            {
                viewC = controller;
            }

            public override UIViewController ViewControllerForPreview (UIDocumentInteractionController controller)
            {
                return viewC;
            }

            public override UIView ViewForPreview (UIDocumentInteractionController controller)
            {
                return viewC.View;
            }

            public override RectangleF RectangleForPreview (UIDocumentInteractionController controller)
            {
                return viewC.View.Frame;
            }
        }
    }
}
