//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net;

namespace NachoCore.Utils
{
    public class MockableHttpWebRequest : IHttpWebRequest
    {
        private HttpWebRequest _request;

        public MockableHttpWebRequest (HttpWebRequest request)
        {
            _request = request;
        }

        public Uri Address { get { return _request.Address; } }
        public Uri RequestUri { get { return _request.RequestUri; } }
    }
}

