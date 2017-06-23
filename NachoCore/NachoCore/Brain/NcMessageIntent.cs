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
        public const string NONE = "None";
        public const string FYI = "FYI";
        public const string PLEASE_READ = "Please Read";
        public const string RESPONSE_REQUIRED = "Response Required";
        public const string URGENT = "Urgent";
        public const string IMPORTANT = "Important";

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

        public static bool IntentIsToday (MessageDeferralType intent)
        {
            return (
                intent == MessageDeferralType.OneHour ||
                intent == MessageDeferralType.TwoHours ||
                intent == MessageDeferralType.Later ||
                intent == MessageDeferralType.Tonight ||
                intent == MessageDeferralType.EndOfDay
            );
        }

        public static string IntentEnumToString (McEmailMessage.IntentType type, bool uppercase = true)
        {
            var intentString = "";
            switch (type) {
            case McEmailMessage.IntentType.None:
                intentString = "None";
                break;
            case McEmailMessage.IntentType.FYI:
                intentString = "FYI";
                break;
            case McEmailMessage.IntentType.PleaseRead:
                intentString = "Please Read";
                break;
            case McEmailMessage.IntentType.ResponseRequired:
                intentString = "Response Required";
                break;
            case McEmailMessage.IntentType.Urgent:
                intentString = "Urgent";
                break;
            case McEmailMessage.IntentType.Important:
                intentString = "Important";
                break;
            default:
                NcAssert.CaseError ("Type not recognized");
                break;
            }
            if (uppercase) {
                return intentString.ToUpper ();
            }
            return intentString;
        }

        public static string DeferralTypeToString (MessageDeferralType intentDateTypeEnum, DateTime? customDate = null)
        {
            string deferralString = "";
            switch (intentDateTypeEnum) {
            case MessageDeferralType.None:
                break;
            case MessageDeferralType.OneHour:
                deferralString = "In One Hour";
                break;
            case MessageDeferralType.TwoHours:
                deferralString = "In Two Hours";
                break;
            case MessageDeferralType.Later:
                deferralString = "Later Today";
                break;
            case MessageDeferralType.EndOfDay:
                deferralString = "By End of Day";
                break;
            case MessageDeferralType.Tonight:
                deferralString = "By Tonight";
                break;
            case MessageDeferralType.Tomorrow:
                deferralString = "By Tomorrow";
                break;
            case MessageDeferralType.NextWeek:
                deferralString = "By Next Week";
                break;
            case MessageDeferralType.MonthEnd:
                deferralString = "By Month End";
                break;
            case MessageDeferralType.NextMonth:
                deferralString = "By Next Month";
                break;
            case MessageDeferralType.Forever:
                break;
            case MessageDeferralType.Custom:
                if (customDate.HasValue) {
                    deferralString = "By " + customDate.Value.ToLocalTime ().ToString ("M/d/yyyy", new System.Globalization.CultureInfo ("en-US"));
                } else {
                    NcAssert.CaseError ("custom deferral type requires custom date");
                }
                break;
            default:
                NcAssert.CaseError ("Not a recognzized deferral type.");
                break;
            }
            return deferralString;
        }

        public static string GetIntentString (McEmailMessage.IntentType intent, MessageDeferralType intentDateTypeEnum, DateTime intentDateTime)
        {
            string intentString = IntentEnumToString (intent);
            string dateString = DeferralTypeToString (intentDateTypeEnum, intentDateTime);
            if (!String.IsNullOrEmpty (dateString)) {
                intentString += " " + dateString;
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
