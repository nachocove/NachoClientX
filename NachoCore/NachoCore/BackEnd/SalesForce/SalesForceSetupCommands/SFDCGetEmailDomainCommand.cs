//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using NachoCore.Model;
using System.Linq;

namespace NachoCore.SFDC
{
    public class SFDCGetEmailDomainCommand : SFDCCommand
    {
        public const string McMutablesKey = "EmailToSalesforce";

        public SFDCGetEmailDomainCommand (IBEContext beContext) : base (beContext)
        {
        }
        protected override void MakeAndSendRequest ()
        {
            var protoControl = BEContext.ProtoControl as SalesForceProtoControl;
            NcAssert.NotNull (protoControl);
            NcAssert.NotNull (protoControl.SFDCSetup);

            var request = NewRequest (HttpMethod.Get, string.Format ("{0}?q={1}", protoControl.SFDCSetup.ResourcePaths ["query"], EmailServicesAddressRecord.GetQuery ()), jsonContentType);
            GetRequest (request);
        }

        protected override Event ProcessSuccessResponse (NcHttpResponse response, CancellationToken token)
        {
            byte[] contentBytes = response.GetContent ();
            string jsonResponse = (null != contentBytes && contentBytes.Length > 0) ? Encoding.UTF8.GetString (contentBytes) : null;
            if (string.IsNullOrEmpty (jsonResponse)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "SFDCEMAILNORESP");
            }
            try {
#if DEBUG
                Log.Info (Log.LOG_SFDC, "SFDCGetEmailDomainCommand Response: {0}", jsonResponse);
#endif

                var responseData = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);
                var jsonRecords = responseData.SelectToken ("records");
                var emailAddressRecord = jsonRecords.ToObject<List<EmailServicesAddressRecord>> ().FirstOrDefault ();
                if (null != emailAddressRecord) {
                    var emailString = string.Format ("{0}@{1}", emailAddressRecord.LocalPart, emailAddressRecord.EmailDomainName);
                    McMutables.Set (AccountId, SalesForceProtoControl.McMutablesModule, McMutablesKey, emailString);
                } else {
                    McMutables.Delete (AccountId, SalesForceProtoControl.McMutablesModule, McMutablesKey);
                }
                return Event.Create ((uint)SmEvt.E.Success, "SFDCEMAILSUCC");
            } catch (JsonReaderException) {
                return ProcessErrorResponse (jsonResponse);
            }
        }

        public class EmailServicesAddressRecord
        {
            public SFDCCommand.RecordAttributes attributes { get; set; }

            public string LocalPart { get; set; }
            public string EmailDomainName { get; set; }

            public override string ToString ()
            {
                return string.Format ("[EmailServicesAddressRecord: attributes={0}, LocalPart={1}, EmailDomainName={2}]", attributes, LocalPart, EmailDomainName);
            }

            public static string GetQuery ()
            {
                // http://salesforce.stackexchange.com/questions/948/users-bcc-to-salesforce-address-via-soql-or-api
                var query = "Select LocalPart, EmailDomainName From EmailServicesAddress Where Function.FunctionName = 'EmailToSalesforce'";
                return Regex.Replace (query, " ", "+");
            }
        }

    }
}

