//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using CoreGraphics;
using System.Collections.Generic;
using System.Linq;
using Foundation;

using UIKit;
using MimeKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using PassKit;

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
            // MimeKit removes trailing dots from CIDs.  Pretty sure that's a MimeKit bug, and to
            // workaround it, we'll trim trailing dots from our CID, too.
            value = value.TrimEnd (new char[] { '.' });
            try {
                bodyId = Convert.ToInt32 (cid.Substring (2, index));
            } catch (System.FormatException) {
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
                Log.Warn (Log.LOG_UTILS, "RenderContentId: {0} for cid {1}", message, cid ?? "");
                return Draw1px ();
            }
            McBody body = McBody.QueryById<McBody> (bodyId);
            MimePart p = null;
            if (null != body) {
                var mime = MimeHelpers.LoadMessage (body);
                p = MimeHelpers.SearchMessage (value, mime);
            }
            if (null == p) {
                Log.Warn (Log.LOG_UTILS, "RenderContentId: MimeEntity is null: {0} for cid {1}", value, cid);
                return RenderStringToImage (value);
            }

            var image = RenderImage (p);
            if (null == image) {
                Log.Warn (Log.LOG_UTILS, "RenderContentId: image is null: {0} for {1}", value, cid);
                return RenderStringToImage (value);
            }
            return image;
        }

        /// <summary>
        /// Renders the image from a MIME part.  Just pass the data to UIImage.LoadFromData() and
        /// see if that routine can parse the data as an image.  Don't check the MIME content type
        /// or try to figure what kind of image it is.  (Not checking the content type is because
        /// we have seen a real message where the sender put the image in an application/octet-stream
        /// section instead of an image/jpeg section.)
        /// </summary>
        /// <returns>The image as a UIImage or null if the image type isn't supported.</returns>
        /// <param name="part">The MIME part.</param>
        static public UIImage RenderImage (MimePart part)
        {
            using (var content = new MemoryStream ()) {
                part.ContentObject.DecodeTo (content);
                content.Seek (0, SeekOrigin.Begin);
                var data = NSData.FromStream (content);
                var image = UIImage.LoadFromData (data);
                return image;
            }
        }

        static public UIImage RenderStringToImage (string value)
        {
            NSString text = new NSString (string.IsNullOrEmpty (value) ? " " : value);
            UIFont font = UIFont.SystemFontOfSize (20);
            CGSize size = text.StringSize (font);
            UIGraphics.BeginImageContextWithOptions (size, false, 0.0f);
            UIColor.Red.SetColor ();
            text.DrawString (new CGPoint (0, 0), font);
            UIImage image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();

            return image;
        }

        public static UIImage Draw1px ()
        {
            var size = new CGSize (1, 1);
            var origin = new CGPoint (0, 0);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (UIColor.Clear.CGColor);
            ctx.FillEllipseInRect (new CGRect (origin, size));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static void DisplayAttachment (UIViewController vc, McAttachment attachment)
        {
            var path = attachment.GetFilePath ();

            // Add extension if there isn't one
            var ext = Pretty.GetExtension (path);
            if (String.IsNullOrEmpty (ext)) {
                if (!String.IsNullOrEmpty (attachment.ContentType)) {
                    var mimeInfo = attachment.ContentType.Split (new char[] { '/' });
                    if (2 == mimeInfo.Length) {
                        if (!String.IsNullOrEmpty (mimeInfo [1])) {
                            var displayName = attachment.DisplayName;
                            if (String.IsNullOrEmpty (displayName)) {
                                displayName = "noname";
                            }
                            displayName += "." + mimeInfo [1].ToLower ();
                            attachment.SetDisplayName(displayName);
                            path = attachment.GetFilePath ();
                        }
                    }
                }
            }

            DisplayFile (vc, path);
        }

        public static void DisplayFile (UIViewController vc, McDocument file)
        {
            var path = file.GetFilePath ();
            DisplayFile (vc, path);
        }

        protected static void DisplayFile (UIViewController vc, string path)
        {
            var url = NSUrl.FromFilename (path);
            if (url.PathExtension.ToLowerInvariant () == "pkpass") {
                if (PKAddPassesViewController.CanAddPasses) {
                    var data = NSData.FromUrl (url);
                    NSError error;
                    var pass = new PKPass (data, out error);
                    if (error == null) {
                        var addPassController = new PKAddPassesViewController (pass);
                        vc.PresentViewController (addPassController, true, null);
                    } else {
                        NachoClient.iOS.NcAlertView.ShowMessage (vc, "Unsupported Pass", "Sorry, we are unable to open this pass");
                    }
                } else {
                    NachoClient.iOS.NcAlertView.ShowMessage (vc, "Cannot Add Pass", "Sorry, passes cannot be added to Passbook on this device");
                }
            } else {
                UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (url);
                Preview.Delegate = new DocumentInteractionControllerDelegate (vc);
                if (!Preview.PresentPreview (true)) {
                    NachoClient.iOS.NcAlertView.ShowMessage (vc, "Unsupported Attachment", "Sorry, we are unable to open this type of attachment");
                }
            }
        }

        public static NcResult DownloadAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Error == attachment.FilePresence) {
                // Clear the error code so the download will be attempted again.
                attachment.DeleteFile ();
            }
            if (McAbstrFileDesc.FilePresenceEnum.None == attachment.FilePresence) {
                return BackEnd.Instance.DnldAttCmd (attachment.AccountId, attachment.Id, true);
            } else if (McAbstrFileDesc.FilePresenceEnum.Partial == attachment.FilePresence) {
                var token = McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id).Token;
                var nr = NcResult.OK (token); // null is potentially ok; callers expect it.
                return nr;
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

            public override CGRect RectangleForPreview (UIDocumentInteractionController controller)
            {
                return viewC.View.Frame;
            }
        }
    }
}
