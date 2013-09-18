using System;

namespace NachoCore.ActiveSync {
	public class Xml {
		public class AirSync {
			public const string Ns = "AirSync";
			// Alpha order.
			public const string Add = "Add";
			public const string ApplicationData = "ApplicationData";
			public const string Collection = "Collection";
			public const string CollectionId = "CollectionId";
			public const string Collections = "Collections";
			public const string Commands = "Commands";
			public const string GetChanges = "GetChanges";
			public const string ServerId = "ServerId";
			public const string Status = "Status";
			public const string Sync = "Sync";
			public const string SyncKey = "SyncKey";
			public const string SyncKey_Initial = "0";

			public enum StatusCode : uint {Success=1, SyncKeyInvalid=3, ProtocolError=4, ServerError=5, ClientError=6,
				ServerWins=7, NotFound=8, NoSpace=9, FolderChange=12, ResendFull=13, LimitReWait=14, TooMany=15,
				Retry=16};
		}
		public class AirSyncBase {
			public const string Ns = "AirSyncBase";
			// Alpha order.
			public const string Body = "Body";
			public const string Data = "Data";
			public const string Type = "Type";
		}
		public class FolderHierarchy {
			public const string Ns = "FolderHierarchy";
			// Alpha order.
			public const string Add = AirSync.Add;
			public const string Changes = "Changes";
			public const string Delete = "Delete";
			public const string DisplayName = "DisplayName";
			public const string FolderSync = "FolderSync";
			public const string ParentId = "ParentId";
			public const string ServerId = AirSync.ServerId;
			public const string Status = AirSync.Status;
			public const string SyncKey = AirSync.SyncKey;
			public const string Type = AirSyncBase.Type;
			public const string Update = "Update";

			public enum StatusCode : uint {Success=1, Retry=6, ReSync=9, BadFormat=10, Unknown=11, ServerFail=12};
			// FIXME: with Unknown, we need to retry & watch for a loop.
		}
	}
}

