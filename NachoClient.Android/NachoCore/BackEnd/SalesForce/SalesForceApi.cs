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

namespace NachoCore
{
    public class SalesForceApiCommand
    {
        protected McServer Server;
        protected McCred Cred;
        protected CancellationToken CToken;

        protected const int KDefaultTimeout = 2;

        public SalesForceApiCommand (McServer server, McCred cred, CancellationToken cToken)
        {
            Server = server;
            Cred = cred;
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

        protected NcHttpRequest NewRequest (HttpMethod method, string commandPath)
        {
            var request = new NcHttpRequest (method, new Uri (Path.Combine (Server.BaseUriString (), commandPath)));
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.Headers.Add ("Authorization", string.Format ("Bearer {0}", Cred.GetAccessToken ()));
            return request;
        }

        protected void SFDCHttpError (Exception ex, CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested) {
                return;
            }
            Log.Error (Log.LOG_SFDC, "Could not do SFDC request: {0}", ex);
        }
    }

    public class SalesForceGetVersionsCommand
    {
        protected CancellationToken CToken;

        public SalesForceGetVersionsCommand (CancellationToken cToken)
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
                    throw new System.Net.WebException("No data returned");
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
            var cmd = new SalesForceGetVersionsCommand (cToken);
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

