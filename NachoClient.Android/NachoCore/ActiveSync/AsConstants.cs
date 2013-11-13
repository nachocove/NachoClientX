using System;

namespace NachoCore.ActiveSync {
	public class Xml {
		// These status code values are common to ALL commands, independent of namespace.
		public enum StatusCode {
			InvalidContent=101, // AsCommand. CODEFIX. HTTP 400 in 2007.
			First = InvalidContent,
			InvalidWBXML=102, // AsCommand. CODEFIX.
			InvalidXML=103, // AsCommand. CODEFIX.
			InvalidDateTime=104, // AsCommand. CODEFIX.
			InvalidCombinationOfIDs=105, // AsCommand subclass. RECOVER.
			InvalidIDs=106, // AsCommand subclass. RECOVER. HTTP 400 in 2007, except SendMail which was HTTP 500.
			InvalidMIME=107, // AsSendMailCommand. RECOVER.
			DeviceIdMissingOrInvalid=108, // AsCommand. CODEFIX.
			DeviceTypeMissingOrInvalid=109, // AsCommand. CODEFIX.
			ServerError=110, // We should NOT retry later. AsCommand => AsControl. ADMIN. HTTP 500 in 2007.
			ServerErrorRetryLater=111, // AsCommand => AsControl. RECOVER. HTTP 503 in 2007.
			ActiveDirectoryAccessDenied=112, // AsCommand => AsControl. ADMIN. HTTP 403 in 2007.
			MailboxQuotaExceeded=113, // AsCommand => AsControl. USER. HTTP 507 in 2007.
			MailboxServerOffline=114, // AsCommand => AsControl. ADMIN.
			SendQuotaExceeded=115, // AsCommand => AsControl. ADMIN.
			MessageRecipientUnresolved=116, // AsSendMailCommand. USER.
			MessageReplyNotAllowed=117, // AsSendMailCommand. USER.
			MessagePreviouslySent=118, // AsSendMailCommand. RECOVER.
			MessageHasNoRecipient=119, // AsSendMailCommand. USER.
			MailSubmissionFailed=120, // AsSendMailCommand. RECOVER.
			MessageReplyFailed=121, // AsSendMailCommand. RECOVER.
			AttachmentIsTooLarge=122, // AsSendMailCommand. USER.
			UserHasNoMailbox=123, // AsCommand => AsControl. ADMIN.
			UserCannotBeAnonymous=124, // AsCommand => AsControl. CODEFIX.
			UserPrincipalCouldNotBeFound=125, // AsCommand => AsControl. ADMIN.
			UserDisabledForSync=126, // AsCommand => AsControl. ADMIN.
			UserOnNewMailboxCannotSync=127, // AsCommand => AsControl. ADMIN.
			UserOnLegacyMailboxCannotSync=128, // AsCommand => AsControl. ADMIN.
			DeviceIsBlockedForThisUser=129, // AsCommand => AsControl. ADMIN.
			AccessDenied=130, // AsCommand. ADMIN.
			AccountDisabled=131, // AsCommand => AsControl. ADMIN.
			SyncStateNotFound=132, // Do a full re-sync. RECOVER. Let USER know. HTTP 500 in 2007, except Provision which was HTTP 403.
			SyncStateLocked=133, // AsCommand => AsControl. ADMIN.
			SyncStateCorrupt=134, // Do a full re-sync. RECOVER. Let USER/ADMIN know.
			SyncStateAlreadyExists=135, // Do a full re-sync. RECOVER. Let USER/ADMIN know.
			SyncStateVersionInvalid=136, // Do a full re-sync. RECOVER. Let USER/ADMIN know.
			CommandNotSupported=137, // AsControl. CODEFIX. HTTP 501 in 2007.
			VersionNotSupported=138, // AsControl. CODEFIX. HTTP 400 in 2007, except for 1.0 devices which got a 505.
			DeviceNotFullyProvisionable=139, // AsProvision. CODEFIX.
			RemoteWipeRequested=140, // AsCommand. Device should wipe via Provision commands. HTTP 449 in 2007, or 403 if no policy header.
			LegacyDeviceOnStrictPolicy=141, // AsCommand. ADMIN.
			DeviceNotProvisioned=142, // AsCommand. RECOVER (reprovision). HTTP 449 in 2007.
			PolicyRefresh=143, // AsCommand. RECOVER (reprovision).
			InvalidPolicyKey=144, // AsCommand. RECOVER (reprovision).
			ExternallyManagedDevicesNotAllowed=145, // AsCommand. CODEFIX.
			NoRecurrenceInCalendar=146, // AsCommand. CODEFIX.
			UnexpectedItemClass=147, // AsCommand. CODEFIX. HTTP 400 or 501 in 2007.
			RemoteServerHasNoSSL=148, // AsCommand. ADMIN.
			InvalidStoredRequest=149, // AsPingCommand? RECOVER.
			ItemNotFound=150, // SmartReply, SmartForward. RECOVER.
			TooManyFolders=151, // AsCommand. USER/ADMIN.
			NoFoldersFound=152, // AsCommand. ADMIN.
			ItemsLostAfterMove=153, // ItemOperations. USER/ADMIN. RECOVER.
			FailureInMoveOperation=154, // ItemOperations. USER/ADMIN. RECOVER.
			MoveCommandDisallowedForNonPersistentMoveAction=155, // ItemOperations. CODEFIX.
			MoveCommandInvalidDestinationFolder=156, // ItemOperations. RECOVER.
			// [157,159] unused.
			AvailabilityTooManyRecipients=160, // AsSendMailCommand. USER.
			AvailabilityDLLimitReached=161, // AsSendMailCommand. USER.
			AvailabilityTransientFailure=162, // USER/RECOVER.
			AvailabilityFailure=163, // USER/ADMIN.
			BodyPartPreferenceTypeNotSupported=164, // Where? CODEFIX.
			DeviceInformationRequired=165, // AsProvisionCommand. CODEFIX.
			InvalidAccountId=166, // Where? USER.
			AccountSendDisabled=167, // AsSendMailCommand. ADMIN.
			IRM_FeatureDisabled=168, // IGNORE: No RM yet.
			IRM_TransientError=169,
			IRM_PermanentError=170,
			IRM_InvalidTemplateID=171,
			IRM_OperationNotPermitted=172,
			NoPicture=173, // Where? RECOVER.
			PictureTooLarge=174, // Where? RECOVER.
			PictureLimitReached=175, // Where? RECOVER.
			BodyPart_ConversationTooLarge=176, // Where? RECOVER.
			MaximumDevicesReached=177, // AsCommand. USER/ADMIN.
			Last = MaximumDevicesReached
		};

        /* The following section is organized as follows:
         * 1) The order is AirSync, AirSyncBase, and then classes are in alpha order.
         * 2) In order to have only one version of any typed-in string, following classes refer to the
         * constants in preceding classes.
         */
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
            public const string MimeSupport = "MIMESupport";
			public const string MoreAvailable = "MoreAvailable";
            public const string Options = "Options";
			public const string ServerId = "ServerId";
			public const string Status = "Status";
			public const string Sync = "Sync";
			public const string SyncKey = "SyncKey";
			public const string SyncKey_Initial = "0";

			public class ClassCode {
				public const string Tasks = "Tasks";
				public const string Email = "Email";
				public const string Calendar = "Calendar";
				public const string Contacts = "Contacts";
				public const string Notes = "Notes";
				public const string Sms = "SMS";
			}
            public enum MimeSupportCode : uint {NoMime=0, SMimeOnly=1, AllMime=2};
            public enum StatusCode : uint {Success=1, SyncKeyInvalid=3, ProtocolError=4, ServerError=5, ClientError=6,
                ServerWins=7, NotFound=8, NoSpace=9, FolderChange=12, ResendFull=13, LimitReWait=14, TooMany=15,
                Retry=16};

		}
		public class AirSyncBase {
			public const string Ns = "AirSyncBase";
			// Alpha order.
			public const string Attachment = "Attachment";
			public const string Attachments = "Attachments";
			public const string Body = "Body";
			public const string ContentLocation = "ContentLocation";
			public const string ContentType = "ContentType";
			public const string Data = "Data";
			public const string DisplayName = "DisplayName";
			public const string EstimatedDataSize = "EstimatedDataSize";
			public const string FileReference = "FileReference";
			public const string IsInline = "IsInline";
			public const string Method = "Method";
			public const string NativeBodyType = "NativeBodyType";
			public const string Truncated = "Truncated";
			public const string Type = "Type";

			public enum MethodCode : uint {NormalAttachment=1, /* [2, 4] Reserved. */ EmbeddedEml=5, AttachOle=6};
			// NOTE that TypeCode is for both Type and NativeBodyType.
			public enum TypeCode : uint {PlainText=1, Html=2, Rtf = 3, /* Data element will be base64-encoded. */ Mime = 4};
		}

        public class Autodisco {
            public const string Autodiscover = "Autodiscover";
            // Alpha order.
            public const string AcceptableResponseSchema = "AcceptableResponseSchema";
            public const string Action = "Action";
            public const string Culture = "Culture";
            public const string DebugData = "DebugData";
            public const string DisplayName = "DisplayName";
            public const string EmailAddress = "EmailAddress";
            public const string Error = "Error";
            public const string ErrorCode = "ErrorCode";
            public const string Message = "Message";
            public const string Name = "Name";
            public const string Redirect = "Redirect";
            public const string Request = "Request";
            public const string Response = "Response";
            public const string Server = "Server";
            public const string ServerData = "ServerData";
            public const string Settings = "Settings";
            public const string Status = "Status";
            public const string Type = AirSyncBase.Type;
            public const string Url = "Url";
            public const string User = "User";

            public enum ErrorCodeCode : uint {InvalidRequest=600, NoProviderForSchema=601};
            public enum StatusCode : uint {Success=1, ProtocolError=2};
            public class TypeCode {
                public const string MobileSync = "MobileSync";
                public const string CertEnroll = "CertEnroll";
            }
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
			// Alpha order.
			public const string UmAttDuration = "UmAttDuration";
			public const string UmAttOrder = "UmAttOrder";
		}
		public class FolderHierarchy {
			public const string Ns = "FolderHierarchy";
			// Alpha order.
			public const string Add = AirSync.Add;
			public const string Changes = "Changes";
			public const string Delete = "Delete";
			public const string DisplayName = AirSyncBase.DisplayName;
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
		public class ItemOperations {
			public const string Ns = "ItemOperations";
			// Alpha order.
			public const string Data = AirSyncBase.Data;
			public const string Fetch = "Fetch";
			public const string Properties = "Properties";
			public const string Status = AirSync.Status;
			public const string Store = "Store";
			public const string Response = Autodisco.Response;

			public class StoreCode {
				public const string DocumentLibrary = "Document Library"; // NOTE: space is intended.
				public const string Mailbox = "Mailbox";
			}

			public enum StatusCode : uint {Success=1, ProtocolError=2, ServerError=3, DocLibBadUri=4,
				DocLibAccessDenied=5, DocLibAccessDeniedOrMissing=6, DocLibFailedServerConn=7,
				ByteRangeInvalidOrTooLarge=8, StoreUnknownOrNotSupported=9, FileEmpty=10, RequestTooLarge=11,
				IoFailure=12, /* 13 omitted */ ConversionFailure=14, AttachmentOrIdInvalid=15, 
				ResourceAccessDenied=16, PartialFailure=17, CredRequired=18, /* [19, 154] omitted */
				ProtocolErrorMissing=155, ActionNotSupported=156};
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
		public class Settings {
			public const string Ns = "Settings";
			// Alpha order.
			public const string DeviceInformation = "DeviceInformation";
			public const string FriendlyName = "FriendlyName";
			public const string Get = "Get";
			public const string Model = "Model";
			public const string OS = "OS";
			public const string OSLanguage = "OSLanguage";
			public const string Set = "Set";
			public const string UserInformation = "UserInformation";
		}
	}
}

