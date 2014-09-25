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
        public const string PLEASE_READ = "Please Read";
        public const string RESPONSE_REQUIRED = "Response Required";
        public const string URGENT = "Urgent";

        public enum IntentTypeEnum {
            None,
            FYI,
            PleaseRead,
            ResponseRequired,
            Urgent,
        }

        protected List<Intent> IntentsList= new List<Intent> () {
            new Intent(IntentTypeEnum.None, NONE),
            new Intent(IntentTypeEnum.FYI, FYI),
            new Intent(IntentTypeEnum.PleaseRead, PLEASE_READ),
            new Intent(IntentTypeEnum.ResponseRequired, RESPONSE_REQUIRED),
            new Intent(IntentTypeEnum.Urgent, URGENT),
        };

        public Intent intentType { get; private set; }

        public NcMessageIntent ()
        {
            intentType = new Intent (IntentTypeEnum.None, NONE);
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
            emailMessage.Intent = intentType.value;
        }

        public void SetMessageIntentDate (ref McEmailMessage emailMessage, DateTime selectedDate)
        {
            emailMessage.IntentDate = selectedDate;
        }

        public class Intent
        {
            public string value;
            public IntentTypeEnum type;

            public Intent (IntentTypeEnum type, string value)
            {
                this.value = value;
                this.type = type;
            }
        }


    }
}
