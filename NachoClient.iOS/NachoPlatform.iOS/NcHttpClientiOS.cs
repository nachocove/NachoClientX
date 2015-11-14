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
using System.Text;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        McCred Cred { get; set; }

        NSMutableUrlRequest OriginalRequest { get; set; }

        public Stream OutputStream { get; set; }

        public readonly bool AllowAutoRedirect = false;

        public readonly bool PreAuthenticate = true;

        public NcHttpClient (McCred cred)
        {
            Cred = cred;
        }

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NSUrlSessionDelegate dele, CancellationToken cancellationToken)
        {
            // Mostly lifted from ModernHttpClientiOS NativeMessageHandler
            NSInputStream RequestBodyStream = null;
            NSData RequestBody = null;
            if (request.Content != null) {
                OutputStream = request.Content;
                if (isSend) {
                    request.AddHeader ("Expect", "100-continue");

                    // TODO For ActiveSync this works fine, because it assumes BASIC auth.
                    // For anything else, this would need to be adapted.
                }
                if (!string.IsNullOrEmpty (request.ContentType)) {
                    if (!request.ContainsHeader ("Content-Type")) {
                        request.AddHeader ("Content-Type", request.ContentType);
                    }
                }
                if (request.ContentLength.HasValue) {
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", request.ContentLength.Value.ToString ());
                    }
                }
                if (request.Content is FileStream) {
                    var fileStream = request.Content as FileStream;
                    RequestBodyStream = NSInputStream.FromFile (fileStream.Name);
                    if (!request.ContainsHeader ("Content-Length")) {
                        request.AddHeader ("Content-Length", fileStream.Length.ToString ());
                    }
                } else if (request.Content is MemoryStream) {
                    var memStream = request.Content as MemoryStream;
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

            if (PreAuthenticate) {
                var basicAuth = Convert.ToBase64String (Encoding.ASCII.GetBytes (string.Format ("{0}:{1}", Cred.Username, Cred.GetPassword ())));
                nsHeaders.Add (new NSString("Authorization"),  new NSString(string.Format ("{0} {1}", "Basic", basicAuth)));
            }

            var req = new NSMutableUrlRequest () {
                AllowsCellularAccess = true,
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = nsHeaders,
                HttpMethod = request.Method.ToString ().ToUpperInvariant (),
                Url = NSUrl.FromString (request.Url),
                TimeoutInterval = timeout,
            };
            if (RequestBody != null) {
                req.Body = RequestBody;
            } else if (RequestBodyStream != null) {
                req.BodyStream = RequestBodyStream;
            }
            OriginalRequest = req;

            var config = NSUrlSessionConfiguration.DefaultSessionConfiguration;
            config.TimeoutIntervalForRequest = timeout;
            config.TimeoutIntervalForResource = timeout;
            config.URLCache = new NSUrlCache (0, 0, "HttpClientCache");

            var session = NSUrlSession.FromConfiguration (config, dele, null);
            task = session.CreateDownloadTask (OriginalRequest);
            cancellationToken.Register (() => {
                task.Cancel ();
            });
            task.Resume ();
        }

        #region INcHttpClient implementation

        NSUrlSessionTask task { get; set; }

        public void GetRequest (NcHttpRequest request, int timeout,
                                SuccessDelete success,
                                ErrorDelegate error,
                                CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void GetRequest (NcHttpRequest request, int timeout,
                                SuccessDelete success,
                                ErrorDelegate error,
                                ProgressDelegate progress,
                                CancellationToken cancellationToken)
        {
            var dele = new NcDownloadTaskDelegate (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (false, request, timeout, dele, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var dele = new NcDownloadTaskDelegate (this, cancellationToken, success, error, progress);
            SetupAndRunRequest (true, request, timeout, dele, cancellationToken);
        }

        #endregion

        #region NcDownloadTaskDelegate

        class NcDownloadTaskDelegate : NSUrlSessionDownloadDelegate
        {
            protected SuccessDelete SuccessAction;

            protected ErrorDelegate ErrorAction { get; set; }

            protected ProgressDelegate ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            NcHttpClient Owner { get; set; }

            public NcDownloadTaskDelegate (NcHttpClient owner, CancellationToken cancellationToken, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                sw.Start ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
                Owner = owner;
            }

            public override void DidFinishDownloading (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
            {
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
                sw.Stop ();
                var sent = (double)task.BytesSent / (double)1024;
                var received = (double)task.BytesReceived / (double)1024;
                Log.Info (Log.LOG_HTTP, "NcHttpClient: Finished request {0}ms (bytes sent:{1} received:{2})", sw.ElapsedMilliseconds, sent.ToString ("n0"), received.ToString ("n0"));

                Token.ThrowIfCancellationRequested ();
                if (null != error && null != ErrorAction) {
                    ErrorAction (createExceptionForNSError (error));
                }
            }

            public override void DidSendBodyData (NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != ProgressAction) {
                    ProgressAction (bytesSent, totalBytesSent, totalBytesExpectedToSend);
                }
            }

            public override void DidWriteData (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != ProgressAction) {
                    ProgressAction (bytesWritten, totalBytesWritten, totalBytesExpectedToWrite);
                }
            }

            public override void NeedNewBodyStream (NSUrlSession session, NSUrlSessionTask task, Action<NSInputStream> completionHandler)
            {
                var fileStream = Owner.OutputStream as FileStream;
                if (fileStream != null) {
                    completionHandler (NSInputStream.FromFile (fileStream.Name));
                } else {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient: NeedNewBodyStream called for stream type {0}", Owner.OutputStream.GetType ().Name);
                }
            }

            public override void WillPerformHttpRedirection (NSUrlSession session, NSUrlSessionTask task, NSHttpUrlResponse response, NSUrlRequest newRequest, Action<NSUrlRequest> completionHandler)
            {
                if (Owner.AllowAutoRedirect) {
                    completionHandler(newRequest);
                } else {
                    completionHandler(null);
                }
            }

            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                BaseDidReceiveChallenge (Owner.Cred, session, task, challenge, completionHandler);
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


        public static void BaseDidReceiveChallenge (McCred cred, NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            if (challenge.ProtectionSpace.AuthenticationMethod != "NSURLAuthenticationMethodServerTrust") {
                Log.Warn (Log.LOG_HTTP, "NcHttpClient: Doing auth, so pre-auth didn't work!");
                HandleCredentialsRequest (cred, challenge, completionHandler);
            } else {
                if (ServicePointManager.ServerCertificateValidationCallback == null) {
                    Log.Warn (Log.LOG_HTTP, "NcHttpClient: No ServerCertificateValidationCallback!");
                    completionHandler (NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, challenge.ProposedCredential);
                } else {
                    CertValidation (task.OriginalRequest.Url, challenge, completionHandler);
                }
            }
        }

        static void HandleCredentialsRequest (McCred cred, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            NetworkCredential credentials = new NetworkCredential (cred.Username, cred.GetPassword ());
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

        static void CertValidation (NSUrl Url, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            // Convert Mono Certificates to .NET certificates and build cert 
            // chain from root certificate
            var serverCertChain = challenge.ProtectionSpace.ServerSecTrust;
            var chain = new X509Chain ();
            X509Certificate2 root = null;
            SslPolicyErrors errors = SslPolicyErrors.None;

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
        sslErrorVerify:
            if (NcHttpCertificateValidation.CertValidation (new Uri(Url.ToString ()), root, chain, errors)) {
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

