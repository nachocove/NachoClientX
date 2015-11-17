//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
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

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        readonly OkHttpClient client = new OkHttpClient ();

        public readonly bool AllowAutoRedirect = false;

        public readonly bool PreAuthenticate = true;

        private NcHttpClient ()
        {
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
            var callbacks = new NcOkHttpCallback (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (false, request, timeout, callbacks, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var callbacks = new NcOkHttpCallback (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (true, request, timeout, callbacks, cancellationToken);
        }

        #endregion

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NcOkHttpCallback callbacks, CancellationToken cancellationToken)
        {
            client.SetHostnameVerifier (new HostnameVerifier (this));

            client.SetConnectTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);
            client.SetWriteTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);
            client.SetReadTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);
            client.FollowRedirects = AllowAutoRedirect;
            client.SetFollowSslRedirects (AllowAutoRedirect);

            var builder = new Request.Builder ()
                .Url (new Java.Net.URL (request.RequestUri.ToString ()));
            
            RequestBody body;
            if (request.Content != null) {
                if (isSend) {
                    // 100-continue doesn't appear to work in OkHttp
                    // https://github.com/square/okhttp/issues/675
                    // https://github.com/square/okhttp/issues/1337
                    builder.AddHeader ("Expect", "100-continue");
                }
                if (request.Content is FileStream) {
                    var fileStream = request.Content as FileStream;
                    Java.IO.File file = new Java.IO.File (fileStream.Name);
                    body = RequestBody.Create (MediaType.Parse (request.ContentType), file);
                } else if (request.Content is MemoryStream) {
                    var memStream = request.Content as MemoryStream;
                    byte[] b = memStream.GetBuffer ().Take ((int)memStream.Length).ToArray ();
                    body = RequestBody.Create (MediaType.Parse (request.ContentType), b);
                } else {
                    NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                    return;
                }
            } else {
                builder.Header ("Content-Type", "text/plain");
                body = default(RequestBody);
            }
            if (null != callbacks.ProgressAction) {
                body = new NcOkHttpProgressRequestBody (body, callbacks.ProgressAction);
            }

            builder.Method (request.Method.ToString ().ToUpperInvariant (), body);
            
            foreach (var kvp in request.Headers) {
                builder.AddHeader (kvp.Key, String.Join (",", kvp.Value));
            }

            if (null != request.Cred) {
                var basicAuth = OkHttp.Credentials.Basic (request.Cred.Username, request.Cred.GetPassword ());
                client.SetAuthenticator (new NcOkNativeAuthenticator (basicAuth));
                if (PreAuthenticate) {
                    builder.Header ("Authorization", basicAuth);
                }
            }

            var rq = builder.Build ();

            var call = client.NewCall (rq);
            cancellationToken.Register (() => {
                call.Cancel ();
            });
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

            public NcOkHttpCallback (NcHttpClient owner, CancellationToken cancellationToken, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                sw.Start ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            void LogCompletion (long sent, long received)
            {
                sw.Stop ();
                Log.Info (Log.LOG_HTTP, "NcHttpClient: Finished request {0}ms (bytes sent:{1} received:{2})", sw.ElapsedMilliseconds, sent.ToString ("n0"), received.ToString ("n0"));
            }

            #region ICallback implementation

            public void OnFailure (Request p0, Java.IO.IOException p1)
            {
                if (Token.IsCancellationRequested) {
                    return;
                }
                var sent = p0.Body ().ContentLength ();

                LogCompletion (sent, -1);
                if (ErrorAction != null) {
                    ErrorAction (createExceptionForJavaIOException (p1));
                }
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
                    var fileStream = new FileStream (filename, FileMode.Open);
                    var buffer = new byte[4 * 1024];
                    long received = 0;
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
                                ProgressAction (false, n, received, -1);
                            }
                        }
                    } while (n > 0);
                    fileStream.Flush ();
                    fileStream.Close ();

                    long sent = p0.Request ().Body ().ContentLength ();
                    LogCompletion (sent, received);

                    // reopen as read-only
                    fileStream = new FileStream (filename, FileMode.Open, FileAccess.Read);
                    try {
                        if (SuccessAction != null) {
                            var response = new NcHttpResponse ((HttpStatusCode)p0.Code (), fileStream, p0.Body ().ContentType ().ToString (), FromOkHttpHeaders (p0.Headers ()));
                            SuccessAction (response, Token);
                        }
                    } finally {
                        fileStream.Dispose ();

                    }
                } finally {
                    File.Delete (filename);
                }
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
            return new IOException (error.Message);
        }

        class NcOkNativeAuthenticator : Java.Lang.Object, IAuthenticator
        {
            private string CredString;

            public NcOkNativeAuthenticator (string credString)
            {
                CredString = credString;
            }

            public Request Authenticate (Java.Net.Proxy proxy, Response response)
            {
                Log.Warn (Log.LOG_HTTP, "NcHttpClient: Doing auth, so pre-auth didn't work!");
                return response.Request ().NewBuilder ().Header ("Authorization", CredString).Build ();
            }

            public Request AuthenticateProxy (Java.Net.Proxy proxy, Response response)
            {
                return null;
            }
        }

        class HostnameVerifier : Java.Lang.Object, IHostnameVerifier
        {
            NcHttpClient Owner { get; set; }

            public HostnameVerifier (NcHttpClient owner)
            {
                Owner = owner;
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
                    Log.Warn (Log.LOG_HTTP, "NcHttpClient: No ServerCertificateValidationCallback!");
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

            public NcOkHttpProgressRequestBody (RequestBody body, ProgressDelegate progress)
            {
                ProgressAction = progress;
                Body = body;
            }

            public override long ContentLength ()
            {
                return Body.ContentLength ();
            }

            public override MediaType ContentType ()
            {
                return Body.ContentType ();
            }

            public override void WriteTo (OkHttp.Okio.IBufferedSink p0)
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
                    Owner.ProgressAction (true, bytesWritten, Owner.ContentLength (), -1);
                }
            }
        }
    }
}

