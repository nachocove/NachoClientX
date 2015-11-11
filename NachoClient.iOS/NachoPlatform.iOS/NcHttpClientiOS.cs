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

namespace NachoPlatform
{
    public class NcHttpClient : INcHttpClient
    {
        protected void SetupAndRunRequest (bool isSend, NcHttpRequest request, int timeout, NSUrlSessionDelegate dele, CancellationToken cancellationToken)
        {
            // Mostly lifted from ModernHttpClientiOS NativeMessageHandler
            NSInputStream RequestBodyStream = null;
            NSData RequestBody = null;
            if (request.Content != null) {
                request.Content.Flush (); // make sure it's all written
                if (!string.IsNullOrEmpty (request.ContentType)) {
                    if (!request.Headers.ContainsKey ("Content-Type")) {
                        request.Headers ["Content-Type"] = new List<string> ();
                    }
                    request.Headers ["Content-Type"].Add (request.ContentType);
                    if (!request.Headers.ContainsKey ("Content-Length")) {
                        request.Headers ["Content-Length"] = new List<string> ();
                    }
                    request.Headers ["Content-Length"].Add (request.Content.Length.ToString ());
                }
                if (request.Content is FileStream) {
                    RequestBodyStream = NSInputStream.FromFile ((request.Content as FileStream).Name);
                    //RequestBody = NSData.FromStream (request.Content);
                } else if (request.Content is MemoryStream) {
                    //RequestBodyStream = NSInputStream.FromData (NSData.FromArray ((request.Content as MemoryStream).GetBuffer ()));
                    RequestBody = NSData.FromStream (request.Content);
                } else {
                    NcAssert.CaseError (string.Format ("request.Content is of unknown type {0}", request.Content.GetType ().Name));
                }
            }

            var nsHeaders = new NSMutableDictionary ();
            foreach (var x in request.Headers) {
                nsHeaders.Add (new NSString (x.Key), new NSString (String.Join (",", x.Value)));
            }

            var rq = new NSMutableUrlRequest () {
                AllowsCellularAccess = true,
                BodyStream = RequestBodyStream,
                Body = RequestBody,
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = nsHeaders,
                HttpMethod = request.Method.ToString ().ToUpperInvariant (),
                Url = NSUrl.FromString (request.Url),
                TimeoutInterval = timeout,
            };
            var config = NSUrlSessionConfiguration.DefaultSessionConfiguration;
            config.TimeoutIntervalForRequest = timeout;
            config.TimeoutIntervalForResource = timeout;
            config.URLCache = new NSUrlCache (0, 0, "HttpClientCache");

            var session = NSUrlSession.FromConfiguration (config, dele, new NSOperationQueue ());
            if (isSend) {
                task = session.CreateUploadTask (rq);
            } else {
                task = session.CreateDownloadTask (rq);
                cancellationToken.Register (() => {
                    task.Cancel ();
                });
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
            var dele = new NcDownloadTaskDelegate (cancellationToken, success, error, progress);
            dele.sw.Start ();
            Log.Info (Log.LOG_HTTP, "GetRequest: Started stopwatch");
            SetupAndRunRequest (false, request, timeout, dele, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, Action success, Action<Exception> error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, Action success, Action<Exception> error, Action<long, long, long> progress, CancellationToken cancellationToken)
        {
            var dele = new NcUploadTaskDelegate (cancellationToken, success, error, progress);
            dele.sw.Start ();
            Log.Info (Log.LOG_HTTP, "SendRequest: Started stopwatch");
            SetupAndRunRequest (true, request, timeout, dele, cancellationToken);
        }

        #endregion

        #region NcUploadTaskDelegate

        class NcUploadTaskDelegate : NSUrlSessionDataDelegate
        {
            protected Action SuccessAction { get; set; }

            protected Action<Exception> ErrorAction { get; set; }

            protected Action<long, long, long> ProgressAction { get; set; }

            protected CancellationToken Token { get; set; }

            public PlatformStopwatch sw { get; protected set; }


            public NcUploadTaskDelegate (CancellationToken cancellationToken, Action success, Action<Exception> error, Action<long, long, long> progress = null)
            {
                sw = new PlatformStopwatch ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
            }

            public override void DidReceiveResponse (NSUrlSession session, NSUrlSessionDataTask dataTask, NSUrlResponse response, Action<NSUrlSessionResponseDisposition> completionHandler)
            {
                Token.ThrowIfCancellationRequested ();
                completionHandler(NSUrlSessionResponseDisposition.Allow);
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
                } else {
                    if (null != SuccessAction) {
                        SuccessAction ();
                    }
                }
            }
            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, challenge.ProposedCredential);
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

            public NcDownloadTaskDelegate (CancellationToken cancellationToken, Action<HttpStatusCode, Stream, Dictionary<string, List<string>>, CancellationToken> success, Action<Exception> error, Action<long, long, long> progress = null)
            {
                sw = new PlatformStopwatch ();
                SuccessAction = success;
                ErrorAction = error;
                ProgressAction = progress;
                Token = cancellationToken;
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

            private Dictionary<string, List<string>> FromNsHeaders (NSDictionary headers)
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

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
                Token.ThrowIfCancellationRequested ();
                if (null != error && null != ErrorAction) {
                    ErrorAction (createExceptionForNSError (error));
                }
            }
            public override void DidReceiveChallenge (NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
            {
                completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, challenge.ProposedCredential);
            }
        }

        #endregion

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

