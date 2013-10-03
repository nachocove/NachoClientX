using System;

namespace NachoCore.ActiveSync {
	public class Xml {
		public class AirSync {
			public const string Ns = "AirSync";
			// Alpha order.
			public const string Add = "Add";
			public const string ApplicationData = "ApplicationData";
			public const string Class = "Class";
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

			public class ClassCode {
				public const string Tasks = "Tasks";
				public const string Email = "Email";
				public const string Calendar = "Calendar";
				public const string Contacts = "Contacts";
				public const string Notes = "Notes";
				public const string SMS = "SMS";
			}
		}
		public class AirSyncBase {
			public const string Ns = "AirSyncBase";
			// Alpha order.
			public const string Body = "Body";
			public const string Data = "Data";
			public const string NativeBodyType = "NativeBodyType";
			public const string Type = "Type";

			// NOTE that TypeCode is for both Type and NativeBodyType.
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
		public class Contacts {
			public const string Ns = "Contacts";
			// Alpha order.
			public const string Anniversary = "Anniversary"; // UTC.
			public const string AssistantName = "AssistantName";
			public const string AssistantPhoneNumber = "AssistantPhoneNumber";
			public const string Birthday = "Birthday"; // UTC.
			// NOTE: AirSyncBase.Body is used to contain notes for the contact.
			public const string BusinessAddressCity = "BusinessAddressCity";
			public const string BusinessAddressCountry = "BusinessAddressCountry";
			public const string BusinessAddressPostalCode = "BusinessAddressPostalCode";
			public const string BusinessAddressState = "BusinessAddressState";
			public const string BusinessAddressStreet = "BusinessAddressStreet";
			public const string BusinessFaxNumber = "BusinessFaxNumber";
			public const string BusinessPhoneNumber = "BusinessPhoneNumber";
			public const string Business2PhoneNumber = "Business2PhoneNumber";
			public const string CarPhoneNumber = "CarPhoneNumber";
			public const string Categories = "Categories"; // Container of 1..300 Category.
			public const string Category = "Category";
			public const string Children = "Children"; // Container of 0..300 Child.
			public const string Child = "Child";
			public const string CompanyName = "CompanyName";
			public const string Department = "Department";
			public const string Email1Address = "Email1Address";
			public const string Email2Address = "Email2Address";
			public const string Email3Address = "Email3Address";
			public const string FileAs = "FileAs"; // RIC - understand.
			public const string FirstName = "FirstName";
			public const string HomeAddressCity = "HomeAddressCity";
			public const string HomeAddressCountry = "HomeAddressCountry";
			public const string HomeAddressPostalCode = "HomeAddressPostalCode";
			public const string HomeAddressState = "HomeAddressState";
			public const string HomeAddressStreet = "HomeAddressStreet";
			public const string HomeFaxNumber = "HomeFaxNumber";
			public const string HomePhoneNumber = "HomePhoneNumber";
			public const string Home2PhoneNumber = "Home2PhoneNumber";
			public const string JobTitle = "JobTitle";
			public const string LastName = "LastName";
			public const string MiddleName = "MiddleName";
			public const string MobilePhoneNumber = "MobilePhoneNumber";
			public const string OfficeLocation = "OfficeLocation";
			public const string OtherAddressCity = "OtherAddressCity";
			public const string OtherAddressCountry = "OtherAddressCountry";
			public const string OtherAddressPostalCode = "OtherAddressPostalCode";
			public const string OtherAddressState = "OtherAddressState";
			public const string OtherAddressStreet = "OtherAddressStreet";
			public const string PagerNumber = "PagerNumber";
			public const string Picture = "Picture"; // base64-encoded img, <= 48kB.
			public const string RadioPhoneNumber = "RadioPhoneNumber";
			public const string Spouse = "Spouse";
			public const string Suffix = "Suffix";
			public const string Title = "Title";
			public const string WebPage = "WebPage";
			public const string WeightedRank = "WeightedRank"; // Only in RIC response, int not string.
			public const string YomiCompanyName = "YomiCompanyName";
			public const string YomiFirstName = "YomiFirstName";
			public const string YomiLastName = "YomiLastName";

		}
		public class Contacts2 {
			public const string Ns = "Contacts2";
			// Alpha order.
			public const string CompanyMainPhone = "CompanyMainPhone";
			public const string CustomerId = "CustomerId";
			public const string GovernmentId = "GovernmentId";
			public const string IMAddress = "IMAddress";
			public const string IMAddress2 = "IMAddress2";
			public const string IMAddress3 = "IMAddress3";
			public const string ManagerName = "ManagerName";
			public const string MMS = "MMS";
			public const string NickName = "NickName";
		}
		public class Email {
			public const string Ns = AirSync.ClassCode.Email;
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

			// FIXME: with Unknown, we need to retry & watch for a loop.
			public enum StatusCode : uint {Success=1, Retry=6, ReSync=9, BadFormat=10, Unknown=11, ServerFail=12};
			// FIXME: Ric is not supported for AS version 12.1.

			public enum TypeCode : uint {UserCreatedGeneric=1, DefaultInbox=2, DefaultDrafts=3, DefaultDeleted=4,
				DefaultSent=5, DefaultOutbox=6, DefaultTasks=7, DefaultCal=8, DefaultContacts=9, DefaultNotes=10,
				DefaultJournal=11, UserCreatedMail=12, UserCreatedCal=13, UserCreatedContacts=14, UserCreatedTasks=15,
				UserCreatedJournal=16, UserCreatedNotes=17, Unknown=18, Ric=19};

			public static string TypeCodeToAirSyncClassCode (uint code) {
				switch (code) {
				case (uint)TypeCode.UserCreatedGeneric:
				case (uint)TypeCode.DefaultJournal:
				case (uint)TypeCode.UserCreatedJournal:
				case (uint)TypeCode.Unknown:
				case (uint)TypeCode.Ric:
					//FIXME - we don't know what to do with these yet.
					throw new Exception ();

				case (uint)TypeCode.DefaultInbox:
				case (uint)TypeCode.DefaultDrafts:
				case (uint)TypeCode.DefaultDeleted:
				case (uint)TypeCode.DefaultSent:
				case (uint)TypeCode.DefaultOutbox:
				case (uint)TypeCode.UserCreatedMail:
					return AirSync.ClassCode.Email;

				case (uint)TypeCode.DefaultTasks:
				case (uint)TypeCode.UserCreatedTasks:
					return AirSync.ClassCode.Tasks;

				case (uint)TypeCode.DefaultCal:
				case (uint)TypeCode.UserCreatedCal:
					return AirSync.ClassCode.Calendar;

				case (uint)TypeCode.DefaultContacts:
				case (uint)TypeCode.UserCreatedContacts:
					return AirSync.ClassCode.Contacts;

				case (uint)TypeCode.DefaultNotes:
				case (uint)TypeCode.UserCreatedNotes:
					return AirSync.ClassCode.Notes;
				}
				throw new Exception ();
			}
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

