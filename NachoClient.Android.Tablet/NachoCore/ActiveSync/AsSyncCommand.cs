using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSyncCommand : AsCommand
	{
		public const string SyncKeyInitial = "0";

		private enum StatusSync : uint {Success=1, SyncKeyInvalid=3, ProtocolError=4, ServerError=5, ClientError=6,
			ServerWins=7, NotFound=8, NoSpace=9, FolderChange=12, ResendFull=13, LimitReWait=14, TooMany=15,
			Retry=16};

		public AsSyncCommand (IAsDataSource dataSource) : base("Sync", dataSource) {}

		protected override XDocument ToXDocument () {
			var doc = AsCommand.ToEmptyXDocument();
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			return (uint)Ev.Success; // FIXME
		}
	}
}

