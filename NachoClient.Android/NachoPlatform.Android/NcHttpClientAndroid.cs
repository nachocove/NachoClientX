//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

// Portions of this software are from ModernHttpClient:
//
//Copyright (c) 2013 Paul Betts
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of
//this software and associated documentation files (the "Software"), to deal in
//the Software without restriction, including without limitation the rights to
//use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
//the Software, and to permit persons to whom the Software is furnished to do so,
//subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
//FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
//COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
//IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
//CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Threading;
using NachoCore.Utils;
using OkHttp;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using Javax.Net.Ssl;
using System.Security.Cryptography.X509Certificates;
using OkHttp.Okio;
using System.Net.Http;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        readonly OkHttpClient _client;

        public readonly bool AllowAutoRedirect = false;

        public readonly bool PreAuthenticate = true;

        long defaultTimeoutSecs = 30;

        int defaultAuthRetries = 0;

        private NcHttpClient ()
        {
            _client = new OkHttpClient ();
            _client.SetHostnameVerifier (new HostnameVerifier ());

            _client.SetConnectTimeout (defaultTimeoutSecs, Java.Util.Concurrent.TimeUnit.Seconds);
            _client.SetWriteTimeout (defaultTimeoutSecs, Java.Util.Concurrent.TimeUnit.Seconds);
            _client.SetReadTimeout (defaultTimeoutSecs, Java.Util.Concurrent.TimeUnit.Seconds);
            _client.FollowRedirects = AllowAutoRedirect;
            _client.SetFollowSslRedirects (AllowAutoRedirect);
            _client.SetConnectionPool (ConnectionPool.Default); // see http://square.github.io/okhttp/2.x/okhttp/com/squareup/okhttp/ConnectionPool.html
        }

        private static object LockObj = new object ();

        static NcHttpClient _Instance { get; set; }

        public static NcHttpClient Instance {
            get {
                if (_Instance == null) {
                    lock (LockObj) {
                        if (_Instance == null) {
                            _Instance = new NcHttpClient ();
                        }
                    }
                }
                return _Instance;
            }
        }

        #region INcHttpClient implementation

        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var callbacks = new NcOkHttpCallback (this, request, cancellationToken, success, error, progress);
            SetupAndRunRequest (false, request, timeout, callbacks, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var callbacks = new NcOkHttpCallback (this, request, cancellationToken, success, error, progress);
            SetupAndRunRequest (true, request, timeout, callbacks, cancellationToken);
        }

        #endregion

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NcOkHttpCallback callbacks, CancellationToken cancellationToken)
        {
            OkHttpClient cloned = _client.Clone (); // Clone to make a customized OkHttp for this request.
            cloned.SetConnectTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Seconds);
            cloned.SetWriteTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Seconds);
            cloned.SetReadTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Seconds);

            var builder = new Request.Builder ()
                .Url (new Java.Net.URL (request.RequestUri.AbsoluteUri))
                .Tag (request.guid);

            RequestBody body = null;
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put) {
                if (request.Content != null) {
                    if (isSend) {
                        // 100-continue doesn't appear to work in OkHttp
                        // https://github.com/square/okhttp/issues/675
                        // https://github.com/square/okhttp/issues/1337
                        //builder.AddHeader ("Expect", "100-continue");
                    }
                    if (request.Content is FileStream) {
                        var fileStream = request.Content as FileStream;
                        Java.IO.File file = new Java.IO.File (fileStream.Name);
                        Log.Info (Log.LOG_HTTP, "NcHttpClient({0}): using file {1}", request.guid, fileStream.Name);
                        body = RequestBody.Create (MediaType.Parse (request.ContentType), file);
                    } else if (request.Content is byte[]) {
                        body = RequestBody.Create (MediaType.Parse (request.ContentType), request.Content as byte[]);
                    } else {
                        NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                        return;
                    }
                } else {
                    if (string.IsNullOrEmpty (request.ContentType)) {
                        builder.Header ("Content-Type", "text/plain");
                    }
                    body = default(RequestBody);
                }
                if (null != callbacks.ProgressAction) {
                    body = new NcOkHttpProgressRequestBody (body, callbacks.ProgressAction, request.guid);
                }
            }

            builder.Method (request.Method.ToString ().ToUpperInvariant (), body);

            foreach (var kvp in request.Headers) {
                builder.AddHeader (kvp.Key, String.Join (",", kvp.Value));
            }

            if (null != request.Cred) {
                if (!request.RequestUri.IsHttps ()) {
                    Log.Error (Log.LOG_HTTP, "Thou shalt not send credentials over http\n{0}", new System.Diagnostics.StackTrace ().ToString ());
                }
                var basicAuth = Credentials.Basic (request.Cred.Username, request.Cred.GetPassword ());
                cloned.SetAuthenticator (new NcOkNativeAuthenticator (basicAuth, defaultAuthRetries));
                if (PreAuthenticate) {
                    builder.Header ("Authorization", basicAuth);
                }
            }

            var rq = builder.Build ();
            var call = cloned.NewCall (rq);
            cancellationToken.Register (() => {
                if (!call.IsCanceled) {
                    if (Android.OS.Looper.MyLooper () == Android.OS.Looper.MainLooper) {
                        // Must not run on main thread
                        NcTask.Run (() => {
                            try {
                                call.Cancel ();
                            } catch (Exception ex) {
                                Log.Warn (Log.LOG_HTTP, "Could not cancel call: {0}", ex.Message);
                            }
                        }, "NcHttpClientAndroid.Cancel");
                    } else {
                        try {
                            call.Cancel ();
                        } catch (Exception ex) {
                            Log.Warn (Log.LOG_HTTP, "Could not cancel call: {0}", ex.Message);
                        }
                    }
                }
            });
            Log.Info (Log.LOG_HTTP, "NcHttpClient({0}): Enqueue task", request.guid);
            call.Enqueue (callbacks);
        }

        public class NcOkHttpCallback : Java.Lang.Object, ICallback
        {
            protected SuccessDelegate SuccessAction;

            protected ErrorDelegate ErrorAction { get; set; }

            public ProgressDelegate ProgressAction { get; protected set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            NcHttpRequest OriginalRequest;

            public NcOkHttpCallback (NcHttpClient owner, NcHttpRequest request, CancellationToken cancellationToken, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                sw.Start ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
                OriginalRequest = request;
            }

            void LogCompletion (long sent, long received)
            {
                sw.Stop ();
                Log.Info (Log.LOG_HTTP, "NcHttpClient({0}): Finished request {1}ms (bytes sent:{2} received:{3})", OriginalRequest.guid, sw.ElapsedMilliseconds, sent.ToString ("n0"), received.ToString ("n0"));
            }

            #region ICallback implementation

            public void OnFailure (Request p0, Java.IO.IOException p1)
            {
                if (Token.IsCancellationRequested) {
                    return;
                }
                LogCompletion (SentBytesFromRequestBody (p0), -1);

                if (ErrorAction != null) {
                    ErrorAction (createExceptionForJavaIOException (p1), Token);
                }
                OriginalRequest.Dispose ();
            }

            public void OnResponse (Response p0)
            {
                if (Token.IsCancellationRequested) {
                    return;
                }
                var source = p0.Body ().Source ();
                var filename = Path.GetTempFileName ();

                // Copy the stream from the network to a file.
                try {
                    long received = 0;
                    using (var fileStream = new FileStream (filename, FileMode.Open, FileAccess.Write)) {
                        var buffer = new byte[4 * 1024];
                        int n;
                        do {
                            if (Token.IsCancellationRequested) {
                                return;
                            }
                            n = source.Read (buffer);
                            if (n > 0) {
                                // Read could take a bit. Check again
                                if (Token.IsCancellationRequested) {
                                    return;
                                }
                                received += n;
                                fileStream.Write (buffer, 0, n);

                                if (ProgressAction != null) {
                                    string description = p0.Request ().Tag ().ToString ();
                                    if (string.IsNullOrEmpty (description)) {
                                        description = "<unknown>";
                                    }
                                    ProgressAction (false, description, n, received, -1);
                                }
                            }
                        } while (n > 0);
                    }

                    LogCompletion (SentBytesFromRequestBody (p0.Request ()), received);

                    // reopen as read-only
                    using (var fileStream = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
                        if (SuccessAction != null) {
                            using (var response = new NcHttpResponse (p0.Request ().Method (), (HttpStatusCode)p0.Code (), fileStream, ContentTypeFromResponseBody (p0), FromOkHttpHeaders (p0.Headers ()))) {
                                SuccessAction (response, Token);
                            }
                        }
                    }
                } catch (Exception ex) {
                    Log.Info (Log.LOG_HTTP, "NcHttpClient({0}): Error Processing response: {0}", OriginalRequest.guid, ex);
                    ErrorAction (ex, Token);
                } finally {
                    File.Delete (filename);
                    OriginalRequest.Dispose ();
                }
            }

            long SentBytesFromRequestBody (Request req)
            {
                long sent = -1;
                if (null != req) {
                    var body = req.Body ();
                    if (null != body) {
                        sent = body.ContentLength ();
                    }
                }
                return sent;
            }

            string ContentTypeFromResponseBody (Response resp)
            {
                string contentType = "";
                if (null != resp) {
                    var body = resp.Body ();
                    if (null != body) {
                        var cType = body.ContentType ();
                        if (cType != null) {
                            contentType = cType.ToString ();
                        }
                    }
                }
                return contentType;
            }


            #endregion
        }

        public static NcHttpHeaders FromOkHttpHeaders (Headers headers)
        {
            var ret = new NcHttpHeaders ();
            foreach (var n in headers.Names()) {
                ret.Add (n, headers.Get (n));
            }
            return ret;
        }

        public static Exception createExceptionForJavaIOException (Java.IO.IOException error)
        {
            return new WebException (error.Message);
        }

        class NcOkNativeAuthenticator : Java.Lang.Object, IAuthenticator
        {
            private string CredString;

            int retries;

            public NcOkNativeAuthenticator (string credString, int maxRetries)
            {
                retries = maxRetries;
                CredString = credString;
            }

            public Request Authenticate (Java.Net.Proxy proxy, Response response)
            {
                Request newRequest = null;
                if (retries-- < 1) {
                    Log.Info (Log.LOG_HTTP, "NcOkNativeAuthenticator: Max-retries exceeded");
                } else {
                    Log.Info (Log.LOG_HTTP, "NcOkNativeAuthenticator: retries left {0}", retries);
                    newRequest = response.Request ().NewBuilder ().Header ("Authorization", CredString).Build ();
                }
                return newRequest;
            }

            public Request AuthenticateProxy (Java.Net.Proxy proxy, Response response)
            {
                return null;
            }
        }

        class HostnameVerifier : Java.Lang.Object, IHostnameVerifier
        {
            public HostnameVerifier ()
            {
            }

            public bool Verify (string hostname, ISSLSession session)
            {
                var uriBuilder = new UriBuilder (null == session ? "http" : "https", hostname);
                return verifyServerCertificate (uriBuilder.Uri, session) & NcHttpCertificateValidation.verifyClientCiphers (uriBuilder.Uri, session.Protocol, session.CipherSuite);
            }

            /// <summary>
            /// Verifies the server certificate by calling into ServicePointManager.ServerCertificateValidationCallback or,
            /// if the is no delegate attached to it by using the default hostname verifier.
            /// </summary>
            /// <returns><c>true</c>, if server certificate was verifyed, <c>false</c> otherwise.</returns>
            /// <param name="uri"></param>
            /// <param name="session"></param>
            static bool verifyServerCertificate (Uri uri, ISSLSession session)
            {
                var defaultVerifier = HttpsURLConnection.DefaultHostnameVerifier;

                if (ServicePointManager.ServerCertificateValidationCallback == null) {
                    return defaultVerifier.Verify (uri.Host, session);
                }

                // Convert java certificates to .NET certificates and build cert chain from root certificate
                var certificates = session.GetPeerCertificateChain ();
                var chain = new X509Chain ();
                X509Certificate2 root = null;
                var errors = System.Net.Security.SslPolicyErrors.None;

                // Build certificate chain and check for errors
                if (certificates == null || certificates.Length == 0) {//no cert at all
                    errors = System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable;
                    goto sslErrorVerify;
                } 

                if (certificates.Length == 1) {//no root?
                    errors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
                    goto sslErrorVerify;
                } 

                var netCerts = certificates.Select (x => new X509Certificate2 (x.GetEncoded ())).ToArray ();

                for (int i = 1; i < netCerts.Length; i++) {
                    chain.ChainPolicy.ExtraStore.Add (netCerts [i]);
                }

                root = netCerts [0];


                sslErrorVerify:
                // Call the delegate to validate
                return NcHttpCertificateValidation.CertValidation (uri, root, chain, errors);
            }
        }

        //https://gist.github.com/lnikkila/d1a4446b93a0185b0969
        public class NcOkHttpProgressRequestBody : RequestBody
        {
            public ProgressDelegate ProgressAction { get; protected set; }

            public RequestBody Body { get; protected set; }

            NcOkHttpCountingSink countingSink { get; set; }

            public string Description { get; set; }

            public NcOkHttpProgressRequestBody (RequestBody body, ProgressDelegate progress, string description)
            {
                ProgressAction = progress;
                Body = body;
                Description = description;
            }

            public override long ContentLength ()
            {
                return Body.ContentLength ();
            }

            public override MediaType ContentType ()
            {
                return Body.ContentType ();
            }

            public override void WriteTo (IBufferedSink p0)
            {
                IBufferedSink bufferedSink;

                countingSink = new NcOkHttpCountingSink (p0, this);
                bufferedSink = Okio.Buffer (countingSink);

                Body.WriteTo (bufferedSink);

                bufferedSink.Flush ();
            }

            public class NcOkHttpCountingSink : ForwardingSink
            {
                long bytesWritten;

                NcOkHttpProgressRequestBody Owner;

                public NcOkHttpCountingSink (ISink sink, NcOkHttpProgressRequestBody owner) : base (sink)
                {
                    Owner = owner;
                }

                public override void Write (OkBuffer p0, long p1)
                {
                    base.Write (p0, p1);
                    bytesWritten += p1;
                    Owner.ProgressAction (true, Owner.Description, bytesWritten, Owner.ContentLength (), -1);
                }
            }
        }
    }
}

