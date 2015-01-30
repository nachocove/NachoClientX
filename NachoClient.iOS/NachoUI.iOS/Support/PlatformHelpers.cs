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
        static public Dictionary<string,MimePart> cidPartDict = new Dictionary<string,MimePart> ();

        public PlatformHelpers ()
        {
        }

        static public string CheckCID (string cid, out int bodyId, out string value)
        {
            value = "";
            bodyId = -1;
            if (null == cid) {
                return "null prefix for cid";
            }
            if (!cid.StartsWith ("//")) {
                return "no prefix for cid";
            }
            if (2 == cid.Length) {
                return "no body id for cid";
            }
            var index = cid.Substring (2).IndexOf ('/');
            if (-1 == index) {
                return "no trailing slash for cid";
            }
            if (cid.Length <= (index + 3)) {
                return "no value for cid";
            }
            value = cid.Substring (index + 3);
            try {
            bodyId = Convert.ToInt32 (cid.Substring (2, index));
            } catch(System.FormatException ex) {
                return "malformed body id";
            }
            return null;
        }

        // https://www.ietf.org/rfc/rfc2392.txt
        static public UIImage RenderContentId (string cid)
        {
            // In order to deal with gmail's logo.png CID, we encode
            // McBody id into the CID URL. The format is cid://[body_id]/[value]
            // NcAssert.True (cid.StartsWith ("//"));
            // Unfortunately, when we are rendering email reply text we do not
            // have the opportunity to set up the base url using NSAttributedString.
            // TODO: Compose mail need to use uiwebview for display and/or editing.
            int bodyId;
            string value;
            string message = CheckCID (cid, out bodyId, out value);
            if (null != message) {
                Log.Warn (Log.LOG_UTILS, "RenderContentId: {0}", message);
                return Draw1px ();
            }
            McBody body = McBody.QueryById<McBody> (bodyId);
            MimePart p = null;
            if (null != body) {
                var mime = MimeHelpers.LoadMessage (body);
                p = MimeHelpers.EntityWithContentId (mime, value);
            }
            if (null == p) {
                Log.Error (Log.LOG_UTILS, "RenderContentId: MimeEntity is null: {0}", value);
                return RenderStringToImage (value);
            }

            var image = RenderImage (p);
            if (null == image) {
                Log.Error (Log.LOG_UTILS, "RenderContentId: image is null: {0}", value);
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
                "jpg",
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

        public static UIImage Draw1px ()
        {
            var size = new SizeF (1, 1);
            var origin = new PointF (0, 0);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (UIColor.Clear.CGColor);
            ctx.FillEllipseInRect (new RectangleF (origin, size));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static void DisplayAttachment (UIViewController vc, McAttachment attachment)
        {
            var path = attachment.GetFilePath ();
            DisplayFile (vc, path);
        }

        public static void DisplayFile (UIViewController vc, McDocument file)
        {
            var path = file.GetFilePath ();
            DisplayFile (vc, path);
        }

        protected static void DisplayFile (UIViewController vc, string path)
        {
            UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (path));
            Preview.Delegate = new DocumentInteractionControllerDelegate (vc);
            Preview.PresentPreview (true);
        }

        public static string DownloadAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Error == attachment.FilePresence) {
                // Clear the error code so the download will be attempted again.
                attachment.DeleteFile ();
            }
            if (McAbstrFileDesc.FilePresenceEnum.None == attachment.FilePresence) {
                return BackEnd.Instance.DnldAttCmd (attachment.AccountId, attachment.Id, true);
            } else if (McAbstrFileDesc.FilePresenceEnum.Partial == attachment.FilePresence) {
                return McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id).Token;
            } 
            NcAssert.True (false, "Should not try to download an already-downloaded attachment");
            return null;
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
