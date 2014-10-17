using System;
using System.Collections.Generic;
using System.Linq;
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
            IEnumerable<string> values = null;
            McProtocolState protocolState = beContext.ProtocolState;
            bool retval = headers.TryGetValues ("MS-ASProtocolVersions", out values);
            if (retval && null != values && 0 < values.Count ()) {
                // numerically sort and pick the highest version.
                if (1 != values.Count ()) {
                    Log.Warn (Log.LOG_AS, "AsOptionsCommand: more than one MS-ASProtocolVersions header.");
                }
                var value = values.First ();
                float[] float_versions = Array.ConvertAll (value.Split (','), x => float.Parse (x));
                Array.Sort (float_versions);
                Array.Reverse (float_versions);
                string[] versions = Array.ConvertAll (float_versions, x => x.ToString ("0.0"));
                protocolState.AsProtocolVersion = versions [0];
            } else {
                Log.Error (Log.LOG_AS, "AsOptionsCommand: Could not retrieve MS-ASProtocolVersions. Defaulting to 12.0");
                protocolState.AsProtocolVersion = "12.0";
            }
            values = null;
            retval = headers.TryGetValues ("MS-ASProtocolCommands", out values);
            if (retval && null != values && 0 < values.Count ()) {
                if (1 != values.Count ()) {
                    Log.Warn (Log.LOG_AS, "AsOptionsCommand: more than one MS-ASProtocolVersions header.");
                }
                var value = values.First ();
                string[] commands = value.Split (',');
                // TODO: check for other potentially missing commands. ensure that all fundamental commands are listed.
                if (!commands.Contains ("Provision")) {
                    protocolState.DisableProvisionCommand = true;
                }
            }
            beContext.ProtocolState = protocolState;
            // Rather than just fail, make conservative assumptions.
            return true;
        }
    }
}

