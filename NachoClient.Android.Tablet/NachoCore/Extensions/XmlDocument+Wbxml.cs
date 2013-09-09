using System;
using System.IO;
using System.Xml.Linq;

namespace NachoCore.Wbxml
{
	public static class XDocument_Wbxml
	{
		public static byte[] ToWbxml (this XDocument doc)
		{
			ASWBXML encoder = new ASWBXML();
			encoder.LoadDoc(doc);
			return encoder.GetBytes();
		}
		public static XDocument LoadWbxml(this XDocument doc, byte[] wbxml)
		{
			ASWBXML decoder = new ASWBXML();
			decoder.LoadBytes(wbxml);
			return XDocument.Parse (decoder.GetXml ());
		}
	}
}

