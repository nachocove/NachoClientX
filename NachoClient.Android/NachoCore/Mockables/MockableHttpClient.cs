//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;

namespace NachoCore.Utils
{
    // This class is here only to reference IHttpClient so that it can be mocked.
    public class MockableHttpClient : HttpClient, IHttpClient
    {
        public MockableHttpClient (HttpClientHandler handler) : base (handler)
        {
        }

        public MockableHttpClient (HttpClientHandler handler, bool disposeHandler) : base (handler, disposeHandler)
        {
        }
    }
}

