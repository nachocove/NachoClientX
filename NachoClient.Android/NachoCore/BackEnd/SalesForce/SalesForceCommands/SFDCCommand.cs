//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;
using System.Net.Http;
using NachoPlatform;
using System.IO;
using System.Net;

namespace NachoCore
{
    public class SFDCCommand : NcCommand
    {
        protected const int KDefaultTimeout = 2;
        protected string CmdName;
        NcStateMachine OwnerSm;

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
            OwnerSm = sm;
            NcTask.Run (() => MakeAndSendRequest (), CmdName);
        }

        protected virtual void MakeAndSendRequest ()
        {
            throw new NotImplementedException ();
        }

        protected void GetRequest (NcHttpRequest request)
        {
            Log.Info (Log.LOG_SFDC, "Url: {0}:{1}", request.Method, request.RequestUri);
            HttpClient.GetRequest (request, KDefaultTimeout, SuccessAction, ErrorAction, Cts.Token);
        }

        protected void SendRequest (NcHttpRequest request)
        {
            Log.Info (Log.LOG_SFDC, "Url: {0}:{1}", request.Method, request.RequestUri);
            HttpClient.SendRequest (request, KDefaultTimeout, SuccessAction, ErrorAction, Cts.Token);
        }

        protected NcHttpRequest NewRequest (HttpMethod method, string commandPath, string contentType = "application/json")
        {
            var request = new NcHttpRequest (method, new Uri (Path.Combine (BEContext.Server.BaseUriString (), commandPath)));
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.Headers.Add ("Authorization", string.Format ("Bearer {0}", BEContext.Cred.GetAccessToken ()));
            request.ContentType = contentType;
            return request;
        }

        protected void SuccessAction (NcHttpResponse response, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            Event evt = ProcessSuccessResponse (response, token);
            Finish (evt, false);
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

