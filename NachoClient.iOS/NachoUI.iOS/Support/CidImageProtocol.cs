// 
// ImageProtocol.cs
//  
// Author:
//       Rolf Bjarne Kvinge (rolf@xamarin.com)
// 
// Copyright 2012, Xamarin Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using CoreGraphics;
using System.IO;
using Foundation;
using UIKit;
using MimeKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class CidImageProtocol : NSUrlProtocol
    {

        [Export ("canInitWithRequest:")]
        public static bool canInitWithRequest (NSUrlRequest request)
        {
            bool retval = false;
            try {
                if ((null == request) || (null == request.Url)) {
                    return false;
                }
                retval = (request.Url.Scheme == "cid");
            } catch (NullReferenceException) {
                Log.Error (Log.LOG_UI, "XAMMIT: CidImageProtocol.canInitWithRequest NullReferenceException");
            }
            return retval;
        }

        [Export ("canonicalRequestForRequest:")]
        public static new NSUrlRequest GetCanonicalRequest (NSUrlRequest forRequest)
        {
            return forRequest;
        }

        [Export ("initWithRequest:cachedResponse:client:")]
        public CidImageProtocol (NSUrlRequest request, NSCachedUrlResponse cachedResponse, INSUrlProtocolClient client)
            : base (request, cachedResponse, client)
        {
        }

        public override void StartLoading ()
        {
            if ((null == Request) || (null == Request.Url)) {
                Log.Error (Log.LOG_UI, "CidImageProtocol: Url is null for {0}", Request);
                Client.FinishedLoading (this);
                return;
            }
            var url = Request.Url;
            var resourceSpecifier = url.ResourceSpecifier;
            if (null == resourceSpecifier) {
                Log.Error (Log.LOG_UI, "CidImageProtocol: ResourceSpecifier is null for {0}", Request);
                Client.FinishedLoading (this);
                return;
            }
            var lastPathComponent = url.LastPathComponent;
            if (lastPathComponent != null){
                if (lastPathComponent.Equals ("__nacho__css__")) {
                    var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
                    var nachoCssPath = Path.Combine (documentsPath, "nacho.css");
                    var data = NSData.FromFile (nachoCssPath);
                    FinishLoading (data, "text/css");
                    return;
                }
            }
            using (var image = PlatformHelpers.RenderContentId (resourceSpecifier)) {
                if (null == image) {
                    Log.Error (Log.LOG_UI, "CidImageProtocol: RenderContentId returned null {0}", url);
                    Client.FinishedLoading (this);
                } else {
                    if (Request.Url.RelativeString.EndsWith (".png", StringComparison.OrdinalIgnoreCase)) {
                        using (var data = image.AsPNG ()) {
                            FinishLoading (data, "image/png");
                        }
                    } else if (Request.Url.RelativeString.EndsWith (".jpg", StringComparison.OrdinalIgnoreCase)) {
                        using (var data = image.AsJPEG ()) {
                            FinishLoading (data, "image/jpeg");
                        }
                    } else {
                        using (var data = image.AsJPEG ()) {
                            FinishLoading (data, "image/jpeg");
                        }
                    }
                }
            }
        }

        protected void FinishLoading (NSData data, string subtype)
        {
            using (var response = new NSUrlResponse (Request.Url, subtype, -1, null)) {
                Client.ReceivedResponse (this, response, NSUrlCacheStoragePolicy.NotAllowed);
                this.InvokeOnMainThread (delegate {
                    Client.DataLoaded (this, data);
                    Client.FinishedLoading (this);
                });
            }
        }

        public override void StopLoading ()
        {
        }
    }
}
