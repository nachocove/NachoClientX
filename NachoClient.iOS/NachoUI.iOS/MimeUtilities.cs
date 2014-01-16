//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MimeKit;
using NachoCore;

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
            return Render (part);
        }

        static public UIImage Render(MimePart part)
        {
            using (var content = new MemoryStream ()) {
                // If the content is base64 encoded (which it probably is), decode it.
                part.ContentObject.DecodeTo (content);

                content.Seek (0, SeekOrigin.Begin);
                var data = NSData.FromStream (content);
                var image = UIImage.LoadFromData (data);

                return image;
            }
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
    }
}
