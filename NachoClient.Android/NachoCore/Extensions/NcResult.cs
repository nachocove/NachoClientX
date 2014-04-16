using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcResult
    {
        // Eventually match up with Syslog severity levels for non-OK results.
        // https://en.wikipedia.org/wiki/Syslog.
        public enum KindEnum
        {
            OK,
            Info,
            Warning,
            Error,
        };

        public enum SubKindEnum
        {
            NotSpecified = 0,
            // Info.
            Info_FolderSetChanged,
            Info_EmailMessageSetChanged,
            Info_ContactSetChanged,
            Info_CalendarSetChanged,
            Info_TaskSetChanged,

            Info_NewUnreadEmailMessageInInbox,
            Info_EmailMessageMarkedReadSucceeded,
            Info_EmailMessageSetFlagSucceeded,
            Info_EmailMessageClearFlagSucceeded,
            Info_EmailMessageMarkFlagDoneSucceeded,
            Info_EmailMessageSendSucceeded,
            Info_EmailMessageDeleteSucceeded,
            Info_EmailMessageMoveSucceeded,
            Info_FolderCreateSucceeded,
            Info_FolderDeleteSucceeded,
            Info_FolderUpdateSucceeded,
            Info_CalendarCreateSucceeded,
            Info_CalendarUpdateSucceeded,
            Info_CalendarDeleteSucceeded,
            Info_ContactCreateSucceeded,
            Info_ContactUpdateSucceeded,
            Info_ContactDeleteSucceeded,
            Info_TaskCreateSucceeded,
            Info_TaskUpdateSucceeded,
            Info_TaskDeleteSucceeded,
            Info_AttDownloadUpdate,
            Info_SyncSucceeded,
            Info_FolderSyncSucceeded,
            Info_MeetingResponseSucceeded,
            Info_SearchCommandSucceeded,
            // Warning.
            // Error.
            Error_UnknownCommandFailure,
            Error_SettingsFailed,
            Error_ProvisionFailed,
            Error_ProtocolError,
            Error_NoSpace,
            Error_ServerConflict,
            Error_AlreadyInFolder,
            Error_NotInFolder,
            Error_InappropriateStatus,
            Error_ObjectNotFoundOnServer,
            Error_CalendarCreateFailed,
            Error_CalendarUpdateFailed,
            Error_CalendarDeleteFailed,
            Error_ContactCreateFailed,
            Error_ContactUpdateFailed,
            Error_ContactDeleteFailed,
            Error_TaskCreateFailed,
            Error_TaskUpdateFailed,
            Error_TaskDeleteFailed,
            Error_EmailMessageSetFlagFailed,
            Error_EmailMessageClearFlagFailed,
            Error_EmailMessageDeleteFailed,
            Error_EmailMessageMarkedReadFailed,
            Error_EmailMessageMarkFlagDoneFailed,
            Error_EmailMessageMoveFailed,
            Error_EmailMessageSendFailed,
            Error_EmailMessageForwardFailed,
            Error_EmailMessageReplyFailed,
            Error_AttDownloadFailed,
            Error_FolderCreateFailed,
            Error_FolderDeleteFailed,
            Error_FolderUpdateFailed,
            Error_SyncFailed,
            Error_FolderSyncFailed,
            Error_MeetingResponseFailed,
            Error_SearchCommandFailed,
        };

        public enum WhyEnum {
            NotSpecified = 0,
            Unknown,
            ProtocolError,
            ServerError,
            ServerOffline,
            QuotaExceeded, // Other than storage capacity.
            NoSpace,
            ConflictWithServer,
            InvalidDest,
            LockedOnServer,
            AlreadyExistsOnServer,
            SpecialFolder,
            MissingOnServer,
            BadOrMalformed,
            AccessDeniedOrBlocked,
            TooComplex,
            TooBig,
            BeyondRange,
            UnresolvedRecipient,
            NoRecipient,
            ReplyNotAllowed,

        };

        public KindEnum Kind { get; set; }

        public SubKindEnum SubKind { get; set; }

        public WhyEnum Why { get; set; }

        public object Value { get; set; }

        public string Message { get; set; }

        private NcResult ()
        {
        }

        public static NcResult OK ()
        {
            return new NcResult () { Kind = KindEnum.OK };
        }

        public static NcResult OK (object o)
        {
            return new NcResult () { Kind = KindEnum.OK, Value = o };
        }

        public static NcResult Info (string message)
        {
            return new NcResult () { Kind = KindEnum.Info, Message = message };
        }

        public static NcResult Info (SubKindEnum subKind)
        {
            return new NcResult () { Kind = KindEnum.Info, SubKind = subKind };
        }

        public static NcResult Error (string message)
        {
            return new NcResult () { Kind = KindEnum.Error, Message = message };
        }

        public static NcResult Error (SubKindEnum subKind)
        {
            return new NcResult () { Kind = KindEnum.Error, SubKind = subKind };
        }

        public static NcResult Error (SubKindEnum subKind, WhyEnum why)
        {
            return new NcResult () { Kind = KindEnum.Error, SubKind = subKind, Why = why };
        }

        public bool isOK ()
        {
            return (KindEnum.OK == Kind);
        }

        public bool isError ()
        {
            return (KindEnum.Error == Kind);
        }

        public bool isInfo ()
        {
            return (KindEnum.Info == Kind);
        }

        public T GetValue<T> ()
        {
            return (T)Value;
        }

        public string GetMessage ()
        {
            return Message;
        }
    }
}

