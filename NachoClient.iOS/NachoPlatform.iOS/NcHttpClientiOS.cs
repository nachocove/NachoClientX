﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;
using Foundation;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using NachoCore.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using NachoCore.Model;
using System.Text;
using System.Net.Http;

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
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

        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NcDownloadTaskDelegate dele, CancellationToken cancellationToken)
        {
            // Mostly lifted from ModernHttpClientiOS NativeMessageHandler
            NSInputStream RequestBodyStream = null;
            NSData RequestBody = null;
            if (request.HasContent ()) {
                if (isSend) {
                    //request.AddHeader ("Expect", "100-continue");
                }
                if (!string.IsNullOrEmpty (request.ContentType)) {
                    if (!request.Headers.Contains ("Content-Type")) {
                        request.Headers.Add ("Content-Type", request.ContentType);
                    }
                }
                if (request.Content is FileStream) {
                    var fileStream = request.Content as FileStream;
                    RequestBodyStream = NSInputStream.FromFile (fileStream.Name);
                    dele.FilePath = fileStream.Name;
                } else if (request.Content is byte[]) {
                    RequestBody = NSData.FromArray (request.Content as byte[]);
                } else {
                    NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                }
            }

            if (null != request.Cred && !request.RequestUri.IsHttps ()) {
                Log.Error (Log.LOG_HTTP, "Thou shalt not send credentials over http\n{0}", new System.Diagnostics.StackTrace().ToString ());
            }
                
            if (PreAuthenticate && null != request.Cred) {
                // In the !PreAuthenticate case, the Creds have alredy been added to the NcDownloadTaskDelegate by the caller,
                // so there's nothing else to do here.
                var basicAuth = Convert.ToBase64String (Encoding.ASCII.GetBytes (string.Format ("{0}:{1}", request.Cred.Username, request.Cred.GetPassword ())));
                request.Headers.Add ("Authorization", string.Format ("{0} {1}", "Basic", basicAuth));
            }

            var nsHeaders = new NSMutableDictionary ();
            foreach (var x in request.Headers) {
                nsHeaders.Add (new NSString (x.Key), new NSString (String.Join (",", x.Value)));
            }

            var req = new NSMutableUrlRequest () {
                AllowsCellularAccess = true,
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = nsHeaders,
                HttpMethod = request.Method.ToString ().ToUpperInvariant (),
                Url = NSUrl.FromString (request.RequestUri.ToString ()),
                TimeoutInterval = timeout,
            };
            if (RequestBody != null) {
                req.Body = RequestBody;
            } else if (RequestBodyStream != null) {
                req.BodyStream = RequestBodyStream;
            }

            var config = NSUrlSessionConfiguration.DefaultSessionConfiguration;
            config.TimeoutIntervalForRequest = timeout;
            config.TimeoutIntervalForResource = timeout;
            config.URLCache = new NSUrlCache (0, 0, "HttpClientCache");

            var session = NSUrlSession.FromConfiguration (config, dele, null);
            var task = session.CreateDownloadTask (req);
            task.TaskDescription = request.guid;
            cancellationToken.Register (() => {
                // make sure don't run on the UI thread.
                NcTask.Run(() => {
                    task.Cancel ();
                }, "NcHttpClient.iOS.Cancel");
            });
            Log.Debug (Log.LOG_HTTP, "NcHttpClient: Starting task {0} for {1}", task.TaskDescription, req.Url);
            task.Resume ();
        }

        #region INcHttpClient implementation

        public void GetRequest (NcHttpRequest request, int timeout,
                                SuccessDelegate success,
                                ErrorDelegate error,
                                CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void GetRequest (NcHttpRequest request, int timeout,
                                SuccessDelegate success,
                                ErrorDelegate error,
                                ProgressDelegate progress,
                                CancellationToken cancellationToken)
        {
            var dele = new NcDownloadTaskDelegate (this, request.Cred, cancellationToken, success, error, progress);
            SetupAndRunRequest (false, request, timeout, dele, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            var dele = new NcDownloadTaskDelegate (this, request.Cred, cancellationToken, success, error, progress);
            SetupAndRunRequest (true, request, timeout, dele, cancellationToken);
        }

        #endregion

        #region NcDownloadTaskDelegate

        protected class NcDownloadTaskDelegate : NSUrlSessionDownloadDelegate
        {
            protected SuccessDelegate SuccessAction;

            protected ErrorDelegate ErrorAction { get; set; }

            protected ProgressDelegate ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }

            McCred Cred { get; set; }

            NcHttpClient Owner { get; set; }

            public string FilePath { get; set; }

            public NcDownloadTaskDelegate (NcHttpClient owner, McCred cred, CancellationToken cancellationToken, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress = null)
            {
                sw = new PlatformStopwatch ();
                sw.Start ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Cred = cred;
                Token = cancellationToken;
                Owner = owner;
            }

            public override void DidFinishDownloading (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
            {
                if (Token.IsCancellationRequested) {
                    return;
                }
                if (null != SuccessAction) {
                    NcAssert.True (downloadTask.Response is NSHttpUrlResponse);
                    var resp = downloadTask.Response as NSHttpUrlResponse;
                    NcAssert.True (location.IsFileUrl);
                    using (var fileStream = new FileStream (location.Path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        try {
                            int status = (int)resp.StatusCode;
                            var response = new NcHttpResponse (downloadTask.CurrentRequest.HttpMethod, (HttpStatusCode)status, fileStream, resp.MimeType, FromNsHeaders (resp.AllHeaderFields));
                            SuccessAction (response, Token);
                        } catch (Exception ex) {
                            Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): Error running SuccessAction: {1}", downloadTask.TaskDescription, ex);
                        }
                    }
                }
            }

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
                try {
                    long sent = task.BytesSent;
                    long received = task.BytesReceived;
                    Log.Debug (Log.LOG_HTTP, "NcHttpClient({0}): Finished request {1}ms (bytes sent:{2} received:{3}){4}", task.TaskDescription, sw.ElapsedMilliseconds, sent.ToString ("n0"), received.ToString ("n0"),
                        error != null ? string.Format(" (Error: {0})", error) : "");

                    if (Token.IsCancellationRequested) {
                        return;
                    }
                    if (null != error && null != ErrorAction) {
                        ErrorAction (createExceptionForNSError (error), Token);
                    }
                    session.FinishTasksAndInvalidate ();
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): DidCompleteWithError exception {1}", task.TaskDescription, ex);
                }
            }

            public override void DidSendBodyData (NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
            {
                try {
                    if (Token.IsCancellationRequested) {
                        return;
                    }
                    if (null != ProgressAction) {
                        ProgressAction (true, task.TaskDescription, bytesSent, totalBytesSent, totalBytesExpectedToSend);
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): DidSendBodyData exception {1}", task.TaskDescription, ex);
                }
            }

            public override void DidWriteData (NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
            {
                try {
                    if (Token.IsCancellationRequested) {
                        return;
                    }
                    if (null != ProgressAction) {
                        ProgressAction (false, downloadTask.TaskDescription, bytesWritten, totalBytesWritten, totalBytesExpectedToWrite);
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): DidWriteData exception {1}", downloadTask.TaskDescription, ex);
                }
            }

            public override void WillPerformHttpRedirection (NSUrlSession session, NSUrlSessionTask task, NSHttpUrlResponse response, NSUrlRequest newRequest, Action<NSUrlRequest> completionHandler)
            {
                try {
                    if (Owner.AllowAutoRedirect) {
                        Log.Debug (Log.LOG_HTTP, "NcHttpClient({0}): WillPerformHttpRedirection", task.TaskDescription);
                        completionHandler (newRequest);
                    } else {
                        Log.Debug (Log.LOG_HTTP, "NcHttpClient({0}): WillPerformHttpRedirection disallowed", task.TaskDescription);
                        completionHandler (null);
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): WillPerformHttpRedirection exception {1}",task.TaskDescription, ex);
                }
            }

            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                try {
                    BaseDidReceiveChallenge (Cred, session, task, challenge, completionHandler);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "NcHttpClient({0}): DidReceiveChallenge exception {1}", task.TaskDescription, ex);
                }
            }

            public override void NeedNewBodyStream (NSUrlSession session, NSUrlSessionTask task, Action<NSInputStream> completionHandler)
            {
                if (!string.IsNullOrEmpty (FilePath)) {
                    completionHandler (new NSInputStream (FilePath));
                } else {
                    throw new Exception (string.Format ("NcHttpClient({0}): Can not satisfy NeedNewBodyStream. No FilePath", task.TaskDescription));
                }
            }
        }

        #endregion

        public static NcHttpHeaders FromNsHeaders (NSDictionary headers)
        {
            var ret = new NcHttpHeaders ();
            foreach (var v in headers) {
                // NB: Cocoa trolling us so hard by giving us back dummy
                // dictionary entries
                if (v.Key == null || v.Value == null)
                    continue;
                ret.Add (v.Key.ToString (), v.Value.ToString ());
            }
            return ret;
        }


        public static void BaseDidReceiveChallenge (McCred cred, NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            if (challenge.ProtectionSpace.AuthenticationMethod != "NSURLAuthenticationMethodServerTrust") {
                Log.Warn (Log.LOG_HTTP, "NcHttpClient({0}): Doing auth, so pre-auth didn't work!", task.TaskDescription);
                HandleCredentialsRequest (cred, challenge, completionHandler);
            } else {
                if (ServicePointManager.ServerCertificateValidationCallback == null) {
                    completionHandler (NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, challenge.ProposedCredential);
                } else {
                    CertValidation (task.OriginalRequest.Url, challenge, completionHandler);
                }
            }
        }

        static void HandleCredentialsRequest (McCred cred, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            if (null != cred) {
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
            if (NcHttpCertificateValidation.CertValidation (new Uri (Url.ToString ()), root, chain, errors)) {
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
