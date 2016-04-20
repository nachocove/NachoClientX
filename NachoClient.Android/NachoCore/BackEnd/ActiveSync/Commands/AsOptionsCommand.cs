using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NachoCore.Model;
using NachoCore.Utils;
using System.Text.RegularExpressions;

namespace NachoCore.ActiveSync
{
    public class AsOptionsCommand : AsCommand
    {
        public AsOptionsCommand (IBEContext beContext) : base ("Options",  beContext)
        {
        }

        public override double TimeoutInSeconds {
            get {
                return AsAutodiscoverCommand.TestTimeoutSecs;
            }
        }

        public override bool DoSendPolicyKey (AsHttpOperation Sender)
        {
            return false;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken)
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

        public override bool IgnoreBody (AsHttpOperation Sender)
        {
            return true;
        }

        internal static void SetOldestProtoVers (IBEContext beContext)
        {
            McProtocolState protocolState = beContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsProtocolVersion = "12.0";
                return true;
            });
        }

        // The ActiveSync protocol versions that are supported by this app, from newest to oldest
        // (or from most preferred to least preferred).
        private static string[] SupportedVersions = new string[] { "14.1", "14.0", "12.1", "12.0" };

        internal static bool ProcessOptionsHeaders (NcHttpHeaders headers, IBEContext beContext)
        {
            IEnumerable<string> values = null;
            McProtocolState protocolState = beContext.ProtocolState;
            bool retval = headers.TryGetValues ("MS-ASProtocolVersions", out values);
            if (retval && null != values && 0 < values.Count ()) {
                if (1 != values.Count ()) {
                    Log.Warn (Log.LOG_AS, "AsOptionsCommand: more than one MS-ASProtocolVersions header.");
                }
                var value = values.First ();
                string[] serverVersions = Regex.Split (value, @"\s*,\s*");
                // Loop through the protocol versions that the app understands, in the preferred order, until
                // we find one that the server also supports.
                bool foundMatch = false;
                foreach (var supportedVersion in SupportedVersions) {
                    if (serverVersions.Contains (supportedVersion)) {
                        foundMatch = true;
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.AsProtocolVersion = supportedVersion;
                            return true;
                        });
                        break;
                    }
                }
                if (!foundMatch) {
                    Log.Error (Log.LOG_AS, "AsOptionsCommand: MS-ASProtocolVersions does not contain a version supported by this client. Defaulting to version 12.0.");
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.AsProtocolVersion = "12.0";
                        return true;
                    });
                }
                Log.Info (Log.LOG_AS, "AsOptionsCommand: Selected version {0} from MS-ASProtocolVersions: {1}", protocolState.AsProtocolVersion, value);
            } else {
                Log.Error (Log.LOG_AS, "AsOptionsCommand: Could not retrieve MS-ASProtocolVersions. Defaulting to 12.0");
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.AsProtocolVersion = "12.0";
                    return true;
                });
            }
            values = null;
            retval = headers.TryGetValues ("MS-ASProtocolCommands", out values);
            if (retval && null != values && 0 < values.Count ()) {
                if (1 != values.Count ()) {
                    Log.Warn (Log.LOG_AS, "AsOptionsCommand: more than one MS-ASProtocolVersions header.");
                }
                var value = values.First ();
                Log.Info (Log.LOG_AS, "AsOptionsCommand: MS-ASProtocolCommands: {0}", value);
                string[] commands = value.Split (',');
                // TODO: check for other potentially missing commands. ensure that all fundamental commands are listed.
                if (!commands.Contains ("Provision")) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.DisableProvisionCommand = true;
                        return true;
                    });
                }
            }
            // Rather than just fail, make conservative assumptions.
            return true;
        }
    }
}

