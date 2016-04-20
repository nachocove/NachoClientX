//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NachoCore
{
    public class Credentials
    {
        public string Username;
        public string Password;
    }

    public class BaseRequest
    {
        public string UserId;
        public string ClientId;
        public string DeviceId;
        public string ClientContext;
        public string OSVersion;
        public string AppBuildVersion;
        public string AppBuildNumber;
    }

    public class SessionRequest : BaseRequest
    {
        public string Token;
    }

    public class StartSessionRequest : BaseRequest
    {
        public string MailServerUrl;
        public string Protocol;
        public string Platform;
        public int ResponseTimeout;
        public int WaitBeforeUse;
        public string PushToken;
        public string PushService;

        public Credentials MailServerCredentials;
        public Dictionary<string, string> HttpHeaders;
        public string RequestData;
        public string ExpectedReply;
        public string NoChangeReply;
        public bool ASIsSyncRequest;

        public string IMAPAuthenticationBlob;
        public string IMAPFolderName;
        public bool IMAPSupportsIdle;
        public bool IMAPSupportsExpunge;
        public int IMAPEXISTSCount;
        public uint IMAPUIDNEXT;
    }

    public class DeferSessionRequest : SessionRequest
    {
        public int ResponseTimeout;
        public string RequestData;
    }

    public class StopSessionRequest : SessionRequest
    {
    }

    public class PingerResponse
    {
        public const string Ok = "OK";
        public const string Warn = "WARN";
        public const string Error = "ERROR";

        public string Message;
        public string Status;
        public string Token;

        public bool IsOk ()
        {
            return (Ok == Status);
        }

        public bool IsWarn ()
        {
            return (Warn == Status);
        }

        public bool IsError ()
        {
            return (Error == Status);
        }

        public bool IsOkOrWarn ()
        {
            return IsOk () || IsWarn ();
        }
    }

    public enum PingerNotificationActionEnum
    {
        UNKNOWN = 0,
        NEW = 1,
        REGISTER = 2,
    };

    public class PingerMetadata : Dictionary<string, string>
    {
        protected const string TIMESTAMP = "time";

        public bool HasTimestamp (out DateTime timestamp)
        {
            string unixTime;
            if (!TryGetValue (TIMESTAMP, out unixTime)) {
                timestamp = DateTime.MinValue;
                return false;
            }
            try {
                var epoch = new DateTime (1970, 1, 1, 0, 0, 0, 0);
                var seconds = double.Parse (unixTime, CultureInfo.InvariantCulture);
                timestamp = epoch.AddSeconds (seconds);
                return true;
            } catch {
                timestamp = DateTime.MinValue;
                return false;
            }
        }
    }

    public class PingerContext
    {
        public const string NEW = "new";
        public const string REGISTER = "reg";

        // Command - "new" or "reg"
        public string cmd;
        // Session
        public string ses;
    }

    public class PingerNotification
    {
        public Dictionary<string, PingerContext> ctxs;
        public PingerMetadata meta;
    }

    public class ApsNotification
    {
    }

    public class Notification
    {
        public ApsNotification aps;
        public PingerNotification pinger;

        public bool HasPingerSection ()
        {
            return (null != pinger);
        }
    }
}

