//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;
using System.Net.Http;
using NachoPlatform;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Text;

namespace NachoCore
{
    public class SFDCCommand : NcCommand
    {
        protected const int KDefaultTimeout = 2;
        protected string CmdName;
        protected NcStateMachine OwnerSm;

        public SFDCCommand (IBEContext beContext) : base (beContext)
        {
            CmdName = GetType ().Name;
            OwnerSm = null;
        }

        static public INcHttpClient TestHttpClient { get; set; }
        public INcHttpClient HttpClient {
            get {
                if (TestHttpClient != null) {
                    return TestHttpClient;
                } else {
                    return NcHttpClient.Instance;
                }
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            if (Cts.IsCancellationRequested) {
                return;
            }
            OwnerSm = sm;
            NcTask.Run (() => MakeAndSendRequest (), CmdName);
        }

        protected virtual void MakeAndSendRequest ()
        {
            throw new NotImplementedException ();
        }

        protected void GetRequest (NcHttpRequest request)
        {
            if (Cts.IsCancellationRequested) {
                return;
            }
            Log.Info (Log.LOG_SFDC, "Url: {0}:{1}", request.Method, request.RequestUri);
            HttpClient.GetRequest (request, KDefaultTimeout, SuccessAction, ErrorAction, Cts.Token);
        }

        protected void SendRequest (NcHttpRequest request)
        {
            if (Cts.IsCancellationRequested) {
                return;
            }
            Log.Info (Log.LOG_SFDC, "Url: {0}:{1}", request.Method, request.RequestUri);
            HttpClient.SendRequest (request, KDefaultTimeout, SuccessAction, ErrorAction, Cts.Token);
        }

        public const string jsonContentType = "application/json";

        protected NcHttpRequest NewRequest (HttpMethod method, string contentType)
        {
            var request = new NcHttpRequest (method, BEContext.Server.BaseUri ());
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.Headers.Add ("Authorization", string.Format ("Bearer {0}", BEContext.Cred.GetAccessToken ()));
            //Log.Info (Log.LOG_SFDC, "Bearer Token: {0}", BEContext.Cred.GetAccessToken ());
            if (!string.IsNullOrEmpty (contentType)) {
                request.ContentType = contentType;
            }
            return request;

        }
        protected NcHttpRequest NewRequest (HttpMethod method, string commandPath, string contentType)
        {
            var sfServer = BEContext.Server;
            sfServer.Path = commandPath; // DO NOT SAVE THIS.
            var request = new NcHttpRequest (method, sfServer.BaseUri ());
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            if (!string.IsNullOrEmpty (contentType)) {
                request.ContentType = contentType;
            }
            request.Headers.Add ("Authorization", string.Format ("Bearer {0}", BEContext.Cred.GetAccessToken ()));
            return request;
        }

        protected void SuccessAction (NcHttpResponse response, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            try {
                Event evt;
                if (response.StatusCode == HttpStatusCode.OK) {
                    evt = ProcessSuccessResponse (response, token);
                } else {
                    evt = ProcessErrorResponse (response, token);
                }
                Finish (evt, false);
            } catch (Exception ex) {
                Log.Error (Log.LOG_SFDC, "{0}: Could not process json: {1}", CmdName, ex);
                Finish (Event.Create ((uint)SmEvt.E.HardFail, "SFDCVERSUNKERROR"), true);
            }
        }

        protected Event ProcessErrorResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCERRORERROR");
            }
            return ProcessErrorResponse (jsonResponse);
        }

        protected Event ProcessErrorResponse (string jsonResponse)
        {
            try {
                var errorList = Newtonsoft.Json.Linq.JArray.Parse (jsonResponse);
                foreach (var error in errorList) {
                    var code = (string)error.SelectToken ("errorCode");
                    switch (code) {
                    case "INVALID_SESSION_ID":
                        return Event.Create ((uint)SalesForceProtoControl.SfdcEvt.E.AuthFail, "ERRORAUTHFAIL");

                    default:
                        var message = error.SelectToken ("message");
                        Log.Error (Log.LOG_SFDC, "{0}: unknown error code: {1}: {2}", CmdName, code, null != message ? (string)message : "UNKNOWN");
                        return Event.Create ((uint)SmEvt.E.HardFail, "SFDCUNKERROR");
                    }
                }
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCNOERROR");
            } catch (JsonReaderException) {
                Log.Warn (Log.LOG_SFDC, "{0}: Could not process json response: {1}", CmdName, jsonResponse);
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCNOJSON");
            }
        }

        protected virtual Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            throw new NotImplementedException ();
        }

        protected virtual void ErrorAction (Exception ex, CancellationToken cToken)
        {
            if (ex != null) {
                Log.Info (Log.LOG_SFDC, "Request {0} failed: {1}", CmdName, ex.Message);
            }

            Event evt;
            bool serverFailedGenerally;
            if (cToken.IsCancellationRequested || ex is OperationCanceledException) {
                return;
            } else if (ex is WebException) {
                serverFailedGenerally = true;
                evt = Event.Create ((uint)SmEvt.E.TempFail, "SFDCWEBEXTEMP");
            } else {
                serverFailedGenerally = true;
                evt = Event.Create ((uint)SmEvt.E.HardFail, "SFDCWEXCHARD");
            }
            Finish (evt, serverFailedGenerally);
        }

        protected void ReportCommResult (bool serverFailedGenerally)
        {
            var fsdcServer = BEContext.Server;
            if (null != fsdcServer && !string.IsNullOrEmpty (fsdcServer.Host)) {
                NcCommStatus.Instance.ReportCommResult (BEContext.Account.Id, fsdcServer.Host, serverFailedGenerally);
            }
        }

        protected void Finish (Event evt, bool serverFailedGenerally)
        {
            // before we do anything, make sure we aren't cancelled. We don't want to process
            // anything or move the SM to a new state if we're cancelled.
            if (Cts.Token.IsCancellationRequested) {
                Log.Info (Log.LOG_SFDC, "{0}({1}): Cancelled", CmdName, AccountId);
                return;
            }
            ReportCommResult (serverFailedGenerally);
            OwnerSm.PostEvent (evt);
        }
    }
}

