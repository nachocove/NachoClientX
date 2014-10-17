﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public class WebCache : NSUrlCache
    {
        const string parseUrl = "https://api.parse.com/";

        public WebCache (uint memoryCapacity, uint diskCapcity) : base (memoryCapacity, diskCapcity, "NachoCache")
        {
            NSUrlCache.SharedCache = this;
        }

        public override NSCachedUrlResponse CachedResponseForRequest (NSUrlRequest request)
        {
            NSCachedUrlResponse response = base.CachedResponseForRequest (request);
            if (request.ToString().StartsWith (parseUrl)) {
                return null;
            }
            return response;
        }

        public override void StoreCachedResponse (NSCachedUrlResponse cachedResponse, NSUrlRequest forRequest)
        {
            if (forRequest.ToString ().StartsWith (parseUrl)) {
                return;
            }
            base.StoreCachedResponse (cachedResponse, forRequest);
        }

        public override void StoreCachedResponse (NSCachedUrlResponse cachedResponse, NSUrlSessionDataTask dataTask)
        {
            if (dataTask.CurrentRequest.ToString ().StartsWith (parseUrl)) {
                return;
            }
            base.StoreCachedResponse (cachedResponse, dataTask);
        }
    }
}

