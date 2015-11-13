//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Threading;
using NachoCore.Utils;
using OkHttp;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        readonly OkHttpClient client = new OkHttpClient();

        McCred Cred { get; set; }

        public bool AllowAutoRedirect { get; set; }

        public NcHttpClient (McCred cred)
        {
            Cred = cred;
            AllowAutoRedirect = true;
        }

        #region INcHttpClient implementation

        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var callbacks = new NcOkHttpCallback (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (false, request, timeout, callbacks, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var callbacks = new NcOkHttpCallback (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (true, request, timeout, callbacks, cancellationToken);
        }

        #endregion

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NcOkHttpCallback callbacks, CancellationToken cancellationToken)
        {
            var url = new Java.Net.URL(request.Url);

            RequestBody body;

            if (request.Content != null) {
                if (isSend) {
                    request.AddHeader ("Expect", "100-continue");

                    // TODO For ActiveSync this works fine, because it assumes BASIC auth.
                    // For anything else, this would need to be adapted.
                    //request.SetBasicAuthHeader (Cred);
                }
                if (!request.ContainsHeader ("Content-Type")) {
                    request.AddHeader ("Content-Type", request.ContentType);
                }
                if (request.Content is FileStream) {
                    var fileStream = request.Content as FileStream;
                    Java.IO.File file = new Java.IO.File (fileStream.Name);
                    body = RequestBody.Create (MediaType.Parse (request.ContentType), file);
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", fileStream.Length.ToString ());
                    }
                } else if (request.Content is MemoryStream) {
                    var memStream = request.Content as MemoryStream;
                    byte[] b = memStream.GetBuffer ().Take ((int)memStream.Length).ToArray ();
                    body = RequestBody.Create (MediaType.Parse (request.ContentType), b);
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", memStream.Length.ToString ());
                    }
                } else {
                    NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                    return;
                }
            } else {
                if (!request.ContainsHeader ("Content-Type")) {
                    request.AddHeader ("Content-Type", "text/plain");
                }
                body = default(RequestBody);
            }

            var builder = new Request.Builder ()
                .Method (request.Method.ToString ().ToUpperInvariant (), body)
                .Url (url);

            foreach (var kvp in request.Headers) {
                builder.AddHeader (kvp.Key, String.Join (",", kvp.Value));
            }
            var rq = builder.Build();
            var call = client.NewCall(rq);
            cancellationToken.Register (() => {
                NcTask.Run (() => {
                    call.Cancel ();
                }, "NcHttpClientCancel");
            });
            call.Enqueue (callbacks);
        }

        public class NcOkHttpCallback : Java.Lang.Object, ICallback
        {
            protected SuccessDelete SuccessAction;

            protected ErrorDelegate ErrorAction { get; set; }

            protected ProgressDelegate ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            public NcOkHttpCallback (NcHttpClient owner, CancellationToken cancellationToken, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            #region ICallback implementation
            public void OnFailure (Request p0, Java.IO.IOException p1)
            {
                Token.ThrowIfCancellationRequested();
                if (ErrorAction != null) {
                    ErrorAction (createExceptionForJavaIOException(p1));
                }
            }

            public void OnResponse (Response p0)
            {
                Token.ThrowIfCancellationRequested();
                try {
                    var newReq = p0.Request();
                    var newUri = newReq == null ? null : newReq.Uri();
                } catch (Java.Net.UnknownHostException ex) {
                    throw new WebException (ex.ToString (), WebExceptionStatus.NameResolutionFailure);
                } catch (IOException ex) {
                    if (ex.Message.ToLowerInvariant ().Contains ("canceled")) {
                        throw new OperationCanceledException ();
                    }
                    throw;
                }
                var respBody = p0.Body();
                if (SuccessAction != null) {
                    SuccessAction ((HttpStatusCode)p0.Code (), respBody.ByteStream (), FromOkHttpHeaders(p0.Headers ()), Token);
                }
            }
            #endregion
        }

        public static Dictionary<string, List<string>> FromOkHttpHeaders (Headers headers)
        {
            var ret = new Dictionary<string, List<string>> ();
            foreach (var n in headers.Names()) {
                if (!ret.ContainsKey (n)) {
                    ret [n] = new List<string> ();
                }
                ret [n].Add(headers.Get (n));
            }
            return ret;
        }

        public static Exception createExceptionForJavaIOException (Java.IO.IOException error)
        {
            return new IOException (error.Message);
        }
    }
}

