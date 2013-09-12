using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSendMailCommand : AsCommand
	{
		private const string CrLf = "\r\n";
		private string m_body;
		private Dictionary<string,string> m_headers;

		public AsSendMailCommand (IAsDataSource dataSource, 
		                          Dictionary<string,string> headers,
		                          string body) : base("SendMail", dataSource) {
			m_headers = headers;
			m_body = body;
		}

		protected override XDocument ToXDocument () {
			if (14.0 > Convert.ToDouble (m_dataSource.ProtocolState.AsProtocolVersion)) {
				return null;
			}
			XNamespace ns = "ComposeMail";
			var sendMail = new XElement (ns + "SendMail", 
			                             new XElement (ns + "SaveInSentItems"),
			                             new XElement (ns + "Mime", ToMime ()));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sendMail);
			return doc;
		}

		protected override string ToMime () {
			string mimeHeaders = string.Join (CrLf, m_headers.Select (kv => kv.Key.ToLower ().ToCapitalized () + ": " + kv.Value));
			return mimeHeaders + CrLf + CrLf + m_body;
		}
	}
}

