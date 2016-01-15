//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore
{
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
            var pathsResponse = JsonConvert.DeserializeObject<Dictionary<string, string>> (jsonResponse);
            if (!pathsResponse.ContainsKey ("query") || !pathsResponse.ContainsKey ("sobjects")) {
                Log.Error (Log.LOG_SFDC, "Path response does not contain 'query': {0}", string.Join (",", pathsResponse.ToList ()));
                return Event.Create ((uint)SmEvt.E.HardFail, "PATHSMISSINGHARD");
            }
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            protoControl.ApiPaths = pathsResponse;
            return Event.Create ((uint)SmEvt.E.Success, "PATHSSUCC");
        }
    }
}

