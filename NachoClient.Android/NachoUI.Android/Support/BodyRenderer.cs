//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;

namespace NachoClient.AndroidClient
{

    public class BodyRenderer
    {
        Android.Webkit.WebView webView;

        public BodyRenderer ()
        {
        }

        public void Start (Android.Webkit.WebView webView, McBody body, int nativeBodyType)
        {
            this.webView = webView;

            switch (body.BodyType) {
            case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                RenderTextString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                RenderHtmlString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                // FIXME
                RenderTextString ("[RTF body type is not yet supported.]");
                break;
            case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                RenderMime (body, nativeBodyType);
                break;
            default:
                Log.Error (Log.LOG_UI, "Body {0} has an unknown body type {1}.", body.Id, (int)body.BodyType);
                RenderTextString (body.GetContentsString ());
                break;
            }
            this.webView = null;
        }

        void RenderTextString (string text)
        {
            webView.LoadData (text, "text/plain", null);
        }

        void RenderHtmlString (string html)
        {
            webView.LoadData (html, "text/html", null);
        }

        void RenderMime (McBody body, int nativeBodyType)
        {
            var mimeMessage = MimeHelpers.LoadMessage (body);
            var list = new List<MimeEntity> ();
            MimeHelpers.MimeDisplayList (mimeMessage, list, MimeHelpers.MimeTypeFromNativeBodyType (nativeBodyType));

            foreach (var entity in list) {
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtmlPart (part);
                } else if (part.ContentType.Matches ("text", "rtf")) {
                    RenderRtfPart (part);
                } else if (part.ContentType.Matches ("text", "*")) {
                    RenderTextPart (part);
                } else if (part.ContentType.Matches ("image", "*")) {
                    // FIXME
                    RenderTextString ("[Image MIME part is not yet supported.]");
                }
            }
        }

        void RenderTextPart (MimePart part)
        {
            RenderTextString ((part as TextPart).Text);
        }

        private void RenderHtmlPart (MimePart part)
        {
            RenderHtmlString ((part as TextPart).Text);
        }

        private void RenderRtfPart (MimePart part)
        {
            // FIXME
            RenderTextString ("[RTF MIME part is not yet supported.]");
        }
    }

}

