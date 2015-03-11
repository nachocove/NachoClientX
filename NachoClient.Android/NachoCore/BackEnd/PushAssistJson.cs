﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoCore
{
    public class Credentials
    {
        public string Username;
        public string Password;
    }

    public class StartSessionRequest
    {
        public string ClientId;
        public string DeviceId;
        public string ClientContext;
        public string MailServerUrl;
        public Credentials MailServerCredentials;
        public string Protocol;
        public string Platform;
        public Dictionary<string, string> HttpHeaders;
        public string HttpRequestData;
        public string HttpExpectedReply;
        public string HttpNoChangeReply;
        public string CommandTerminator;
        public string CommandAcknowledgement;
        public int ResponseTimeout;
        public int WaitBeforeUse;
        public string PushToken;
        public string PushService;
    }

    public class DeferSessionRequest
    {
        public string ClientId;
        public string DeviceId;
        public string ClientContext;
        public string Token;
        public int ResponseTimeout;
    }

    public class StopSessionRequest
    {
        public string ClientId;
        public string DeviceId;
        public string ClientContext;
        public string Token;
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

    public class PingerNotification : Dictionary<string, string>
    {
        public PingerNotificationActionEnum GetAction (string actionStr)
        {
            PingerNotificationActionEnum action = PingerNotificationActionEnum.UNKNOWN;
            switch (actionStr) {
            case "new":
                action = PingerNotificationActionEnum.NEW;
                break;
            case "register":
                action = PingerNotificationActionEnum.REGISTER;
                break;
            }
            return action;
        }
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

