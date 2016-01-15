//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System;

namespace NachoCore
{
    public class SFDCGetContactsCommand : SFDCCommand
    {
        public SFDCGetContactsCommand (IBEContext beContext) : base (beContext)
        {
        }
        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);

            var request = NewRequest (HttpMethod.Get, string.Format ("{0}/Contact/", protoControl.ApiPaths["sobjects"]));
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC");
        }
    }

    public class SFDCGetContactIdsCommand : SFDCCommand
    {
        public SFDCGetContactIdsCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);

            var query = "SELECT+Id+Contact";
            var request = NewRequest (HttpMethod.Get, string.Format ("{0}?q={1}", protoControl.ApiPaths["query"], query));
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL1");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC1");
        }
    }

    public class SFDCGetContactCommand2 : SFDCCommand
    {
        public SFDCGetContactCommand2 (IBEContext beContext) : base (beContext)
        {
        }
        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);

            var request = NewRequest (HttpMethod.Get, string.Format ("{0}/Contact/{1}", protoControl.ApiPaths["sobjects"], 1));
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCCONTFAIL2");
            }
            return Event.Create ((uint)SmEvt.E.Success, "SFDCCONTACSUCC2");
        }
    }
}

