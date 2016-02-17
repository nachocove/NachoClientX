//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Http;
using NachoPlatform;

namespace NachoCore.SFDC
{
    public class SFDCGetApiVersionsCommand : SFDCCommand
    {
        public SFDCGetApiVersionsCommand (IBEContext beContext) : base (beContext)
        {
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


        const string SFDCApiServicesPath = "/services/data/";
        const double SFDCApiVersionWanted = 35.0;

        protected void GetVersionsError (Exception ex, CancellationToken token)
        {
            if (token.IsCancellationRequested) {
                return;
            }
            Log.Error (Log.LOG_SFDC, "Could not get SFDC Versions request: {0}", ex);
        }

        protected override void MakeAndSendRequest ()
        {
            var sfdcVersionsUri = new Uri (string.Format ("https://{0}{1}", BEContext.Server.Host, SFDCApiServicesPath));
            var request = new NcHttpRequest (HttpMethod.Get, sfdcVersionsUri);
            request.Headers.Add ("User-Agent", Device.Instance.UserAgent ());
            request.ContentType = "application/json";
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                throw new WebException("No data returned");
            }
            try {
                var versions = JsonConvert.DeserializeObject<List<SFDCVersion>> (jsonResponse);
                var apiDict = new Dictionary<double, SFDCVersion> ();
                foreach (var version in versions) {
                    apiDict[version.version] = version;
                }
                if (!apiDict.ContainsKey (SFDCApiVersionWanted)) {
                    Log.Error (Log.LOG_SFDC, "Could not find wanted version {0}: {1}", SFDCApiVersionWanted, string.Join (", ", versions));
                    return Event.Create ((uint)SmEvt.E.HardFail, "APIVERSIONNOTFOUND");
                }
                BEContext.Server.UpdateWithOCApply<McServer> ((record) => {
                    var target = (McServer)record;
                    target.Path = apiDict[SFDCApiVersionWanted].url; // they call it 'url' but it's really the path.
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "SFDCVERSIONSSUCC");
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }
    }
}

