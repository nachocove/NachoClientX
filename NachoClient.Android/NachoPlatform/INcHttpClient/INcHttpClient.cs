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

    public class NcHttpHeaders : HttpHeaders
    {
        public NcHttpHeaders ()
        {}
    }

    public class NcHttpRequest
    {
        public NcHttpHeaders Headers { get; protected set; }

        public long? ContentLength {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Length", out values)) {
                    long len;
                    var lenStr = values.First ();
                    if (long.TryParse (lenStr, out len)) {
                        return len;
                    } else {
                        Log.Info (Log.LOG_HTTP, "Could not parse Content-Length header value {0}", lenStr);
                    }
                }
                return null;
            }
        }

        public string ContentType {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Type", out values)) {
                    return values.First ();
                }
                return null;
            }
        }

        public Uri RequestUri { get; protected set; }

        public HttpMethod Method { get; protected set; }

        protected FileStream ContentStream { get; set; }

        protected byte[] ContentData { get; set; }

        protected McCred _Cred;

        public McCred Cred { 
            get { return _Cred; }
            set {
                if (value != null) {
                    NcAssert.True (RequestUri.IsHttps());
                }
                _Cred = value;
            }
        }

        public string guid { get; protected set; }

        public NcHttpRequest (HttpMethod method, Uri uri)
        {
            RequestUri = uri;
            Method = method;
            Headers = new NcHttpHeaders ();
            guid = Guid.NewGuid ().ToString ();
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

        public object Content {
            get {
                if (ContentStream != null && ContentStream.Length > 0) {
                    return ContentStream;
                }
                if (ContentData != null && ContentData.Length > 0) {
                    return ContentData;
                }
                return null;
            }
        }

        public bool HasContent ()
        {
            return (ContentStream != null && ContentStream.Length > 0) || (ContentData != null && ContentData.Length > 0);
        }

        public void SetContent (FileStream stream, string contentType)
        {
            SetContent (stream, null, null, contentType);
        }

        public void SetContent (FileStream stream, long? contentLength, string contentType)
        {
            SetContent (stream, null, contentLength, contentType);
        }

        public void SetContent (byte[] data, string contentType)
        {
            SetContent (data, data.Length, contentType);
        }

        public void SetContent (byte[] data, long? contentLength, string contentType)
        {
            SetContent (null, data, contentLength, contentType);
        }

        void SetContent (FileStream stream, byte[] data, long? contentLength, string contentType)
        {
            ContentStream = stream;
            ContentData = data;
            if (!Headers.Contains ("Content-Type")) {
                Headers.Add ("Content-Type", contentType);
            }
            if (!Headers.Contains ("Content-Length")) {
                long? len = null;
                if (contentLength.HasValue) {
                    len = contentLength.Value;
                } else if (ContentData != null) {
                    len = ContentData.Length;
                } else if (ContentStream != null) {
                    len = ContentStream.Length;
                }
                if (len.HasValue) {
                    Headers.Add ("Content-Length", len.Value.ToString ());
                }
            }
        }


        public byte[] GetContent ()
        {
            var content = Content;
            if (content == null) {
                return null;
            }

            if (content is FileStream) {
                var mem = new MemoryStream ();
                (content as FileStream).CopyTo (mem);
                return mem.GetBuffer ();
            }
            if (content is byte[]) {
                return content as byte[];
            }
            NcAssert.CaseError ("unknown type of content");
            return null;
        }

        public override string ToString ()
        {
            // TODO Remove this method or add redaction to the URL
            return string.Format ("[NcHttpRequest({0}): {1}:{2} ContentLength={3}, ContentType={4}, Headers={5}]", guid, Method, RequestUri, ContentLength, ContentType, Headers);
        }
    }

    public class NcHttpResponse
    {
        public NcHttpHeaders Headers { get; protected set; }

        public Stream Content { get; protected set; }

        public string ContentType {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Type", out values)) {
                    return values.First ();
                }
                return null;
            }
            protected set {
                if (!Headers.Contains ("Content-Type")) {
                    Headers.Add ("Content-Type", value);
                }
            }
        }

        public HttpStatusCode StatusCode { get; protected set; }

        public long ContentLength {
            get {
                // need to check the headers first, so that unit tests can set bogus values.
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Length", out values)) {
                    long len;
                    var lenStr = values.First ();
                    if (long.TryParse (lenStr, out len)) {
                        return len;
                    } else {
                        Log.Info (Log.LOG_HTTP, "Could not parse Content-Length header value {0}", lenStr);
                    }
                }
                if (null != Content) {
                    return Content.Length;
                }
                return -1;
            }
        }

        public bool HasBody {
            get {
                return ContentLength > 0;
            }
        }

        string tempFileName { get; set; }

        public NcHttpResponse (HttpStatusCode status, Stream stream, string contentType, NcHttpHeaders headers = null)
        {
            if (!(stream is FileStream)) {
                Log.Warn (Log.LOG_HTTP, "Creating NcHttpResponse with non-FileStream stream: {0}. Should only be used for unit tests", stream.GetType ().Name);
                tempFileName = Path.GetTempFileName ();
                using (var fs = new FileStream (tempFileName, FileMode.OpenOrCreate, FileAccess.Write)) {
                    stream.CopyTo (fs);
                }
                stream = new FileStream(tempFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            NcAssert.True (stream is FileStream);
            Headers = headers ?? new NcHttpHeaders ();
            StatusCode = status;
            Content = stream;
            ContentType = contentType;
        }

        ~NcHttpResponse ()
        {
            if (!string.IsNullOrEmpty (tempFileName)) {
                File.Delete (tempFileName);
            }
        }

        public NcHttpResponse (HttpStatusCode status)
        {
            StatusCode = status;
            Headers = new NcHttpHeaders ();
        }

        public byte[] GetContent ()
        {
            var content = Content;
            if (content == null) {
                return null;
            }

            if (content is FileStream) {
                var mem = new MemoryStream ();
                (content as FileStream).CopyTo (mem);
                return mem.GetBuffer ().Take ((int)mem.Length).ToArray ();
            }
            NcAssert.CaseError ("unknown type of content");
            return null;
        }
    }

    /// <summary>
    /// Progress delegate. Note that the totalBytesExpected is frequently wrong. -1 means 'unknown'.
    /// </summary>
    public delegate void ProgressDelegate (bool isRequest, string taskDescription, long bytes, long totalBytes, long totalBytesExpected);
    /// <summary>
    /// Success delete. Called when the request (and response) are done.
    /// </summary>
    public delegate void SuccessDelegate (NcHttpResponse response, CancellationToken token);
    /// <summary>
    /// Error delegate. Called on error.
    /// </summary>
    public delegate void ErrorDelegate (Exception exception, CancellationToken token);

    public interface INcHttpClient
    {
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
    }

    #endregion
}

