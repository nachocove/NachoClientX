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
        public const string NONE = "NONE";
        public const string FYI = "FYI";
        public const string PLEASE_READ = "PLEASE READ";
        public const string RESPONSE_REQUIRED = "RESPONSE REQUIRED";
        public const string URGENT = "URGENT";
        public const string IMPORTANT = "IMPORTANT";

        public static MessageIntent NONE_INTENT = new MessageIntent (McEmailMessage.IntentType.None, NONE, false);
        public static MessageIntent FYI_INTENT = new MessageIntent (McEmailMessage.IntentType.FYI, FYI, false);
        public static MessageIntent PLEASE_READ_INTENT = new MessageIntent (McEmailMessage.IntentType.PleaseRead, PLEASE_READ, true);
        public static MessageIntent RESPONSE_REQUIRED_INTENT = new MessageIntent (McEmailMessage.IntentType.ResponseRequired, RESPONSE_REQUIRED, true);
        public static MessageIntent URGENT_INTENT = new MessageIntent (McEmailMessage.IntentType.Urgent, URGENT, true);
        public static MessageIntent IMPORTANT_INTENT = new MessageIntent (McEmailMessage.IntentType.Important, IMPORTANT, true);


        protected static List<MessageIntent> MessageIntentList = new List<MessageIntent> () {
            NONE_INTENT,
            FYI_INTENT,
            PLEASE_READ_INTENT,
            RESPONSE_REQUIRED_INTENT,
            URGENT_INTENT,
            IMPORTANT_INTENT,
        };

        public MessageIntent intentType { get; private set; }

        public NcMessageIntent ()
        {
            intentType = NONE_INTENT;
        }

        public static List<MessageIntent> GetIntentList ()
        {
            return MessageIntentList;
        }

        public static string IntentEnumToString (McEmailMessage.IntentType type)
        {
            switch (type) {
            case McEmailMessage.IntentType.None:
                return "NONE";
            case McEmailMessage.IntentType.FYI:
                return "FYI";
            case McEmailMessage.IntentType.PleaseRead:
                return "PLEASE READ";
            case McEmailMessage.IntentType.ResponseRequired:
                return "RESPONSE REQUIRED";
            case McEmailMessage.IntentType.Urgent:
                return "URGENT";
            case McEmailMessage.IntentType.Important:
                return "IMPORTANT";
            default:
                NcAssert.CaseError ("Type not recognized");
                return null;
            }
        }

        public static string GetIntentString (McEmailMessage.IntentType intent, MessageDeferralType intentDateTypeEnum, DateTime intentDateTime)
        {
            string intentString = IntentEnumToString (intent);

            switch (intentDateTypeEnum) {
            case MessageDeferralType.None:
                intentString += "";
                break;
            case MessageDeferralType.OneHour:
                intentString += " In One Hour";
                break;
            case MessageDeferralType.TwoHours:
                intentString += " In Two Hours";
                break;
            case MessageDeferralType.Later:
                intentString += " Later Today";
                break;
            case MessageDeferralType.EndOfDay:
                intentString += " By End of Day";
                break;
            case MessageDeferralType.Tonight:
                intentString += " By Tonight";
                break;
            case MessageDeferralType.Tomorrow:
                intentString += " By Tomorrow";
                break;
            case MessageDeferralType.NextWeek:
                intentString += " By Next Week";
                break;
            case MessageDeferralType.MonthEnd:
                intentString += "By Month End";
                break;
            case MessageDeferralType.NextMonth:
                intentString += " By Next Month";
                break;
            case MessageDeferralType.Forever:
                intentString += "";
                break;
            case MessageDeferralType.Custom:
                intentString += " By " + intentDateTime.ToShortDateString ();
                break;
            default:
                NcAssert.CaseError ("Not a recognzized deferral type.");
                break;
            } 
            return intentString;
        }

        public class MessageIntent
        {
            public McEmailMessage.IntentType type { get; private set; }

            public string text { get; private set; }

            public bool dueDateAllowed { get; private set; }

            public MessageIntent (McEmailMessage.IntentType type, string text, bool dueDateAllowed)
            {
                this.type = type;
                this.text = text;
                this.dueDateAllowed = dueDateAllowed;
            }
        }
    }
}
