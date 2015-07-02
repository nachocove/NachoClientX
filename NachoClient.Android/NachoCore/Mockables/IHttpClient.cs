//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NachoCore.Utils
{
    public interface IHttpClient : IDisposable
    {
        TimeSpan Timeout { get; set; }
        Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, 
                                     HttpCompletionOption completionOption,
                                     CancellationToken cancellationToken);
        Task<HttpResponseMessage> GetAsync (Uri uri);
    }
}

