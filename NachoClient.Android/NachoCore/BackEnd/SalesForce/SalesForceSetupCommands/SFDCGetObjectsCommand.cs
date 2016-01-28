//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using NachoCore.Utils;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Linq;

namespace NachoCore
{
    public class SFDCGetObjectsCommand : SFDCCommand
    {
        public SFDCGetObjectsCommand (IBEContext beContext) : base (beContext)
        {
        }
        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            NcAssert.NotNull (protoControl.SFDCSetup);

            var request = NewRequest (HttpMethod.Get, protoControl.SFDCSetup.ResourcePaths ["sobjects"], jsonContentType);
            GetRequest (request);
        }
        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCOBJNORESP");
            }
            try {
                var objectInfo = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);
                var sobjects = objectInfo.SelectToken ("sobjects");
                var objects = new Dictionary<string, string> ();
                foreach (var obj in sobjects) {
                    var urls = obj.SelectToken ("urls");
                    objects[(string)obj["label"]] = (string)urls["sobject"];
                }
                var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
                NcAssert.NotNull (protoControl);
                NcAssert.NotNull (protoControl.SFDCSetup);
                protoControl.SFDCSetup.ObjectUrls = objects;
                if (objects.ContainsKey ("Contact")) {
                    return Event.Create ((uint)SmEvt.E.Success, "SFDCOBJSUCC");
                } else {
                    // FIXME: Tell the UI We don't have any contacts
                    Log.Warn (Log.LOG_SFDC, "{0}: No Contact type in sobjects. Can't sync", CmdName);
                    return Event.Create ((uint)SmEvt.E.Success, "SFDCOBJIDLE");
                }
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }
    }
}

