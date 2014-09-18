//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcMessageIntent
    {
        public const string MANDATORY = "MANDATORY";
        public const string READ = "READ";
        public const string RESPOND = "RESPOND";
        public const string CALL = "CALL"; 
        public const string FYI = "FYI";
        public const string DISCRETIONARY = "DISCRETIONARY";
        public const string ACKNOWLEDGED = "ACKNOWLEDGED";
        public const string MEETING = "MEETING";


        protected List<string> ForwardIntentsList = new List<string> () {
            MANDATORY,
            READ,
            RESPOND,
            CALL,
        };

        protected List<string> ComposeIntentsList = new List<string> () {
            FYI,
            MANDATORY,
            READ,
            RESPOND,
            CALL,
        };

        protected List<string> ReplyIntentsList = new List<string> () {
            DISCRETIONARY,
            ACKNOWLEDGED,
            MANDATORY,
            READ,
            RESPOND,
            MEETING,
            CALL,
        };

        public NcQuickResponse.QRTypeEnum messageType { get; private set; }
        public string intentValue {get;set;}

        public NcMessageIntent (NcQuickResponse.QRTypeEnum whatType)
        {
            this.messageType = whatType;
        }

        public List<string> GetIntentList ()
        {
            switch (messageType) {
            case NcQuickResponse.QRTypeEnum.Compose:
                return ComposeIntentsList;
            case NcQuickResponse.QRTypeEnum.Reply:
                return ReplyIntentsList;
            case NcQuickResponse.QRTypeEnum.Forward:
                return ForwardIntentsList;
            default:
                return null;
            }
        }

        public void EmbedIntentIntoMessage (string intent, ref McEmailMessage emailMessage)
        {
            if (null != intentValue) {
                if (emailMessage.Subject.Contains (intentValue)) {
                    emailMessage.Subject = emailMessage.Subject.Replace (intentValue, intent);
                } else {
                    emailMessage.Subject = intent + " - " + emailMessage.Subject;
                }
            } else {
                emailMessage.Subject = intent + " - " + emailMessage.Subject;
            }
            this.intentValue = intent;
        }

    }
}
