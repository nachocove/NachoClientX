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

        public enum IntentTypeEnum {
            None = 0,
            FYI = 1,
            PleaseRead = 2,
            ResponseRequired = 3,
            Urgent = 4,
        }

        public static Intent NONE_INTENT = new Intent(IntentTypeEnum.None, NONE, false);
        public static Intent FYI_INTENT = new Intent (IntentTypeEnum.FYI, FYI, false);
        public static Intent PLEASE_READ_INTENT = new Intent (IntentTypeEnum.PleaseRead, PLEASE_READ, true);
        public static Intent RESPONSE_REQUIRED_INTENT = new Intent(IntentTypeEnum.ResponseRequired, RESPONSE_REQUIRED, true);
        public static Intent URGENT_INTENT = new Intent(IntentTypeEnum.Urgent, URGENT, true);

        protected List<Intent> IntentsList = new List<Intent> () {
            NONE_INTENT,
            FYI_INTENT,
            PLEASE_READ_INTENT,
            RESPONSE_REQUIRED_INTENT,
            URGENT_INTENT,
        };

        public Intent intentType { get; private set; }

        public NcMessageIntent ()
        {
            intentType = NONE_INTENT;
        }

        public List<Intent> GetIntentList ()
        {
            return IntentsList;
        }

        public void SetType (Intent intent)
        {
            intentType = intent;
        }

        public void SetMessageIntent (ref McEmailMessage emailMessage)
        {
            emailMessage.Intent = (int)intentType.type;
        }

        public void SetMessageIntentDate (ref McEmailMessage emailMessage, DateTime selectedDate)
        {
            emailMessage.IntentDate = selectedDate;
        }

        public static string IntentEnumToString (IntentTypeEnum type)
        {
            switch (type) {
            case IntentTypeEnum.None:
                return "NONE";
            case IntentTypeEnum.FYI:
                return "FYI";
            case IntentTypeEnum.PleaseRead:
                return "PLEASE READ";
            case IntentTypeEnum.ResponseRequired:
                return "RESPONSE REQUIRED";
            case IntentTypeEnum.Urgent:
                return "URGENT";
            default:
                NcAssert.CaseError ("Type not recognized");
                return null;
            }
        }
        public static string GetIntentString (MessageDeferralType intentDateTypeEnum, McEmailMessage mcMessage)
        {
            string intentString = IntentEnumToString((IntentTypeEnum)mcMessage.Intent);

            if (MessageDeferralType.None != intentDateTypeEnum) {
                switch (intentDateTypeEnum) {
                case MessageDeferralType.Later:
                    intentString += " By Today";
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
                case MessageDeferralType.NextMonth:
                    intentString += " By Next Month";
                    break;
                case MessageDeferralType.Custom:
                    intentString += " By " + mcMessage.IntentDate.ToShortDateString ();
                    break;
                default:
                    NcAssert.CaseError ("Not a recognzized deferral type.");
                    break;
                } 
            }
            return intentString;
        }

        public class Intent
        {
            public IntentTypeEnum type { get; private set; }
            public string value { get; private set; }
            public bool dueDateAllowed {get; private set;}

            public Intent (IntentTypeEnum type, string value, bool dueDateAllowed)
            {
                this.type = type;
                this.value = value;
                this.dueDateAllowed = dueDateAllowed;
            }
        }
    }
}
