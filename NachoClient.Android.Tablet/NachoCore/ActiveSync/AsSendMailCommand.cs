using System;
using System.Collections.Generic;
using System.Globalization;
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
		                          Dictionary<string,string> message) : base(Xml.ComposeMail.SendMail, dataSource) {
			m_body = message ["body"];
			message.Remove ("body");
			m_headers = message;
			DateTime date = DateTime.UtcNow;
			m_headers ["date"] = date.ToString ("ddd, dd MMM yyyy HH:mm:ss K",
			                                    DateTimeFormatInfo.InvariantInfo);
		}

		protected override XDocument ToXDocument () {
			if (14.0 > Convert.ToDouble (m_dataSource.ProtocolState.AsProtocolVersion)) {
				return null;
			}
			XNamespace ns = Xml.ComposeMail.Ns;
			var sendMail = new XElement (ns + Xml.ComposeMail.SendMail, 
			                             // FIXME - ClientId.
			                             new XElement (ns + Xml.ComposeMail.SaveInSentItems),
			                             new XElement (ns + Xml.ComposeMail.Mime, ToMime ()));
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

