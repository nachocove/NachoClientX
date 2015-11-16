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

        public void AddHeader (string key, string value)
        {
            if (!Headers.Contains (key)) {
                Headers.Add (key, new List<string> ());
            }
            // FIXME: Make sure this does lists properly
            Headers.Add (key, value);
        }

        public bool ContainsHeader (string key)
        {
            return Headers.Contains (key);
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

    public class NachoHttpTest
    {
        public string Location { get; set; }

        McCred Cred { get; set; }

        public MemoryStream memStream { get; set; }

        public FileStream fileStream { get; set; }

        CancellationTokenSource Cts { get; set; }

        public NachoHttpTest ()
        {
            Location = Path.GetTempFileName ();
            Cred = new McCred () {
                Username = "janv@d2.officeburrito.com",
                Password = "Password1",
            };
            Cts = new CancellationTokenSource ();
        }

        ~NachoHttpTest ()
        {
            File.Delete (Location);
        }


        static string wbxml1 = "AwFqAAAURUZHA01haWxib3gAAQARUQM2JTNhMSUzYTAAAQEB";
        static string url1 = "https://mail.d2.officeburrito.com/Microsoft-Server-ActiveSync?Cmd=ItemOperations&User=janv@d2.officeburrito.com&DeviceId=Ncho3c15c2c87c06&DeviceType=iPhone";

        public void StartTest ()
        {
            StartUpload ();
            //StartDownload ();
        }

        void StartUpload ()
        {
#if __IOS__
            var destinationPath = "/tmp/wbxml.bin";
#elif __ANDROID__
            var documentsPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.MyDocuments);
            var destinationPath = Path.Combine (documentsPath, "wbxml.bin");
#else
#error "BOOOOOO"
#endif
                
            fileStream = new FileStream (destinationPath, FileMode.Open);

            var request = new NcHttpRequest (HttpMethod.Post, url1, Cred);
            request.AddHeader ("User-Agent", Device.Instance.UserAgent ());
            request.AddHeader ("X-MS-PolicyKey", "1495342484");
            request.AddHeader ("MS-ASProtocolVersion", "14.1");
            request.SetContent (fileStream, "application/vnd.ms-sync.wbxml");

            Log.Info (Log.LOG_HTTP, "Starting Upload with {0}:{1}", request.Method.ToString (), request.RequestUri.ToString ());
            NcHttpClient.Instance.SendRequest (request, 1000000, UploadSuccess, Error, Progress, Cts.Token);
        }

        void StartDownload ()
        {
            var bs = Convert.FromBase64String (wbxml1);
            memStream = new MemoryStream (bs, 0, bs.Length, false, true);

            var request = new NcHttpRequest (HttpMethod.Post, url1);
            request.AddHeader ("User-Agent", Device.Instance.UserAgent ());
            request.AddHeader ("X-MS-PolicyKey", "1495342484");
            request.AddHeader ("MS-ASProtocolVersion", "14.1");
            request.SetContent (memStream, "application/vnd.ms-sync.wbxml");
            request.Cred = Cred;

            Log.Info (Log.LOG_HTTP, "Starting Download with {0}:{1}", request.Method.ToString (), request.RequestUri.ToString ());
            NcHttpClient.Instance.GetRequest (request, 1000000, DownloadSuccess, Error, Progress, Cts.Token);
        }

        public void DownloadSuccess (NcHttpResponse response, CancellationToken token)
        {
            FileStream fs = response.Content as FileStream;
            NcAssert.NotNull (fs);
            Log.Info (Log.LOG_HTTP, "Response {0} content-type {1} length {2}", response.StatusCode, response.ContentType, fs.Length);
            foreach (var header in response.Headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            using (var data = new FileStream (Location, FileMode.OpenOrCreate)) {
                fs.CopyTo (data);
                Log.Info (Log.LOG_HTTP, "Response written ({0}) to file {1}", data.Length, Location);
            }
        }

        public void UploadSuccess (NcHttpResponse response, CancellationToken token)
        {
            Log.Info (Log.LOG_HTTP, "Response {0} content-type {1}", response.StatusCode, response.ContentType);
            foreach (var header in response.Headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            if (response.Content != null) {
                var mem = new MemoryStream ();
                response.Content.CopyTo (mem);
                Log.Info (Log.LOG_HTTP, "Response Data({0}):\n{1}", mem.Length, Convert.ToBase64String (mem.GetBuffer ().Take ((int)mem.Length).ToArray ()));
            }
            Console.WriteLine ("HTTP: Finished Upload");
        }

        public void Error (Exception ex)
        {
            Console.WriteLine ("HTTP: Request failed: {0}", ex.Message);
        }

        public void Progress (bool isRequest, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
        {
            Console.WriteLine ("HTTP: {0} Progress: {1}:{2}:{3}", isRequest ? "Request" : "Response", bytesSent, totalBytesSent, totalBytesExpectedToSend);
        }
    }
}

