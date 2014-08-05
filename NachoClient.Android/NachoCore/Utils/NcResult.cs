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
            Info_FileSetChanged,
            Info_EmailMessageChanged,
            Info_ContactChanged,
            Info_CalendarChanged,
            Info_TaskChanged,

            Info_NewUnreadEmailMessageInInbox,
            Info_EmailMessageMarkedReadSucceeded,
            Info_EmailMessageSetFlagSucceeded,
            Info_EmailMessageClearFlagSucceeded,
            Info_EmailMessageMarkFlagDoneSucceeded,
            Info_EmailMessageSendSucceeded,
            Info_EmailMessageDeleteSucceeded,
            Info_EmailMessageMoveSucceeded,
            Info_EmailMessageBodyDownloadSucceeded,
            Info_FolderCreateSucceeded,
            Info_FolderDeleteSucceeded,
            Info_FolderUpdateSucceeded,
            Info_CalendarCreateSucceeded,
            Info_CalendarUpdateSucceeded,
            Info_CalendarDeleteSucceeded,
            Info_CalendarMoveSucceeded,
            Info_CalendarBodyDownloadSucceeded,
            Info_ContactCreateSucceeded,
            Info_ContactUpdateSucceeded,
            Info_ContactDeleteSucceeded,
            Info_ContactMoveSucceeded,
            Info_ContactBodyDownloadSucceeded,
            Info_TaskCreateSucceeded,
            Info_TaskUpdateSucceeded,
            Info_TaskDeleteSucceeded,
            Info_TaskMoveSucceeded,
            Info_TaskBodyDownloadSucceeded,
            Info_AttDownloadUpdate,
            Info_SyncSucceeded,
            Info_FolderSyncSucceeded,
            Info_MeetingResponseSucceeded,
            Info_SearchCommandSucceeded,
            Info_BackgroundAbateStarted,
            Info_BackgroundAbateStopped,
            Info_ServiceUnavailable,
            Info_ValidateConfigSucceeded,
            Info_ExplicitThrottling,
            Info_NeedContactsPermission,
            Info_AsAutoDComplete,
            Info_AsProvisionSuccess,
            Info_AsOptionsSuccess,
            Info_AsSettingsSuccess,
            Info_EmailAddressScoreUpdated,
            Info_EmailMessageScoreUpdated,
            Info_RicInitialSyncCompleted,

            // Warning.
            // Error.
            Error_NetworkUnavailable,
            Error_InvalidParameter,
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
            Error_CalendarMoveFailed,
            Error_CalendarBodyDownloadFailed,
            Error_ContactCreateFailed,
            Error_ContactUpdateFailed,
            Error_ContactDeleteFailed,
            Error_ContactMoveFailed,
            Error_ContactBodyDownloadFailed,
            Error_TaskCreateFailed,
            Error_TaskUpdateFailed,
            Error_TaskDeleteFailed,
            Error_TaskMoveFailed,
            Error_TaskBodyDownloadFailed,
            Error_EmailMessageSetFlagFailed,
            Error_EmailMessageClearFlagFailed,
            Error_EmailMessageDeleteFailed,
            Error_EmailMessageMarkedReadFailed,
            Error_EmailMessageMarkFlagDoneFailed,
            Error_EmailMessageMoveFailed,
            Error_EmailMessageSendFailed,
            Error_EmailMessageForwardFailed,
            Error_EmailMessageReplyFailed,
            Error_EmailMessageBodyDownloadFailed,
            Error_AttDownloadFailed,
            Error_FolderCreateFailed,
            Error_FolderDeleteFailed,
            Error_FolderUpdateFailed,
            Error_SyncFailed,
            Error_SyncFailedToComplete,
            Error_FolderSyncFailed,
            Error_MeetingResponseFailed,
            Error_SearchCommandFailed,
            Error_AuthFailBlocked,
            Error_AuthFailPasswordExpired,
            Error_AutoDStatus1,
            Error_AutoDStatus2,
            Error_AutoDError600,
            Error_AutoDError601,
            Error_ValidateConfigFailedAuth,
            Error_ValidateConfigFailedComm,
            Error_ValidateConfigFailedUser,
            Error_ServerConfReqCallback,
            Error_CredReqCallback,
            Error_CertAskReqCallback
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

