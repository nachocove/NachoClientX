//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;
using Foundation;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Http;
using NachoCore.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using NachoCore.Model;
using System.Security.Cryptography;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        NetworkCredential Credentials { get; set; }

        NSMutableUrlRequest OriginalRequest { get; set; }

        public NcHttpClient (McCred cred)
        {
            Credentials = new NetworkCredential (cred.Username, cred.GetPassword ());
        }

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NSUrlSessionDelegate dele, CancellationToken cancellationToken)
        {
            // Mostly lifted from ModernHttpClientiOS NativeMessageHandler
            string uploadFilename = null;
            NSData RequestBody = null;
            if (request.Content != null) {
                if (isSend) {
                    request.AddHeader ("Expect", "100-continue");
                    // TODO: Should also set up the authorization header here, so we don't wind up uploading
                    // the data more than once on servers that don't support '100-continue' (i.e. upload once,
                    // get a 401, add the header, and upload again)
                }
                if (!string.IsNullOrEmpty (request.ContentType)) {
                    if (!request.ContainsHeader ("Content-Type")) {
                        request.AddHeader ("Content-Type", request.ContentType);
                    }
                    if (!request.ContainsHeader ("Content-Length") && request.ContentLength.HasValue) {
                        request.AddHeader ("Content-Length", request.ContentLength.Value.ToString ());
                    }
                }
                if (request.Content is FileStream) {
                    // FIX: RequestBodyStream doesn't yet work. Need to figure out how to use NeedNewBodyStream
                    var fileStream = request.Content as FileStream;
                    uploadFilename = fileStream.Name;
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", fileStream.Length.ToString ());
                    }
                } else if (request.Content is MemoryStream) {
                    var memStream = request.Content as MemoryStream;
                    // FIX: RequestBodyStream doesn't yet work. Need to figure out how to use NeedNewBodyStream
                    //RequestBodyStream = NSInputStream.FromData (NSData.FromArray (memStream.GetBuffer ().Take ((int)memStream.Length).ToArray ()));
                    RequestBody = NSData.FromStream (memStream);
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", memStream.Length.ToString ());
                    }
                } else {
                    NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                }
            }

            var nsHeaders = new NSMutableDictionary ();
            foreach (var x in request.Headers) {
                nsHeaders.Add (new NSString (x.Key), new NSString (String.Join (",", x.Value)));
            }

            OriginalRequest = new NSMutableUrlRequest () {
                AllowsCellularAccess = true,
                //BodyStream = RequestBodyStream,
                Body = RequestBody,
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = nsHeaders,
                HttpMethod = request.Method.ToString ().ToUpperInvariant (),
                Url = NSUrl.FromString (request.Url),
                TimeoutInterval = timeout,
            };
            Log.Info (Log.LOG_HTTP, "Request Http URL: {0}\n{1}\n", OriginalRequest.Url, OriginalRequest.Headers.ToString ());

            var config = NSUrlSessionConfiguration.DefaultSessionConfiguration;
            config.TimeoutIntervalForRequest = timeout;
            config.TimeoutIntervalForResource = timeout;
            config.URLCache = new NSUrlCache (0, 0, "HttpClientCache");

            var session = NSUrlSession.FromConfiguration (config, dele, new NSOperationQueue ());
            if (isSend) {
                if (!string.IsNullOrEmpty (uploadFilename)) {
                    task = session.CreateUploadTask (OriginalRequest, NSUrl.FromFilename (uploadFilename));
                } else {
                    task = session.CreateUploadTask (OriginalRequest);
                }
            } else {
                if (null != RequestBody) {
                    Log.Warn (Log.LOG_HTTP, "Download task without an uploaded body. This is likely OK.");
                }
                task = session.CreateDownloadTask (OriginalRequest);
            }
            cancellationToken.Register (() => {
                task.Cancel ();
            });
            task.Resume ();
        }

        #region INcHttpClient implementation

        NSUrlSessionTask task { get; set; }

        public void GetRequest (NcHttpRequest request, int timeout,
                                Action<HttpStatusCode, Stream, Dictionary<string, List<string>>, CancellationToken> success,
                                Action<Exception> error,
                                CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void GetRequest (NcHttpRequest request, int timeout,
                                Action<HttpStatusCode, Stream, Dictionary<string, List<string>>, CancellationToken> success,
                                Action<Exception> error,
                                Action<long, long, long> progress,
                                CancellationToken cancellationToken)
        {
            var dele = new NcDownloadTaskDelegate (this, cancellationToken, success, error, progress);
            dele.sw.Start ();
            Log.Info (Log.LOG_HTTP, "GetRequest: Started stopwatch");
            SetupAndRunRequest (false, request, timeout, dele, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, Action<HttpStatusCode, Dictionary<string, List<string>>, CancellationToken> success, Action<Exception> error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, Action<HttpStatusCode, Dictionary<string, List<string>>, CancellationToken> success, Action<Exception> error, Action<long, long, long> progress, CancellationToken cancellationToken)
        {
            var dele = new NcUploadTaskDelegate (this, cancellationToken, success, error, progress);
            dele.sw.Start ();
            Log.Info (Log.LOG_HTTP, "SendRequest: Started stopwatch");
            SetupAndRunRequest (true, request, timeout, dele, cancellationToken);
        }

        #endregion

        #region NcUploadTaskDelegate

        class NcUploadTaskDelegate : NSUrlSessionDataDelegate
        {
            protected Action<HttpStatusCode, Dictionary<string, List<string>>, CancellationToken> SuccessAction { get; set; }

            protected Action<Exception> ErrorAction { get; set; }

            protected Action<long, long, long> ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            public NcUploadTaskDelegate (NcHttpClient owner, CancellationToken cancellationToken, Action<HttpStatusCode, Dictionary<string, List<string>>, CancellationToken> success, Action<Exception> error, Action<long, long, long> progress = null)
            {
                sw = new PlatformStopwatch ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            public override void DidReceiveResponse (NSUrlSession session, NSUrlSessionDataTask dataTask, NSUrlResponse response, Action<NSUrlSessionResponseDisposition> completionHandler)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != SuccessAction) {
                    NcAssert.True (dataTask.Response is NSHttpUrlResponse);
                    var resp = dataTask.Response as NSHttpUrlResponse;
                    NcAssert.NotNull (resp);

                    int status = (int)resp.StatusCode;
                    var headers = FromNsHeaders (resp.AllHeaderFields);
                    SuccessAction ((HttpStatusCode)status, headers, Token);
                }
                //public void DownloadSuccess (HttpStatusCode status , Stream stream, Dictionary<string, List<string>> headers, CancellationToken token)

                completionHandler (NSUrlSessionResponseDisposition.Allow);
            }

            public override void DidSendBodyData (NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != ProgressAction) {
                    ProgressAction (bytesSent, totalBytesSent, totalBytesExpectedToSend);
                }
            }

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
                sw.Stop ();
                Log.Info (Log.LOG_HTTP, "Uploaded {0}kB in {1}ms", ((double)task.BytesSent / (double)1024).ToString ("n2"), sw.ElapsedMilliseconds);
                Token.ThrowIfCancellationRequested ();
                if (null != error) {
                    if (null != ErrorAction) {
                        ErrorAction (createExceptionForNSError (error));
                    }
                }
            }

            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                Log.Info (Log.LOG_HTTP, "DidReceiveChallenge: {0}", challenge.ProtectionSpace.AuthenticationMethod);
                BaseDidReceiveChallenge (Owner.Credentials, session, task, challenge, completionHandler);
            }
        }

        #endregion

        #region NcDownloadTaskDelegate

        class NcDownloadTaskDelegate : NSUrlSessionDownloadDelegate
        {
            protected Action<HttpStatusCode, Stream, Dictionary<string, List<string>>, CancellationToken> SuccessAction;

            protected Action<Exception> ErrorAction { get; set; }

            protected Action<long, long, long> ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            public NcDownloadTaskDelegate (NcHttpClient owner, CancellationToken cancellationToken, Action<HttpStatusCode, Stream, Dictionary<string, List<string>>, CancellationToken> success, Action<Exception> error, Action<long, long, long> progress = null)
            {
                sw = new PlatformStopwatch ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            ~NcDownloadTaskDelegate ()
            {
                Log.Info (Log.LOG_HTTP, "NcUploadTaskDelegate Destruct");
            }

            public override void DidWriteData (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != ProgressAction) {
                    ProgressAction (bytesWritten, totalBytesWritten, totalBytesExpectedToWrite);
                }
            }

            public override void DidFinishDownloading (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
            {
                sw.Stop ();
                Log.Info (Log.LOG_HTTP, "Downloaded {0}kB in {1}ms ({2} bytes)", ((double)downloadTask.BytesReceived / (double)1024).ToString ("n2"), sw.ElapsedMilliseconds, downloadTask.BytesReceived);

                Token.ThrowIfCancellationRequested ();
                if (null != SuccessAction) {
                    NcAssert.True (downloadTask.Response is NSHttpUrlResponse);
                    var resp = downloadTask.Response as NSHttpUrlResponse;
                    NcAssert.NotNull (resp);

                    NcAssert.True (location.IsFileUrl);
                    var fileStream = new FileStream (location.Path, FileMode.Open);
                    NcAssert.True (null != fileStream && downloadTask.BytesReceived == fileStream.Length);

                    int status = (int)resp.StatusCode;
                    var headers = FromNsHeaders (resp.AllHeaderFields);
                    SuccessAction ((HttpStatusCode)status, fileStream, headers, Token);
                }
            }

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != error && null != ErrorAction) {
                    ErrorAction (createExceptionForNSError (error));
                }
            }

            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                BaseDidReceiveChallenge (Owner.Credentials, session, task, challenge, completionHandler);
            }
        }

        #endregion

        public static Dictionary<string, List<string>> FromNsHeaders (NSDictionary headers)
        {
            var ret = new Dictionary<string, List<string>> ();
            foreach (var v in headers) {
                // NB: Cocoa trolling us so hard by giving us back dummy
                // dictionary entries
                if (v.Key == null || v.Value == null)
                    continue;
                var key = v.Key.ToString ();
                if (!ret.ContainsKey (key)) {
                    ret [key] = new List<string> ();
                }
                foreach (var val in v.Value.ToString ().Split (new []{','})) {
                    ret [key].Add (val);
                }
            }
            return ret;
        }


        static readonly Regex cnRegex = new Regex (@"CN\s*=\s*([^,]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public static void BaseDidReceiveChallenge (NetworkCredential credentials, NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            if (challenge.ProtectionSpace.AuthenticationMethod != "NSURLAuthenticationMethodServerTrust" ||
                ServicePointManager.ServerCertificateValidationCallback == null) {
                HandleCredentialsRequest (credentials, challenge, completionHandler);
            } else {
                CertValidation (task.OriginalRequest.Url, challenge, completionHandler);
            }
        }

        static void HandleCredentialsRequest (NetworkCredential credentials, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            if (null != credentials) {
                var authenticationType = AuthenticationTypeFromAuthenticationMethod (challenge.ProtectionSpace.AuthenticationMethod);
                var uri = UriFromNSUrlProtectionSpace (challenge.ProtectionSpace);
                if (null != authenticationType && null != uri) {
                    var credential = new NSUrlCredential (
                                         credentials.UserName,
                                         credentials.Password,
                                         NSUrlCredentialPersistence.ForSession);
                    completionHandler (NSUrlSessionAuthChallengeDisposition.UseCredential, credential);
                    return;
                }
            }
            completionHandler (NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, challenge.ProposedCredential);
        }

        static void DummyCertValidation (NSUrl Url, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            // just accept the cert. Useful for testing ONLY
            Log.Error (Log.LOG_HTTP, "DummyCertValidation");
            completionHandler (NSUrlSessionAuthChallengeDisposition.UseCredential, NSUrlCredential.FromTrust (challenge.ProtectionSpace.ServerSecTrust));
        }

        static string SubjectAltNameOid = "2.5.29.17";
        static void CertValidation (NSUrl Url, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            // Convert Mono Certificates to .NET certificates and build cert 
            // chain from root certificate
            var serverCertChain = challenge.ProtectionSpace.ServerSecTrust;
            var chain = new X509Chain ();
            X509Certificate2 root = null;
            var errors = SslPolicyErrors.None;

            if (serverCertChain == null || serverCertChain.Count == 0) { 
                errors = SslPolicyErrors.RemoteCertificateNotAvailable;
                goto sslErrorVerify;
            }

            if (serverCertChain.Count == 1) {
                errors = SslPolicyErrors.RemoteCertificateChainErrors;
                goto sslErrorVerify;
            }

            var netCerts = Enumerable.Range (0, serverCertChain.Count)
                .Select (x => serverCertChain [x].ToX509Certificate2 ())
                .ToArray ();

            for (int i = 1; i < netCerts.Length; i++) {
                chain.ChainPolicy.ExtraStore.Add (netCerts [i]);
            }

            root = netCerts [0];

            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            try {
                if (!chain.Build (root)) {
                    errors = SslPolicyErrors.RemoteCertificateChainErrors;
                    goto sslErrorVerify;
                }
            } catch (System.Security.Cryptography.CryptographicException) {
                // As best we can tell, a XAMMIT (spurious).
                errors = SslPolicyErrors.RemoteCertificateChainErrors;
                goto sslErrorVerify;
            }

            var subject = root.Subject;
            var subjectCn = cnRegex.Match (subject).Groups [1].Value;

            if (String.IsNullOrWhiteSpace (subjectCn) || !MatchHostnameToPattern (Url.Host, subjectCn)) {
                bool found = false;
                foreach (var ext in root.Extensions) {
                    if (ext.Oid.Value == SubjectAltNameOid) {
                        // TODO Quite the hack. Need to figure out how to get the raw data rather than string splitting
                        foreach (var line in ext.Format (true).Split (new [] {'\n'})) {
                            var parts = line.Split (new []{ '=' });
                            if (parts[0] == "DNS Name") {
                                if (MatchHostnameToPattern(Url.Host, parts[1])) {
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (found)
                        break;
                }
                if (!found) {
                    errors = SslPolicyErrors.RemoteCertificateNameMismatch;
                    goto sslErrorVerify;
                }
            }

        sslErrorVerify:
            // NachoCove: Add this to make it look like other HTTP client
            var url = Url.ToString ();
            var request = new HttpWebRequest (new Uri (url));
            // End of NachoCove
            bool result = ServicePointManager.ServerCertificateValidationCallback (request, root, chain, errors);
            if (result) {
                completionHandler (
                    NSUrlSessionAuthChallengeDisposition.UseCredential,
                    NSUrlCredential.FromTrust (challenge.ProtectionSpace.ServerSecTrust));
            } else {
                completionHandler (NSUrlSessionAuthChallengeDisposition.CancelAuthenticationChallenge, null);
            }
        }

        static Uri UriFromNSUrlProtectionSpace (NSUrlProtectionSpace pSpace)
        {
            var builder = new UriBuilder (pSpace.Protocol, pSpace.Host);
            builder.Port = (int)pSpace.Port;
            Uri retval;
            try {
                retval = builder.Uri;
            } catch (UriFormatException) {
                retval = null;
            }
            return retval;
        }

        static string AuthenticationTypeFromAuthenticationMethod (string method)
        {
            if (NSUrlProtectionSpace.AuthenticationMethodDefault == method ||
                NSUrlProtectionSpace.AuthenticationMethodHTTPBasic == method ||
                NSUrlProtectionSpace.AuthenticationMethodNTLM == method ||
                NSUrlProtectionSpace.AuthenticationMethodHTTPDigest == method) {
                // Use Basic as a way to get the user+pass cred out.
                return "Basic";
            } else {
                return null;
            }
        }

        static bool MatchHostnameToPattern (string hostname, string pattern)
        {
            // check if this is a pattern
            int index = pattern.IndexOf ('*');
            if (index == -1) {
                // not a pattern, do a direct case-insensitive comparison
                return (String.Compare (hostname, pattern, true, CultureInfo.InvariantCulture) == 0);
            }

            // check pattern validity
            // A "*" wildcard character MAY be used as the left-most name component in the certificate.

            // unless this is the last char (valid)
            if (index != pattern.Length - 1) {
                // then the next char must be a dot .'.
                if (pattern [index + 1] != '.') {
                    return false;
                }
            }

            // only one (A) wildcard is supported
            int i2 = pattern.IndexOf ('*', index + 1);
            if (i2 != -1)
                return false;

            // match the end of the pattern
            string end = pattern.Substring (index + 1);
            int length = hostname.Length - end.Length;
            // no point to check a pattern that is longer than the hostname
            if (length <= 0)
                return false;

            if (String.Compare (hostname, length, end, 0, end.Length, true, CultureInfo.InvariantCulture) != 0) {
                return false;
            }

            // special case, we start with the wildcard
            if (index == 0) {
                // ensure we hostname non-matched part (start) doesn't contain a dot
                int i3 = hostname.IndexOf ('.');
                return ((i3 == -1) || (i3 >= (hostname.Length - end.Length)));
            }

            // match the start of the pattern
            string start = pattern.Substring (0, index);
            return (String.Compare (hostname, 0, start, 0, start.Length, true, CultureInfo.InvariantCulture) == 0);
        }


        /// <summary>
        /// Convert an NSError into an exception
        /// Lifted from ModernHttpClientiOS NativeMessageHandler
        /// </summary>
        /// <returns>An Exception</returns>
        /// <param name="error">NSError.</param>
        public static Exception createExceptionForNSError (NSError error)
        {
            var ret = default(Exception);
            var urlError = default(NSUrlError);
            var webExceptionStatus = WebExceptionStatus.UnknownError;

            // If the domain is something other than NSUrlErrorDomain, 
            // just grab the default info
            if (error.Domain != NSError.NSUrlErrorDomain)
                goto leave;

            // Convert the error code into an enumeration (this is future
            // proof, rather than just casting integer)
            if (!Enum.TryParse<NSUrlError> (error.Code.ToString (), out urlError))
                urlError = NSUrlError.Unknown;

            // Parse the enum into a web exception status or exception. some
            // of these values don't necessarily translate completely to
            // what WebExceptionStatus supports, so made some best guesses
            // here.  for your reading pleasure, compare these:
            //
            // Apple docs: https://developer.apple.com/library/mac/documentation/Cocoa/Reference/Foundation/Miscellaneous/Foundation_Constants/Reference/reference.html
            // .NET docs: http://msdn.microsoft.com/en-us/library/system.net.webexceptionstatus(v=vs.110).aspx
            switch (urlError) {
            case NSUrlError.Cancelled:
            case NSUrlError.UserCancelledAuthentication:
                ret = new OperationCanceledException ();
                break;
            case NSUrlError.BadURL:
            case NSUrlError.UnsupportedURL:
            case NSUrlError.CannotConnectToHost:
            case NSUrlError.ResourceUnavailable:
            case NSUrlError.NotConnectedToInternet:
            case NSUrlError.UserAuthenticationRequired:
                webExceptionStatus = WebExceptionStatus.ConnectFailure;
                break;
            case NSUrlError.TimedOut:
                webExceptionStatus = WebExceptionStatus.Timeout;
                break;
            case NSUrlError.CannotFindHost:
            case NSUrlError.DNSLookupFailed:
                webExceptionStatus = WebExceptionStatus.NameResolutionFailure;
                break;
            case NSUrlError.DataLengthExceedsMaximum:
                webExceptionStatus = WebExceptionStatus.MessageLengthLimitExceeded;
                break;
            case NSUrlError.NetworkConnectionLost:
                webExceptionStatus = WebExceptionStatus.ConnectionClosed;
                break;
            case NSUrlError.HTTPTooManyRedirects:
            case NSUrlError.RedirectToNonExistentLocation:
                webExceptionStatus = WebExceptionStatus.ProtocolError;
                break;
            case NSUrlError.BadServerResponse:
            case NSUrlError.ZeroByteResource:
            case NSUrlError.CannotDecodeContentData:
            case NSUrlError.CannotDecodeRawData:
            case NSUrlError.CannotParseResponse:
            case NSUrlError.FileDoesNotExist:
            case NSUrlError.FileIsDirectory:
            case NSUrlError.NoPermissionsToReadFile:
            case NSUrlError.CannotLoadFromNetwork:
            case NSUrlError.CannotCreateFile:
            case NSUrlError.CannotOpenFile:
            case NSUrlError.CannotCloseFile:
            case NSUrlError.CannotWriteToFile:
            case NSUrlError.CannotRemoveFile:
            case NSUrlError.CannotMoveFile:
            case NSUrlError.DownloadDecodingFailedMidStream:
            case NSUrlError.DownloadDecodingFailedToComplete:
                webExceptionStatus = WebExceptionStatus.ReceiveFailure;
                break;
            case NSUrlError.SecureConnectionFailed:
                webExceptionStatus = WebExceptionStatus.SecureChannelFailure;
                break;
            case NSUrlError.ServerCertificateHasBadDate:
            case NSUrlError.ServerCertificateHasUnknownRoot:
            case NSUrlError.ServerCertificateNotYetValid:
            case NSUrlError.ServerCertificateUntrusted:
            case NSUrlError.ClientCertificateRejected:
                webExceptionStatus = WebExceptionStatus.TrustFailure;
                break;
            }

            // If we parsed a web exception status code, create an exception
            // for it
            if (webExceptionStatus != WebExceptionStatus.UnknownError) {
                ret = new WebException (error.LocalizedDescription, webExceptionStatus);
            }

            leave:
            // If no exception generated yet, throw a normal exception with
            // the error message.
            return ret ?? new Exception (error.LocalizedDescription);
        }
    }
}

