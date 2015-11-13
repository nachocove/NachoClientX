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

namespace NachoPlatform
{
    #region INcHttpClient

    public enum NcHttpClientState {
        Unknown = 0,
        Running,
        Suspended,
        Canceling,
        Completed
    }

    public class NcHttpRequest
    {
        public Dictionary<string, List<string>> Headers { get; protected set; }

        public Stream Content { get; protected set; }

        public long? ContentLength { get; protected set; }

        public string ContentType { get; protected set; }

        public string Url { get; protected set; }

        public HttpMethod Method { get; protected set; }

        public NcHttpRequest(HttpMethod method, string url)
        {
            Url = url;
            Method = method;
            Headers = new Dictionary<string, List<string>> ();
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
            if (!Headers.ContainsKey (key)) {
                Headers [key] = new List<string> ();
            }
            Headers [key].Add (value);
        }

        public bool ContainsHeader (string key)
        {
            return Headers.ContainsKey (key);
        }

        public void SetBasicAuthHeader (McCred cred)
        {
            AddHeader ("Authorization", string.Format ("{0} {1}", "Basic", Convert.ToBase64String (Encoding.ASCII.GetBytes (string.Format ("{0}:{1}", cred.Username, cred.GetPassword ())))));
        }
    }

    public delegate void ProgressDelegate (long bytes, long totalBytes, long totalBytesExpected);
    public delegate void SuccessDelete (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token);
    public delegate void ErrorDelegate (Exception exception);

    public interface INcHttpClient
    {
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken);
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
        void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken);
        void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
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

            var request = new NcHttpRequest (HttpMethod.Post, url1);
            request.AddHeader ("User-Agent", Device.Instance.UserAgent ());
            request.AddHeader ("X-MS-PolicyKey", "1495342484");
            request.AddHeader ("MS-ASProtocolVersion", "14.1");
            request.SetContent (fileStream, "application/vnd.ms-sync.wbxml");

            Log.Info (Log.LOG_HTTP, "Starting Upload with {0}:{1}", request.Method.ToString (), request.Url);
            var client = new NcHttpClient (Cred);
            client.SendRequest (request, 1000000, UploadSuccess, Error, Progress, Cts.Token);
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

            Log.Info (Log.LOG_HTTP, "Starting Download with {0}:{1}", request.Method.ToString (), request.Url);
            var client = new NcHttpClient (Cred);
            client.GetRequest (request, 1000000, DownloadSuccess, Error, Progress, Cts.Token);
        }

        public void DownloadSuccess (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token)
        {
            FileStream fs = stream as FileStream;
            NcAssert.NotNull (fs);
            Log.Info (Log.LOG_HTTP, "Response {0} length {1}", status, fs.Length);
            foreach (var header in headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            using (var data = new FileStream (Location, FileMode.OpenOrCreate)) {
                fs.CopyTo (data);
                Log.Info (Log.LOG_HTTP, "Response written ({0}) to file {1}", data.Length, Location);
            }
        }

        public void UploadSuccess (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token)
        {
            Log.Info (Log.LOG_HTTP, "Response {0}", status);
            foreach (var header in headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            if (stream != null) {
                var mem = new MemoryStream ();
                stream.CopyTo (mem);
                Log.Info (Log.LOG_HTTP, "Response Data({0}):\n{1}", mem.Length, Convert.ToBase64String (mem.GetBuffer ().Take ((int)mem.Length).ToArray ()));
            }
            Console.WriteLine ("HTTP: Finished Upload");
        }

        public void Error (Exception ex)
        {
            Console.WriteLine ("HTTP: Request failed: {0}", ex.Message);
        }

        public void Progress (long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
        {
            Console.WriteLine ("HTTP: Progress: {0}:{1}:{2}", bytesSent, totalBytesSent, totalBytesExpectedToSend);
        }
    }}

