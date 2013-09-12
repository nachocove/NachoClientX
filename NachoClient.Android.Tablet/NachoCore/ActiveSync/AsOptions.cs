using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync {
	public class AsOptions {
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
				ProcessOptionsHeaders (response.Headers);
				sm.ProcEvent ((uint)Ev.Success);
			}
			catch (OperationCanceledException ex) {}
			catch {
				sm.ProcEvent ((uint)Ev.Failure);
			}
		}
		public void Cancel() {
			m_cts.Cancel ();
		}
		private void ProcessOptionsHeaders(HttpResponseHeaders headers) {
			IEnumerable<string> values;
			headers.TryGetValues ("MS-ASProtocolVersions", out values);
			foreach (var value in values) {
				float[] float_versions = Array.ConvertAll(value.Split (','), x => float.Parse (x));
				Array.Sort (float_versions);
				Array.Reverse (float_versions);
				string[] versions = Array.ConvertAll(float_versions, x => x.ToString ("0.0"));
				m_dataSource.ProtocolState.AsProtocolVersion = versions[0];
				//FIXME: Figure out how to use supported_commands: response.headers["MS-ASProtocolCommands"].split pattern=",").sort

			}
		}
	}
}

