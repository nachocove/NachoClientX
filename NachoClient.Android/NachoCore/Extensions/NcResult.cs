using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcResult
    {
        // Eventually match up with Syslog severity levels for non-OK results.
        // https://en.wikipedia.org/wiki/Syslog.
        public enum KindEnum { OK, Info, Warning, Error };

        public enum SubKindEnum {
            // OK.
            // Info.
            Info_FolderSetChanged,
            Info_EmailMessageSetChanged,
            Info_ContactSetChanged,
            Info_CalendarSetChanged,
            Info_NewUnreadEmailMessageInInbox,
            Info_EmailMessageSendSucceeded,
            Info_EmailMessageDeleteSucceeded,
            Info_EmailMessageMoveSucceeded,
            Info_AttDownloadUpdate,
            // Warning.
            // Error.
            Error_EmailMessageSendFailed,
            Error_EmailMessageDeleteFailed,
            Error_EmailMessageMoveFailed,
            Error_AttDownloadFailed,
        };

        public KindEnum Kind { get; set; }
        public SubKindEnum SubKind { get; set; }
        public object Value { get; set; }
        public string Message { get; set; }

        private NcResult ()
        {
        }
     
        public static NcResult OK()
        {
            return new NcResult () { Kind = KindEnum.OK };
        }

        public static NcResult OK(object o)
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

        public static NcResult Error(string message)
        {
            return new NcResult () { Kind = KindEnum.Error, Message = message };
        }

        public static NcResult Error (SubKindEnum subKind)
        {
            return new NcResult () { Kind = KindEnum.Error, SubKind = subKind };
        }

        public bool isOK()
        {
            return (KindEnum.OK == Kind);
        }

        public bool isError()
        {
            return (KindEnum.Error == Kind);
        }

        public bool isInfo ()
        {
            return (KindEnum.Info == Kind);
        }

        public T GetValue<T>()
        {
            return (T) Value;
        }

        public string GetMessage()
        {
            return Message;
        }

    }
}

