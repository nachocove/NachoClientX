//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.SFDC
{
    public class SFDCGetResourcesCommand : SFDCCommand
    {
        public SFDCGetResourcesCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override void MakeAndSendRequest ()
        {
            var request = NewRequest (HttpMethod.Get, jsonContentType);
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            try {
#if DEBUG
                Log.Info (Log.LOG_SFDC, "SFDCGetResourcesCommand Response: {0}", jsonResponse);
#endif
                var resourcesResponse = JsonConvert.DeserializeObject <Dictionary<string,string>> (jsonResponse);
                if (!resourcesResponse.ContainsKey ("query") || !resourcesResponse.ContainsKey ("sobjects")) {
                    Log.Error (Log.LOG_SFDC, "Resources response does not contain 'query': {0}", string.Join (",", resourcesResponse.ToList ()));
                    return Event.Create ((uint)SmEvt.E.HardFail, "RESMISSINGHARD");
                }
                var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
                NcAssert.NotNull (protoControl);
                NcAssert.NotNull (protoControl.SFDCSetup);
                protoControl.SFDCSetup.ResourcePaths = resourcesResponse;
                return Event.Create ((uint)SmEvt.E.Success, "RESSSUCC");
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }
    }
}

