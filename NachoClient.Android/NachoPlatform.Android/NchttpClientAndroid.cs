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
using System.Net;
using Javax.Net.Ssl;
using System.Security.Cryptography.X509Certificates;
using OkHttp.Okio;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        readonly OkHttpClient client = new OkHttpClient ();

        Request OriginalRequest { get; set; }

        McCred Cred { get; set; }

        public bool AllowAutoRedirect { get; set; }

        public NcHttpClient (McCred cred)
        {
            Cred = cred;
            AllowAutoRedirect = true;
        }

        string BasicAuthorizationString ()
        {
            NcAssert.NotNull (Cred);
            return OkHttp.Credentials.Basic (Cred.Username, Cred.GetPassword ());
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
            var url = new Java.Net.URL (request.Url);

            client.SetHostnameVerifier (new HostnameVerifier (this));

            client.SetConnectTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);
            client.SetWriteTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);
            client.SetReadTimeout ((long)timeout, Java.Util.Concurrent.TimeUnit.Milliseconds);

            RequestBody body;

            if (request.Content != null) {
                if (isSend) {
                    // 100-continue doesn't appear to work in OkHttp
                    // https://github.com/square/okhttp/issues/675
                    // https://github.com/square/okhttp/issues/1337
                    //request.AddHeader ("Expect", "100-continue");

                    // TODO For ActiveSync this works fine, because it assumes BASIC auth.
                    // For anything else, this would need to be adapted.
                    request.SetBasicAuthHeader (Cred);
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


            if (null != callbacks.ProgressAction) {
                body = new NcOkHttpProgressRequestBody (body, callbacks.ProgressAction);
            }

            var builder = new Request.Builder ()
                .Method (request.Method.ToString ().ToUpperInvariant (), body)
                .Url (url);
            
            if (null != Cred) {
                var basicAuth = BasicAuthorizationString ();
                builder = builder.AddHeader ("Authorization", basicAuth);
                client.SetAuthenticator (new NcOkNativeAuthenticator (basicAuth));
            }

            foreach (var kvp in request.Headers) {
                builder.AddHeader (kvp.Key, String.Join (",", kvp.Value));
            }
            var rq = builder.Build ();
            OriginalRequest = rq;
            var call = client.NewCall (rq);
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

            public ProgressDelegate ProgressAction { get; protected set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            public NcOkHttpCallback (NcHttpClient owner, CancellationToken cancellationToken, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                sw.Start ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            void LogCompletion (Request request, Response response)
            {
                sw.Stop ();
                var sent = request.Body ().ContentLength ();
                var received = response.Body ().ByteStream ().Length;
                Log.Info (Log.LOG_HTTP, "NcHttpClient: Finished request {0}ms (sent:{1} received:{2})", sw.ElapsedMilliseconds, sent.ToString ("n"), received.ToString ("n"));
            }

            #region ICallback implementation

            public void OnFailure (Request p0, Java.IO.IOException p1)
            {
                LogCompletion (p0, null);
                Token.ThrowIfCancellationRequested ();
                if (ErrorAction != null) {
                    ErrorAction (createExceptionForJavaIOException (p1));
                }
            }

            public void OnResponse (Response p0)
            {
                LogCompletion (p0.Request (), p0);
                Token.ThrowIfCancellationRequested ();
                var respBody = p0.Body ();
                if (SuccessAction != null) {
                    SuccessAction ((HttpStatusCode)p0.Code (), respBody.ByteStream (), FromOkHttpHeaders (p0.Headers ()), Token);
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
                ret [n].Add (headers.Get (n));
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
                Uri uri = new Uri (Owner.OriginalRequest.Uri ().ToString ());
                return verifyServerCertificate (uri, session) & NcHttpCertificateValidation.verifyClientCiphers (uri, session.Protocol, session.CipherSuite);
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

                if (ServicePointManager.ServerCertificateValidationCallback == null)
                    return defaultVerifier.Verify (uri.Host, session);

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

                public NcOkHttpCountingSink (ISink dele, NcOkHttpProgressRequestBody owner) : base (dele)
                {
                    Owner = owner;
                }

                public override void Write (OkBuffer p0, long p1)
                {
                    base.Write (p0, p1);
                    bytesWritten += p1;
                    Owner.ProgressAction (bytesWritten, Owner.ContentLength (), -1);
                }
            }
        }
    }
}

