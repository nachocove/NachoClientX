using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync {
	public class AsOptions : IAsCommand {
		private IAsDataSource m_dataSource;
		private CancellationTokenSource m_cts;
		public AsOptions(IAsDataSource dataSource) {
			m_dataSource = dataSource;
			m_cts = new CancellationTokenSource();
		}
		public async void Execute(StateMachine sm)
		{
			var client = AsCommand.HttpClientFactory (new HttpClientHandler());
			try {
				var response = await client.SendAsync 
					(new HttpRequestMessage (HttpMethod.Options,
					                         AsCommand.BaseUri(m_dataSource.Server)),
					 m_cts.Token);
				ProcessOptionsHeaders (response.Headers, m_dataSource);
				sm.PostEvent ((uint)Ev.Success);
			}
			catch (OperationCanceledException) {
				Console.WriteLine ("as:options: OperationCanceledException");
			}
			catch (WebException) {
				sm.PostEvent ((uint)Ev.TempFail);
			}
			catch {
				sm.PostEvent ((uint)Ev.HardFail);
			}
		}
		public void Cancel() {
			m_cts.Cancel ();
		}
		internal static void SetOldestProtoVers (IAsDataSource dataSource) {
			dataSource.ProtocolState.AsProtocolVersion = "12.0";
		}
		internal static void ProcessOptionsHeaders(HttpResponseHeaders headers, IAsDataSource dataSource) {
			IEnumerable<string> values;
			headers.TryGetValues ("MS-ASProtocolVersions", out values);
			foreach (var value in values) {
				float[] float_versions = Array.ConvertAll(value.Split (','), x => float.Parse (x));
				Array.Sort (float_versions);
				Array.Reverse (float_versions);
				string[] versions = Array.ConvertAll(float_versions, x => x.ToString ("0.0"));
				dataSource.ProtocolState.AsProtocolVersion = versions[0];
				// NOTE: We don't have any reason to do anything with MS-ASProtocolCommands yet.
			}
		}
	}
}

