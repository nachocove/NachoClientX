using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsOptionsCommand : AsCommand
    {
        public AsOptionsCommand (IBEContext beContext) : base ("Options",  beContext)
        {
        }

        public override bool DoSendPolicyKey (AsHttpOperation Sender)
        {
            return false;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            if (ProcessOptionsHeaders (response.Headers, BEContext)) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsOptionsSuccess));
                return Event.Create ((uint)SmEvt.E.Success, "OPTSUCCESS");
            }
            return Event.Create ((uint)SmEvt.E.HardFail, "OPTHARD");
        }

        public override HttpMethod Method (AsHttpOperation Sender)
        {
            return HttpMethod.Options;
        }

        public override string QueryString (AsHttpOperation Sender)
        {
            return "";
        }

        internal static void SetOldestProtoVers (IBEContext beContext)
        {
            McProtocolState update = beContext.ProtocolState;
            update.AsProtocolVersion = "12.0";
            beContext.ProtocolState = update;
        }

        internal static bool ProcessOptionsHeaders (HttpResponseHeaders headers, IBEContext beContext)
        {
            IEnumerable<string> values;
            bool retval = headers.TryGetValues ("MS-ASProtocolVersions", out values);
            if (null != values) {
                foreach (var value in values) {
                    float[] float_versions = Array.ConvertAll (value.Split (','), x => float.Parse (x));
                    Array.Sort (float_versions);
                    Array.Reverse (float_versions);
                    string[] versions = Array.ConvertAll (float_versions, x => x.ToString ("0.0"));
                    McProtocolState update = beContext.ProtocolState;
                    update.AsProtocolVersion = versions [0];
                    beContext.ProtocolState = update;
                    // NOTE: We don't have any reason to do anything with MS-ASProtocolCommands yet.
                }
            }
            return retval;
        }
    }
}

