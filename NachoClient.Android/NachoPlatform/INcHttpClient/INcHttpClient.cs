//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using NachoCore.Model;
using System.Net;
using System.Threading;
using System.Text;
using NachoCore.Utils;
using System.Linq;
using System.Net.Http.Headers;

namespace NachoPlatform
{
    #region INcHttpClient

    public enum NcHttpClientState
    {
        Unknown = 0,
        Running,
        Suspended,
        Canceling,
        Completed
    }

    public class NcHttpHeaders : HttpHeaders
    {
        public NcHttpHeaders ()
        {}
    }

    public class NcHttpRequest
    {
        public NcHttpHeaders Headers { get; protected set; }

        public Stream Content { get; protected set; }

        public long? ContentLength { get; protected set; }

        public string ContentType { get; protected set; }

        public Uri RequestUri { get; protected set; }

        public HttpMethod Method { get; protected set; }

        McCred _Cred;

        public McCred Cred { 
            get { return _Cred; }
            set {
                if (value != null) {
                    NcAssert.True (RequestUri.IsHttps());
                }
                _Cred = value;
            }
        }

        public NcHttpRequest (HttpMethod method, Uri uri)
        {
            RequestUri = uri;
            Method = method;
            Headers = new NcHttpHeaders ();
        }

        public NcHttpRequest (HttpMethod method, string url) : this (method, new Uri (url))
        {
        }

        public NcHttpRequest (HttpMethod method, string url, McCred cred) : this (method, new Uri (url), cred)
        {
        }

        public NcHttpRequest (HttpMethod method, Uri uri, McCred cred) : this (method, uri)
        {
            Cred = cred;
        }

        public void SetContent (Stream stream, string contentType)
        {
            SetContent (stream, null, contentType);
        }

        public void SetContent (Stream stream, long? contentLength, string contentType)
        {
            Content = stream;
            ContentType = contentType;
            ContentLength = contentLength;
        }
    }

    public class NcHttpResponse
    {
        public NcHttpHeaders Headers { get; protected set; }

        public Stream Content { get; protected set; }

        public string ContentType { get; protected set; }

        public HttpStatusCode StatusCode { get; protected set; }

        public long ContentLength { get {
                if (null != Content) {
                    return Content.Length;
                }
                if (Headers.Contains ("Content-Length")) {
                    long len;
                    if (long.TryParse (Headers.GetValues ("Content-Length").First (), out len)) {
                        return len;
                    }
                }
                return -1;
            }
        }

        public bool HasBody {
            get {
                return ContentLength > 0;
            }
        }

        public NcHttpResponse (HttpStatusCode status, Stream stream, string contentType, NcHttpHeaders headers)
        {
            StatusCode = status;
            Content = stream;
            ContentType = contentType;
            Headers = headers;
        }
    }

    /// <summary>
    /// Progress delegate. Note that the totalBytesExpected is frequently wrong. -1 means 'unknown'.
    /// </summary>
    public delegate void ProgressDelegate (bool isRequest, long bytes, long totalBytes, long totalBytesExpected);
    /// <summary>
    /// Success delete. Called when the request (and response) are done.
    /// </summary>
    public delegate void SuccessDelegate (NcHttpResponse response, CancellationToken token);
    /// <summary>
    /// Error delegate. Called on error.
    /// </summary>
    public delegate void ErrorDelegate (Exception exception);

    public interface INcHttpClient
    {
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
    }

    #endregion
}

