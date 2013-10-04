using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync {
	public class AsOptionsCommand : AsCommand {
		public AsOptionsCommand (IAsDataSource dataSource) : base ("Options", dataSource) {
			Method = HttpMethod.Options;
		}

		protected override uint ProcessResponse (HttpResponseMessage response) {
			if(ProcessOptionsHeaders (response.Headers, m_dataSource)) {
				return (uint)Ev.Success;
			}
			return (uint)Ev.HardFail;
		}

		protected override string QueryString () {
			return "";
		}

		internal static void SetOldestProtoVers (IAsDataSource dataSource) {
			dataSource.ProtocolState.AsProtocolVersion = "12.0";
		}

		internal static bool ProcessOptionsHeaders(HttpResponseHeaders headers, IAsDataSource dataSource) {
			IEnumerable<string> values;
			bool retval = headers.TryGetValues ("MS-ASProtocolVersions", out values);
			foreach (var value in values) {
				float[] float_versions = Array.ConvertAll(value.Split (','), x => float.Parse (x));
				Array.Sort (float_versions);
				Array.Reverse (float_versions);
				string[] versions = Array.ConvertAll(float_versions, x => x.ToString ("0.0"));
				dataSource.ProtocolState.AsProtocolVersion = versions[0];
				// NOTE: We don't have any reason to do anything with MS-ASProtocolCommands yet.
			}
			return retval;
		}
	}
}

