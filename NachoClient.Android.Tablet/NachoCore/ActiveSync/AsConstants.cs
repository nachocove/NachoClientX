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
			public const string Delete = "Delete";
			public const string DeleteAsMoves = "DeleteAsMoves";
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

			public enum TypeCode : uint {PlainText=1, Html=2, Rtf = 3, /* Data element will be base64-encoded. */ Mime = 4};
		}
		public class ComposeMail {
			public const string Ns = "ComposeMail";
			// Alpha order.
			public const string SendMail = "SendMail";
			public const string ClientId = "ClientId";
			public const string AccountId = "AccountId";
			public const string SaveInSentItems = "SaveInSentItems";
			public const string Mime = "Mime";
		}
		public class Email {
			public const string Ns = "Email";
			// Alpha order.
			public const string DateReceived = "DateReceived";
			public const string DisplayTo = "DisplayTo";
			public const string From = "From";
			public const string Importance = "Importance";
			public const string MessageClass = "MessageClass";
			public const string Read = "Read";
			public const string ReplyTo = "ReplyTo";
			public const string Subject = "Subject";
			public const string To = "To";
		}
		public class Email2 {
			public const string Ns = "Email2";
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
		public class Ping {
			public const string Ns = "Ping";
			// Alpha order.
			public const string Class = "Class";
			public const string Folder = "Folder";
			public const string Folders = "Folders";
			public const string HeartbeatInterval = "HeartbeatInterval";
			public const string Id = "Id";
			public const string MaxFolders = "MaxFolders";
			public const string Status = AirSync.Status;

			public enum StatusCode : uint {NoChanges=1, Changes=2, MissingParams=3, SyntaxError=4, BadHeartbeat=5, 
				TooManyFolders=6, NeedFolderSync=7, ServerError=8};
		}
	}
}

