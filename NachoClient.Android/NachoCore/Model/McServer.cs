using System;
using System.Linq;
using SQLite;

namespace NachoCore.Model
{
	public class McServer : McObject
	{
		public McServer ()
		{
			Path = "/Microsoft-Server-ActiveSync";
			Scheme = "https";
			Port = 443;
		}

		public string Host { get; set; }

		public string Path { get; set; }

		public string Scheme { get; set; }

		public int Port { get; set; }

		public bool UsedBefore { get; set; }

		public static McServer Create (Uri uri)
		{
			return new McServer () {
				Host = uri.Host,
				Path = uri.AbsolutePath,
				Scheme = uri.Scheme,
				Port = uri.Port
			};
		}

        public void CopyFrom (McServer src)
		{
			Host = src.Host;
			Path = src.Path;
			Scheme = src.Scheme;
			Port = src.Port;
		}

		public static McServer QueryById (int id)
		{
            return NcModel.Instance.Db.Table<McServer> ().Where (rec => id == rec.Id).SingleOrDefault ();
		}

		public static McServer QueryByHost (string host)
		{
            return NcModel.Instance.Db.Table<McServer> ().Where (x => host == x.Host).SingleOrDefault ();
		}
	}
}
