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

namespace NachoCore
{
    public class SalesForceApiCommand
    {
        McServer Server;
        McCred Cred;
        CancellationToken CToken;

        const int KDefaultTimeout = 2;
        //const string SFDCApiBaseUrl = "https://na1.salesforce.com";

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

        void AttemptHttpSuccess (NcHttpResponse response, CancellationToken token)
        {
            
        }

        void SFDCHttpError (Exception ex, CancellationToken cToken)
        {
            
        }

        #region GetVersions

        public void GetVersions ()
        {
            var request = NewRequest (HttpMethod.Get, "/services/data/");
            HttpClient.SendRequest (request, KDefaultTimeout, GetVersionsSuccess, SFDCHttpError, CToken);
        }

        void GetVersionsSuccess (NcHttpResponse response, CancellationToken token)
        {
            
        }
        #endregion
    }
}

