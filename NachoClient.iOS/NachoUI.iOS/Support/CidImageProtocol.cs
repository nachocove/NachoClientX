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
using System.Drawing;
using System.IO;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MimeKit;

namespace NachoClient.iOS
{
    public class CidImageProtocol : NSUrlProtocol
    {
        [Export ("canInitWithRequest:")]
        public static bool canInitWithRequest (NSUrlRequest request)
        {
            return request.Url.Scheme == "cid";
        }

        [Export ("canonicalRequestForRequest:")]
        public static new NSUrlRequest GetCanonicalRequest (NSUrlRequest forRequest)
        {
            return forRequest;
        }

        [Export ("initWithRequest:cachedResponse:client:")]
        public CidImageProtocol (NSUrlRequest request, NSCachedUrlResponse cachedResponse, NSUrlProtocolClient client) 
            : base (request, cachedResponse, client)
        {
        }

        public override void StartLoading ()
        {
            var value = Request.Url.ResourceSpecifier;
            using (var image = PlatformHelpers.RenderContentId (value)) {
                // FIXME: hardcoded width
                var scaledImage = image.Scale (new SizeF (320.0f, image.Size.Height * (320.0f / image.Size.Width)));
                using (var response = new NSUrlResponse (Request.Url, "image/jpeg", -1, null)) {
                    Client.ReceivedResponse (this, response, NSUrlCacheStoragePolicy.NotAllowed);
                    this.InvokeOnMainThread (delegate {
                        using (var data = scaledImage.AsJPEG ()) {
                            Client.DataLoaded (this, data);
                        }
                        Client.FinishedLoading (this);
                    });
                }
            }
        }

        public override void StopLoading ()
        {
        }
    }
}
