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
using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    ///  Functions to help us with Mime stuffs on iOS
    /// </summary>
    public class MimeUtilities
    {
        static public MimeMessage motd;

        public MimeUtilities ()
        {
        }

        static public UIImage RenderContentId (string value)
        {
            MimeEntity e = FindMimePart (value);

            MimePart part = (MimePart)e;
            var image = RenderImage (part);
            if (null != image) {
                return image;
            }
            // TODO: Handle case where we cannot convert
            return RenderStringToImage (value);
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

        public static MimeEntity FindMimePart (string cid)
        {
            return SearchMessage (cid, motd);
        }

        public static MimeEntity SearchMessage (string cid, MimeMessage message)
        {
            NachoAssert.True (null != message);
            return SearchMimeEntity (cid, message.Body);
        }

        public static MimeEntity SearchMimeEntity (string cid, MimeEntity entity)
        {
            if (entity is MessagePart) {
                var messagePart = (MessagePart)entity;
                return SearchMessage (cid, messagePart.Message);
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                foreach (var subpart in multipart) {
                    var e = SearchMimeEntity (cid, subpart);
                    if (null != e) {
                        return e;
                    }
                }
                return null;
            }
            var part = (MimePart)entity;
            if ((null != part.ContentId) && part.ContentId.Contains (cid)) {
                return entity;
            } else {
                return null;
            }
        }

        static string Indent (int indent)
        {
            return indent.ToString ().PadRight (2 + (indent * 2));
        }

        static public void DumpMessage (MimeMessage message, int indent)
        {
            Log.Info ("{0}MimeMessage: {1}", Indent (indent), message);
            DumpMimeEntity (message.Body, indent + 1);
        }

        static void DumpMimeEntity (MimeEntity entity, int indent)
        {
            if (entity is MessagePart) {
                var messagePart = (MessagePart)entity;
                Log.Info ("{0}MimeEntity: {1} {2}", Indent (indent), messagePart, messagePart.ContentType);
                DumpMessage (messagePart.Message, indent + 1);
                return;
            }
            if (entity is Multipart) {
                var multipart = (Multipart)entity;
                Log.Info ("{0}Multipart: {1} {2}", Indent (indent), multipart, multipart.ContentType);
                foreach (var subpart in multipart) {
                    Log.Info ("{0}Subpart: {1} {2}", Indent (indent), subpart, subpart.ContentType);
                    DumpMimeEntity (subpart, indent + 1);
                }
                return;
            }
            Log.Info ("{0}MimeEntity: {1} {2}", Indent (indent), entity, entity.ContentType);
        }
    }
}
