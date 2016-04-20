using System;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class Xml
    {
        // These status code values are common to ALL commands, independent of namespace.
        public enum StatusCode
        {
            InvalidContent_101 = 101,
            // AsCommand. CODEFIX. HTTP 400 in 2007.
            First = InvalidContent_101,
            InvalidWBXML_102 = 102,
            // AsCommand. CODEFIX.
            InvalidXML_103 = 103,
            // AsCommand. CODEFIX.
            InvalidDateTime_104 = 104,
            // AsCommand. CODEFIX.
            InvalidCombinationOfIDs_105 = 105,
            // AsCommand subclass. RECOVER.
            InvalidIDs_106 = 106,
            // AsCommand subclass. RECOVER. HTTP 400 in 2007, except SendMail which was HTTP 500.
            InvalidMIME_107 = 107,
            // AsSendMailCommand. RECOVER.
            DeviceIdMissingOrInvalid_108 = 108,
            // AsCommand. CODEFIX.
            DeviceTypeMissingOrInvalid_109 = 109,
            // AsCommand. CODEFIX.
            ServerError_110 = 110,
            // We should NOT retry later. AsCommand => AsControl. ADMIN. HTTP 500 in 2007.
            ServerErrorRetryLater_111 = 111,
            // AsCommand => AsControl. RECOVER. HTTP 503 in 2007.
            ActiveDirectoryAccessDenied_112 = 112,
            // AsCommand => AsControl. ADMIN. HTTP 403 in 2007.
            MailboxQuotaExceeded_113 = 113,
            // AsCommand => AsControl. USER. HTTP 507 in 2007.
            MailboxServerOffline_114 = 114,
            // AsCommand => AsControl. ADMIN.
            SendQuotaExceeded_115 = 115,
            // AsCommand => AsControl. ADMIN.
            MessageRecipientUnresolved_116 = 116,
            // AsSendMailCommand. USER.
            MessageReplyNotAllowed_117 = 117,
            // AsSendMailCommand. USER.
            MessagePreviouslySent_118 = 118,
            // AsSendMailCommand. RECOVER.
            MessageHasNoRecipient_119 = 119,
            // AsSendMailCommand. USER.
            MailSubmissionFailed_120 = 120,
            // AsSendMailCommand. RECOVER.
            MessageReplyFailed_121 = 121,
            // AsSendMailCommand. RECOVER.
            AttachmentIsTooLarge_122 = 122,
            // AsSendMailCommand. USER.
            UserHasNoMailbox_123 = 123,
            // AsCommand => AsControl. ADMIN.
            UserCannotBeAnonymous_124 = 124,
            // AsCommand => AsControl. CODEFIX.
            UserPrincipalCouldNotBeFound_125 = 125,
            // AsCommand => AsControl. ADMIN.
            UserDisabledForSync_126 = 126,
            // AsCommand => AsControl. ADMIN.
            UserOnNewMailboxCannotSync_127 = 127,
            // AsCommand => AsControl. ADMIN.
            UserOnLegacyMailboxCannotSync_128 = 128,
            // AsCommand => AsControl. ADMIN.
            DeviceIsBlockedForThisUser_129 = 129,
            // AsCommand => AsControl. ADMIN.
            AccessDenied_130 = 130,
            // AsCommand. ADMIN.
            AccountDisabled_131 = 131,
            // AsCommand => AsControl. ADMIN.
            SyncStateNotFound_132 = 132,
            // Do a full re-sync. RECOVER. Let USER know. HTTP 500 in 2007, except Provision which was HTTP 403.
            SyncStateLocked_133 = 133,
            // AsCommand => AsControl. ADMIN.
            SyncStateCorrupt_134 = 134,
            // Do a full re-sync. RECOVER. Let USER/ADMIN know.
            SyncStateAlreadyExists_135 = 135,
            // Do a full re-sync. RECOVER. Let USER/ADMIN know.
            SyncStateVersionInvalid_136 = 136,
            // Do a full re-sync. RECOVER. Let USER/ADMIN know.
            CommandNotSupported_137 = 137,
            // AsControl. CODEFIX. HTTP 501 in 2007.
            VersionNotSupported_138 = 138,
            // AsControl. CODEFIX. HTTP 400 in 2007, except for 1.0 devices which got a 505.
            DeviceNotFullyProvisionable_139 = 139,
            // AsProvision. CODEFIX.
            RemoteWipeRequested_140 = 140,
            // AsCommand. Device should wipe via Provision commands. HTTP 449 in 2007, or 403 if no policy header.
            LegacyDeviceOnStrictPolicy_141 = 141,
            // AsCommand. ADMIN.
            DeviceNotProvisioned_142 = 142,
            // AsCommand. RECOVER (reprovision). HTTP 449 in 2007.
            PolicyRefresh_143 = 143,
            // AsCommand. RECOVER (reprovision).
            InvalidPolicyKey_144 = 144,
            // AsCommand. RECOVER (reprovision).
            ExternallyManagedDevicesNotAllowed_145 = 145,
            // AsCommand. CODEFIX.
            NoRecurrenceInCalendar_146 = 146,
            // AsCommand. CODEFIX.
            UnexpectedItemClass_147 = 147,
            // AsCommand. CODEFIX. HTTP 400 or 501 in 2007.
            RemoteServerHasNoSSL_148 = 148,
            // AsCommand. ADMIN.
            InvalidStoredRequest_149 = 149,
            // AsPingCommand? RECOVER.
            ItemNotFound_150 = 150,
            // SmartReply, SmartForward. RECOVER.
            TooManyFolders_151 = 151,
            // AsCommand. USER/ADMIN.
            NoFoldersFound_152 = 152,
            // AsCommand. ADMIN.
            ItemsLostAfterMove_153 = 153,
            // ItemOperations. USER/ADMIN. RECOVER.
            FailureInMoveOperation_154 = 154,
            // ItemOperations. USER/ADMIN. RECOVER.
            MoveCommandDisallowedForNonPersistentMoveAction_155 = 155,
            // ItemOperations. CODEFIX.
            MoveCommandInvalidDestinationFolder_156 = 156,
            // ItemOperations. RECOVER.
            // [157,159] unused.
            AvailabilityTooManyRecipients_160 = 160,
            // AsSendMailCommand. USER.
            AvailabilityDLLimitReached_161 = 161,
            // AsSendMailCommand. USER.
            AvailabilityTransientFailure_162 = 162,
            // USER/RECOVER.
            AvailabilityFailure_163 = 163,
            // USER/ADMIN.
            BodyPartPreferenceTypeNotSupported_164 = 164,
            // Where? CODEFIX.
            DeviceInformationRequired_165 = 165,
            // AsProvisionCommand. CODEFIX.
            InvalidAccountId_166 = 166,
            // Where? USER.
            AccountSendDisabled_167 = 167,
            // AsSendMailCommand. ADMIN.
            IRM_FeatureDisabled_168 = 168,
            // IGNORE: No RM yet.
            IRM_TransientError_169 = 169,
            IRM_PermanentError_170 = 170,
            IRM_InvalidTemplateID_171 = 171,
            IRM_OperationNotPermitted_172 = 172,
            NoPicture_173 = 173,
            // Where? RECOVER.
            PictureTooLarge_174 = 174,
            // Where? RECOVER.
            PictureLimitReached_175 = 175,
            // Where? RECOVER.
            BodyPart_ConversationTooLarge_176 = 176,
            // Where? RECOVER.
            MaximumDevicesReached_177 = 177,
            // AsCommand. USER/ADMIN.
            Last = MaximumDevicesReached_177,
        }
        ;
        /* The following section is organized as follows:
         * 1) The order is AirSync, AirSyncBase, and then classes are in alpha order.
         * 2) In order to have only one version of any typed-in string, following classes refer to the
         * constants in preceding classes.
         */
        public class AirSync
        {
            public const string Ns = "AirSync";
            // Alpha order.
            public const string Add = "Add";
            public const string ApplicationData = "ApplicationData";
            public const string BodyPreference = "BodyPreference";
            public const string Change = "Change";
            public const string Class = "Class";
            public const string ClientId = "ClientId";
            public const string Collection = "Collection";
            public const string CollectionId = "CollectionId";
            public const string Collections = "Collections";
            public const string Commands = "Commands";
            public const string Delete = "Delete";
            public const string DeletesAsMoves = "DeletesAsMoves";
            public const string Fetch = "Fetch";
            public const string FilterType = "FilterType";
            public const string GetChanges = "GetChanges";
            public const string Limit = "Limit";
            public const string MaxItems = "MaxItems";
            public const string MimeSupport = "MIMESupport";
            public const string MoreAvailable = "MoreAvailable";
            public const string Options = "Options";
            public const string Responses = "Responses";
            public const string ServerId = "ServerId";
            public const string SoftDelete = "SoftDelete";
            public const string Status = "Status";
            public const string Sync = "Sync";
            public const string SyncKey = "SyncKey";
            public const string Type = "Type";
            public const string WindowSize = "WindowSize";
            public const string Wait = "Wait";
            public const string HeartbeatInterval = "HeartbeatInterval";
           
            public class ClassCode
            {
                public const string Tasks = "Tasks";
                public const string Email = "Email";
                public const string Calendar = "Calendar";
                public const string Contacts = "Contacts";
                public const string Notes = "Notes";
                public const string Sms = "SMS";
            }

            public McAbstrItem.ClassCodeEnum ClassCode2Enum (string classCode)
            {
                switch (classCode) {
                case ClassCode.Tasks:
                    return McAbstrItem.ClassCodeEnum.Tasks;
                case ClassCode.Email:
                    return McAbstrItem.ClassCodeEnum.Email;
                case ClassCode.Calendar:
                    return McAbstrItem.ClassCodeEnum.Calendar;
                case ClassCode.Contacts:
                    return McAbstrItem.ClassCodeEnum.Contact;
                case ClassCode.Notes:
                    return McAbstrItem.ClassCodeEnum.Notes;
                case ClassCode.Sms:
                    return McAbstrItem.ClassCodeEnum.Sms;
                default:
                    throw new Exception (string.Format ("Unknown ClassCode {0}", classCode));
                }
            }

            public enum MimeSupportCode : uint
            {
                NoMime_0 = 0,
                SMimeOnly_1 = 1,
                AllMime_2 = 2,
            };

            public enum StatusCode : uint
            {
                Success_1 = 1,
                SyncKeyInvalid_3 = 3,
                ProtocolError_4 = 4,
                ServerError_5 = 5,
                ClientError_6 = 6,
                ServerWins_7 = 7,
                NotFound_8 = 8,
                NoSpace_9 = 9,
                FolderChange_12 = 12,
                ResendFull_13 = 13,
                LimitReWait_14 = 14,
                TooMany_15 = 15,
                Retry_16 = 16,
            };

            public enum TypeCode : uint
            {
                PlainText_1 = 1,
                Html_2 = 2,
                Rtf_3 = 3,
                Mime_4 = 4,
            }
        }

        public class AirSyncBase
        {
            public const string Ns = "AirSyncBase";
            // Alpha order.
            public const string AllOrNone = "AllOrNone";
            public const string Attachment = "Attachment";
            public const string Attachments = "Attachments";
            public const string Body = "Body";
            public const string BodyPreference = "BodyPreference";
            public const string ContentId = "ContentId";
            public const string ContentLocation = "ContentLocation";
            public const string ContentType = "ContentType";
            public const string ConversationId = "ConversationId";
            public const string Data = "Data";
            public const string DisplayName = "DisplayName";
            public const string EstimatedDataSize = "EstimatedDataSize";
            public const string FileReference = "FileReference";
            public const string IsInline = "IsInline";
            public const string Method = "Method";
            public const string NativeBodyType = "NativeBodyType";
            public const string Preview = "Preview";
            public const string Truncated = "Truncated";
            public const string TruncationSize = "TruncationSize";
            public const string Type = AirSync.Type;

            public enum MethodCode : uint
            {
                NormalAttachment_1 = 1,
                /* [2, 4] Reserved. */
                EmbeddedEml_5 = 5,
                AttachOle_6 = 6,
            };
            // NOTE that TypeCode is for both Type and NativeBodyType.
            public enum TypeCode : uint
            {
                PlainText_1 = 1,
                Html_2 = 2,
                Rtf_3 = 3,
                /* Data element will be base64-encoded. */
                Mime_4 = 4,
            };
        }

        public class Autodisco
        {
            public const string Autodiscover = "Autodiscover";
            // Alpha order.
            public const string AcceptableResponseSchema = "AcceptableResponseSchema";
            public const string Action = "Action";
            public const string Culture = "Culture";
            public const string DebugData = "DebugData";
            public const string DisplayName = AirSyncBase.DisplayName;
            public const string EMailAddress = "EMailAddress";
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
            public const string Status = AirSync.Status;
            public const string Type = AirSyncBase.Type;
            public const string Url = "Url";
            public const string User = "User";

            public const string Error_Attr_Id = Ping.Id;
            public const string Error_Attr_Time = "Time";

            public enum ErrorCodeCode : uint
            {
                InvalidRequest_600 = 600,
                NoProviderForSchema_601 = 601,
            };

            public enum StatusCode : uint
            {
                Success_1 = 1,
                ProtocolError_2 = 2,
            };

            public class TypeCode
            {
                public const string MobileSync = "MobileSync";
                public const string CertEnroll = "CertEnroll";
            }
        }

        public class Calendar
        {
            public const string Ns = "Calendar";
            // Elements in alpha order.
            public const string AllDayEvent = "AllDayEvent";
            public const string AppointmentReplyTime = "AppointmentReplyTime";
            public const string AttendeeStatus = "AttendeeStatus";
            public const string AttendeeType = "AttendeeType";
            // public const string airsyncbase:Body = "airsyncbase:Body";
            public const string BusyStatus = "BusyStatus";
            public const string Category = "Category";
            public const string DisallowNewTimeProposal = "DisallowNewTimeProposal";
            public const string DtStamp = "DtStamp";
            public const string Email = "Email";
            public const string EndTime = "EndTime";
            public const string Location = "Location";
            public const string MeetingStatus = "MeetingStatus";
            public const string Name = "Name";
            public const string OnlineMeetingConfLink = "OnlineMeetingConfLink";
            public const string OnlineMeetingExternalLink = "OnlineMeetingExternalLink";
            public const string OrganizerEmail = "OrganizerEmail";
            public const string OrganizerName = "OrganizerName";
            public const string Reminder = "Reminder";
            public const string ResponseRequested = "ResponseRequested";
            public const string ResponseType = "ResponseType";
            public const string Sensitivity = "Sensitivity";
            public const string StartTime = "StartTime";
            public const string Subject = "Subject";
            public const string Timezone = "Timezone";
            public const string UID = "UID";
            // Containers in alpha order
            public const string Calendar_Attendees = "Attendees";
            public const string Calendar_Categories = "Categories";
            public const string Calendar_Exceptions = "Exceptions";
            public const string Calendar_Recurrence = "Recurrence";

            public class Attendees
            {
                // Alpha order.
                public const string Attendee = "Attendee";
            }

            public class Attendee
            {
                // Alpha order.
                public const string AttendeeStatus = "AttendeeStatus";
                public const string AttendeeType = "AttendeeType";
                public const string Email = "Email";
                public const string Name = "Name";
            }

            public class Categories
            {
                // Alpha order.
                public const string Category = "Category";
            }

            public class Exceptions
            {
                // Alpha order
                public const string Exception = "Exception";
            }

            public class Exception
            {
                // Alpha order.
                public const string AllDayEvent = "AllDayEvent";
                public const string AppointmentReplyTime = "AppointmentReplyTime";
                public const string Attendees = "Attendees";
                public const string BusyStatus = "BusyStatus";
                public const string Categories = "Categories";
                public const string Deleted = "Deleted";
                public const string DtStamp = "DtStamp";
                public const string EndTime = "EndTime";
                public const string ExceptionStartTime = "ExceptionStartTime";
                public const string Location = "Location";
                public const string MeetingStatus = "MeetingStatus";
                public const string OnlineMeetingConfLink = "OnlineMeetingConfLink";
                public const string OnlineMeetingExternalLink = "OnlineMeetingExternalLink";
                public const string Reminder = "Reminder";
                public const string ResponseType = "ResponseType";
                public const string Sensitivity = "Sensitivity";
                public const string StartTime = "StartTime";
                public const string Subject = Calendar.Subject;
                // public const string airsyncbase:Body = "airsyncbase:Body";
                // Containers in alpha order
                public const string Exception_Attendees = "Attendees";
                public const string Exception_Categories = "Categories";
            }

            public class Recurrence
            {
                // Alpha order.
                public const string CalendarType = "CalendarType";
                public const string DayOfMonth = "DayOfMonth";
                public const string DayOfWeek = "DayOfWeek";
                public const string FirstDayOfWeek = "FirstDayOfWeek";
                public const string Interval = "Interval";
                public const string IsLeapMonth = "IsLeapMonth";
                public const string MonthOfYear = "MonthOfYear";
                public const string Occurrences = "Occurrences";
                public const string Type = AirSync.Type;
                public const string Until = "Until";
                public const string WeekOfMonth = "WeekOfMonth";
            }
        }

        public class ComposeMail
        {
            public const string Ns = "ComposeMail";
            // Alpha order.
            public const string ClientId = AirSync.ClientId;
            public const string AccountId = "AccountId";
            public const string FolderId = "FolderId";
            public const string ItemId = "ItemId";
            public const string ReplaceMime = "ReplaceMime";
            public const string SaveInSentItems = "SaveInSentItems";
            public const string SendMail = "SendMail";
            public const string SmartForward = "SmartForward";
            public const string SmartReply = "SmartReply";
            public const string Source = "Source";
            public const string Status = AirSync.Status;
            public const string Mime = "Mime";
        }

        public class Contacts
        {
            public const string Ns = "Contacts";
            // Alpha order.
            public const string Alias = "Alias";
            public const string Anniversary = "Anniversary";
            public const string AssistantName = "AssistantName";
            public const string AssistantPhoneNumber = "AssistantPhoneNumber";
            public const string Birthday = "Birthday";
            public const string BusinessAddressCity = "BusinessAddressCity";
            public const string BusinessAddressCountry = "BusinessAddressCountry";
            public const string BusinessAddressPostalCode = "BusinessAddressPostalCode";
            public const string BusinessAddressState = "BusinessAddressState";
            public const string BusinessAddressStreet = "BusinessAddressStreet";
            public const string BusinessFaxNumber = "BusinessFaxNumber";
            public const string BusinessPhoneNumber = "BusinessPhoneNumber";
            public const string Business2PhoneNumber = "Business2PhoneNumber";
            public const string CarPhoneNumber = "CarPhoneNumber";
            public const string Categories = "Categories";
            public const string Category = "Category";
            public const string Child = "Child";
            public const string Children = "Children";
            public const string CompanyName = "CompanyName";
            public const string Department = "Department";
            public const string Email1Address = "Email1Address";
            public const string Email2Address = "Email2Address";
            public const string Email3Address = "Email3Address";
            public const string FileAs = "FileAs";
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
            public const string Picture = "Picture";
            public const string RadioPhoneNumber = "RadioPhoneNumber";
            public const string Spouse = "Spouse";
            public const string Suffix = "Suffix";
            public const string Title = "Title";
            public const string WebPage = "WebPage";
            public const string WeightedRank = "WeightedRank";
            public const string YomiCompanyName = "YomiCompanyName";
            public const string YomiFirstName = "YomiFirstName";
            public const string YomiLastName = "YomiLastName";
        }

        public class Contacts2
        {
            public const string Ns = "Contacts2";
            // Alpha order.
            public const string AccountName = "AccountName";
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

        public class Email
        {
            public const string Ns = AirSync.ClassCode.Email;
            // Alpha order.
            public const string AllDayEvent = "AllDayEvent";
            public const string BusyStatus = "BusyStatus";
            public const string Category = "Category";
            public const string Categories = "Categories";
            public const string Cc = "Cc";
            public const string CompleteTime = "CompleteTime";
            public const string ContentClass = "ContentClass";
            public const string DateReceived = "DateReceived";
            public const string DisallowNewTimeProposal = "DisallowNewTimeProposal";
            public const string DisplayTo = "DisplayTo";
            public const string DtStamp = "DtStamp";
            public const string EndTime = "EndTime";
            public const string Flag = "Flag";
            public const string FlagType = "FlagType";
            public const string From = "From";
            public const string GlobalObjId = "GlobalObjId";
            public const string Importance = "Importance";
            public const string InternetCPID = "InternetCPID";
            public const string InstanceType = "InstanceType";
            public const string Location = "Location";
            public const string MeetingRequest = "MeetingRequest";
            public const string MessageClass = "MessageClass";
            public const string MeetingMessageType = "MeetingMessageType";
            public const string Organizer = "Organizer";
            public const string Read = "Read";
            public const string RecurrenceId = "RecurrenceId";
            public const string Recurrence = "Recurrence";
            public const string Recurrences = "Recurrences";
            public const string Reminder = "Reminder";
            public const string ReplyTo = "ReplyTo";
            public const string ResponseRequested = "ResponseRequested";
            public const string Sender = "Sender";
            public const string Sensitivity = "Sensitivity";
            public const string Status = AirSync.Status;
            public const string StartTime = "StartTime";
            public const string Subject = Calendar.Subject;
            public const string ThreadTopic = "ThreadTopic";
            public const string TimeZone = "TimeZone";
            public const string To = "To";

            public enum FlagStatusCode : uint
            {
                Clear_0 = 0,
                MarkDone_1 = 1,
                Set_2 = 2,
            };
        }

        public class Email2
        {
            public const string Ns = "Email2";
            // Alpha order.
            public const string ConversationId = "ConversationId";
            public const string ConversationIndex = "ConversationIndex";
            public const string LastVerbExecuted = "LastVerbExecuted";
            public const string LastVerbExecutionTime = "LastVerbExecutionTime";
            public const string ReceivedAsBcc = "ReceivedAsBcc";
            public const string UmAttDuration = "UmAttDuration";
            public const string UmAttOrder = "UmAttOrder";
        }

        public class FolderHierarchy
        {
            public const string Ns = "FolderHierarchy";
            // Alpha order.
            public const string Add = AirSync.Add;
            public const string Changes = "Changes";
            public const string Delete = "Delete";
            public const string DisplayName = AirSyncBase.DisplayName;
            public const string FolderCreate = "FolderCreate";
            public const string FolderDelete = "FolderDelete";
            public const string FolderSync = "FolderSync";
            public const string FolderUpdate = "FolderUpdate";
            public const string ParentId = "ParentId";
            public const string ServerId = AirSync.ServerId;
            public const string Status = AirSync.Status;
            public const string SyncKey = AirSync.SyncKey;
            public const string Type = AirSync.Type;
            public const string Update = "Update";

            public enum FolderSyncStatusCode : uint
            {
                Success_1 = 1,
                Retry_6 = 6,
                ReSync_9 = 9,
                BadFormat_10 = 10,
                Unknown_11 = 11,
                ServerFail_12 = 12,
            };
            // Note: Ric is not supported for AS version 12.1.
            public enum FolderCreateStatusCode : uint
            {
                Success_1 = 1,
                Exists_2 = 2,
                SpecialParent_3 = 3,
                BadParent_5 = 5,
                ServerError_6 = 6,
                ReSync_9 = 9,
                BadFormat_10 = 10,
                Unknown_11 = 11,
                BackEndError_12 = 12,
            };

            public enum FolderDeleteStatusCode : uint
            {
                Success_1 = 1,
                Special_3 = 3,
                Missing_4 = 4,
                ServerError_6 = 6,
                ReSync_9 = 9,
                BadFormat_10 = 10,
            };

            public enum FolderUpdateStatusCode : uint
            {
                Success_1 = 1,
                Exists_2 = 2,
                Special_3 = 3,
                Missing_4 = 4,
                MissingParent_5 = 5,
                ServerError_6 = 6,
                ReSync_9 = 9,
                BadFormat_10 = 10,
                Unknown_11 = 11,
            };

            public enum TypeCode
            {
                UserCreatedGeneric_1 = 1,
                DefaultInbox_2 = 2,
                DefaultDrafts_3 = 3,
                DefaultDeleted_4 = 4,
                DefaultSent_5 = 5,
                DefaultOutbox_6 = 6,
                DefaultTasks_7 = 7,
                DefaultCal_8 = 8,
                DefaultContacts_9 = 9,
                DefaultNotes_10 = 10,
                DefaultJournal_11 = 11,
                UserCreatedMail_12 = 12,
                UserCreatedCal_13 = 13,
                UserCreatedContacts_14 = 14,
                UserCreatedTasks_15 = 15,
                UserCreatedJournal_16 = 16,
                UserCreatedNotes_17 = 17,
                Unknown_18 = 18,
                Ric_19 = 19,
            };

            public static McAbstrFolderEntry.ClassCodeEnum TypeCodeToAirSyncClassCodeEnum (TypeCode code)
            {
                switch (code) {
                case TypeCode.DefaultJournal_11:
                case TypeCode.UserCreatedJournal_16:
                    return McAbstrFolderEntry.ClassCodeEnum.Journal;

                case TypeCode.DefaultInbox_2:
                case TypeCode.DefaultDrafts_3:
                case TypeCode.DefaultDeleted_4:
                case TypeCode.DefaultSent_5:
                case TypeCode.DefaultOutbox_6:
                case TypeCode.UserCreatedMail_12:
                    // Treat Unknown/Generic as Email until proven otherwise.
                case TypeCode.Unknown_18:
                case TypeCode.UserCreatedGeneric_1:
                    return McAbstrFolderEntry.ClassCodeEnum.Email;

                case TypeCode.DefaultTasks_7:
                case TypeCode.UserCreatedTasks_15:
                    return McAbstrFolderEntry.ClassCodeEnum.Tasks;

                case TypeCode.DefaultCal_8:
                case TypeCode.UserCreatedCal_13:
                    return McAbstrFolderEntry.ClassCodeEnum.Calendar;

                case TypeCode.DefaultContacts_9:
                case TypeCode.UserCreatedContacts_14:
                case TypeCode.Ric_19:
                    return McAbstrFolderEntry.ClassCodeEnum.Contact;

                case TypeCode.DefaultNotes_10:
                case TypeCode.UserCreatedNotes_17:
                    return McAbstrFolderEntry.ClassCodeEnum.Notes;
                }
                throw new Exception ();
            }

            public static string TypeCodeToAirSyncClassCode (TypeCode code)
            {
                var enumVal = TypeCodeToAirSyncClassCodeEnum (code);
                switch (enumVal) {
                case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                    return AirSync.ClassCode.Calendar;
                case McAbstrFolderEntry.ClassCodeEnum.Contact:
                    return AirSync.ClassCode.Contacts;
                case McAbstrFolderEntry.ClassCodeEnum.Email:
                    return AirSync.ClassCode.Email;
                case McAbstrFolderEntry.ClassCodeEnum.Notes:
                    return AirSync.ClassCode.Notes;
                case McAbstrFolderEntry.ClassCodeEnum.Sms:
                    return AirSync.ClassCode.Sms;
                case McAbstrFolderEntry.ClassCodeEnum.Tasks:
                    return AirSync.ClassCode.Tasks;

                default:
                    throw new Exception (string.Format ("Un-syncable class code: {0}", enumVal.ToString ()));
                }
            }
        }

        public class Gal
        {
            public const string Ns = "GAL";
            // Alpha order.
            public const string Alias = Contacts.Alias;
            public const string Company = "Company";
            public const string Data = AirSyncBase.Data;
            public const string DisplayName = AirSyncBase.DisplayName;
            public const string EmailAddress = "EmailAddress";
            public const string FirstName = Contacts.FirstName;
            public const string HomePhone = "HomePhone";
            public const string LastName = Contacts.LastName;
            public const string MobilePhone = "MobilePhone";
            public const string Office = "Office";
            public const string Phone = "Phone";
            public const string Picture = Contacts.Picture;
            public const string Status = AirSync.Status;
            public const string Title = Contacts.Title;

            public enum StatusCode : uint
            {
                Success_1 = 1,
                NoPhoto_173 = 173,
                TooBig_174 = 174,
                TooMany_175 = 175,
            };
        }

        public class ItemOperations
        {
            public const string Ns = "ItemOperations";
            // Alpha order.
            public const string Data = AirSyncBase.Data;
            public const string Fetch = "Fetch";
            public const string Options = AirSync.Options;
            public const string Properties = "Properties";
            public const string Status = AirSync.Status;
            public const string Store = "Store";
            public const string Response = Autodisco.Response;

            public class StoreCode
            {
                public const string DocumentLibrary = "DocumentLibrary";
                // NOTE: space is intended.
                public const string Mailbox = "Mailbox";
            }

            public enum StatusCode : uint
            {
                Success_1 = 1,
                ProtocolError_2 = 2,
                ServerError_3 = 3,
                DocLibBadUri_4 = 4,
                DocLibAccessDenied_5 = 5,
                DocLibAccessDeniedOrMissing_6 = 6,
                DocLibFailedServerConn_7 = 7,
                ByteRangeInvalidOrTooLarge_8 = 8,
                StoreUnknownOrNotSupported_9 = 9,
                FileEmpty_10 = 10,
                RequestTooLarge_11 = 11,
                IoFailure_12 = 12,
                /* 13 omitted */
                ConversionFailure_14 = 14,
                AttachmentOrIdInvalid_15 = 15,
                ResourceAccessDenied_16 = 16,
                PartialFailure_17 = 17,
                CredRequired_18 = 18,
                /* [19, 154] omitted */
                ProtocolErrorMissing_155 = 155,
                ActionNotSupported_156 = 156,
            };
        }

        public class MeetingResp
        {
            public const string Ns = "MeetingResponse";
            // Alpha order.
            public const string CollectionId = "CollectionId";
            public const string InstanceId = "InstanceId";
            public const string MeetingResponse = "MeetingResponse";
            public const string Request = "Request";
            public const string RequestId = "RequestId";
            public const string Result = "Result";
            public const string Status = "Status";
            public const string UserResponse = "UserResponse";

            public enum UserResponseCode
            {
                Accepted_1 = 1,
                Tentatively_2 = 2,
                Declined_3 = 3,
            }

            public enum StatusCode
            {
                Success_1 = 1,
                InvalidMeetingRequest_2 = 2,
            }
        }

        public class Mov
        {
            public const string Ns = "Move";
            // Alpha order.
            public const string DstFldId = "DstFldId";
            public const string DstMsgId = "DstMsgId";
            public const string Move = "Move";
            public const string MoveItems = "MoveItems";
            public const string Response = "Response";
            public const string SrcFldId = "SrcFldId";
            public const string SrcMsgId = "SrcMsgId";
            public const string Status = AirSync.Status;

            public enum StatusCode : uint
            {
                InvalidSrc_1 = 1,
                InvalidDest_2 = 2,
                Success_3 = 3,
                SrcDestSame_4 = 4,
                ClobberOrMulti_5 = 5,
                // 6 omitted.
                Locked_7 = 7,
            };
        }

        public class Ping
        {
            public const string Ns = "Ping";
            // Alpha order.
            public const string Class = "Class";
            public const string Folder = "Folder";
            public const string Folders = "Folders";
            public const string HeartbeatInterval = "HeartbeatInterval";
            public const string Id = "Id";
            public const string MaxFolders = "MaxFolders";
            public const string Status = AirSync.Status;

            public enum StatusCode : uint
            {
                NoChanges_1 = 1,
                Changes_2 = 2,
                MissingParams_3 = 3,
                SyntaxError_4 = 4,
                BadHeartbeat_5 = 5,
                TooManyFolders_6 = 6,
                NeedFolderSync_7 = 7,
                ServerError_8 = 8,
            };
        }

        public class Provision
        {
            public const string Ns = "Provision";
            // Alpha order.
            public const string AllowBluetooth = "AllowBluetooth";
            public const string AllowBrowser = "AllowBrowser";
            public const string AllowCamera = "AllowCamera";
            public const string AllowConsumerEmail = "AllowConsumerEmail";
            public const string AllowDesktopSync = "AllowDesktopSync";
            public const string AllowHTMLEmail = "AllowHTMLEmail";
            public const string AllowInternetSharing = "AllowInternetSharing";
            public const string AllowIrDA = "AllowIrDA";
            public const string AllowPOPIMAPEmail = "AllowPOPIMAPEmail";
            public const string AllowRemoteDesktop = "AllowRemoteDesktop";
            public const string AllowSimpleDevicePassword = "AllowSimpleDevicePassword";
            public const string AllowSMIMEEncryptionAlgorithmNegotiation = "AllowSMIMEEncryptionAlgorithmNegotiation";
            public const string AllowSMIMESoftCerts = "AllowSMIMESoftCerts";
            public const string AllowStorageCard = "AllowStorageCard";
            public const string AllowTextMessaging = "AllowTextMessaging";
            public const string AllowUnsignedApplications = "AllowUnsignedApplications";
            public const string AllowUnsignedInstallationPackages = "AllowUnsignedInstallationPackages";
            public const string AllowWiFi = "AllowWiFi";
            public const string AlphanumericDevicePasswordRequired = "AlphanumericDevicePasswordRequired";
            public const string ApplicationName = "ApplicationName";
            public const string ApprovedApplicationList = "ApprovedApplicationList";
            public const string AttachmentsEnabled = "AttachmentsEnabled";
            public const string Data = AirSyncBase.Data;
            public const string DevicePasswordEnabled = "DevicePasswordEnabled";
            public const string DevicePasswordExpiration = "DevicePasswordExpiration";
            public const string DevicePasswordHistory = "DevicePasswordHistory";
            public const string EASProvisionDoc = "EASProvisionDoc";
            public const string Hash = "Hash";
            public const string MaxAttachmentSize = "MaxAttachmentSize";
            public const string MaxCalendarAgeFilter = "MaxCalendarAgeFilter";
            public const string MaxDevicePasswordFailedAttempts = "MaxDevicePasswordFailedAttempts";
            public const string MaxEmailAgeFilter = "MaxEmailAgeFilter";
            public const string MaxEmailBodyTruncationSize = "MaxEmailBodyTruncationSize";
            public const string MaxEmailHTMLBodyTruncationSize = "MaxEmailHTMLBodyTruncationSize";
            public const string MaxInactivityTimeDeviceLock = "MaxInactivityTimeDeviceLock";
            public const string MinDevicePasswordComplexCharacters = "MinDevicePasswordComplexCharacters";
            public const string MinDevicePasswordLength = "MinDevicePasswordLength";
            public const string PasswordRecoveryEnabled = "PasswordRecoveryEnabled";
            public const string Policies = "Policies";
            public const string Policy = "Policy";
            public const string PolicyKey = "PolicyKey";
            public const string PolicyType = "PolicyType";
            public const string PolicyTypeValue = "MS-EAS-Provisioning-WBXML";
            public const string RemoteWipe = "RemoteWipe";
            public const string RequireDeviceEncryption = "RequireDeviceEncryption";
            public const string RequireEncryptedSMIMEMessages = "RequireEncryptedSMIMEMessages";
            public const string RequireEncryptionSMIMEAlgorithm = "RequireEncryptionSMIMEAlgorithm";
            public const string RequireManualSyncWhenRoaming = "RequireManualSyncWhenRoaming";
            public const string RequireSignedSMIMEAlgorithm = "RequireSignedSMIMEAlgorithm";
            public const string RequireSignedSMIMEMessages = "RequireSignedSMIMEMessages";
            public const string RequireStorageCardEncryption = "RequireStorageCardEncryption";
            public const string Status = AirSync.Status;
            public const string UnapprovedInROMApplicationList = "UnapprovedInROMApplicationList";

            public enum ProvisionStatusCode : uint
            {
                Success_1 = 1,
                ProtocolError_2 = 2,
                ServerError_3 = 3,
            };

            public enum PolicyRespStatusCode : uint
            {
                Success_1 = 1,
                NoPolicy_2 = 2,
                UnknownPolicyType_3 = 3,
                ServerCorrupt_4 = 4,
                WrongPolicyKey_5 = 5,
            };

            public enum PolicyReqStatusCode : uint
            {
                Success_1 = 1,
                PartialSuccess_2 = 2,
                NotApplied_3 = 3,
                // MDM case:
                External_4 = 4,
            };

            public enum RemoteWipeStatusCode : uint
            {
                Success_1 = 1,
                Failure_2 = 2,
            };

            public enum MaxAgeFilterCode
            {
                Min = 0,
                SyncAll_0 = Min,
                OneDay_1 = 1,
                ThreeDays_2 = 2,
                OneWeek_3 = 3,
                TwoWeeks_4 = 4,
                OneMonth_5 = 5,
                ThreeMonths_6 = 6,
                SixMonths_7 = 7,
                Max = SixMonths_7,
            };
        }

        public class Search
        {
            public const string Ns = "Search";
            // Alpha order.
            public const string And = "And";
            public const string DeepTraversal = "DeepTraversal";
            public const string FreeText = "FreeText";
            public const string LessThan = "LessThan";
            public const string MaxPictures = "MaxPictures";
            public const string Name = "Name";
            public const string Options = "Options";
            public const string Picture = "Picture";
            public const string Properties = "Properties";
            public const string Query = "Query";
            public const string Range = "Range";
            public const string RebuildResults = "RebuildResults";
            public const string Response = Autodisco.Response;
            public const string Result = MeetingResp.Result;
            public const string Status = AirSync.Status;
            public const string Store = ItemOperations.Store;
            public const string Value = "Value";

            public class NameCode
            {
                public const string DocumentLibrary = ItemOperations.StoreCode.DocumentLibrary;
                public const string Mailbox = ItemOperations.StoreCode.Mailbox;
                public const string GAL = Gal.Ns;
            }

            public enum SearchStatusCode : uint
            {
                Success_1 = 1,
                ServerError_3 = 3,
            };

            public enum StoreStatusCode : uint
            {
                Success_1 = 1,
                InvalidRequest_2 = 2,
                // likely transient - do retry.
                ServerError_3 = 3,
                BadLink_4 = 4,
                AccessDenied_5 = 5,
                // "Prompt user."
                NotFound_6 = 6,
                // "Prompt the user. Sometimes these are transient, so retry. If it continues to fail, point user to administrator."
                ConnectionFailed_7 = 7,
                TooComplex_8 = 8,
                // 9 omitted.
                // "The search timed out. Retry with or without rebuilding results. If it continues, contact the Administrator."
                TimedOut_10 = 10,
                // do FSync.
                FSyncRequired_11 = 11,
                EndOfRRange_12 = 12,
                AccessBlocked_13 = 13,
                CredRequired_14 = 14,
            };
        }

        public class Settings
        {
            public const string Ns = "Settings";
            // Alpha order.
            public const string DeviceInformation = "DeviceInformation";
            public const string FriendlyName = "FriendlyName";
            public const string Get = "Get";
            public const string Model = "Model";
            public const string OS = "OS";
            public const string OSLanguage = "OSLanguage";
            public const string Set = "Set";
            public const string Status = AirSync.Status;
            public const string UserAgent = "UserAgent";
            public const string UserInformation = "UserInformation";

            public enum StatusCode : uint
            {
                Success_1 = 1,
                ProtocolError_2 = 2,
                AccessDenied_3 = 3,
                ServerUnavailable_4 = 4,
                InvalidArgs_5 = 5,
                ConflictingArgs_6 = 6,
                PolicyDeny_7 = 7
            }

            public enum SetGetStatusCode
            {
                Success_1 = 1,
                ProtocolError_2 = 2,
                InvalidArgs_5 = 5,
                ConflictingArgs_6 = 6,
            }
        }

        public class Tasks
        {
            public const string Ns = "Tasks";
            // Alpha order.
            public const string CalendarType = "CalendarType";
            public const string Categories = "Categories";
            public const string Category = "Category";
            public const string Complete = "Complete";
            public const string DateCompleted = "DateCompleted";
            public const string DayOfMonth = "DayOfMonth";
            public const string DayOfWeek = "DayOfWeek";
            public const string DeadOccur = "DeadOccur";
            public const string DueDate = "DueDate";
            public const string FirstDayOfWeek = "FirstDayOfWeek";
            public const string Importance = "Importance";
            public const string Interval = "Interval";
            public const string IsLeapMonth = "IsLeapMonth";
            public const string MonthOfYear = "MonthOfYear";
            public const string Occurrences = "Occurrences";
            public const string OrdinalDate = "OrdinalDate";
            public const string Recurrence = "Recurrence";
            public const string Regenerate = "Regenerate";
            public const string ReminderSet = "ReminderSet";
            public const string ReminderTime = "ReminderTime";
            public const string Sensitivity = "Sensitivity";
            public const string Start = "Start";
            public const string StartDate = "StartDate";
            public const string Subject = "Subject";
            public const string SubOrdinalDate = "SubOrdinalDate";
            public const string Type = "Type";
            public const string Until = "Until";
            public const string UtcDueDate = "UtcDueDate";
            public const string UtcStartDate = "UtcStartDate";
            public const string WeekOfMonth = "WeekOfMonth";
        }
    }
}

