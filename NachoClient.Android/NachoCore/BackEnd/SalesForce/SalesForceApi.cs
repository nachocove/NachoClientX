//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using System.Threading;
using System.Net.Http;
using NachoPlatform;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
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
            NcTask.Run (() => {
                MakeAndSendRequest ();
            }, CmdName);
        }

        protected virtual void MakeAndSendRequest ()
        {
            throw new NotImplementedException ();
        }

        protected void GetRequest (NcHttpRequest request)
        {
            HttpClient.GetRequest (request, KDefaultTimeout, SuccessAction, ErrorAction, Cts.Token);
        }

        protected void SendRequest (NcHttpRequest request)
        {
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
            NcCommStatus.Instance.ReportCommResult (BEContext.Account.Id, BEContext.Server.Host, serverFailedGenerally);
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

    public class SFDCGetContactsCommand : SFDCCommand
    {
        public SFDCGetContactsCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override void MakeAndSendRequest ()
        {
            var request = NewRequest (HttpMethod.Get, "/contacts/");
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCBOGUSSUCCESS");
        }
    }

    public class SFDCGetPathsCommand : SFDCCommand
    {
        public SFDCGetPathsCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override void MakeAndSendRequest ()
        {
            var request = NewRequest (HttpMethod.Get, "");
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCPATHSBOGUSSUCCESS");
        }
    }

    public class SFDCLoginCommand : SFDCCommand
    {
        const string LoginUrl = "https://login.salesforce.com/services/oauth2/token";

        public SFDCLoginCommand (IBEContext beContext) : base (beContext)
        {
        }

        public class LoginOauthData
        {
            public string grant_type { get; set; }
            public string client_id { get; set; }
            public string client_secret { get; set; }
            public string username { get; set; }
            public string password { get; set; }
        }

        protected override void MakeAndSendRequest ()
        {
            var request = new NcHttpRequest (HttpMethod.Post, new Uri(LoginUrl));
            string clientId = "foo";
            string clientSecret = "bar";
            string userToken = "jO6iGkkyD5AywB5zIJh96nSY3";
//            string postData = string.Format ("grant_type={0}&client_id={1}&client_secret={2}&username={3}&password={4}{5}",
//                                  "password", clientId, clientSecret, BEContext.Cred.Username, BEContext.Cred.GetPassword (), userToken);
            string postData = string.Format ("grant_type={0}&username={3}&password={4}{5}",
                "password", clientId, clientSecret, BEContext.Cred.Username, BEContext.Cred.GetPassword (), userToken);
            
            request.SetContent (Encoding.UTF8.GetBytes (postData), "application/json");
            SendRequest (request);
        }
        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            try {
                var loginResponse = JsonConvert.DeserializeObject<Dictionary<string, string>> (jsonResponse);
            } catch (Exception ex) {
                ErrorAction (ex, Cts.Token);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCLOGINBOGUSSUCCESS");
        }
    }

    public class SFDCGetApiVersions
    {
        protected CancellationToken CToken;

        public SFDCGetApiVersions (CancellationToken cToken)
        {
            CToken = cToken;
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

        public class SFDCVersion
        {
            public string label { get; set; }
            public string url { get; set; }
            public double version { get; set; }

            public override string ToString ()
            {
                return string.Format ("[SFDCVersion: label={0}, url={1}, version={2}]", label, url, version);
            }
        }


        const string SFDCApiServicesHost = "na1.salesforce.com";
        const string SFDCApiServicesPath = "/services/data/";
        void GetVersions (Action<List<SFDCVersion>> SetServer)
        {
            var sfdcVersionsUri = new Uri (string.Format ("https://{0}{1}", SFDCApiServicesHost, SFDCApiServicesPath));
            var request = new NcHttpRequest (HttpMethod.Get, sfdcVersionsUri);
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.ContentType = "application/json";
            HttpClient.SendRequest (request, 5, ((response, token) => {
                if (token.IsCancellationRequested) {
                    return;
                }
                byte[] contentBytes = response.GetContent ();
                string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
                if (string.IsNullOrEmpty (jsonResponse)) {
                    throw new WebException("No data returned");
                }
                try {
                    var versions = JsonConvert.DeserializeObject<List<SFDCVersion>> (jsonResponse);
                    SetServer (versions);
                } catch (Exception ex) {
                    GetVersionsError(ex, token);
                }
            }), GetVersionsError, CToken);
        }

        protected void GetVersionsError (Exception ex, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            Log.Error (Log.LOG_SFDC, "Could not get SFDC Versions request: {0}", ex);
        }

        public static void PossiblyCreateServer (int accountId, CancellationToken cToken)
        {
            var fsAccount = McAccount.QueryById<McAccount> (accountId);
            var fsServer = McServer.QueryByAccountId<McServer> (accountId).FirstOrDefault ();
            var cmd = new SFDCGetApiVersions (cToken);
            cmd.GetVersions ((versions) => {
                if (fsServer != null) {
                    bool foundServerVersion = false;
                    foreach (var version in versions) {
                        if (fsServer.Path == version.url) {
                            foundServerVersion = true;
                            break;
                        }
                    }
                    if (!foundServerVersion) {
                        Log.Error (Log.LOG_SFDC, "Old server with path {0} no longer in version list. Uh...");
                        fsServer.Delete ();
                        fsServer = null;
                    }
                }
                if (fsServer == null) {
                    var version = versions.OrderByDescending (x => x.version).First ();
                    fsServer = new McServer () {
                        AccountId = fsAccount.Id,
                        Host = SFDCApiServicesHost,
                        Scheme = "https",
                        Port = 443,
                        Path = version.url, // they call it 'url' but it's really the path.
                        Capabilities = fsAccount.AccountCapability,
                    };
                    fsServer.Insert ();
                }
            });
        }
    }
}

